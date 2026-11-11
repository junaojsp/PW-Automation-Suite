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
   message="Servidor indisponível. Verifique a conexão e tente novamente.";return false;
  }
  return false;
 }
}
