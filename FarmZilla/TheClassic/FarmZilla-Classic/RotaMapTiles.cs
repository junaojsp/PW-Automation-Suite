using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Collections.Generic;

namespace RotaAvalon {
    public sealed class MapTiles : IDisposable {
        readonly PckArchive package;readonly Dictionary<string,PckEntry> entries=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();readonly Queue<string> order=new Queue<string>();
        byte[] atlas;Dictionary<string,int> atlasPositions;Dictionary<string,PckEntry> byFile;int iconWidth,iconHeight,atlasColumns;
        public MapTiles(string gameDirectory){package=new PckArchive(Path.Combine(gameDirectory,"surfaces.pck"));foreach(var e in package.Entries)entries[e.Name]=e;}
        public Bitmap Image(string path){if(String.IsNullOrWhiteSpace(path))return null;path=path.Replace('\\','/');PckEntry entry;if(entries.TryGetValue(path,out entry))return GameImage.Decode(package.Read(entry));
            EnsureFiles();string file=path.Substring(path.LastIndexOf('/')+1);if(byFile.TryGetValue(file,out entry))return GameImage.Decode(package.Read(entry));
            EnsureAtlas();if(atlasPositions==null)return null;int index;if(!atlasPositions.TryGetValue(file,out index))return null;return DdsImage.Region(atlas,new Rectangle((index%atlasColumns)*iconWidth,(index/atlasColumns)*iconHeight,iconWidth,iconHeight));}
        void EnsureFiles(){if(byFile!=null)return;byFile=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);foreach(var kv in entries){string file=kv.Key;int slash=file.LastIndexOf('/');if(slash>=0)file=file.Substring(slash+1);if(file.Length>0&&!byFile.ContainsKey(file))byFile[file]=kv.Value;}}
        void EnsureAtlas(){if(atlasPositions!=null)return;atlasPositions=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);string[] lists=new[]{"surfaces/iconset/iconlist_ivtrm","surfaces/iconset/iconlist_ivtr","surfaces/iconset/iconlist_ivtr1","surfaces/iconset/iconlist_ivtrf"};PckEntry text=null,image=null;foreach(var list in lists)if(entries.TryGetValue(list+".txt",out text)&&entries.TryGetValue(list+".dds",out image))break;if(text==null||image==null)return;
            string[] lines=Encoding.GetEncoding(936).GetString(package.Read(text)).Split('\n');if(lines.Length<4)throw new InvalidDataException("Índice de ícones incompleto.");iconWidth=Int32.Parse(lines[0].Trim());iconHeight=Int32.Parse(lines[1].Trim());int rows=Int32.Parse(lines[2].Trim());atlasColumns=Int32.Parse(lines[3].Trim());if(iconWidth!=32||iconHeight!=32||rows<1||atlasColumns<1||rows*(long)atlasColumns>100000)throw new InvalidDataException("Atlas de ícones não suportado.");atlas=package.Read(image);for(int i=4;i<lines.Length&&i-4<rows*atlasColumns;i++){string name=lines[i].Trim();if(name.Length>0)atlasPositions[name]=i-4;}}
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
