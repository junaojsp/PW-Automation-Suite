using System;using System.Diagnostics;using System.Linq;
// 0.41.1 (owner: "remontar pt quebrou"): a game client that closed stayed in the lists (Limago, PID 6868) and Remontar PT
// stopped at its first check. Every 2 s the listed clients are checked; when one closed, the lists are synchronized
// (same as "Sincronizar personagens"), and a character that comes back with a new PID returns to its slot by name.
partial class AvalonInterno {
 System.Windows.Forms.Timer clientWatch;bool clientGone;
 internal static bool ProcessAlive(int pid){try{using(var p=Process.GetProcessById(pid)){try{return !p.HasExited;}catch{return true;}}}catch(ArgumentException){return false;}catch{return true;}}
 void InitClientWatch(){clientWatch=new System.Windows.Forms.Timer{Interval=2000};clientWatch.Tick+=(s,e)=>WatchClients();Shown+=(s,e)=>clientWatch.Start();FormClosed+=(s,e)=>clientWatch.Dispose();}
 void WatchClients(){
  if(closeInProgress||authorizationDenied)return;
  var gone=main.Items.Cast<Entry>().Where(e=>!ProcessAlive(e.Pid)).ToArray();
  if(gone.Length>0&&!clientGone){clientGone=true;foreach(var e in gone)Note(e.Name+": cliente do jogo fechado; tirado da lista.");}
  if(!clientGone||busy||teleport.Working||missions.Working)return;
  clientGone=false;Refresh();
 }
}
