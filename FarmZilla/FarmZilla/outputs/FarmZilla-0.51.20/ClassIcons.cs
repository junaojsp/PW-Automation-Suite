using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.IO.Compression;using System.Text;
// Class icons come from the game installed on this PC (surfaces.pck, the same 25 px emblems as the party window).
// Nothing from the game is shipped with FarmZilla: the archive index and six small images are read once, in memory.
// Profession ids follow the Avalon client (host+0x74C).
static class ClassIcons {
 public const int Warrior=0,Mage=1,Venomancer=3,Barbarian=4,Archer=6,Cleric=7;
 static readonly Dictionary<int,int> fileOf=new Dictionary<int,int>{{Warrior,1},{Mage,2},{Venomancer,3},{Barbarian,4},{Archer,5},{Cleric,6}};
 static readonly Dictionary<int,Image> icons=new Dictionary<int,Image>();static readonly object gate=new object();static bool tried;
 public static bool Known(int c){lock(gate)return icons.ContainsKey(c);}
 public static string Name(int c){switch(c){case Warrior:return "Guerreiro";case Mage:return "Mago";case Venomancer:return "Feiticeira";case Barbarian:return "Bárbaro";case Archer:return "Arqueiro";case Cleric:return "Sacerdote";default:return "";}}
 public static void Draw(Graphics g,RectangleF r,int c){Image icon;lock(gate)if(!icons.TryGetValue(c,out icon))return;var state=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawImage(icon,r);g.Restore(state);}
 // Loads the icons once from <game folder>\surfaces.pck. Returns a short note for the log, or null when nothing changed.
 public static string Load(string gameExe){lock(gate){if(tried)return null;tried=true;}
  try{string pck=Path.Combine(Path.GetDirectoryName(gameExe),"surfaces.pck");if(!File.Exists(pck))return "Ícones de classe: surfaces.pck não encontrado; nomes sem ícone.";
   var found=Read(pck);lock(gate)foreach(var kv in found)icons[kv.Key]=kv.Value;return found.Count==fileOf.Count?null:"Ícones de classe: "+found.Count+"/"+fileOf.Count+" encontrados no jogo.";}
  catch(Exception e){return "Ícones de classe indisponíveis: "+e.Message;}}
 // Owner request: less neon on the rim, crisper symbol. The outer ring is desaturated into a darker metal;
 // the inside gets a light unsharp mask and a little contrast. Works on the in-memory copy only.
 public static Bitmap Refine(Bitmap src){int w=src.Width,h=src.Height;var bmp=new Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(bmp))g.DrawImage(src,0,0,w,h);src.Dispose();
  var rect=new Rectangle(0,0,w,h);var data=bmp.LockBits(rect,System.Drawing.Imaging.ImageLockMode.ReadWrite,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var px=new byte[data.Stride*h];System.Runtime.InteropServices.Marshal.Copy(data.Scan0,px,0,px.Length);var orig=(byte[])px.Clone();
  float cx=(w-1)/2f,cy=(h-1)/2f,R=Math.Min(w,h)/2f;
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*data.Stride+x*4;if(orig[i+3]==0)continue;float d=(float)Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy))/R;
   for(int c=0;c<3;c++){float v=orig[i+c];
    if(d<0.76f&&x>0&&y>0&&x<w-1&&y<h-1){float n=(orig[i+c-4]+orig[i+c+4]+orig[i+c-data.Stride]+orig[i+c+data.Stride])/4f;v=v+0.9f*(v-n);v=(v-128)*1.12f+128;}
    px[i+c]=(byte)Math.Max(0,Math.Min(255,v));}
   if(d>=0.74f){float b=px[i],gg=px[i+1],r=px[i+2];float lum=0.299f*r+0.587f*gg+0.114f*b;float t=Math.Min(1f,(d-0.74f)/0.12f)*0.8f;
    px[i]=(byte)(b+(lum*0.8f-b)*t);px[i+1]=(byte)(gg+(lum*0.8f-gg)*t);px[i+2]=(byte)(r+(lum*0.82f-r)*t);}}
  System.Runtime.InteropServices.Marshal.Copy(px,0,data.Scan0,px.Length);bmp.UnlockBits(data);return bmp;}
 const uint K1=0xA8937462,K2=0xF1A43653;
 static byte[] Inflate(byte[] data,int size){if(data.Length>=size)return data;using(var ms=new MemoryStream(data,2,data.Length-2))using(var z=new DeflateStream(ms,CompressionMode.Decompress)){var o=new byte[size];int n=0,r;while(n<size&&(r=z.Read(o,n,size-n))>0)n+=r;if(n!=size)throw new InvalidDataException("Entrada incompleta");return o;}}
 static Dictionary<int,Image> Read(string pck){var wanted=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);foreach(var kv in fileOf)wanted[@"surfaces\new\classes\"+kv.Value+".png"]=kv.Key;var result=new Dictionary<int,Image>();var gbk=Encoding.GetEncoding(936);
  using(var fs=new FileStream(pck,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var br=new BinaryReader(fs)){long length=fs.Length;fs.Seek(-8,SeekOrigin.End);int count=br.ReadInt32();uint version=br.ReadUInt32();if(version!=0x20003)throw new InvalidDataException("formato de pacote 0x"+version.ToString("X")+" não suportado");if(count<1||count>200000)throw new InvalidDataException("índice inválido");
   fs.Seek(-280,SeekOrigin.End);long table=br.ReadUInt32()^K1;if(table<=0||table>=length)throw new InvalidDataException("índice fora do arquivo");fs.Seek(table,SeekOrigin.Begin);
   var entries=new List<Tuple<int,long,int,int>>();
   for(int i=0;i<count&&entries.Count<wanted.Count;i++){int s1=(int)(br.ReadUInt32()^K1);br.ReadUInt32();if(s1<=0||s1>4096)throw new InvalidDataException("entrada inválida");byte[] e=br.ReadBytes(s1);if(s1<288)e=Inflate(e,288);string path=gbk.GetString(e,0,260).Split('\0')[0];int cls;if(wanted.TryGetValue(path,out cls))entries.Add(Tuple.Create(cls,(long)BitConverter.ToUInt64(e,264),BitConverter.ToInt32(e,272),BitConverter.ToInt32(e,276)));}
   foreach(var t in entries){if(t.Item3<=0||t.Item3>1<<20||t.Item4<=0||t.Item2+t.Item4>length)continue;fs.Seek(t.Item2,SeekOrigin.Begin);byte[] data=Inflate(br.ReadBytes(t.Item4),t.Item3);using(var ms=new MemoryStream(data))using(var img=Image.FromStream(ms))result[t.Item1]=Refine(new Bitmap(img));}}
  return result;}
}
