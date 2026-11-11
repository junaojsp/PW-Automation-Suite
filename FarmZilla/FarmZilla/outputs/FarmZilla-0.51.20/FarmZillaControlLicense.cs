using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

// FarmZilla Control licensing transport. Not yet connected to the startup gate.
static class FarmZillaControlLicense {
 const string Endpoint="https://farmzilla.jrconect.com.br/api/bot/activate";
 const string FileName="farmzilla-control-license.dat";
 sealed class Reply {public bool ok {get;set;} public string reason {get;set;} public string expiresAt {get;set;}}
 internal static string DeviceHash(OnlineIdentity identity) {
  if(identity==null||String.IsNullOrEmpty(identity.Code))throw new InvalidOperationException("Identificação indisponível.");
  using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity.Code))).Replace("-","").ToLowerInvariant();
 }
 // Discover updates through the authenticated FarmZilla Control API only.
 // The response is informational: executables are NOT downloaded or installed here.
 sealed class UpdateCheckReply {public bool ok {get;set;} public bool updateAvailable {get;set;} public UpdateCheckRelease release {get;set;}}
 sealed class UpdateCheckRelease {public string version {get;set;} public string sha256 {get;set;} public long bytes {get;set;}}
 internal static string CheckPublishedUpdate(OnlineIdentity identity) {
  string key=LoadKey();
  if(String.IsNullOrWhiteSpace(key))throw new InvalidOperationException("Licença não disponível para consultar atualizações.");
  ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
  var req=(HttpWebRequest)WebRequest.Create("https://farmzilla.jrconect.com.br/api/bot/update-check");
  req.Method="POST";req.ContentType="application/json; charset=utf-8";req.Timeout=12000;req.ReadWriteTimeout=12000;req.AllowAutoRedirect=false;
  byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new {key=key.Trim(),deviceHash=DeviceHash(identity),version=ReleaseInfo.Version}));
  req.ContentLength=data.Length;
  try {
   using(var stream=req.GetRequestStream())stream.Write(data,0,data.Length);
   using(var response=(HttpWebResponse)req.GetResponse())
   using(var reader=new StreamReader(response.GetResponseStream())){
    if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("Resposta inválida do servidor de versões.");
    var result=new JavaScriptSerializer().Deserialize<UpdateCheckReply>(reader.ReadToEnd());
    if(result==null||!result.ok)throw new InvalidDataException("Resposta de atualização inválida.");
    if(!result.updateAvailable)return null;
    Version published,current;
    if(result.release==null||!Version.TryParse(result.release.version,out published)||!Version.TryParse(ReleaseInfo.Version,out current)||published<=current||
       result.release.bytes<1024||result.release.bytes>AppUpdate.MaxExe||String.IsNullOrEmpty(result.release.sha256)||result.release.sha256.Length!=64)
       throw new InvalidDataException("Metadados de versão inválidos.");
    return result.release.version;
   }
  }catch(WebException ex){
   var response=ex.Response as HttpWebResponse;
   if(response!=null)using(response){
    if(response.StatusCode==HttpStatusCode.Forbidden)throw new InvalidOperationException("Licença não autorizada para consultar atualizações.");
    throw new IOException("Falha HTTP ao consultar atualizações.");
   }
   throw new IOException("Servidor de atualizações indisponível.",ex);
  }
 }
 internal static string LoadKey() {
  string path=Path.Combine(LicenseCore.Home,FileName);
  if(!File.Exists(path))return null;
  return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));
 }
 internal static void SaveKey(string key) {
  if(String.IsNullOrWhiteSpace(key)||key.Length>128)throw new InvalidDataException("Chave inválida.");
  Directory.CreateDirectory(LicenseCore.Home);
  File.WriteAllBytes(Path.Combine(LicenseCore.Home,FileName),ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()),null,DataProtectionScope.CurrentUser));
 }
 internal static bool Activate(string key,OnlineIdentity identity,out string message) {
  bool unavailable;
  return Activate(key,identity,out message,out unavailable);
 }
 internal static bool Activate(string key,OnlineIdentity identity,out string message,out bool unavailable) {
  unavailable=false;
  message="Não foi possível validar a licença.";
  if(String.IsNullOrWhiteSpace(key)||key.Length>128){message="Informe a chave completa.";return false;}
  ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
  var req=(HttpWebRequest)WebRequest.Create(Endpoint);
  req.Method="POST";req.ContentType="application/json; charset=utf-8";req.Timeout=12000;req.ReadWriteTimeout=12000;req.AllowAutoRedirect=false;
  byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new {key=key.Trim(),deviceHash=DeviceHash(identity),version=ReleaseInfo.Version}));
  req.ContentLength=data.Length;
  try {
   using(var stream=req.GetRequestStream())stream.Write(data,0,data.Length);
   using(var response=(HttpWebResponse)req.GetResponse()) {
    if(response.StatusCode!=HttpStatusCode.OK){message="Resposta inesperada do servidor.";return false;}
    using(var reader=new StreamReader(response.GetResponseStream())) {
     var result=new JavaScriptSerializer().Deserialize<Reply>(reader.ReadToEnd());
     if(result!=null&&result.ok){message="Licença autorizada até "+result.expiresAt;return true;}
    }
   }
  }catch(WebException ex){
   var response=ex.Response as HttpWebResponse;
   if(response!=null){using(response){if(response.StatusCode==HttpStatusCode.Forbidden){message="Licença indisponível, vencida ou vinculada a outro computador.";return false;}}}
   unavailable=true;message="Servidor indisponível. Verifique a conexão e tente novamente.";return false;
  }
  return false;
 }
}
