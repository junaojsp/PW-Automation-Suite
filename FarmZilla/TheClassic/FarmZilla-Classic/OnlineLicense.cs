using System;using System.Diagnostics;using System.IO;using System.Net;using System.Security.Cryptography;using System.Text;using System.Web.Script.Serialization;
sealed class OnlineLease {public string product {get;set;}public string device {get;set;}public long issued {get;set;}public long expires {get;set;}public string kind {get;set;}public string[] features {get;set;}public bool log {get;set;}}
sealed class LeaseCache {public string Token {get;set;}public long LastUtc {get;set;}public string Service {get;set;}}
sealed class AccessResult {public bool Allowed;public string Reason;public string Token;}
static class OnlineLicense {
 internal static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
 internal static long Millis(DateTime utc){return (long)(utc.ToUniversalTime()-Epoch).TotalMilliseconds;}
 internal static OnlineLease Verify(string token,string code,string publicKey,long now){
  if(token==null||token.Length>8192)throw new InvalidDataException("Autorização inválida.");string[] p=token.Split('.');if(p.Length!=3||(p[0]!="BGL2"&&p[0]!="BGL3"))throw new InvalidDataException("Autorização antiga não permite controle remoto. Solicite liberação no gerenciador.");
  byte[] bytes=LicenseCore.Decode(p[1]);using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(!rsa.VerifyData(bytes,"SHA256",LicenseCore.Decode(p[2])))throw new InvalidDataException("Assinatura de autorização inválida.");}
  var lease=new JavaScriptSerializer().Deserialize<OnlineLease>(new UTF8Encoding(false,true).GetString(bytes));
  // BGL3 (0.38.0): lifetime license, no expiry, carries the tools the administrator gave this PC.
  if(p[0]=="BGL3"){if(lease==null||lease.product!="Bagaceira"||lease.kind!="lifetime"||lease.device!=code||lease.features==null)throw new InvalidDataException("Licença inválida ou de outro PC.");return lease;}
  // BGL2: 24 h authorization saved by versions before 0.38.0; it gives both tools until the first online check.
  if(lease!=null)lease.features=new[]{"controle","rota"};
  if(lease==null||lease.product!="Bagaceira"||lease.device!=code||lease.issued>now+300000||lease.expires<=now||lease.expires-lease.issued!=86400000)throw new InvalidDataException("Autorização expirada, inválida ou de outro PC. Conecte-se à internet.");return lease;
 }
 internal static string CacheFileOverride=null;
 static string CachePath {get{return CacheFileOverride??Path.Combine(LicenseCore.Home,"lease-v2.dat");}}
 internal static LeaseCache Load(){if(!File.Exists(CachePath))return null;return new JavaScriptSerializer().Deserialize<LeaseCache>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CachePath),null,DataProtectionScope.CurrentUser)));}
 internal static void Save(LeaseCache cache){Directory.CreateDirectory(LicenseCore.Home);File.WriteAllBytes(CachePath,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(cache)),null,DataProtectionScope.CurrentUser));}
 internal static bool FallbackEligible(Exception e){var w=e as WebException;if(w==null)return false;var response=w.Response as HttpWebResponse;if(response!=null)return (int)response.StatusCode>=500;return w.Status==WebExceptionStatus.Timeout||w.Status==WebExceptionStatus.ConnectFailure||w.Status==WebExceptionStatus.NameResolutionFailure||w.Status==WebExceptionStatus.ProxyNameResolutionFailure||w.Status==WebExceptionStatus.ReceiveFailure;}
 internal static AccessResult Evaluate(OnlineIdentity identity,string service,Func<string> acquire){
  return Evaluate(identity,service,acquire,LicenseCore.PublicKey);
 }
 internal static AccessResult Evaluate(OnlineIdentity identity,string service,Func<string> acquire,string publicKey){
  long now=Millis(DateTime.UtcNow);LeaseCache cache=null;try{cache=Load();}catch{}
  try{string token=acquire();Verify(token,identity.Code,publicKey,now);Save(new LeaseCache{Token=token,LastUtc=now,Service=service});return new AccessResult{Allowed=true,Token=token,Reason="Autorizado pelo gerenciador"};}
  catch(OnlineDenied e){Save(new LeaseCache{Token=null,LastUtc=now,Service=service});return new AccessResult{Reason=e.Message};}
  catch(Exception e){
   // Offline: the saved lifetime license keeps working with no time limit (an old 24 h one only until it expires).
   bool lifetime=cache!=null&&cache.Token!=null&&cache.Token.StartsWith("BGL3.");
   if(FallbackEligible(e)&&cache!=null&&cache.Token!=null&&cache.Service==service&&(lifetime||now+300000>=cache.LastUtc)){try{Verify(cache.Token,identity.Code,publicKey,now);cache.LastUtc=Math.Max(now,cache.LastUtc);Save(cache);return new AccessResult{Allowed=true,Token=cache.Token,Reason=lifetime?"Sem conexão: usando a licença vitalícia salva":"Sem conexão: usando tolerância offline de até 24 horas"};}catch{}}
   return new AccessResult{Reason="Não foi possível confirmar autorização. Conecte-se à internet ou contate o administrador."};
  }
 }
}
