using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.IO.Compression;using System.Text;
// Class icons come from the game installed on this PC (surfaces.pck, the same 25 px emblems as the party window).
// Nothing from the game is shipped with FarmZilla: the archive index and six small images are read once, in memory.
// Profession ids follow the The Classic client (host+0x6DC; names from its table at 0x9C3848). The Classic keeps every class
// emblem in one vertical strip, surfaces\ingame\profession.tga, of 24 equal frames; the game draws a party member with the
// frame whose index is the member's class id as received (team member +0x44), so class N = frame N. The Avalon PNGs below
// are preferred when the installed game has them.
static class ClassIcons {
 public const int Warrior=0,Mage=1,Venomancer=3,Barbarian=4,Archer=6,Cleric=7;
 static readonly Dictionary<int,int> fileOf=new Dictionary<int,int>{{Warrior,1},{Mage,2},{Venomancer,3},{Barbarian,4},{Archer,5},{Cleric,6}};
 static readonly Dictionary<int,Image> icons=new Dictionary<int,Image>();static readonly object gate=new object();static bool tried;
 const string Strip=@"surfaces\ingame\profession.tga";const int StripFrames=24,Classes=12;
 public static bool Known(int c){lock(gate)return icons.ContainsKey(c);}
 static readonly string[] names={"Guerreiro","Mago","Espiritualista","Feiticeira","Bárbaro","Mercenário","Arqueiro","Sacerdote","Arcano","Místico","Retalhador","Tormentador"};
 public static string Name(int c){if(c>=0&&c<names.Length)return names[c];return c>=0&&c<32?"Classe "+c:"";}
 public static void Draw(Graphics g,RectangleF r,int c){Image icon;lock(gate)if(!icons.TryGetValue(c,out icon))return;var state=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawImage(icon,r);g.Restore(state);}
 // Loads the icons once from <game folder>\surfaces.pck. Returns a short note for the log, or null when nothing changed.
 public static string Load(string gameExe){lock(gate){if(tried)return null;tried=true;}
  try{string pck=Path.Combine(Path.GetDirectoryName(gameExe),"surfaces.pck");if(!File.Exists(pck))return "Ícones de classe: surfaces.pck não encontrado; nomes sem ícone.";
   var found=Read(pck);lock(gate)foreach(var kv in found)icons[kv.Key]=kv.Value;return found.Count>=Classes?null:"Ícones de classe: "+found.Count+"/"+Classes+" encontrados no jogo.";}
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
 // Both archive revisions the clients use: 0x20003 (Avalon; 288-byte entries, 64-bit offset) and 0x20002 (older; 276-byte
 // entries, 32-bit offset). Same keys and per-entry compression.
 static Dictionary<int,Image> Read(string pck){var wanted=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);foreach(var kv in fileOf)wanted[@"surfaces\new\classes\"+kv.Value+".png"]=kv.Key;wanted[Strip]=-1;var result=new Dictionary<int,Image>();var gbk=Encoding.GetEncoding(936);
  using(var fs=new FileStream(pck,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var br=new BinaryReader(fs)){long length=fs.Length;fs.Seek(-8,SeekOrigin.End);int count=br.ReadInt32();uint version=br.ReadUInt32();if(version!=0x20003&&version!=0x20002)throw new InvalidDataException("formato de pacote 0x"+version.ToString("X")+" não suportado");if(count<1||count>500000)throw new InvalidDataException("índice inválido");
   bool v3=version==0x20003;int entrySize=v3?288:276;
   fs.Seek(v3?-280:-272,SeekOrigin.End);long table=br.ReadUInt32()^K1;if(table<=0||table>=length)throw new InvalidDataException("índice fora do arquivo");fs.Seek(table,SeekOrigin.Begin);
   var entries=new List<Tuple<int,long,int,int>>();
   for(int i=0;i<count&&entries.Count<wanted.Count;i++){int s1=(int)(br.ReadUInt32()^K1);br.ReadUInt32();if(s1<=0||s1>4096)throw new InvalidDataException("entrada inválida");byte[] e=br.ReadBytes(s1);if(s1<entrySize)e=Inflate(e,entrySize);string path=gbk.GetString(e,0,260).Split('\0')[0];int cls;
    if(wanted.TryGetValue(path,out cls))entries.Add(v3?Tuple.Create(cls,(long)BitConverter.ToUInt64(e,264),BitConverter.ToInt32(e,272),BitConverter.ToInt32(e,276)):Tuple.Create(cls,(long)BitConverter.ToUInt32(e,260),BitConverter.ToInt32(e,264),BitConverter.ToInt32(e,268)));}
   foreach(var t in entries){if(t.Item3<=0||t.Item3>8<<20||t.Item4<=0||t.Item2+t.Item4>length)continue;fs.Seek(t.Item2,SeekOrigin.Begin);byte[] data=Inflate(br.ReadBytes(t.Item4),t.Item3);
    try{if(t.Item1>=0){using(var ms=new MemoryStream(data))using(var img=Image.FromStream(ms))result[t.Item1]=Refine(new Bitmap(img));}
     else using(var strip=Tga(data)){int h=strip.Height/StripFrames;if(h<4)continue;for(int c=0;c<Classes;c++){if(result.ContainsKey(c))continue;var frame=new Bitmap(strip.Width,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(frame))g.DrawImage(strip,new Rectangle(0,0,strip.Width,h),new Rectangle(0,c*h,strip.Width,h),GraphicsUnit.Pixel);if(Blank(frame)){frame.Dispose();continue;}result[c]=Refine(frame);}}}
    catch{}}}
  return result;}
 static bool Blank(Bitmap b){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>16)return false;return true;}
 // Truecolor TGA (types 2 and 10, 24/32 bits), the format of the game's interface sprites.
 internal static Bitmap Tga(byte[] d){if(d.Length<18)throw new InvalidDataException("TGA curto");int idLen=d[0],cmap=d[1],type=d[2],w=d[12]|d[13]<<8,h=d[14]|d[15]<<8,bpp=d[16],desc=d[17];
  if(cmap!=0||(type!=2&&type!=10)||(bpp!=24&&bpp!=32)||w<1||h<1||w>4096||h>8192)throw new InvalidDataException("TGA não suportado");
  int px=bpp/8,at=18+idLen+(d[5]|d[6]<<8)*((d[7]+7)/8);var raw=new byte[w*h*4];int n=0,total=w*h;
  Action<int> put=(src)=>{raw[n*4]=d[src];raw[n*4+1]=d[src+1];raw[n*4+2]=d[src+2];raw[n*4+3]=px==4?d[src+3]:(byte)255;n++;};
  if(type==2){if(at+total*px>d.Length)throw new InvalidDataException("TGA incompleto");for(int i=0;i<total;i++)put(at+i*px);}
  else while(n<total){if(at>=d.Length)throw new InvalidDataException("TGA incompleto");int head=d[at++],run=(head&0x7F)+1;if(n+run>total)run=total-n;
   if((head&0x80)!=0){if(at+px>d.Length)throw new InvalidDataException("TGA incompleto");for(int k=0;k<run;k++)put(at);at+=px;}
   else{if(at+run*px>d.Length)throw new InvalidDataException("TGA incompleto");for(int k=0;k<run;k++){put(at);at+=px;}}}
  bool top=(desc&0x20)!=0;var bmp=new Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var data=bmp.LockBits(new Rectangle(0,0,w,h),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  for(int y=0;y<h;y++)System.Runtime.InteropServices.Marshal.Copy(raw,(top?y:h-1-y)*w*4,data.Scan0+y*data.Stride,w*4);bmp.UnlockBits(data);return bmp;}
}
