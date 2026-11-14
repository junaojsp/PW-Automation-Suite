using System;using System.Linq;using System.Threading;using System.Threading.Tasks;
// Automatic popup (Win_NPC visible without an NPC interaction): when the principal closes it without sending any action
// (notification, "Entendo!" on a finished step, X/Voltar), the popups still open on the secondaries are closed too.
// Popups that trigger an action are handled by TeleportService (mirror, confirm, then close).
partial class AvalonInterno {
 bool popupWasOpen,popupChecking,popupClosing;
 async Task PopupTick(){if(popupChecking||popupClosing||closeInProgress||authorizationDenied||!shortcutActive)return;var primary=main.SelectedItem as Entry;if(primary==null)return;
  bool open;popupChecking=true;try{var who=primary.Ref();open=await Task.Run(()=>QuestService.PopupOpen(who));}catch{return;}finally{popupChecking=false;}
  if(open){popupWasOpen=true;return;}if(!popupWasOpen)return;popupWasOpen=false;
  popupClosing=true;try{
   // Give an action time to be captured; if one is being mirrored, that flow closes the windows itself.
   await Task.Delay(1500);if(closeInProgress||teleport.Working||teleport.QuestChoicePending||missions.Working)return;
   var current=main.SelectedItem as Entry;if(current==null||current.Pid!=primary.Pid)return;
   var targets=followers.CheckedItems.Cast<Entry>().Where(rz=>!RotaLock.Busy(rz.Pid)).Where(e=>e.Pid!=primary.Pid&&!blocked.Contains(e.Pid)&&!teleport.IsClientBusy(e.Pid)&&!missions.IsClientBusy(e.Pid)).Select(e=>e.Ref()).ToArray();if(targets.Length==0)return;
   var results=await Task.WhenAll(targets.Select(t=>Task.Run(()=>{bool lockedOut=false;string error=null;bool closed=false;try{closed=QuestService.ClosePopup(t,CancellationToken.None,out lockedOut);}catch(Exception e){error=e.Message;}return Tuple.Create(t,closed,lockedOut,error);})));
   var closedNames=results.Where(r=>r.Item2).Select(r=>r.Item1.Name).ToArray();if(closedNames.Length>0)Note("Janela automática fechada no principal; fechada também em "+string.Join(", ",closedNames)+".");
   foreach(var r in results){if(r.Item3){blocked.Add(r.Item1.Pid);Note(r.Item1.Name+": ponte de fechamento sem restauração confirmada; reinicie esse cliente.");}else if(r.Item4!=null)Note(r.Item1.Name+": não foi possível fechar a janela automática: "+r.Item4);}
  }finally{popupClosing=false;}}
}
