using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace RotaAvalon {
    public sealed class MapTiles : IDisposable {
        readonly PckArchive package;readonly Dictionary<string,PckEntry> entries=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);
        // Preserve the original artwork from surfaces.pck even if the catalog's
        // directory differs from the directory stored in the archive.
        readonly Dictionary<string,PckEntry> uniqueImageNames=new Dictionary<string,PckEntry>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> ambiguousImageNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();readonly Queue<string> order=new Queue<string>();
        sealed class AtlasEntry {public byte[] Data;public int Index,Width,Height,Columns;}
        Dictionary<string,AtlasEntry> atlasEntries;
        string atlasReport="not inspected";
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
            // Real DDS with a different folder name, only when unambiguous.
            string name=IconKey(path);
            if(uniqueImageNames.TryGetValue(name,out entry))return GameImage.Decode(package.Read(entry));
            EnsureAtlas();
            AtlasEntry region;
            if(atlasEntries==null||!atlasEntries.TryGetValue(name,out region))return null;
            return DdsImage.Region(region.Data,new Rectangle((region.Index%region.Columns)*region.Width,
                (region.Index/region.Columns)*region.Height,region.Width,region.Height));
        }
        // The game's icon atlases may be stored in different PCK folders and
        // may use different iconlist_* names. Do not assume one hard-coded path.
        void EnsureAtlas(){
            if(atlasEntries!=null)return;
            atlasEntries=new Dictionary<string,AtlasEntry>(StringComparer.OrdinalIgnoreCase);
            int found=0,loaded=0,failed=0;var examples=new List<string>();
            var candidates=new List<PckEntry>();
            foreach(var item in entries.Values){
                string path=item.Name.Replace('\\','/');
                string file=path.Substring(path.LastIndexOf('/')+1);
                if(file.StartsWith("iconlist_",StringComparison.OrdinalIgnoreCase)&&file.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))
                    candidates.Add(item);
            }
            // Prefer the item atlas, but allow the client's other icon atlases.
            foreach(var txt in candidates.OrderByDescending(e=>e.Name.IndexOf("ivtrm",StringComparison.OrdinalIgnoreCase)>=0)){
                found++;
                try{
                    string full=txt.Name.Replace('\\','/');
                    string ddsPath=full.Substring(0,full.Length-4)+".dds";
                    PckEntry atlasDds;
                    if(!entries.TryGetValue(ddsPath,out atlasDds))continue;
                    string[] lines=Encoding.GetEncoding(936).GetString(package.Read(txt)).Split('\n');
                    if(lines.Length<5)continue;
                    int iw,ih,rows,columns;
                    if(!Int32.TryParse(lines[0].Trim(),out iw)||!Int32.TryParse(lines[1].Trim(),out ih)||
                       !Int32.TryParse(lines[2].Trim(),out rows)||!Int32.TryParse(lines[3].Trim(),out columns)||
                       iw<=0||ih<=0||iw>256||ih>256||rows<1||columns<1||rows*(long)columns>250000)continue;
                    byte[] bytes=package.Read(atlasDds);
                    if(bytes.Length<128||BitConverter.ToUInt32(bytes,0)!=0x20534444)continue;
                    int count=Math.Min(lines.Length-4,checked(rows*columns));
                    for(int i=0;i<count;i++){
                        string name=IconKey(lines[i+4].Trim());
                        if(name.Length==0||atlasEntries.ContainsKey(name))continue;
                        atlasEntries[name]=new AtlasEntry{Data=bytes,Index=i,Width=iw,Height=ih,Columns=columns};
                    }
                    loaded++;if(examples.Count<3)examples.Add(full);
                }catch(Exception ex){failed++;if(examples.Count<3)examples.Add(txt.Name+": "+ex.Message);}
            }
            atlasReport="iconlist encontrados="+found+", lidos="+loaded+", entradas="+atlasEntries.Count+
              ", erros="+failed+(examples.Count>0?"; "+String.Join("; ",examples):"");
        }
        public string IconDiagnostics(){EnsureAtlas();return "PCK="+entries.Count+", DDS unicos="+uniqueImageNames.Count+", "+atlasReport;}
        public Bitmap Tile(int row,int column){if(row<0||row>=22||column<0||column>=16)return null;string name="surfaces/minimaps/world/"+row.ToString("D2")+column.ToString("D2")+".dds";Bitmap image;if(images.TryGetValue(name,out image))return image;image=Image(name);if(image==null)return null;var fast=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(fast))g.DrawImage(image,0,0,image.Width,image.Height);image.Dispose();image=fast;images.Add(name,image);order.Enqueue(name);while(images.Count>64){string key=order.Dequeue();images[key].Dispose();images.Remove(key);}return image;}
        // The game's big map (the parchment of the M window): 11 rows x 8 columns, each 1024 world units; kept at 512 px.
        readonly Dictionary<string,Bitmap> big=new Dictionary<string,Bitmap>();readonly Queue<string> bigOrder=new Queue<string>();
        public Bitmap BigTile(int row,int column){if(row<0||row>10||column<0||column>7)return null;string name="surfaces/maps/"+row.ToString("D2")+column+".dds";Bitmap image;if(big.TryGetValue(name,out image))return image;
            if(!entries.ContainsKey(name))return null;using(var source=GameImage.Decode(package.Read(entries[name]))){if(source==null)return null;image=new Bitmap(512,512,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(image)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(source,0,0,512,512);}}
            big.Add(name,image);bigOrder.Enqueue(name);while(big.Count>40){string key=bigOrder.Dequeue();big[key].Dispose();big.Remove(key);}return image;}
        public void Dispose(){foreach(var image in big.Values)image.Dispose();big.Clear();foreach(var image in images.Values)image.Dispose();images.Clear();atlasEntries=null;package.Dispose();}
    }
    public sealed class MapTransform {
        public double CenterX,CenterZ,Scale;public int Width,Height;
        public PointF Screen(double x,double z){return new PointF((float)(Width/2.0+(x-CenterX)*Scale),(float)(Height/2.0-(z-CenterZ)*Scale));}
        public PointF World(double x,double y){return new PointF((float)(CenterX+(x-Width/2.0)/Scale),(float)(CenterZ-(y-Height/2.0)/Scale));}
        public static int TileColumn(double x){return (int)Math.Floor((x+4096)/512);}
        public static int TileRow(double z){return (int)Math.Floor((5632-z)/512);}
    }
}
