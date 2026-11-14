using System;using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;
// 0.49.0 (owner): the tactical bag given inside the ice city (item 52287) is equipped by itself on every listed account as soon
// as it shows up in the bag. Command captured on the principal (06/11/2026 20:16:47, Limao, bag in slot 20):
// 11 00 <bag slot> 16 = move that bag slot to equipment slot 22 (the game also sent 15 00 right after; not needed to equip).
// Confirmed by reading the bag again: the item must leave it. Up to 3 tries, then 10 minutes of pause for that account.
partial class AvalonInterno {
 const int TacticalBag=52287;const byte BagEquipSlot=0x16;
 System.Windows.Forms.Timer bagTimer;bool bagRunning;
 readonly Dictionary<int,RotaAvalon.GameClient> bagClients=new Dictionary<int,RotaAvalon.GameClient>();readonly Dictionary<string,bool> bagVerified=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<int,int> bagHandled=new Dictionary<int,int>(),bagTries=new Dictionary<int,int>();readonly Dictionary<int,DateTime> bagPause=new Dictionary<int,DateTime>(),deadSince=new Dictionary<int,DateTime>();
 void InitBagEquip(){bagTimer=new System.Windows.Forms.Timer{Interval=2000};bagTimer.Tick+=async(s,e)=>await BagTick();Shown+=(s,e)=>bagTimer.Start();
  FormClosed+=(s,e)=>{bagTimer.Dispose();lock(bagClients){foreach(var c in bagClients.Values)try{c.Dispose();}catch{}bagClients.Clear();}};}
 async Task BagTick(){
  if(bagRunning||closeInProgress||authorizationDenied||!shortcutActive)return;
  var list=main.Items.Cast<object>().OfType<Entry>().Where(e=>!blocked.Contains(e.Pid)&&!teleport.IsClientBusy(e.Pid)&&!missions.IsClientBusy(e.Pid)).Select(e=>e.Ref()).ToList();
  if(list.Count==0)return;bagRunning=true;var clericPids=new HashSet<int>(main.Items.Cast<object>().OfType<Entry>().Where(e=>e.Class==ClassIcons.Cleric).Select(e=>e.Pid));var wfPids=new HashSet<int>(main.Items.Cast<object>().OfType<Entry>().Where(e=>e.Class==ClassIcons.Venomancer&&!RotaLock.Busy(e.Pid)).Select(e=>e.Pid));
  try{var notes=await Task.Run(()=>BagWork(list,clericPids,wfPids));foreach(var n in notes)Note(n);}catch{}finally{bagRunning=false;}
 }
 static int BagCount(RotaAvalon.InventorySnapshot bag){return bag.Items.Where(i=>i.Id==TacticalBag).Sum(i=>Math.Max(1,i.Quantity));}
 List<string> BagWork(List<CharacterRef> list,HashSet<int> clericPids,HashSet<int> wfPids){var notes=new List<string>();var dead=new List<CharacterRef>();var clerics=new List<CharacterRef>();
  lock(bagClients){
   foreach(var pid in bagClients.Keys.Where(p=>list.All(c=>c.Pid!=p)).ToList()){try{bagClients[pid].Dispose();}catch{}bagClients.Remove(pid);}
   foreach(var c in list){
    try{
     RotaAvalon.GameClient g;if(!bagClients.TryGetValue(c.Pid,out g)){g=new RotaAvalon.GameClient(c.Pid,bagVerified);bagClients[c.Pid]=g;}
     // 0.50.2 (owner): a dead account accepts a resurrection by itself (sent every 2 s while dead; with no offer the game ignores it).
     var life=g.Read();if(life.Hp<=0)dead.Add(c);else if(clericPids.Contains(c.Pid))clerics.Add(c);
     if(life.Hp<=0&&!reviveOn){deadSince.Remove(c.Pid);}else if(life.Hp<=0){DateTime since;if(!deadSince.TryGetValue(c.Pid,out since)){deadSince[c.Pid]=DateTime.UtcNow;notes.Add(c.Name+": morto; aceitando ressurreição assim que alguém der a prece.");continue;}
      if((DateTime.UtcNow-since).TotalSeconds<1)continue;
      TeleportBridge rb=null;try{rb=new TeleportBridge(c.Pid);if(rb.CharacterId!=c.Id)throw new Exception("Personagem mudou.");rb.SendReviveAccept();}
      finally{if(rb!=null){rb.Dispose();if(!rb.RestoreSucceeded){lock(blocked)blocked.Add(c.Pid);notes.Add(c.Name+": ponte sem restauração ao aceitar ressurreição; reinicie esse cliente.");}}}
      continue;}
     if(deadSince.Remove(c.Pid))notes.Add(c.Name+": voltou à vida.");
     var bag=g.ReadInventory();int count=BagCount(bag);int handled;bagHandled.TryGetValue(c.Pid,out handled);
     if(count==0){bagHandled[c.Pid]=0;bagTries.Remove(c.Pid);continue;}
     if(count<=handled)continue;
     DateTime pause;if(bagPause.TryGetValue(c.Pid,out pause)&&DateTime.UtcNow<pause)continue;
     var item=bag.Items.Where(i=>i.Id==TacticalBag).OrderBy(i=>i.Slot).First();if(item.Slot<0||item.Slot>200){bagHandled[c.Pid]=count;continue;}
     TeleportBridge b=null;try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");b.SendEquip(new byte[]{0x11,0x00,(byte)item.Slot,BagEquipSlot});}
     finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(c.Pid);notes.Add(c.Name+": ponte sem restauração ao equipar a bolsa; reinicie esse cliente.");}}}
     int after=count;for(int i=0;i<15&&after>=count;i++){Thread.Sleep(100);after=BagCount(g.ReadInventory());}
     if(after<count){bagHandled[c.Pid]=after;bagTries.Remove(c.Pid);notes.Add(c.Name+": bolsa tática equipada.");continue;}
     int tries;bagTries.TryGetValue(c.Pid,out tries);tries++;bagTries[c.Pid]=tries;
     if(tries>=3){bagTries.Remove(c.Pid);bagPause[c.Pid]=DateTime.UtcNow.AddMinutes(10);notes.Add(c.Name+": a bolsa tática não saiu da mochila em 3 tentativas; tento de novo em 10 min.");}
    }
    catch(Exception e){RotaAvalon.GameClient g;if(bagClients.TryGetValue(c.Pid,out g)){try{g.Dispose();}catch{}bagClients.Remove(c.Pid);}
     DateTime last;if(!bagPause.TryGetValue(-c.Pid,out last)||DateTime.UtcNow>last){bagPause[-c.Pid]=DateTime.UtcNow.AddMinutes(5);notes.Add(c.Name+": bolsa tática: "+e.Message);}}
   }
  }
  lock(deadNow){deadNow.Clear();foreach(var d in dead)deadNow.Add(d.Pid);}
  try{ResurrectWork(dead,clerics,notes);}catch(Exception e){notes.Add("Ressuscitar: "+e.Message);}
  try{PetCareWork(list.Where(c=>wfPids.Contains(c.Pid)&&dead.All(d=>d.Pid!=c.Pid)).ToList(),notes);}catch(Exception e){notes.Add("Pet: "+e.Message);}
  return notes;}
}
