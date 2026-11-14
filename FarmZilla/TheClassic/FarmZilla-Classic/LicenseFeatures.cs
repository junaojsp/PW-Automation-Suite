using System;using System.Drawing;using System.Linq;using System.Windows.Forms;
// 0.38.0 (owner): the administrator chooses per PC which tools FarmZilla opens: "controle" (Personagens: follow, attack,
// PT, buff, teleport, missions) and "rota" (Rota/Farm). The choice comes inside the signed lifetime license and is
// applied at startup and on every online check (30 min); a tool taken away stops by the normal flow.
partial class AvalonInterno {
 bool controlLocked,featuresApplied;Label lockedNotice;
 void ApplyFeatures(){
  bool control=AuthorizationSession.Has("controle"),rota=AuthorizationSession.Has("rota");
  if(sideRota!=null){sideRota.Text=rota?"Mat Farm":"Mat Farm · bloqueado";hints.SetToolTip(sideRota,rota?"Rotas de coleta por coordenadas (mapa, materiais, inventário e log), na mesma janela. Quem está em rota fica fora do seguir, ataque, teleporte e missões.":"O Mat Farm não está liberado para este computador. Fale com o administrador.");}
  if(!rota&&rotaWindows.Count>0){if(rotaView)ShowRotaView(false);var task=CloseRota();Note("Mat Farm retirado pelo administrador.");}
  if(control==!controlLocked&&featuresApplied)return;featuresApplied=true;controlLocked=!control;
  if(controlLocked){RequestAttackStop();missions.CancelAll();CancelParty();}
  foreach(var c in panelBody)c.Enabled=control;
  if(lockedNotice==null){lockedNotice=new Label{Text="Espelho não liberado para este computador.\nFale com o administrador do FarmZilla.",TextAlign=ContentAlignment.MiddleCenter,ForeColor=Rubi.Text,BackColor=Rubi.Panel,Font=new Font("Segoe UI",(float)(10)*global::UiScale.F,FontStyle.Bold),Bounds=new Rectangle(SideWidth+24,250,PanelWidth-48,70),Visible=false};Controls.Add(lockedNotice);}
  lockedNotice.Visible=controlLocked&&!rotaView;if(lockedNotice.Visible)lockedNotice.BringToFront();
  Bind();if(controlLocked)Note("Espelho não liberado para este computador.");
 }
}
