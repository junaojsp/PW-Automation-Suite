using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;
namespace RotaAvalon {
    public sealed class RouteMap : Control {
        public RouteDocument Route;public ClientSnapshot Player;public int Selected=-1;public MapTiles Tiles;public TerrainBounds Terrain;
        public List<ResourceMarker> Resources=new List<ResourceMarker>();public uint SelectedResource;
        public bool ShowTerrain=true;public event Action<ResourceMarker> ResourceClicked;public Func<int,Image> IconFor;
        readonly ToolTip tip=new ToolTip();readonly MapTransform transform=new MapTransform();readonly Dictionary<ResourceMarker,PointF> markers=new Dictionary<ResourceMarker,PointF>();
        bool follow=true,dragging,moved;Point dragStart;double centerX,centerZ,zoom=0.55;string tooltipText="";
        public RouteMap(){DoubleBuffered=true;BackColor=Color.FromArgb(37,49,48);SetStyle(ControlStyles.Selectable,true);TabStop=true;Cursor=Cursors.Hand;}
        public void CenterOnPlayer(){follow=true;Invalidate();}
        // Area "fences" (0.29.0): circles drawn on the map, e.g. the farm radius and the scan radius. (x, z, radius, color, label)
        public readonly List<Tuple<double,double,double,Color,string>> Fences=new List<Tuple<double,double,double,Color,string>>();
        public void FitRadius(double radius){if(radius<=0||Width<10||Height<10)return;zoom=Math.Max(0.02,Math.Min(12,Math.Min(Width,Height)/(2.5*radius)));Invalidate();}
        public void Zoom(double factor){zoom=Math.Max(0.02,Math.Min(12,zoom*factor));Invalidate();}
        public void FocusResource(ResourceMarker marker){if(marker==null)return;follow=false;centerX=marker.X;centerZ=marker.Z;SelectedResource=marker.InstanceId;Invalidate();}
        public RectangleF VisibleWorld(){var t=Transform();var first=t.World(0,Height);var last=t.World(Width,0);return RectangleF.FromLTRB(first.X,first.Y,last.X,last.Y);}
        MapTransform Transform(){if(follow&&Player!=null){centerX=Player.X;centerZ=Player.Z;}transform.CenterX=centerX;transform.CenterZ=centerZ;transform.Scale=zoom;transform.Width=Width;transform.Height=Height;return transform;}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Zoom(e.Delta>0?1.3:1/1.3);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button==MouseButtons.Left){Transform();dragging=true;moved=false;dragStart=e.Location;Capture=true;}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){int dx=e.X-dragStart.X,dy=e.Y-dragStart.Y;if(Math.Abs(dx)+Math.Abs(dy)>2){follow=false;moved=true;centerX-=dx/zoom;centerZ+=dy/zoom;dragStart=e.Location;Invalidate();}return;}
            var closest=markers.FirstOrDefault(v=>Math.Abs(v.Value.X-e.X)<12&&Math.Abs(v.Value.Y-e.Y)<12).Key;string text=closest==null?"":closest.Name+" · ID "+closest.TemplateId+"\nDistância: "+closest.Distance.ToString("F1")+" · Nível mínimo: "+closest.RequiredLevel+"\nItem: "+closest.ItemId+" · Instância: "+closest.InstanceId.ToString("X8")+"\n"+(closest.LevelAllowed?"Nível suficiente; outros requisitos podem se aplicar.":"Nível insuficiente.");if(text!=tooltipText){tooltipText=text;tip.SetToolTip(this,text);}}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;if(!moved){var marker=markers.FirstOrDefault(v=>Math.Abs(v.Value.X-e.X)<12&&Math.Abs(v.Value.Y-e.Y)<12).Key;if(marker!=null){SelectedResource=marker.InstanceId;if(ResourceClicked!=null)ResourceClicked(marker);Invalidate();}}}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var t=Transform();markers.Clear();
            bool terrain=ShowTerrain&&Tiles!=null&&Terrain!=null&&Terrain.IsMainWorld;
            if(terrain){g.Clear(Color.FromArgb(40,34,27));g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.Half;var nw=t.World(0,0);var se=t.World(Width,Height);int c0=Math.Max(0,(int)Math.Floor((nw.X+4096)/1024)),c1=Math.Min(7,(int)Math.Floor((se.X+4096)/1024)),r0=Math.Max(0,(int)Math.Floor((5632-nw.Y)/1024)),r1=Math.Min(10,(int)Math.Floor((5632-se.Y)/1024));for(int row=r0;row<=r1;row++)for(int col=c0;col<=c1;col++){var image=Tiles.BigTile(row,col);if(image==null)continue;PointF a=t.Screen(-4096+col*1024,5632-row*1024),b=t.Screen(-4096+(col+1)*1024,5632-(row+1)*1024);g.DrawImage(image,new RectangleF(a.X,a.Y,b.X-a.X+0.5f,b.Y-a.Y+0.5f));}}
            else{g.Clear(Rubi.Field);using(var pen=new Pen(Rubi.Line)){for(int x=0;x<Width;x+=28)g.DrawLine(pen,x,0,x,Height);for(int y=0;y<Height;y+=28)g.DrawLine(pen,0,y,Width,y);}}
            if(Route!=null&&Route.Points.Count>0){using(var line=new Pen(Rubi.Accent,2.5f)){for(int i=1;i<Route.Points.Count;i++)g.DrawLine(line,t.Screen(Route.Points[i-1].X,Route.Points[i-1].Z),t.Screen(Route.Points[i].X,Route.Points[i].Z));if(Route.Loop&&Route.Points.Count>1){line.DashStyle=DashStyle.Dash;g.DrawLine(line,t.Screen(Route.Points[Route.Points.Count-1].X,Route.Points[Route.Points.Count-1].Z),t.Screen(Route.Points[0].X,Route.Points[0].Z));}}
                for(int i=0;i<Route.Points.Count;i++){var p=t.Screen(Route.Points[i].X,Route.Points[i].Z);if(p.X<0||p.Y<0||p.X>Width||p.Y>Height)continue;using(var brush=new SolidBrush(i==Selected?Color.Gold:Rubi.Shade(Rubi.Accent,30)))g.FillEllipse(brush,p.X-4,p.Y-4,8,8);if(Route.Points.Count<=100)TextRenderer.DrawText(g,(i+1).ToString(),Font,new Point((int)p.X+5,(int)p.Y-16),terrain?Color.White:Rubi.Text);}}
            var usedLabels=new List<Rectangle>();foreach(var resource in Resources){var p=t.Screen(resource.X,resource.Z);if(p.X<8||p.Y<30||p.X>Width-8||p.Y>Height-12)continue;markers[resource]=p;bool chosen=Route!=null&&Route.ResourceIds.Contains(resource.TemplateId);Color color=resource.Historical?Color.SlateGray:resource.LevelAllowed?Color.Gold:Color.Tomato;Badge(g,resource,p,chosen,color);if(zoom<1.0)continue;
                string label=resource.Name+(resource.Historical?" (registrado)":"");var size=TextRenderer.MeasureText(label,Font);int lx=(int)p.X+9,ly=(int)p.Y-9;if(lx+size.Width>Width)lx=Math.Max(2,(int)p.X-size.Width-9);var rect=new Rectangle(lx,ly,size.Width,size.Height);for(int attempt=0;attempt<8&&usedLabels.Any(v=>v.IntersectsWith(rect));attempt++)rect.Y+=size.Height;usedLabels.Add(rect);if(rect.Bottom<Height){using(var brush=new SolidBrush(Color.FromArgb(175,19,28,30)))g.FillRectangle(brush,rect);TextRenderer.DrawText(g,label,Font,rect,Color.White,TextFormatFlags.NoPadding);}}
            foreach(var fence in Fences){var c=t.Screen(fence.Item1,fence.Item2);float r=(float)(fence.Item3*zoom);if(r<3)continue;var box=new RectangleF(c.X-r,c.Y-r,2*r,2*r);using(var fill=new SolidBrush(Color.FromArgb(28,fence.Item4)))g.FillEllipse(fill,box);using(var pen=new Pen(Color.FromArgb(220,fence.Item4),2f){DashStyle=DashStyle.Dash})g.DrawEllipse(pen,box);
                string label=fence.Item5+" · raio "+fence.Item3.ToString("F0");var size=TextRenderer.MeasureText(label,Font);var at=new Rectangle((int)(c.X-size.Width/2),(int)(c.Y-r-size.Height-2),size.Width+4,size.Height);if(at.Y<32)at.Y=(int)(c.Y-r+4);using(var b=new SolidBrush(Color.FromArgb(185,20,20,24)))g.FillRectangle(b,at);TextRenderer.DrawText(g,label,Font,at,fence.Item4,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
            if(Player!=null){var p=t.Screen(Player.X,Player.Z);g.FillEllipse(Brushes.White,p.X-7,p.Y-7,14,14);using(var brush=new SolidBrush(Color.FromArgb(13,174,125)))g.FillEllipse(brush,p.X-5,p.Y-5,10,10);if(!String.IsNullOrEmpty(Player.Name)){var size=TextRenderer.MeasureText(Player.Name,Font);var box=new Rectangle((int)p.X+10,(int)p.Y-size.Height/2,size.Width+4,size.Height);using(var brush=new SolidBrush(Color.FromArgb(190,13,60,45)))g.FillRectangle(brush,box);TextRenderer.DrawText(g,Player.Name,Font,box,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}
            string title="N ↑ · "+(terrain?"Mapa do jogo":"Vista X/Z")+(follow?" · Seguindo você":" · Arraste para mover");var bounds=new Rectangle(7,7,Math.Max(1,Math.Min(Width-14,340)),23);using(var brush=new SolidBrush(Color.FromArgb(195,23,38,44)))g.FillRectangle(brush,bounds);TextRenderer.DrawText(g,title,Font,bounds,Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            if(Player==null)TextRenderer.DrawText(g,"Conecte ao personagem para mostrar o mapa",Font,ClientRectangle,Rubi.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
        // Game-map style marker: round seal with the item's icon; ring gold (loaded), red (level), grey (remembered), Rubi when chosen.
        void Badge(Graphics g,ResourceMarker r,PointF p,bool chosen,Color ring){
            if(zoom<0.22){using(var b=new SolidBrush(chosen?Rubi.Accent:ring))g.FillEllipse(b,p.X-3,p.Y-3,6,6);return;}
            const float R=11;var box=new RectangleF(p.X-R,p.Y-R,2*R,2*R);var inner=RectangleF.Inflate(box,-3,-3);
            using(var b=new SolidBrush(Color.FromArgb(r.Historical?185:235,26,24,22)))g.FillEllipse(b,box);
            var icon=IconFor==null?null:IconFor(r.TemplateId);
            if(icon!=null){var state=g.Save();using(var clip=new GraphicsPath()){clip.AddEllipse(inner);g.SetClip(clip,CombineMode.Intersect);g.DrawImage(icon,inner);}g.Restore(state);if(r.Historical)using(var veil=new SolidBrush(Color.FromArgb(60,0,0,0)))g.FillEllipse(veil,inner);}
            else using(var b=new SolidBrush(ring))g.FillEllipse(b,RectangleF.Inflate(box,-6,-6));
            bool selected=r.InstanceId==SelectedResource;using(var pen=new Pen(selected?Color.White:chosen?Rubi.Accent:ring,selected||chosen?2.6f:1.8f))g.DrawEllipse(pen,box);}
        protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
    }
}

