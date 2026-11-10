using System;using System.Diagnostics;using System.Linq;using System.Threading.Tasks;using System.Windows.Forms;
static class AuthorizationSession {
 internal static string Token;static Stopwatch clock;static long remaining;static long highUtc;static bool lifetime;static string[] features=new string[0];
 // Whether this PC sends its log to the panel (turned on per PC by the administrator; off by default).
 internal static bool Log;
 internal static bool Has(string feature){return features.Contains(feature);}
 internal static void Begin(string token){if(Token==token&&clock!=null)return;long now=OnlineLicense.Millis(DateTime.UtcNow);var data=OnlineLicense.Verify(token,LicenseGate.Identity.Code,LicenseCore.PublicKey,now);Token=token;lifetime=data.kind=="lifetime";features=data.features??new string[0];Log=lifetime&&data.log;remaining=lifetime?long.MaxValue:Math.Min(86400000,data.expires-now);clock=Stopwatch.StartNew();highUtc=now;}
 // A lifetime license has no expiry, so clock changes do not matter; an old 24 h one keeps the previous checks.
 internal static bool Valid(){long now=OnlineLicense.Millis(DateTime.UtcNow);if(clock==null)return false;if(!lifetime&&(clock.ElapsedMilliseconds>=remaining||now+300000<highUtc))return false;highUtc=Math.Max(now,highUtc);try{OnlineLicense.Verify(Token,LicenseGate.Identity.Code,LicenseCore.PublicKey,now);return true;}catch{return false;}}
}
partial class AvalonInterno {
 bool authorizationDenied,authorizationChecking,logSending;string authorizationFailureReason;System.Windows.Forms.Timer authorizationTimer,logTimer;DateTime nextAuthorizationCheck;
 void InitAuthorization(){
  // Online check every 30 minutes, only to pick up a block, deletion or tool change (lifetime license, 0.38.0): without
  // internet the PC keeps working; an explicit refusal from the server ends the operations by the normal flow.
  nextAuthorizationCheck=DateTime.UtcNow.AddMinutes(30);authorizationTimer=new System.Windows.Forms.Timer{Interval=15000};
  authorizationTimer.Tick+=async(s,e)=>{
   if(authorizationDenied){if(!closeInProgress)Close();return;}
   if(authorizationChecking||closeInProgress)return;
   if(!AuthorizationSession.Valid()){DenyAuthorization("A autorização offline expirou ou o relógio mudou. Abra novamente com internet.");return;}
   if(DateTime.UtcNow<nextAuthorizationCheck)return;
   authorizationChecking=true;
   try{var result=await Task.Run(()=>OnlineLicense.Evaluate(LicenseGate.Identity,LicenseGate.ServiceUrl,()=>OnlineApi.Acquire(LicenseGate.Identity,LicenseGate.ServiceUrl)));if(IsDisposed||closeInProgress)return;if(!result.Allowed){DenyAuthorization(result.Reason);return;}AuthorizationSession.Begin(result.Token);AppUpdate.ClientAuthorization=result.Token;ApplyFeatures();nextAuthorizationCheck=DateTime.UtcNow.AddMinutes(30);}
   catch{DenyAuthorization("Não foi possível validar a autorização local com segurança.");}
   finally{authorizationChecking=false;}
  };
  // Every minute, new log lines go to the administrator's panel (/admin > Registro), only when he turned it on for this PC.
  logTimer=new System.Windows.Forms.Timer{Interval=60000};logTimer.Tick+=async(s,e)=>{if(logSending||authorizationDenied||!AuthorizationSession.Log)return;string token=AuthorizationSession.Token,service=LicenseGate.ServiceUrl;logSending=true;try{await Task.Run(()=>RemoteLog.Upload(service,token));}catch{}finally{logSending=false;}};
  Shown+=(s,e)=>{ApplyFeatures();authorizationTimer.Start();logTimer.Start();};FormClosed+=(s,e)=>{authorizationTimer.Dispose();logTimer.Dispose();if(authorizationFailureReason!=null)MessageBox.Show(authorizationFailureReason+"\nAs operações do FarmZilla foram encerradas. O jogo continua aberto.","Autorização do FarmZilla",MessageBoxButtons.OK,MessageBoxIcon.Information);};
 }
 void DenyAuthorization(string reason){if(authorizationDenied)return;authorizationDenied=true;authorizationFailureReason=reason;Enabled=false;shortcutActive=false;Bind();RequestAttackStop();missions.CancelAll();CancelParty();Note("Autorização encerrada: "+reason);Close();}
}

