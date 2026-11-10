using System;
using System.Drawing;
using System.Windows.Forms;
namespace RotaAvalon {
    // Mat Farm screen, "Manual" mode (0.47.0): record the path by flying it, save/load it as .txt, run it.
    public sealed partial class FarmPanel {
        public event Action RecordRequested,TrailStartRequested,TrailSaveRequested,TrailLoadRequested,ModeChanged,ReachChanged;
        public bool Manual{get;private set;}
        readonly Button autoMode=new Button(),manualMode=new Button(),record=new Button(),runTrail=new Button(),stopTrail=new Button(),saveTrail=new Button(),loadTrail=new Button();
        readonly NumericUpDown reach=new NumericUpDown(),trailHeight=new NumericUpDown();public event Action TrailHeightChanged;
        public double TrailHeight{get{return (double)trailHeight.Value;}set{trailHeight.Value=(decimal)Math.Max(10,Math.Min(300,value));}}readonly Label trailInfo=new Label();Control[] autoRows;Control trailButtons,trailArea;bool recordingTrail;
        public double Reach{get{return (double)reach.Value;}set{reach.Value=(decimal)Math.Max(TrailRoute.MinReach,Math.Min(TrailRoute.MaxReach,value));}}
        Control BuildModeBar(){var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};
            bar.Controls.Add(new Label{Text="2 · FARM",AutoSize=true,Font=new Font("Segoe UI Semibold",(float)(10.5f)*global::UiScale.F),Margin=new Padding(0,6,10,0)});
            foreach(var b in new[]{autoMode,manualMode}){b.Width=110;b.Height=27;b.Margin=new Padding(0,1,4,0);b.FlatStyle=FlatStyle.Flat;bar.Controls.Add(b);}
            autoMode.Text="Automático";manualMode.Text="Manual";autoMode.Click+=(s,e)=>SetMode(false);manualMode.Click+=(s,e)=>SetMode(true);return bar;}
        Control BuildTrailButtons(){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};trailButtons=p;
            record.Text="●  Gravar trajeto";record.Width=170;record.Height=40;record.Font=new Font("Segoe UI Semibold",(float)(10)*global::UiScale.F);record.Click+=(s,e)=>{if(RecordRequested!=null)RecordRequested();};p.Controls.Add(record);
            runTrail.Text="▶  Rodar trajeto";runTrail.Width=160;runTrail.Height=40;runTrail.BackColor=Color.FromArgb(36,109,169);runTrail.Font=new Font("Segoe UI Semibold",(float)(10)*global::UiScale.F);runTrail.Click+=(s,e)=>{if(TrailStartRequested!=null)TrailStartRequested();};p.Controls.Add(runTrail);
            stopTrail.Text="■  Parar";stopTrail.Width=110;stopTrail.Height=40;stopTrail.Font=new Font("Segoe UI Semibold",(float)(10)*global::UiScale.F);stopTrail.Enabled=false;stopTrail.Click+=(s,e)=>{if(StopRequested!=null)StopRequested();};p.Controls.Add(stopTrail);
            trailInfo.AutoSize=false;trailInfo.Width=330;trailInfo.Height=40;trailInfo.ForeColor=Color.SlateGray;trailInfo.Margin=new Padding(12,2,0,0);p.Controls.Add(trailInfo);SetTrail(null);return p;}
        Control BuildTrailArea(){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};trailArea=p;
            saveTrail.Text="Salvar trajeto (.txt)";saveTrail.Width=160;saveTrail.Height=30;saveTrail.Click+=(s,e)=>{if(TrailSaveRequested!=null)TrailSaveRequested();};p.Controls.Add(saveTrail);
            loadTrail.Text="Carregar trajeto (.txt)";loadTrail.Width=170;loadTrail.Height=30;loadTrail.Click+=(s,e)=>{if(TrailLoadRequested!=null)TrailLoadRequested();};p.Controls.Add(loadTrail);
            p.Controls.Add(new Label{Text="Descer até",AutoSize=true,Margin=new Padding(10,8,4,0)});reach.Minimum=(decimal)TrailRoute.MinReach;reach.Maximum=(decimal)TrailRoute.MaxReach;reach.Increment=5;reach.Value=50;reach.Width=56;reach.Margin=new Padding(0,5,4,0);reach.ValueChanged+=(s,e)=>{if(ReachChanged!=null)ReachChanged();};p.Controls.Add(reach);
            p.Controls.Add(new Label{Text="do caminho",AutoSize=true,ForeColor=Color.SlateGray,Margin=new Padding(0,8,0,0)});
            p.Controls.Add(new Label{Text="Altura de voo",AutoSize=true,Margin=new Padding(14,8,4,0)});trailHeight.Minimum=10;trailHeight.Maximum=300;trailHeight.Value=40;trailHeight.Width=56;trailHeight.Margin=new Padding(0,5,0,0);trailHeight.ValueChanged+=(s,e)=>{if(TrailHeightChanged!=null)TrailHeightChanged();};p.Controls.Add(trailHeight);return p;}
        public void SetMode(bool manual){if(running&&manual!=Manual)return;Manual=manual;
            if(autoRows!=null)foreach(var c in autoRows)c.Visible=!manual;if(trailButtons!=null)trailButtons.Visible=manual;if(trailArea!=null)trailArea.Visible=manual;
            autoMode.BackColor=manual?Rubi.Field:Rubi.Accent;autoMode.ForeColor=manual?Rubi.Text:Rubi.OnAccent;manualMode.BackColor=manual?Rubi.Accent:Rubi.Field;manualMode.ForeColor=manual?Rubi.OnAccent:Rubi.Text;
            hint.Visible=!manual;if(ModeChanged!=null)ModeChanged();UpdateText();}
        public void SetRecording(bool on){recordingTrail=on;record.Text=on?"■  Parar gravação":"●  Gravar trajeto";record.BackColor=on?Rubi.Shade(Rubi.Accent,-75):Rubi.Field;runTrail.Enabled=!on&&!running;loadTrail.Enabled=!on&&!running;autoMode.Enabled=!on&&!running;UpdateText();}
        public void SetTrail(string info){trailInfo.Text=info??"Clique Gravar e voe o caminho no jogo (voe baixo, uns 20–35 acima do chão, para os materiais aparecerem). Depois Parar e Salvar.";}
        void TrailRunning(bool value){record.Enabled=!value;runTrail.Enabled=!value&&!recordingTrail;stopTrail.Enabled=value;loadTrail.Enabled=!value&&!recordingTrail;reach.Enabled=trailHeight.Enabled=!value;autoMode.Enabled=manualMode.Enabled=!value&&!recordingTrail;}
    }
}
