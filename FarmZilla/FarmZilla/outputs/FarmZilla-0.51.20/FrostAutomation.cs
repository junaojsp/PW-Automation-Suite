using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class FrostAutomationForm : Form {
 sealed class Choice {public DgBot.Account Account;public override string ToString(){return Account.Name+" · PID "+Account.Pid;}}
 readonly Func<List<DgBot.Account>> accounts;readonly Func<bool> allowed;readonly Func<Task> prepare;
 readonly ComboBox leader=new ComboBox();readonly CheckedListBox followers=new CheckedListBox();readonly TextBox log=new TextBox();readonly Label status=new Label();
 readonly Button dg=new NeonButton(),refresh=new NeonButton();readonly Timer timer=new Timer();
 FrostBot boss;DgBot dungeon;bool starting,closing;readonly List<int> reserved=new List<int>();
 public CharacterRef CapturePrimary {get{if(dungeon==null||!dungeon.Running)return null;var l=leader.SelectedItem as Choice;return l==null?null:new CharacterRef{Pid=l.Account.Pid,Id=l.Account.Id,Name=l.Account.Name};}}
 public void Observe(byte[] packet){if(dungeon!=null&&dungeon.Running)dungeon.Observe(packet);}
 public bool Running {get{return starting||(boss!=null&&boss.Running)||(dungeon!=null&&dungeon.Running);}}
 public FrostAutomationForm(Func<List<DgBot.Account>> accounts,Func<bool> allowed,Func<Task> prepare,bool discover=true){
  this.accounts=accounts;this.allowed=allowed;this.prepare=prepare;Text="FarmZilla · Frost · teste Bagaceira";ClientSize=new Size(390,604);StartPosition=FormStartPosition.CenterParent;BackColor=Rubi.Background;ForeColor=Rubi.Text;Font=new Font("Segoe UI",9);
  AddLabel("BOT FROST · TESTE AVALON",12,10,366,22,Rubi.Accent);AddLabel("Rotinas Bagaceira. DG de teste: um ciclo, sem remontagem automática.",12,36,366,36,Rubi.Muted);
  AddLabel("Líder",12,80,280,20,Rubi.Text);leader.SetBounds(12,102,240,26);leader.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(leader);
  refresh.Text="Sincronizar";refresh.SetBounds(260,100,118,30);refresh.Click+=(s,e)=>RefreshAccounts();Controls.Add(refresh);
  AddLabel("Secundários (líder incluído automaticamente)",12,142,366,20,Rubi.Text);followers.SetBounds(12,166,366,132);followers.CheckOnClick=true;followers.BackColor=Rubi.Panel;followers.ForeColor=Rubi.Text;Controls.Add(followers);
  dg.Text="Iniciar DG · "+DgBot.RouteSteps+" passos";dg.SetBounds(12,312,366,36);dg.Click+=async(s,e)=>await Toggle(null);Controls.Add(dg);
  status.SetBounds(12,360,366,42);status.Text="Parado. Selecione as contas que participarão do teste.";Controls.Add(status);
  log.SetBounds(12,410,366,180);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Rubi.Panel;log.ForeColor=Rubi.Text;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(log);
  timer.Interval=500;timer.Tick+=(s,e)=>{if(!allowed())RequestStop();Buttons();};timer.Start();Shown+=(s,e)=>{if(discover)RefreshAccounts();};
  FormClosing+=async(s,e)=>{if(closing)return;e.Cancel=true;Enabled=false;await StopAsync();closing=true;Close();};
 }
 void AddLabel(string text,int x,int y,int w,int h,Color color){Controls.Add(new Label{Text=text,Bounds=new Rectangle(x,y,w,h),ForeColor=color});}
 void Ui(Action action){if(IsDisposed||Disposing)return;try{if(InvokeRequired)BeginInvoke(action);else action();}catch(InvalidOperationException){}}
 void Note(string text){DiagnosticLog.Write("Frost runtime: "+text);Ui(()=>{if(log.TextLength>35000)log.Clear();log.AppendText(DateTime.Now.ToString("HH:mm:ss")+" "+text+Environment.NewLine);});}
 void State(string text){Ui(()=>status.Text=text);}
 void RefreshAccounts(){if(Running)return;leader.Items.Clear();followers.Items.Clear();foreach(var a in accounts()){var choice=new Choice{Account=a};leader.Items.Add(choice);followers.Items.Add(choice,false);}if(leader.Items.Count>0)leader.SelectedIndex=0;Note("DESCOBERTA contas="+leader.Items.Count);}
 List<DgBot.Account> Selection(){var l=leader.SelectedItem as Choice;if(l==null)throw new InvalidOperationException("Selecione o líder.");var list=new List<DgBot.Account>{l.Account};foreach(Choice c in followers.CheckedItems)if(!list.Any(a=>a.Pid==c.Account.Pid))list.Add(c.Account);return list;}
 async Task Toggle(FrostBot.Mode? mode){
  if(Running){RequestStop();return;}if(!allowed()){Note("INICIO_RECUSADO: ative o FarmZilla e confirme a autorização antes do teste.");return;}
  starting=true;Buttons();try{
   var selected=Selection();foreach(var a in selected){if(!RotaLock.TrySet(a.Pid,"Frost Farm"))throw new InvalidOperationException(a.Name+" está em outra automação de rota/Frost.");reserved.Add(a.Pid);}
   await prepare();if(!allowed())throw new InvalidOperationException("Sessão indisponível.");
   // Revalidate each client immediately before any imported routine can send commands.
   foreach(var a in selected)using(var client=new RotaAvalon.GameClient(a.Pid,new Dictionary<string,bool>())){var snapshot=client.Read();if(snapshot.Name!=a.Name)throw new InvalidOperationException("Personagem mudou: "+a.Name);}
   Note("INICIO versão="+ReleaseInfo.Version+" modo="+(mode.HasValue?mode.ToString():"DG ciclo único")+" contas="+String.Join(",",selected.Select(a=>a.Name+":"+a.Pid)));
   if(mode.HasValue){boss=new FrostBot(mode.Value,Note);boss.Start(selected.Select(a=>new FrostBot.Account{Pid=a.Pid,Name=a.Name}));State(FrostBot.Title(mode.Value)+" em execução.");}
   else{dungeon=new DgBot(selected[0],selected,Note,State,null);dungeon.Start();}
  }catch(Exception ex){Note("INICIO_ERRO "+ex);RequestStop();}finally{starting=false;if(!Running)Release();Buttons();}
 }
 void RequestStop(){if(boss!=null)boss.Stop();if(dungeon!=null)dungeon.Stop();if(Running)State("Parando; aguardando comandos em andamento…");}
 void Release(){foreach(int pid in reserved)RotaLock.ClearIf(pid,"Frost Farm");reserved.Clear();}
 void Buttons(){bool running=Running;if(!running)Release();leader.Enabled=followers.Enabled=refresh.Enabled=!running;dg.Enabled=!starting;dg.Text=running?"Parar Frost":"Iniciar DG · "+DgBot.RouteSteps+" passos";}
 public async Task StopAsync(){RequestStop();while(Running)await Task.Delay(100);Release();}
 protected override void Dispose(bool disposing){if(disposing){RequestStop();timer.Dispose();}base.Dispose(disposing);}
}
partial class AvalonInterno {
 FrostAutomationForm frostAutomation;
 List<DgBot.Account> FrostAccounts(){return main.Items.Cast<object>().OfType<Entry>().Where(e=>!blocked.Contains(e.Pid)).Select(e=>new DgBot.Account{Pid=e.Pid,Id=e.Id,Name=e.Name,Class=e.Class}).ToList();}
 async Task PrepareFrost(){await StopAttacks();await missions.CancelAndWait();}
 Panel frostInlineHost;bool frostInlineView;SideItem sideFrostInline;
 void OpenFrostAutomation(){
  if(closeInProgress||authorizationDenied)return;if(rotaView)ShowRotaView(false);
  if(frostAutomation==null||frostAutomation.IsDisposed){
   frostAutomation=new FrostAutomationForm(FrostAccounts,()=>!authorizationDenied&&!closeInProgress&&shortcutActive&&!controlLocked,PrepareFrost);
   frostAutomation.TopLevel=false;frostAutomation.FormBorderStyle=FormBorderStyle.None;frostAutomation.Dock=DockStyle.Fill;
   frostInlineHost=new Panel{BackColor=Rubi.Background};frostInlineHost.Controls.Add(frostAutomation);Controls.Add(frostInlineHost);
  }
  teleport.FrostPrimary=()=>frostAutomation==null||frostAutomation.IsDisposed?null:frostAutomation.CapturePrimary;
  teleport.OnFrostCommand=p=>{if(frostAutomation!=null&&!frostAutomation.IsDisposed)frostAutomation.Observe(p);};
  if(!frostInlineView){panelVisible.Clear();foreach(var c in panelBody){panelVisible[c]=c.Visible;c.Visible=false;}}
  frostInlineView=true;sidePersonagens.Selected=false;sideRota.Selected=false;if(sideFrostInline!=null)sideFrostInline.Selected=true;
  LayoutInlinePanels();frostInlineHost.Visible=true;frostInlineHost.BringToFront();frostAutomation.Show();if(sharedPotBar!=null)sharedPotBar.BringToFront();if(potDrawer!=null&&potDrawer.Visible)potDrawer.BringToFront();
 }
 void ShowEspelhoInline(){
  if(rotaView)ShowRotaView(false);
  if(frostInlineView){frostInlineView=false;if(frostInlineHost!=null)frostInlineHost.Visible=false;foreach(var kv in panelVisible)kv.Key.Visible=kv.Value;UpdateAttackRows();}
  sidePersonagens.Selected=true;if(sideFrostInline!=null)sideFrostInline.Selected=false;LayoutInlinePanels();Invalidate(true);
 }
 async Task StopFrostAutomation(){if(frostAutomation!=null&&!frostAutomation.IsDisposed)await frostAutomation.StopAsync();}
}
