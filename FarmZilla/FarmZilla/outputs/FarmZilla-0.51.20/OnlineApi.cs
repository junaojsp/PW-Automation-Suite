using System;using System.IO;using System.Net;using System.Text;using System.Web.Script.Serialization;
sealed class OnlineDenied:Exception {public OnlineDenied(string message):base(message){}}
sealed class ChallengeReply {public string nonce {get;set;}}
sealed class LeaseReply {public string lease {get;set;}public string license {get;set;}}
static class OnlineApi {
 internal static string Service {
  get{string path=Path.Combine(LicenseCore.Home,"online-service.txt");return File.Exists(path)?File.ReadAllText(path).Trim():ReleaseInfo.DefaultService;}
  set{ValidateService(value);Directory.CreateDirectory(LicenseCore.Home);File.WriteAllText(Path.Combine(LicenseCore.Home,"online-service.txt"),new Uri(value.Trim()).GetLeftPart(UriPartial.Authority));}
 }
 internal static void ValidateService(string value){Uri u;if(!Uri.TryCreate(value,UriKind.Absolute,out u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo!=""||u.Query!=""||u.Fragment!=""||u.AbsolutePath!="/")throw new InvalidDataException("Informe somente o endereço HTTPS do serviço de ativação, sem caminhos ou parâmetros.");}
 internal static T Post<T>(string service,string path,object body,string bearer=null){
  ValidateService(service);var request=(HttpWebRequest)WebRequest.Create(service.TrimEnd('/')+path);ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;request.Method="POST";request.ContentType="application/json; charset=utf-8";request.UserAgent="Bagaceira/"+ReleaseInfo.Version;request.Timeout=12000;request.ReadWriteTimeout=12000;request.AllowAutoRedirect=false;if(bearer!=null)request.Headers[HttpRequestHeader.Authorization]="Bearer "+bearer;byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));request.ContentLength=bytes.Length;
  try{using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK)throw new IOException("Resposta inesperada do serviço.");using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){byte[] buffer=new byte[4096];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){if(memory.Length+n>16384)throw new InvalidDataException("Resposta excedeu o limite.");memory.Write(buffer,0,n);}return new JavaScriptSerializer().Deserialize<T>(Encoding.UTF8.GetString(memory.ToArray()));}}}
  catch(WebException ex){var response=ex.Response as HttpWebResponse;if(response!=null){int status=(int)response.StatusCode;response.Dispose();if(status==401||status==403)throw new OnlineDenied("Computador não ativado, bloqueado ou removido pelo administrador. Copie o código e envie a ele.");}throw;}
 }
 internal static string Acquire(OnlineIdentity identity,string service){var challenge=Post<ChallengeReply>(service,"/v1/device/register",new{device=identity.Code,hardware=identity.Hardware,publicKey=identity.Public});if(challenge==null||String.IsNullOrEmpty(challenge.nonce)||challenge.nonce.Length>100)throw new InvalidDataException("Desafio do servidor inválido.");var reply=Post<LeaseReply>(service,"/v1/device/lease",new{device=identity.Code,nonce=challenge.nonce,signature=identity.Sign(challenge.nonce),format=3});if(reply==null||String.IsNullOrEmpty(reply.license))throw new InvalidDataException("Autorização não recebida.");return reply.license;}
}
