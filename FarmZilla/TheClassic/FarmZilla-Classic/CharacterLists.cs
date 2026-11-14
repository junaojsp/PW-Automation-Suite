using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;using System.Threading.Tasks;using System.Windows.Forms;
partial class AvalonInterno {
 void InitializeCharacterLists(){
  main.DropDown+=(s,e)=>OrderPrincipalForDropDown();
  main.DropDownClosed+=(s,e)=>OrderPrincipalList();
  Func<object,int> classOf=item=>{var entry=item as Entry;return entry==null?-1:entry.Class;};foreach(var list in new[]{main}.Concat(rows))if(list is CyanCombo)((CyanCombo)list).ItemClass=classOf;
  var combo=main as CyanCombo;if(combo!=null)combo.ItemTag=item=>{var entry=item as Entry;return entry!=null&&entry.Pid==lastActiveGamePid?"● janela ativa":null;};
  hints.SetToolTip(main,"Ao abrir, a janela do jogo usada por último aparece no topo (● janela ativa). O principal só muda quando você escolhe.");
 }
 // Last game window in the foreground (checked by the main timer); suggested at the top of the Principal list, never selected automatically.
 int lastActiveGamePid;
 void TrackActiveGame(){uint pid;Win.GetWindowThreadProcessId(Win.GetForegroundWindow(),out pid);if(pid!=0&&main.Items.Cast<Entry>().Any(e=>e.Pid==pid))lastActiveGamePid=(int)pid;}
 void OrderPrincipalForDropDown(){
  if(syncing||main.Items.Count<2)return;var entries=main.Items.Cast<Entry>().ToArray();var current=main.SelectedItem as Entry;var active=entries.FirstOrDefault(e=>e.Pid==lastActiveGamePid);
  var order=new List<Entry>();if(active!=null)order.Add(active);if(current!=null&&current!=active)order.Add(current);foreach(var entry in entries)if(!order.Contains(entry))order.Add(entry);
  if(order.SequenceEqual(entries))return;var selected=main.SelectedItem;bool previous=syncing;syncing=true;main.BeginUpdate();
  try{main.Items.Clear();foreach(var entry in order)main.Items.Add(entry);if(selected!=null)main.SelectedItem=selected;}
  finally{main.EndUpdate();syncing=previous;}
 }
 // Class icons are read once from the game installed on this PC, in the background; lists repaint when they arrive.
 void LoadClassIcons(int pid){Task.Run(()=>{string exe;try{using(var p=Process.GetProcessById(pid))exe=p.MainModule.FileName;}catch{return;}string note=ClassIcons.Load(exe);try{if(!IsDisposed)BeginInvoke(new Action(()=>{if(note!=null)Note(note);main.Invalidate();foreach(var r in rows)r.Invalidate();}));}catch(InvalidOperationException){}});}
 void OrderPrincipalList(){
  if(syncing||main.SelectedItem==null||main.Items.Count<2)return;
  var entries=main.Items.Cast<Entry>().ToArray();var first=main.SelectedItem as Entry;if(first==null||entries[0]==first)return;
  var selected=main.SelectedItem;bool previous=syncing;syncing=true;main.BeginUpdate();
  try{main.Items.Clear();main.Items.Add(first);foreach(var entry in entries)if(entry!=first)main.Items.Add(entry);if(selected!=null)main.SelectedItem=selected;}
  finally{main.EndUpdate();syncing=previous;}
 }
 void PopulateFollowers(int[] chosen){bool previous=syncing;syncing=true;try{
  var primary=main.SelectedItem as Entry;int pid=primary==null?0:primary.Pid;
  var entries=followers.Items.Cast<Entry>().Where(v=>v.Pid!=pid).ToArray();
  var used=new HashSet<int>();var selected=new Entry[rows.Length];
  for(int i=0;i<rows.Length;i++){int wanted=i<chosen.Length?chosen[i]:0;var entry=entries.FirstOrDefault(x=>x.Pid==wanted);if(entry!=null&&used.Add(entry.Pid))selected[i]=entry;}
  for(int i=0;i<rows.Length;i++){var row=rows[i];row.BeginUpdate();try{
   row.Items.Clear();row.Items.Add("—");foreach(var entry in entries)if(!used.Contains(entry.Pid)||entry==selected[i])row.Items.Add(entry);
   if(selected[i]==null)row.SelectedIndex=0;else row.SelectedItem=selected[i];
  }finally{row.EndUpdate();}}
  for(int i=0;i<followers.Items.Count;i++)followers.SetItemChecked(i,used.Contains(((Entry)followers.Items[i]).Pid));
 }finally{syncing=previous;UpdateAttackRows();}}
 void SyncFollowers(ComboBox changed){if(syncing)return;RequestAttackStop();int changedRow=Array.IndexOf(rows,changed);if(changedRow>=0){rolePid[changedRow]=0;UpdateAttackRows();}teleport.SelectionChanged();CancelParty();missions.CancelAll();
  var selected=changed.SelectedItem as Entry;var chosen=rows.Select(r=>r.SelectedItem as Entry).Select(x=>x==null?0:x.Pid).ToArray();
  if(selected!=null)for(int i=0;i<chosen.Length;i++)if(i!=changedRow&&chosen[i]==selected.Pid)chosen[i]=0;
  PopulateFollowers(chosen);
 }
}

