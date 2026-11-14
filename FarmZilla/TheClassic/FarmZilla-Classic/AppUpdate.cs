using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

sealed class UpdateEnvelope { public string payload {get;set;} public string signature {get;set;} }
sealed class UpdateManifest {
 public string product {get;set;} public string architecture {get;set;}
 public string version {get;set;} public string url {get;set;}
 public string sha256 {get;set;} public long size {get;set;} public string notes {get;set;}
}
sealed class UpdateRequest {
 public string repository {get;set;} public string envelope {get;set;}
 public string target {get;set;} public int parentId {get;set;} public long parentStart {get;set;}
}
static class AppUpdate {
 internal static string ClientAuthorization=null;
 internal const int MaxExe=32*1024*1024, MaxManifest=65536;
 internal static string Home {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AvalonInterno");}}
 internal static string Repository {
  get {return OnlineApi.Service;}
  set {OnlineApi.Service=value;}
 }
 internal static void CheckPrivateSource(string source){Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.Host=="github.com"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||uri.AbsolutePath!="/")throw new InvalidDataException("Informe a origem HTTPS do servidor privado de atualizações, sem caminhos ou parâmetros.");}
 internal static bool PrivateSource(string source){return source!=null&&source.StartsWith("https://",StringComparison.OrdinalIgnoreCase);}
 internal static void CheckRepository(string repo){if(PrivateSource(repo)){CheckPrivateSource(repo);return;}if(repo==null||!Regex.IsMatch(repo,@"\A[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}\z"))throw new InvalidDataException("Endereço de atualização inválido.");}
 internal static string ManifestUrl(string repo){CheckRepository(repo);return PrivateSource(repo)?repo.TrimEnd('/')+"/update.json":"https://github.com/"+repo+"/releases/latest/download/update.json";}
 internal static UpdateManifest Verify(string envelope,string repo,string publicKey){
  CheckRepository(repo);if(envelope==null||Encoding.UTF8.GetByteCount(envelope)>MaxManifest)throw new InvalidDataException("Manifesto muito grande.");
  var json=new JavaScriptSerializer();var signed=json.Deserialize<UpdateEnvelope>(envelope);
  if(signed==null||signed.payload==null||signed.signature==null)throw new InvalidDataException("Manifesto incompleto.");
  byte[] payload=Convert.FromBase64String(signed.payload),sig=Convert.FromBase64String(signed.signature);
  using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(!rsa.VerifyData(payload,"SHA256",sig))throw new InvalidDataException("Assinatura da atualização inválida.");}
  var m=json.Deserialize<UpdateManifest>(new UTF8Encoding(false,true).GetString(payload));Version version;
  if(m==null||m.product!="AvalonInterno"||m.architecture!="x86"||!Version.TryParse(m.version,out version)||version.Major<0||m.size<1024||m.size>MaxExe||m.sha256==null||!Regex.IsMatch(m.sha256,@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("Versão ou metadados inválidos.");
  Uri url;if(!Uri.TryCreate(m.url,UriKind.Absolute,out url)||url.Scheme!="https"||!url.IsDefaultPort||url.UserInfo!=""||url.Query!=""||url.Fragment!=""||url.AbsolutePath.Contains("%"))throw new InvalidDataException("Endereço de download inválido.");
  if(PrivateSource(repo)){if(m.url!=repo.TrimEnd('/')+"/releases/v"+m.version+"/Bagaceira.exe")throw new InvalidDataException("Download fora do servidor privado configurado.");}
  else if(url.Host!="github.com"||!url.AbsolutePath.StartsWith("/"+repo+"/releases/download/",StringComparison.Ordinal)||!url.AbsolutePath.EndsWith("/Bagaceira.exe",StringComparison.Ordinal))throw new InvalidDataException("Download fora do repositório configurado.");
  return m;
 }
 internal static byte[] Download(string url,int limit){
  ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
  var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="Bagaceira/"+ReleaseInfo.Version;request.Timeout=20000;request.ReadWriteTimeout=20000;request.AllowAutoRedirect=false;
  if(!String.IsNullOrEmpty(ClientAuthorization)){request.Headers[HttpRequestHeader.Authorization]="Bearer "+ClientAuthorization;}
  using(var response=(HttpWebResponse)request.GetResponse()){
   if(response.StatusCode!=HttpStatusCode.OK||response.ResponseUri.Scheme!="https"||response.ContentLength>limit)throw new InvalidDataException("Download inválido ou muito grande.");
   using(var stream=response.GetResponseStream())using(var output=new MemoryStream()){
    byte[] buffer=new byte[16384];int n;while((n=stream.Read(buffer,0,buffer.Length))!=0){if(output.Length+n>limit)throw new InvalidDataException("Download excedeu o limite.");output.Write(buffer,0,n);}return output.ToArray();
   }
  }
 }
 internal static void VerifyBinary(byte[] bytes,UpdateManifest m){
  if(bytes.LongLength!=m.size)throw new InvalidDataException("Tamanho da atualização não confere.");
  string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
  if(!String.Equals(hash,m.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 da atualização não confere.");
  if(bytes.Length<64||bytes[0]!=0x4d||bytes[1]!=0x5a)throw new InvalidDataException("Executável inválido.");
  int pe=BitConverter.ToInt32(bytes,60);if(pe<64||pe>bytes.Length-26||BitConverter.ToUInt32(bytes,pe)!=0x4550||BitConverter.ToUInt16(bytes,pe+4)!=0x14c||BitConverter.ToUInt16(bytes,pe+24)!=0x10b)throw new InvalidDataException("A atualização precisa ser um executável x86.");
 }
 internal static string Stage(string repo,string envelope,UpdateManifest m,string target){
  byte[] bytes=Download(m.url,MaxExe);VerifyBinary(bytes,m);
  string directory=Path.Combine(Home,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  File.WriteAllBytes(Path.Combine(directory,"Bagaceira.exe"),bytes);
  var request=new UpdateRequest{repository=repo,envelope=envelope,target=Path.GetFullPath(target)};
  using(var process=Process.GetCurrentProcess()){request.parentId=process.Id;request.parentStart=process.StartTime.ToUniversalTime().Ticks;}
  string path=Path.Combine(directory,"request.json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(request));return path;
 }
 internal static void LaunchInstaller(string requestPath,string currentExe){
  string helper=Path.Combine(Path.GetDirectoryName(requestPath),"installer.exe");File.Copy(currentExe,helper,false);
  Process.Start(new ProcessStartInfo(helper,"--apply-update \""+requestPath+"\""){UseShellExecute=false,CreateNoWindow=true});
 }
 internal static bool RunInstaller(string[] args){
  if(args.Length==0||args[0]!="--apply-update")return false;
  try {
   if(args.Length!=2)throw new InvalidDataException("Pedido de atualização inválido.");
   string requestPath=Path.GetFullPath(args[1]),dir=Path.GetDirectoryName(requestPath);
   string stagingRoot=Path.GetFullPath(Path.Combine(Home,"updates"))+Path.DirectorySeparatorChar;
   if(!requestPath.StartsWith(stagingRoot,StringComparison.OrdinalIgnoreCase)||new FileInfo(requestPath).Length>MaxManifest*2)throw new InvalidDataException("Pedido fora da pasta de atualizações.");
   var r=new JavaScriptSerializer().Deserialize<UpdateRequest>(File.ReadAllText(requestPath));
   if(r==null||Path.GetFileName(r.target)!="Bagaceira.exe"||!Path.IsPathRooted(r.target)||!File.Exists(r.target))throw new InvalidDataException("Destino inválido.");
   var m=Verify(r.envelope,r.repository,ReleaseInfo.PublicKey);
   if(new Version(m.version)<=new Version(ReleaseInfo.Version))throw new InvalidDataException("Atualização precisa ser mais recente.");
   byte[] bytes=File.ReadAllBytes(Path.Combine(dir,"Bagaceira.exe"));VerifyBinary(bytes,m);
   Process parent=null;try{parent=Process.GetProcessById(r.parentId);}catch(ArgumentException){}
   if(parent!=null)using(parent){if(parent.StartTime.ToUniversalTime().Ticks!=r.parentStart||!String.Equals(Path.GetFullPath(parent.MainModule.FileName),Path.GetFullPath(r.target),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Aplicativo de origem não confere.");if(!parent.WaitForExit(60000))throw new IOException("O aplicativo ainda está encerrando. Tente novamente.");}
   ApplyFile(r.target,bytes,path=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)}));
   File.Delete(Path.Combine(dir,"Bagaceira.exe"));File.Delete(requestPath);
  }catch(Exception ex){DiagnosticLog.Write("Atualização não aplicada: "+ex.Message);System.Windows.Forms.MessageBox.Show("Não foi possível aplicar a atualização: "+ex.Message+"\nA versão anterior foi preservada.","Atualização do FarmZilla");}
  return true;
 }
 internal static void ApplyFile(string target,byte[] bytes,Action<string> launch){
  string candidate=target+".update-"+Guid.NewGuid().ToString("N"),backup=candidate+".bak";bool replaced=false;
  try {File.WriteAllBytes(candidate,bytes);File.Replace(candidate,target,backup);replaced=true;launch(target);}
  catch {if(replaced&&File.Exists(backup))File.Replace(backup,target,null);throw;}
  finally {if(File.Exists(candidate))File.Delete(candidate);}
  if(File.Exists(backup))File.Delete(backup);
 }
}

