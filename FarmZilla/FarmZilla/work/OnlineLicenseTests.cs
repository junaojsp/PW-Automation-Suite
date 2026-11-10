using System;using System.Collections.Generic;using System.IO;using System.Net;using System.Security.Cryptography;using System.Text;using System.Web.Script.Serialization;
class OnlineLicenseTests {
 static int count;static void Assert(bool b,string label){if(!b)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}static void Reject(Action a,string label){bool fail=false;try{a();}catch{fail=true;}Assert(fail,label);}
 static string Token(OnlineLease lease,RSACryptoServiceProvider rsa){byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(lease));return "BGL2."+LicenseCore.Encode(bytes)+"."+LicenseCore.Encode(rsa.SignData(bytes,"SHA256"));}
 static void Main(){using(var rsa=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;long now=OnlineLicense.Millis(DateTime.UtcNow);string code="BGC2-"+new string('A',64),pub=rsa.ToXmlString(false);var lease=new OnlineLease{product="Bagaceira",device=code,issued=now,expires=now+86400000};string token=Token(lease,rsa);
  Assert(OnlineLicense.Verify(token,code,pub,now).device==code,"autorização assinada aceita no PC correto");
  Reject(()=>OnlineLicense.Verify(token,"BGC2-"+new string('B',64),pub,now),"autorização de outro PC recusada");
  Reject(()=>OnlineLicense.Verify(token,code,pub,now+86400000),"tolerância de 24 horas expira");
  lease.expires=now+172800000;Reject(()=>OnlineLicense.Verify(Token(lease,rsa),code,pub,now),"autorização maior que 24 horas recusada");lease.expires=now+86400000;
  Reject(()=>OnlineLicense.Verify("BGL1.old.offline",code,pub,now),"licença antiga permanente não burla controle online");
  Assert(OnlineLicense.FallbackEligible(new WebException("offline",WebExceptionStatus.ConnectFailure)),"sem conexão permite avaliar cache");
  Assert(!OnlineLicense.FallbackEligible(new OnlineDenied("blocked")),"bloqueio explícito não permite tolerância offline");
  Assert(!OnlineLicense.FallbackEligible(new WebException("certificate",WebExceptionStatus.TrustFailure)),"falha de certificado não permite contornar validação");
  var parts=new Dictionary<string,string>{{"cpu","CPU-1"},{"disk","DISK-1"},{"board","BOARD-1"}};string h=OnlineIdentity.HardwareFromParts(parts);parts["disk"]="DISK-2";Assert(h!=OnlineIdentity.HardwareFromParts(parts),"troca de disco altera identificação");
  Reject(()=>OnlineIdentity.HardwareFromParts(new Dictionary<string,string>{{"cpu","CPU-1"}}),"um único identificador não basta");
  var key=new DevicePublicKey{kty="RSA",n="public-one",e="AQAB"};string a=OnlineIdentity.BuildCode(h,key);key.n="public-two";Assert(a!=OnlineIdentity.BuildCode(h,key),"copiar resumo de hardware sem chave não mantém código");
  OnlineLicense.CacheFileOverride=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"online-cache-test-"+Guid.NewGuid().ToString("N")+".dat");
  try{var identity=new OnlineIdentity{Code=code};string service="https://control.example";Func<string> offline=()=>{throw new WebException("offline",WebExceptionStatus.ConnectFailure);};
   var result=OnlineLicense.Evaluate(identity,service,()=>token,pub);Assert(result.Allowed&&OnlineLicense.Load().Token==token,"autorização online persiste cache protegido");
   Assert(OnlineLicense.Evaluate(identity,service,offline,pub).Allowed,"cache válido permite até 24 horas offline");
   Assert(!OnlineLicense.Evaluate(identity,"https://other.example",offline,pub).Allowed,"cache não muda de servidor silenciosamente");
   result=OnlineLicense.Evaluate(identity,service,()=>{throw new OnlineDenied("blocked");},pub);Assert(!result.Allowed&&OnlineLicense.Load().Token==null,"bloqueio explícito apaga autorização em cache");
   Assert(!OnlineLicense.Evaluate(identity,service,offline,pub).Allowed,"desconectar após bloqueio não restaura acesso");
   Assert(OnlineLicense.Evaluate(identity,service,()=>token,pub).Allowed,"nova autorização permite liberar novamente");
   var cache=OnlineLicense.Load();cache.LastUtc=now+3600000;OnlineLicense.Save(cache);Assert(!OnlineLicense.Evaluate(identity,service,offline,pub).Allowed,"voltar relógio além da tolerância não estende uso");
   lease.issued=now-86400001;lease.expires=now-1;OnlineLicense.Save(new LeaseCache{Token=Token(lease,rsa),LastUtc=now,Service=service});Assert(!OnlineLicense.Evaluate(identity,service,offline,pub).Allowed,"cache vencido não libera offline");
  }finally{if(File.Exists(OnlineLicense.CacheFileOverride))File.Delete(OnlineLicense.CacheFileOverride);OnlineLicense.CacheFileOverride=null;}
 }
 Console.WriteLine(count+" verificações de cliente online aprovadas.");}
}
