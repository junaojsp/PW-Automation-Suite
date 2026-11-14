using System;using System.Diagnostics;using System.Linq;using System.Threading.Tasks;using System.Windows.Forms;
static class AuthorizationSession {
 internal static string Token;static Stopwatch clock;static long remaining;static long highUtc;static bool lifetime;static bool controlMode;static string[] features=new string[0];
 // Whether this PC sends its log to the panel (turned on per PC by the administrator; off by default).
 internal static bool Log;
 internal static void BeginControl(){controlMode=true;Token=null;features=new[]{"controle","rota"};Log=false;clock=Stopwatch.StartNew();}
 internal static bool Has(string feature){return features.Contains(feature);}
 internal static void Begin(string token){controlMode=false;if(Token==token&&clock!=null)return;long now=OnlineLicense.Millis(DateTime.UtcNow);var data=OnlineLicense.Verify(token,LicenseGate.Identity.Code,LicenseCore.PublicKey,now);Token=token;lifetime=data.kind=="lifetime";features=data.features??new string[0];Log=lifetime&&data.log;remaining=lifetime?long.MaxValue:Math.Min(86400000,data.expires-now);clock=Stopwatch.StartNew();highUtc=now;}
 // A lifetime license has no expiry, so clock changes do not matter; an old 24 h one keeps the previous checks.
 internal static bool Valid(){if(controlMode)return clock!=null;long now=OnlineLicense.Millis(DateTime.UtcNow);if(clock==null)return false;if(!lifetime&&(clock.ElapsedMilliseconds>=remaining||now+300000<highUtc))return false;highUtc=Math.Max(now,highUtc);try{OnlineLicense.Verify(Token,LicenseGate.Identity.Code,LicenseCore.PublicKey,now);return true;}catch{return false;}}
}
partial class AvalonInterno {
 bool authorizationDenied;string authorizationFailureReason;
 // The Classic: the FarmZilla Control license and its online checks belong to the Avalon product; this build runs unlicensed.
 void InitAuthorization(){Shown+=(s,e)=>ApplyFeatures();}
 void DenyAuthorization(string reason){if(authorizationDenied)return;authorizationDenied=true;authorizationFailureReason=reason;Enabled=false;shortcutActive=false;Bind();RequestAttackStop();missions.CancelAll();CancelParty();Note("Autorização encerrada: "+reason);Close();}
}

