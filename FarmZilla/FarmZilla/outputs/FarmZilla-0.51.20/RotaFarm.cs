using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
namespace RotaAvalon {
    // Automatic farm (FarmZilla 0.26.0, owner request): the points come from what was scanned (history of the current map),
    // only the chosen materials, in nearest-next order from the character. Rebuilt every lap, so new finds join the next one.
    public static class FarmPlanner {
        public const int MaxPoints=200;
        // radius 0 = no limit; center defaults to the character; excluded = Key() of points the owner removed (0.28.0).
        public static string Key(int template,double x,double z){return template+":"+Math.Round(x)+":"+Math.Round(z);}
        public static RouteDocument Build(IEnumerable<ResourceMarker> known,ICollection<int> chosen,ClientSnapshot player,double radius=0,ICollection<string> excluded=null,double centerX=double.NaN,double centerZ=double.NaN){
            var route=new RouteDocument{Name="Farm automático",Character=player==null?"":player.Name,Loop=false,ResourceIds=chosen.ToList()};
            if(player==null||known==null)return route;
            var kept=new List<ResourceMarker>();
            double cx=Double.IsNaN(centerX)?player.X:centerX,cz=Double.IsNaN(centerZ)?player.Z:centerZ;
            foreach(var r in known.Where(r=>chosen.Contains(r.TemplateId)&&r.LevelAllowed&&GameClient.ValidCoordinate(r.X)&&GameClient.ValidCoordinate(r.Z)&&(radius<=0||(r.X-cx)*(r.X-cx)+(r.Z-cz)*(r.Z-cz)<=radius*radius)&&(excluded==null||!excluded.Contains(Key(r.TemplateId,r.X,r.Z)))))
                if(!kept.Any(k=>k.TemplateId==r.TemplateId&&Math.Abs(k.X-r.X)<6&&Math.Abs(k.Z-r.Z)<6))kept.Add(r);
            double x=player.X,z=player.Z;
            while(kept.Count>0&&route.Points.Count<MaxPoints){
                int best=0;double bestDistance=double.MaxValue;for(int i=0;i<kept.Count;i++){double dx=kept[i].X-x,dz=kept[i].Z-z,d=dx*dx+dz*dz;if(d<bestDistance){bestDistance=d;best=i;}}
                var r=kept[best];kept.RemoveAt(best);x=r.X;z=r.Z;
                route.Points.Add(new Waypoint{X=r.X,Y=r.Seed?0:r.Y,Z=r.Z,Action="Coletar",ResourceId=r.TemplateId,ItemId=r.ItemId,WaitMs=0,Area=r.Seed});
            }
            return route;
        }
    }
    public sealed class FarmMaterial {public int Id;public string Name,Source;public int Points,Loaded;}
    // The simple screen: choose materials, start, stop, see what is happening.
    public sealed partial class FarmPanel : UserControl {
        public event Action StartRequested,StopRequested,RestoreRequested,PlanSettingsChanged,ScanRadiusChanged,OverlayRequested,SaveRouteRequested,LoadRouteRequested;readonly Button saveRoute=new Button(),loadRoute=new Button();readonly Label routeLabel=new Label();
        // Loaded .txt route (0.42.0): the load button turns into "Tirar rota" while it is in use.
        public void SetLoadedRoute(string name,int points){bool on=name!=null;loadRoute.Text=on?"Tirar rota carregada":"Carregar rota (.txt)";routeLabel.Text=on?"Rota: "+name+" · "+points+" pontos":"Plano automático pelos materiais marcados";routeLabel.ForeColor=on?Color.FromArgb(62,207,142):Color.SlateGray;}readonly Button overlayButton=new Button();
        public string Summary{get{return (running?"Farmando":"Parado")+" · volta "+cycle+" · coletados "+collected+" · "+last;}}public double ScanRadius{get{return (double)radius.Value;}}readonly NumericUpDown farmRadius=new NumericUpDown();readonly Button restore=new Button();
        public double Radius{get{return (double)farmRadius.Value;}set{farmRadius.Value=(decimal)Math.Max(100,Math.Min(30000,value));}}
        public void SetExcludedCount(int n){restore.Text="Restaurar pontos ("+n+")";restore.Enabled=n>0;}public event Action<double> ScanRequested;public event Action<int,bool> MaterialToggled;
        public readonly RouteMap Map=new RouteMap();public Func<int,Image> IconFor;
        readonly ListView materials=new ListView();readonly TextBox search=new TextBox();readonly Label found=new Label();readonly ImageList icons=new ImageList();readonly Dictionary<int,int> iconIndex=new Dictionary<int,int>();
        readonly Button start=new Button(),stop=new Button(),scan=new Button();readonly NumericUpDown radius=new NumericUpDown(),height=new NumericUpDown();
        public double Height{get{return (double)height.Value;}set{height.Value=(decimal)Math.Max(10,Math.Min(300,value));}}
        readonly Label state=new Label(),detail=new Label(),hint=new Label();bool updating,running;string signature="";
        int cycle,collected,defenses,skipped,plan;string next="—",last="—";
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,string l);
        public FarmPanel(){
            Dock=DockStyle.Fill;Padding=new Padding(8);BackColor=Color.FromArgb(244,247,250);
            icons.ImageSize=new Size(24,24);icons.ColorDepth=ColorDepth.Depth32Bit;using(var blank=new Bitmap(24,24))icons.Images.Add((Image)blank.Clone());
            var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,380));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(split);
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(0,0,10,0)};left.RowStyles.Add(new RowStyle(SizeType.Absolute,30));left.RowStyles.Add(new RowStyle(SizeType.Absolute,40));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,40));left.RowStyles.Add(new RowStyle(SizeType.Absolute,62));split.Controls.Add(left,0,0);
            left.Controls.Add(new Label{Text="01  /  MATERIAIS PARA FARMAR",Dock=DockStyle.Fill,Font=new Font("Segoe UI Semibold",(float)(10.5f)*global::UiScale.F),ForeColor=Rubi.Text,TextAlign=ContentAlignment.MiddleLeft},0,0);
            var searchBar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,92));
            search.Dock=DockStyle.Fill;search.Margin=new Padding(0,7,6,0);search.TextChanged+=(s,e)=>RenderMaterials();search.HandleCreated+=(s,e)=>SendMessage(search.Handle,0x1501,(IntPtr)1,"Pesquisar material…");searchBar.Controls.Add(search,0,0);
            found.Dock=DockStyle.Fill;found.ForeColor=Rubi.Muted;found.TextAlign=ContentAlignment.MiddleRight;searchBar.Controls.Add(found,1,0);left.Controls.Add(searchBar,0,1);
            materials.Dock=DockStyle.Fill;materials.ShowItemToolTips=true;materials.View=View.Details;materials.CheckBoxes=true;materials.FullRowSelect=true;materials.HideSelection=false;materials.SmallImageList=icons;materials.Columns.Add("Material",230);materials.Columns.Add("Pontos",62);materials.Columns.Add("Perto",52);
            materials.ItemChecked+=(s,e)=>{if(updating)return;var ids=(int[])e.Item.Tag;if(MaterialToggled!=null)foreach(var id in ids)MaterialToggled(id,e.Item.Checked);};left.Controls.Add(materials,0,2);
            var scanBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
            overlayButton.Text="Mapinha por cima";overlayButton.Width=150;overlayButton.Height=31;overlayButton.Click+=(s,e)=>{if(OverlayRequested!=null)OverlayRequested();};scanBar.Controls.Add(overlayButton);
            scan.Text="Escanear arredores";scan.Width=150;scan.Height=31;scan.Click+=(s,e)=>{if(ScanRequested!=null)ScanRequested((double)radius.Value);};/* 0.47.0 (dono): escanear removido — os materiais já vêm fixos no mapa. */
            radius.Minimum=200;radius.Maximum=30000;radius.Increment=500;radius.Value=1500;radius.ValueChanged+=(s,e)=>{if(ScanRadiusChanged!=null)ScanRadiusChanged();};radius.Width=72;radius.Margin=new Padding(0,5,0,0);
            left.Controls.Add(scanBar,0,3);
            var routeBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
            saveRoute.Text="Salvar rota (.txt)";saveRoute.Width=150;saveRoute.Height=31;saveRoute.Click+=(s,e)=>{if(SaveRouteRequested!=null)SaveRouteRequested();};routeBar.Controls.Add(saveRoute);
            loadRoute.Text="Carregar rota (.txt)";loadRoute.Width=150;loadRoute.Height=31;loadRoute.Click+=(s,e)=>{if(LoadRouteRequested!=null)LoadRouteRequested();};routeBar.Controls.Add(loadRoute);
            routeLabel.AutoSize=true;routeLabel.Margin=new Padding(3,2,0,0);routeLabel.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);routeBar.Controls.Add(routeLabel);SetLoadedRoute(null,0);left.Controls.Add(routeBar,0,4);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};right.RowStyles.Add(new RowStyle(SizeType.Absolute,30));right.RowStyles.Add(new RowStyle(SizeType.Absolute,52));right.RowStyles.Add(new RowStyle(SizeType.Absolute,40));right.RowStyles.Add(new RowStyle(SizeType.Absolute,92));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));split.Controls.Add(right,1,0);
            right.Controls.Add(BuildModeBar(),0,0);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            start.Text="▶  Iniciar farm";start.Width=170;start.Height=40;start.BackColor=Color.FromArgb(36,109,169);start.Font=new Font("Segoe UI Semibold",(float)(10)*global::UiScale.F);start.Click+=(s,e)=>{if(StartRequested!=null)StartRequested();};buttons.Controls.Add(start);
            stop.Text="■  Parar";stop.Width=120;stop.Height=40;stop.Font=new Font("Segoe UI Semibold",(float)(10)*global::UiScale.F);stop.Enabled=false;stop.Click+=(s,e)=>{if(StopRequested!=null)StopRequested();};buttons.Controls.Add(stop);
            buttons.Controls.Add(new Label{Text="Altura de voo",AutoSize=true,Margin=new Padding(14,13,4,0)});height.Minimum=10;height.Maximum=300;height.Value=60;height.Width=60;height.Margin=new Padding(0,9,0,0);buttons.Controls.Add(height);
            hint.Text="Sobe à altura, vai a cada ponto, desce, coleta, faz a missão e repete até Parar.";hint.AutoSize=false;hint.Width=330;hint.Height=40;hint.ForeColor=Color.SlateGray;hint.Margin=new Padding(12,2,0,0);buttons.Controls.Add(hint);
            var row1=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};buttons.Dock=DockStyle.Fill;row1.Controls.Add(buttons);row1.Controls.Add(BuildTrailButtons());right.Controls.Add(row1,0,1);
            var area=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};area.Controls.Add(new Label{Text="Raio do farm",AutoSize=true,Margin=new Padding(0,9,4,0)});farmRadius.Minimum=100;farmRadius.Maximum=30000;farmRadius.Increment=250;farmRadius.Value=900;farmRadius.Width=72;farmRadius.Margin=new Padding(0,5,10,0);farmRadius.ValueChanged+=(s,e)=>{if(PlanSettingsChanged!=null)PlanSettingsChanged();};area.Controls.Add(farmRadius);
            restore.Text="Restaurar pontos (0)";restore.Enabled=false;restore.Width=170;restore.Height=30;restore.Click+=(s,e)=>{if(RestoreRequested!=null)RestoreRequested();};area.Controls.Add(restore);
            area.Controls.Add(new Label{Text="Clique num ponto do mapa para tirá-lo.",AutoSize=true,ForeColor=Color.SlateGray,Margin=new Padding(10,8,0,0)});var row2=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};area.Dock=DockStyle.Fill;row2.Controls.Add(area);row2.Controls.Add(BuildTrailArea());right.Controls.Add(row2,0,2);autoRows=new Control[]{buttons,area,routeBar};SetMode(false);
            var status=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(12,6,12,6),Margin=new Padding(0,4,0,6)};
            state.Dock=DockStyle.Top;state.Height=30;state.Font=new Font("Segoe UI Semibold",(float)(12)*global::UiScale.F);state.Text="Parado";
            detail.Dock=DockStyle.Fill;detail.ForeColor=Color.FromArgb(93,111,128);status.Controls.Add(detail);status.Controls.Add(state);right.Controls.Add(status,0,3);
            Map.Dock=DockStyle.Fill;right.Controls.Add(Map,0,4);
            UpdateText();
        }
        // List (0.43.0, owner): every collectible material of the catalog, one row per name (all its variants together), with
        // the scanned points; a search box filters by name (accents and case ignored). Chosen ones stay on top.
        IList<FarmMaterial> allItems=new List<FarmMaterial>();ICollection<int> allChosen=new HashSet<int>();
        // Light on the PC: hidden panels skip the list (redrawn when shown), folded names are cached, and icons are read only
        // for rows with points, chosen ones or search results.
        bool materialsPending;string userSignature="";DateTime lastBuild=DateTime.MinValue;static readonly Dictionary<string,string> folded=new Dictionary<string,string>();
        public void SetMaterials(IList<FarmMaterial> items,ICollection<int> chosen){allItems=items;allChosen=chosen;if(!Visible){materialsPending=true;return;}RenderMaterials();}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible&&materialsPending){materialsPending=false;RenderMaterials();}}
        static string Folded(string s){string v;s=s??"";if(folded.TryGetValue(s,out v))return v;v=Fold(s);if(folded.Count<5000)folded[s]=v;return v;}
        static string Fold(string s){var n=(s??"").Normalize(System.Text.NormalizationForm.FormD);var b=new System.Text.StringBuilder();foreach(char c in n)if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark)b.Append(char.ToLowerInvariant(c));return b.ToString();}
        sealed class MaterialRow {public string Name,Source;public int[] Ids;public int Points,Loaded;public bool Chosen;}
        void RenderMaterials(){
            string q=Fold(search.Text.Trim());var chosen=allChosen;
            var rows=allItems.GroupBy(m=>m.Name??("Recurso "+m.Id)).Select(g=>new MaterialRow{Name=g.Key,Source=String.Join(" / ",g.Select(m=>m.Source).Where(x=>!String.IsNullOrEmpty(x)).Distinct()),Ids=g.Select(m=>m.Id).Distinct().OrderBy(i=>i).ToArray(),Points=g.Sum(m=>m.Points),Loaded=g.Sum(m=>m.Loaded)}).ToList();
            foreach(var r in rows)r.Chosen=r.Ids.Any(chosen.Contains);
            var sorted=rows.Where(r=>q.Length==0||Folded(r.Name).Contains(q)||Folded(r.Source).Contains(q)).OrderByDescending(r=>r.Chosen).ThenByDescending(r=>r.Points).ThenBy(r=>r.Name).ToList();
            string next=q+"|"+String.Join(";",sorted.Select(r=>String.Join(",",r.Ids)+(r.Chosen?"+":"")));
            updating=true;try{
                // Reordering the whole list is the costly part: data-only changes (new points from a scan) reorder at most every 3 s;
                // a search or a ticked material reorders at once.
                string user=q+"|"+String.Join(",",chosen.OrderBy(i=>i));bool soon=user==userSignature&&(DateTime.UtcNow-lastBuild).TotalMilliseconds<3000;
                if(next!=signature&&!soon){signature=next;userSignature=user;lastBuild=DateTime.UtcNow;materials.BeginUpdate();try{materials.Items.Clear();var built=new ListViewItem[sorted.Count];for(int i=0;i<sorted.Count;i++){var m=sorted[i];built[i]=new ListViewItem(new[]{m.Name,m.Points>0?m.Points.ToString():"",m.Loaded>0?m.Loaded.ToString():""},m.Points>0||m.Chosen||q.Length>0?Icon(m.Ids[0]):0){Tag=m.Ids,Checked=m.Chosen,ToolTipText=String.IsNullOrEmpty(m.Source)?m.Name:"No chão: "+m.Source};}materials.Items.AddRange(built);}finally{materials.EndUpdate();}}
                else{var byName=sorted.ToDictionary(m=>String.Join(",",m.Ids));foreach(ListViewItem row in materials.Items){MaterialRow m;if(!byName.TryGetValue(String.Join(",",(int[])row.Tag),out m))continue;string pts=m.Points>0?m.Points.ToString():"",l=m.Loaded>0?m.Loaded.ToString():"";if(row.SubItems[1].Text!=pts)row.SubItems[1].Text=pts;if(row.SubItems[2].Text!=l)row.SubItems[2].Text=l;if(row.ImageIndex==0&&(m.Points>0||m.Chosen||q.Length>0))row.ImageIndex=Icon(m.Ids[0]);}}
            }finally{updating=false;}
            found.Text=q.Length==0?rows.Count+" materiais":sorted.Count+" de "+rows.Count;
        }
        int Icon(int id){int index;if(iconIndex.TryGetValue(id,out index))return index;var image=IconFor==null?null:IconFor(id);if(image==null)return 0;icons.Images.Add((Image)image.Clone());index=icons.Images.Count-1;iconIndex[id]=index;return index;}
        public void ResetIcons(){iconIndex.Clear();while(icons.Images.Count>1)icons.Images.RemoveAt(icons.Images.Count-1);signature="";}
        public void SetRunning(bool value){if(value==running)return;running=value;start.Enabled=!value;stop.Enabled=value;scan.Enabled=!value;loadRoute.Enabled=!value;height.Enabled=!value;farmRadius.Enabled=!value;TrailRunning(value);if(value){cycle=collected=defenses=skipped=plan=0;next=last="—";}UpdateText();}
        public void SetPlan(RouteDocument route){if(Manual)return;plan=route==null?0:route.Points.Count;Map.Route=route;Map.Invalidate();UpdateText();}
        public void ShowTrail(RouteDocument path){plan=path==null?0:path.Points.Count;Map.Route=path;Map.Invalidate();UpdateText();}
        // Execution events ("phase · json") turned into the counters shown on screen.
        public void OnEvent(string text){int cut=text.IndexOf(" · ");string phase=cut<0?text:text.Substring(0,cut);
            switch(phase){case "ciclo":cycle++;break;case "recompensa_recebida":case "coletado":collected++;break;case "defesa":case "defesa_proximo":defenses++;break;case "pulado_ausente":case "ponto_falhou":case "coleta_falhou":skipped++;break;
                case "deslocando":next=text.Length>cut+3&&cut>=0?Pretty(text.Substring(cut+3)):"—";break;}
            last=Describe(phase);UpdateText();}
        static string Pretty(string json){return json.Length>80?json.Substring(0,80)+"…":json;}
        static string Describe(string phase){switch(phase){case "deslocando":return "Indo ao próximo ponto";case "chegou":return "Chegou ao ponto";case "coleta_solicitada":return "Coletando";case "temporario_recebido":return "Material coletado";case "missao_confirmada":return "Missão do material";case "segunda_opcao_repetida":return "Recompensa não chegou; escolhendo de novo";case "perto":return "Material perto; indo direto";case "indo_alto":return "Indo na altura de voo até o material";case "defesa_concluida":return "Luta encerrada; voltando à rota";case "vertical_ok":return "Subindo/descendo reto";case "decolando":return "No chão; abrindo o voo";case "decolou":return "Voo aberto; subindo reto";case "sem_decolar":return "Voo não abriu; decolando pelo caminho";case "vertical_passos":return "Espaço/Z sozinho não moveu; passos curtos";case "vertical_indisponivel":return "Subida reta indisponível; usando linha inclinada";case "destravando":return "Travado; desviando o caminho";case "destravado":return "Desviou; seguindo";case "destravar_falhou":return "Ainda travado; tentando de novo";case "achado":return "Material achado; descendo";case "procurando":return "Procurando o material na área";case "missao_sem_confirmacao":return "Missão sem confirmação; tentando de novo";case "missao_nao_apareceu":return "Missão não apareceu";case "recompensa_nao_confirmada":return "Recompensa não confirmada (veja o Log)";case "recompensa_recebida":return "Recompensa recebida";case "material_no_caminho":return "Material nascido no caminho; descendo";case "morto":return "Morreu; esperando a Prece da Vida";case "ressuscitado":return "Ressuscitado; continuando";case "reviver_cidade":return "Revivendo na cidade";case "na_cidade":return "Na cidade; voltando ao farm";case "voltando_trajeto":return "Coletado; subindo de volta ao trajeto";case "coletado":return "Material coletado";case "defesa":return "Defendendo (macro 0)";case "defesa_desistiu":return "Monstro não morreu em 1 min; trocando de alvo";case "defesa_proximo":return "Outro monstro batendo; atacando ele";case "defesa_limite":return "Defesa longa demais; voltando à rotina";case "recuperando_vida":return "Vida baixa: esperando recuperar";case "vida_recuperada":return "Vida recuperada; seguindo";case "aguardando":return "Aguardando para tentar de novo";case "pausa":return "Pausa curta após falhas; continua";case "falha_recuperada":return "Falha contornada; seguindo";case "altura":return "Na altura do material";case "subindo":return "Subindo à altura de voo";case "descendo":return "Descendo até o material";case "voo_reto_parado":return "Obstáculo no voo reto; contornando";case "altura_indisponivel":return "Altura indisponível; seguindo na altura atual";case "monstro_derrotado":return "Monstro derrotado";case "pulado_ausente":return "Ponto vazio (material não está lá agora)";case "ponto_falhou":return "Ponto não alcançado; seguiu para o próximo";case "coleta_falhou":return "Coleta não confirmada; seguiu";case "sem_pontos":return "Sem pontos conhecidos: escaneie os arredores";case "ciclo":return "Nova volta planejada";case "erro":return "Parou por erro (veja o Log)";case "parada":return "Parado por você";default:return phase.Replace('_',' ');}}
        void UpdateText(){state.Text=running?(Manual?"Rodando trajeto":"Farmando"):(recordingTrail?"Gravando trajeto":"Parado");state.ForeColor=running?Color.FromArgb(110,205,120):Color.FromArgb(175,177,191);
            detail.Text="Volta "+cycle+"  ·  Coletados "+collected+"  ·  Pontos no plano "+plan+"  ·  Pulados "+skipped+"  ·  Defesas "+defenses+"\r\nAgora: "+last;}
    }
    // Small always-on-top farm map (0.35.0, owner): only the map and one status line, over the game, without taking focus.
    // Drag by the top bar, resize from the bottom-right corner, close with ×.
    public sealed class FarmOverlay : Form {
        public readonly RouteMap Map=new RouteMap();readonly Label status=new Label();
        public FarmOverlay(){FormBorderStyle=FormBorderStyle.None;TopMost=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Size=new Size(300,330);MinimumSize=new Size(180,200);BackColor=Rubi.Background;Opacity=0.93;Padding=new Padding(1);
            var bar=new Panel{Dock=DockStyle.Top,Height=24,BackColor=Rubi.Panel};
            var title=new Label{Text="FarmZilla · Farm",Dock=DockStyle.Fill,ForeColor=Rubi.Accent,Font=new Font("Segoe UI",(float)(8.5f)*global::UiScale.F,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(6,0,0,0)};
            var close=new Label{Text="×",Dock=DockStyle.Right,Width=26,ForeColor=Rubi.Muted,Font=new Font("Segoe UI",(float)(11)*global::UiScale.F,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Cursor=Cursors.Hand};close.Click+=(s,e)=>Hide();
            MouseEventHandler drag=(s,e)=>{if(e.Button==MouseButtons.Left){DragNative.ReleaseCapture();DragNative.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};bar.MouseDown+=drag;title.MouseDown+=drag;
            bar.Controls.Add(title);bar.Controls.Add(close);
            status.Dock=DockStyle.Bottom;status.Height=20;status.ForeColor=Rubi.Text;status.BackColor=Rubi.Panel;status.Font=new Font("Segoe UI",(float)(8)*global::UiScale.F);status.TextAlign=ContentAlignment.MiddleLeft;status.Padding=new Padding(6,0,0,0);status.AutoEllipsis=true;status.Text="Parado";
            Map.Dock=DockStyle.Fill;Controls.Add(Map);Controls.Add(status);Controls.Add(bar);Map.Zoom(0.6);}
        public void SetStatus(string text){if(status.Text!=text)status.Text=text;}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ExStyle|=0x08000000|0x00000080;return cp;}}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x84){var p=PointToClient(new Point((short)(m.LParam.ToInt32()&0xFFFF),(short)(m.LParam.ToInt32()>>16)));if(p.X>=ClientSize.Width-14&&p.Y>=ClientSize.Height-14)m.Result=(IntPtr)17;}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Rubi.Line))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
    }
}
