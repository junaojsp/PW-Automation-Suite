using System;using System.IO;using System.Net;using System.Security.Cryptography;using System.Text;using System.Web.Script.Serialization;
// Autoteste 0.38.0 sem servidor: licença vitalícia BGL3 (sem prazo, ferramentas por PC), cache offline sem limite e
// recusa explícita (bloqueio/remoção) apagando o cache.
class LifetimeLicenseTest {
 static int count;static void Assert(bool b,string label){if(!b)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}static void Reject(Action a,string label){bool fail=false;try{a();}catch{fail=true;}Assert(fail,label);}
 static string Token(string prefix,object payload,RSACryptoServiceProvider rsa){byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(payload));return prefix+"."+LicenseCore.Encode(bytes)+"."+LicenseCore.Encode(rsa.SignData(bytes,"SHA256"));}
 static int Main(){try{using(var rsa=new RSACryptoServiceProvider(2048))using(var other=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;other.PersistKeyInCsp=false;
  long now=OnlineLicense.Millis(DateTime.UtcNow);string code="BGC2-"+new string('A',64),pub=rsa.ToXmlString(false);
  string life=Token("BGL3",new{product="Bagaceira",kind="lifetime",device=code,issued=now,features=new[]{"rota"},log=true},rsa);
  var data=OnlineLicense.Verify(life,code,pub,now+20L*365*86400000);Assert(data.kind=="lifetime"&&data.features.Length==1&&data.features[0]=="rota"&&data.log,"licença vitalícia vale 20 anos depois e traz as ferramentas");
  Assert(OnlineLicense.Verify(life,code,pub,now-3L*86400000).device==code,"relógio atrasado não invalida a licença vitalícia");
  Reject(()=>OnlineLicense.Verify(life,"BGC2-"+new string('B',64),pub,now),"licença vitalícia de outro PC recusada");
  Reject(()=>OnlineLicense.Verify(Token("BGL3",new{product="Bagaceira",kind="lifetime",device=code,issued=now,features=new[]{"controle","rota"}},other),code,pub,now),"licença assinada por outra chave recusada");
  {string[] p=life.Split('.');var forged=Encoding.UTF8.GetString(LicenseCore.Decode(p[1])).Replace("[\"rota\"]","[\"controle\",\"rota\"]");Reject(()=>OnlineLicense.Verify("BGL3."+LicenseCore.Encode(Encoding.UTF8.GetBytes(forged))+"."+p[2],code,pub,now),"editar as ferramentas na licença quebra a assinatura");}
  Reject(()=>OnlineLicense.Verify(Token("BGL3",new{product="Bagaceira",kind="trial",device=code,issued=now,features=new string[0]},rsa),code,pub,now),"tipo diferente de vitalícia recusado");
  string old=Token("BGL2",new OnlineLease{product="Bagaceira",device=code,issued=now,expires=now+86400000},rsa);var o=OnlineLicense.Verify(old,code,pub,now);Assert(o.features.Length==2&&o.kind==null,"autorização antiga de 24 h ainda aceita e libera as duas ferramentas");
  Reject(()=>OnlineLicense.Verify(old,code,pub,now+86400000),"autorização antiga continua expirando em 24 h");
  OnlineLicense.CacheFileOverride=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lifetime-cache-test-"+Guid.NewGuid().ToString("N")+".dat");
  try{var identity=new OnlineIdentity{Code=code};string service="https://control.example";Func<string> offline=()=>{throw new WebException("offline",WebExceptionStatus.ConnectFailure);};
   Assert(OnlineLicense.Evaluate(identity,service,()=>life,pub).Allowed,"ativação online salva a licença vitalícia");
   var cache=OnlineLicense.Load();cache.LastUtc=now+30L*86400000;OnlineLicense.Save(cache);
   var r=OnlineLicense.Evaluate(identity,service,offline,pub);Assert(r.Allowed&&r.Token==life&&r.Reason.Contains("vitalícia"),"sem internet continua funcionando sem limite de tempo");
   Assert(!OnlineLicense.Evaluate(identity,"https://other.example",offline,pub).Allowed,"cache não muda de servidor silenciosamente");
   Assert(!OnlineLicense.Evaluate(identity,service,()=>{throw new OnlineDenied("bloqueado");},pub).Allowed,"bloqueio ou remoção no painel encerra quando online");
   Assert(!OnlineLicense.Evaluate(identity,service,offline,pub).Allowed,"depois do bloqueio, ficar offline não reaproveita a licença");}
  finally{try{File.Delete(OnlineLicense.CacheFileOverride);}catch{}OnlineLicense.CacheFileOverride=null;}
  Console.WriteLine(count+" verificações da licença vitalícia aprovadas.");return 0;}}catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}
