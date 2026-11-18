using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Collections.Generic;

namespace RotaAvalon {
    public sealed class MapTiles : IDisposable {
        readonly PckArchive package;readonly Dictionary<string,PckEntry> entries=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);
        // Preserve the original artwork from surfaces.pck even if the catalog's
        // directory differs from the directory stored in the archive.
        readonly Dictionary<string,PckEntry> uniqueImageNames=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> ambiguousImageNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();readonly Queue<string> order=new Queue<string>();
        byte[] atlas;Dictionary<string,int> atlasPositions;int iconWidth,iconHeight,atlasColumns;
        public MapTiles(string gameDirectory){
            package=new PckArchive(Path.Combine(gameDirectory,"surfaces.pck"));
            foreach(var e in package.Entries){
                string key=e.Name.Replace('\\','/').Trim();
                entries[key]=e;
                // Basename fallback is only used when there is exactly one matching
                // image. Never pick an arbitrary icon when two files share a name.
                string name=IconKey(key);
                if(name.Length==0||!key.EndsWith(".dds",StringComparison.OrdinalIgnoreCase))continue;
                if(ambiguousImageNames.Contains(name))continue;
                if(uniqueImageNames.ContainsKey(name)){uniqueImageNames.Remove(name);ambiguousImageNames.Add(name);}
                else uniqueImageNames.Add(name,e);
            }
        }
        static string IconKey(string value){
            if(String.IsNullOrWhiteSpace(value))return "";
            value=value.Trim().Replace('\\','/');value=value.Substring(value.LastIndexOf('/')+1);
            return value.EndsWith(".dds",StringComparison.OrdinalIgnoreCase)?value.Substring(0,value.Length-4):value;
        }
        public Bitmap Image(string path){
            if(String.IsNullOrWhiteSpace(path))return null;
            path=path.Trim().Replace('\\','/');
            PckEntry entry;
            if(entries.TryGetValue(path,out entry))return GameImage.Decode(package.Read(entry));
            // Fallback 1: o mesmo DDS do material, em outro diretorio do pacote.
            string name=IconKey(path);
            if(uniqueImageNames.TryGetValue(name,out entry))return GameImage.Decode(package.Read(entry));
            // Fallback 2: a arte ORIGINAL do item dentro do atlas de icones.
            EnsureAtlas();if(atlas==null||atlasPositions==null)return null;
            int index;if(!atlasPositions.TryGetValue(name,out index))return null;
            return DdsImage.Region(atlas,new Rectangle((index%atlasColumns)*iconWidth,(index/atlasColumns)*iconHeight,iconWidth,iconHeight));
        }
        void EnsureAtlas(){if(atlasPositions!=null)return;atlasPositions=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);PckEntry text,image;if(!entries.TryGetValue("surfaces/iconset/iconlist_ivtrm.txt",out text)||!entries.TryGetValue("surfaces/iconset/iconlist_ivtrm.dds",out image))return;
            string[] lines=Encoding.GetEncoding(936).GetString(package.Read(text)).Split('\n');if(lines.Length<4)throw new InvalidDataException("Índice de ícones incompleto.");iconWidth=Int32.Parse(lines[0].Trim());iconHeight=Int32.Parse(lines[1].Trim());int rows=Int32.Parse(lines[2].Trim());atlasColumns=Int32.Parse(lines[3].Trim());if(iconWidth!=32||iconHeight!=32||rows<1||atlasColumns<1||rows*(long)atlasColumns>100000)throw new InvalidDataException("Atlas de ícones não suportado.");atlas=package.Read(image);for(int i=4;i<lines.Length&&i-4<rows*atlasColumns;i++){string name=lines[i].Trim();if(name.Length>0){string key=IconKey(name);if(key.Length>0&&!atlasPositions.ContainsKey(key))atlasPositions[key]=i-4;}}}
        public Bitmap Tile(int row,int column){if(row<0||row>=22||column<0||column>=16)return null;string name="surfaces/minimaps/world/"+row.ToString("D2")+column.ToString("D2")+".dds";Bitmap image;if(images.TryGetValue(name,out image))return image;image=Image(name);if(image==null)return null;var fast=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(fast))g.DrawImage(image,0,0,image.Width,image.Height);image.Dispose();image=fast;images.Add(name,image);order.Enqueue(name);while(images.Count>64){string key=order.Dequeue();images[key].Dispose();images.Remove(key);}return image;}
        // The game's big map (the parchment of the M window): 11 rows x 8 columns, each 1024 world units; kept at 512 px.
        readonly Dictionary<string,Bitmap> big=new Dictionary<string,Bitmap>();readonly Queue<string> bigOrder=new Queue<string>();
        public Bitmap BigTile(int row,int column){if(row<0||row>10||column<0||column>7)return null;string name="surfaces/maps/"+row.ToString("D2")+column+".dds";Bitmap image;if(big.TryGetValue(name,out image))return image;
            if(!entries.ContainsKey(name))return null;using(var source=GameImage.Decode(package.Read(entries[name]))){if(source==null)return null;image=new Bitmap(512,512,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(image)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(source,0,0,512,512);}}
            big.Add(name,image);bigOrder.Enqueue(name);while(big.Count>40){string key=bigOrder.Dequeue();big[key].Dispose();big.Remove(key);}return image;}
        public void Dispose(){foreach(var image in big.Values)image.Dispose();big.Clear();foreach(var image in images.Values)image.Dispose();images.Clear();atlas=null;package.Dispose();}
    }
    public sealed class MapTransform {
        public double CenterX,CenterZ,Scale;public int Width,Height;
        public PointF Screen(double x,double z){return new PointF((float)(Width/2.0+(x-CenterX)*Scale),(float)(Height/2.0-(z-CenterZ)*Scale));}
        public PointF World(double x,double y){return new PointF((float)(CenterX+(x-Width/2.0)/Scale),(float)(CenterZ-(y-Height/2.0)/Scale));}
        public static int TileColumn(double x){return (int)Math.Floor((x+4096)/512);}
        public static int TileRow(double z){return (int)Math.Floor((5632-z)/512);}
    }
}
