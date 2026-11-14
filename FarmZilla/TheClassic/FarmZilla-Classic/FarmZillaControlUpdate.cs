using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// FarmZilla Control authenticated updater. No legacy Bagaceira paths.
// Trust: TLS to fixed host + license validation + published SHA256 + PE/version verification.
// Code signing is a separate deployment requirement; do not treat SHA256 as a publisher signature.
sealed class ControlRelease {
 public string version {get;set;} public string sha256 {get;set;} public long bytes {get;set;}
}
sealed class ControlCheckReply {
 public bool ok {get;set;} public bool updateAvailable {get;set;} public ControlRelease release {get;set;}
}
sealed class ControlUpdateRequest {
 public string version {get;set;} public string sha256 {get;set;} public long bytes {get;set;}
 public string target {get;set;} public int parentId {get;set;} public long parentStart {get;set;}
}
static class FarmZillaControlUpdate {
 const string Url="https://farmzilla.jrconect.com.br/api/bot/";
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static string UpdatesRoot {get{return Path.Combine(AppUpdate.Home,"control-updates");}}
 static bool HashValid(string hex){return hex!=null&&Regex.IsMatch(hex,@"\A[a-fA-F0-9]{64}\z");}
 static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
 static byte[] Post(string endpoint,object payload,int max){
  ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
  var request=(HttpWebRequest)WebRequest.Create(Url+endpoint);
  request.Method="POST";request.ContentType="application/json; charset=utf-8";request.Accept="application/octet-stream";
  request.AllowAutoRedirect=false;request.Timeout=25000;request.ReadWriteTimeout=25000;
  byte[] body=Encoding.UTF8.GetBytes(Json.Serialize(payload));request.ContentLength=body.Length;
  using(var output=request.GetRequestStream())output.Write(body,0,body.Length);
  using(var response=(HttpWebResponse)request.GetResponse()){
   if(response.StatusCode!=HttpStatusCode.OK||response.ResponseUri.Scheme!="https"||
      response.ResponseUri.Host!="farmzilla.jrconect.com.br"||response.ResponseUri.Port!=443||
      response.ContentLength>max)throw new InvalidDataException("Resposta de download inválida.");
   using(var input=response.GetResponseStream())using(var bytes=new MemoryStream()){
    byte[] buffer=new byte[16384];int n;
    while((n=input.Read(buffer,0,buffer.Length))>0){
     if(bytes.Length+n>max)throw new InvalidDataException("Arquivo excede limite.");
     bytes.Write(buffer,0,n);
    }
    if(response.ContentLength>=0&&response.ContentLength!=bytes.Length)throw new InvalidDataException("Download incompleto.");
    return bytes.ToArray();
   }
  }
 }
 internal static ControlRelease Check(OnlineIdentity identity){
  var key=FarmZillaControlLicense.LoadKey();
  if(String.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Licença do FarmZilla Control indisponível.");
  byte[] raw=Post("update-check",new {key=key.Trim(),deviceHash=FarmZillaControlLicense.DeviceHash(identity),version=ReleaseInfo.Version},65536);
  var result=Json.Deserialize<ControlCheckReply>(Encoding.UTF8.GetString(raw));
  if(result==null||!result.ok)throw new InvalidDataException("Resposta de atualização inválida.");
  if(!result.updateAvailable)return null;
  var r=result.release;Version current,next;
  if(r==null||!Version.TryParse(r.version,out next)||!Version.TryParse(ReleaseInfo.Version,out current)||
     next<=current||!HashValid(r.sha256)||r.bytes<1024||r.bytes>AppUpdate.MaxExe)
   throw new InvalidDataException("Metadados da atualização inválidos.");
  return r;
 }
 static void Validate(byte[] bytes,ControlRelease release){
  if(release==null||!HashValid(release.sha256)||bytes.LongLength!=release.bytes||
      !String.Equals(Hash(bytes),release.sha256,StringComparison.OrdinalIgnoreCase))
   throw new InvalidDataException("SHA-256 ou tamanho do FarmZilla.exe não confere.");
  var m=new UpdateManifest{size=release.bytes,sha256=release.sha256};
  AppUpdate.VerifyBinary(bytes,m);
 }
 internal static string Stage(ControlRelease release,OnlineIdentity identity,string target){
  if(!String.Equals(Path.GetFileName(target),"FarmZilla.exe",StringComparison.OrdinalIgnoreCase))
   throw new InvalidDataException("O aplicativo instalado precisa se chamar FarmZilla.exe.");
  var key=FarmZillaControlLicense.LoadKey();
  if(String.IsNullOrWhiteSpace(key))throw new InvalidDataException("Licença indisponível.");
  byte[] bytes=Post("update-download",new {key=key.Trim(),deviceHash=FarmZillaControlLicense.DeviceHash(identity),
    version=ReleaseInfo.Version,expectedVersion=release.version,expectedSha256=release.sha256},AppUpdate.MaxExe);
  Validate(bytes,release);
  var folder=Path.Combine(UpdatesRoot,Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(folder);
  string staged=Path.Combine(folder,"FarmZilla.exe");
  File.WriteAllBytes(staged,bytes);
  var embedded=FileVersionInfo.GetVersionInfo(staged);
  Version fileVersion,expected;
  if(!Version.TryParse(embedded.FileVersion,out fileVersion)||!Version.TryParse(release.version,out expected)||
     fileVersion.Major!=expected.Major||fileVersion.Minor!=expected.Minor||fileVersion.Build!=expected.Build)
   throw new InvalidDataException("Versão interna do executável diferente da publicada.");
  var data=new ControlUpdateRequest{target=Path.GetFullPath(target),version=release.version,
    sha256=release.sha256.ToLowerInvariant(),bytes=release.bytes};
  using(var current=Process.GetCurrentProcess()){data.parentId=current.Id;data.parentStart=current.StartTime.ToUniversalTime().Ticks;}
  string request=Path.Combine(folder,"control-request.json");
  File.WriteAllText(request,Json.Serialize(data),Encoding.UTF8);
  return request;
 }
 // The helper is a copy of the current FarmZilla.exe. Match it to the on-disk
 // original before any replacement, even when Windows has already released the PID.
 internal static bool SameOriginalBinary(string helper,string target){
  if(String.IsNullOrEmpty(helper)||String.IsNullOrEmpty(target)||!File.Exists(helper)||!File.Exists(target))return false;
  var a=new FileInfo(helper);var b=new FileInfo(target);
  if(a.Length!=b.Length||a.Length<1024||a.Length>AppUpdate.MaxExe)return false;
  using(var sha=SHA256.Create()){
   byte[] first,second;
   using(var input=File.OpenRead(helper))first=sha.ComputeHash(input);
   sha.Initialize();
   using(var input=File.OpenRead(target))second=sha.ComputeHash(input);
   return CryptographicEquals(first,second);
  }
 }
 static bool CryptographicEquals(byte[] a,byte[] b){
  if(a==null||b==null||a.Length!=b.Length)return false;
  int difference=0;for(int i=0;i<a.Length;i++)difference|=a[i]^b[i];
  return difference==0;
 }
 internal static bool RunInstaller(string[] args){
  if(args.Length==0||args[0]!="--apply-control-update")return false;
  try {
   if(args.Length!=2)throw new InvalidDataException("Pedido de atualização inválido.");
   string request=Path.GetFullPath(args[1]),folder=Path.GetDirectoryName(request);
   string allowed=Path.GetFullPath(UpdatesRoot)+Path.DirectorySeparatorChar;
   if(!request.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||
      Path.GetFileName(request)!="control-request.json"||new FileInfo(request).Length>8192)
    throw new InvalidDataException("Pasta da atualização inválida.");
   var data=Json.Deserialize<ControlUpdateRequest>(File.ReadAllText(request,Encoding.UTF8));
   Version next,current;
   if(data==null||!HashValid(data.sha256)||!Version.TryParse(data.version,out next)||
      !Version.TryParse(ReleaseInfo.Version,out current)||next<=current||
      data.parentId<=0||data.parentStart<=0||data.bytes<1024||data.bytes>AppUpdate.MaxExe||
      !Path.IsPathRooted(data.target)||!String.Equals(Path.GetFileName(data.target),"FarmZilla.exe",StringComparison.OrdinalIgnoreCase)||
      !File.Exists(data.target))throw new InvalidDataException("Metadados de instalação inválidos.");
   string staged=Path.Combine(folder,"FarmZilla.exe");
   byte[] bytes=File.ReadAllBytes(staged);
   Validate(bytes,new ControlRelease{version=data.version,sha256=data.sha256,bytes=data.bytes});
   Version v;if(!Version.TryParse(FileVersionInfo.GetVersionInfo(staged).FileVersion,out v)||
       v.Major!=next.Major||v.Minor!=next.Minor||v.Build!=next.Build)
    throw new InvalidDataException("Versão do arquivo não corresponde à publicação.");
   // The parent can legitimately exit before the helper gets scheduled.
   // In that case verify the still-installed EXE is byte-for-byte the helper's origin.
   // Never skip this check for a live PID, reused PID, or a modified target.
   Process parent=null;bool parentAlreadyGone=false;
   try{parent=Process.GetProcessById(data.parentId);}
   catch(ArgumentException){parentAlreadyGone=true;}
   if(!parentAlreadyGone)using(parent){
    if(parent.StartTime.ToUniversalTime().Ticks!=data.parentStart||
       !String.Equals(Path.GetFullPath(parent.MainModule.FileName),data.target,StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("Processo de origem diferente.");
    if(!parent.WaitForExit(60000))throw new IOException("FarmZilla ainda está encerrando.");
   }
   if(!SameOriginalBinary(Process.GetCurrentProcess().MainModule.FileName,data.target))
    throw new InvalidDataException("Arquivo de origem foi alterado desde o fechamento.");
   AppUpdate.ApplyFile(data.target,bytes,path=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)}));
   File.Delete(staged);File.Delete(request);
  }catch(Exception ex){
   DiagnosticLog.Write("Atualização Control não aplicada: "+ex.Message);
   System.Windows.Forms.MessageBox.Show("Não foi possível instalar: "+ex.Message+"\nA versão anterior foi preservada.","FarmZilla");
  }
  return true;
 }
 internal static void LaunchInstaller(string request,string currentExe){
  string installer=Path.Combine(Path.GetDirectoryName(request),"installer.exe");
  File.Copy(currentExe,installer,false);
  Process.Start(new ProcessStartInfo(installer,"--apply-control-update \""+request+"\""){UseShellExecute=false,CreateNoWindow=true});
 }
}
