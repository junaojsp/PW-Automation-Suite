using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// FarmZilla 0.50.0 — localização dos endereços do jogo por assinatura de bytes.
// A tabela (GameSignatures.Data.cs) é gerada por work/game-signatures/make-signatures.cjs a partir do exe validado.
// Regras: cada alvo precisa ser encontrado de forma ÚNICA; se houver mais de uma assinatura para o alvo, todas as que
// forem encontradas precisam concordar; qualquer ambiguidade, ausência, endereço fora da seção esperada ou guarda de
// deslocamento diferente recusa o perfil inteiro (o FarmZilla bloqueia o cliente como antes).
public static partial class GameSignatures {
 public enum Kind {Fn,Abs,Rel,Rtti}
 public sealed class Sig {
  public Kind Kind;public byte[] Bytes;public bool[] Fixed;public int Offset;public string Type;public string Text;
  static Sig Pattern(Kind kind,string text,int offset){var parts=text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);var s=new Sig{Kind=kind,Offset=offset,Text=text,Bytes=new byte[parts.Length],Fixed=new bool[parts.Length]};
   for(int i=0;i<parts.Length;i++){if(parts[i]=="??")continue;s.Bytes[i]=Convert.ToByte(parts[i],16);s.Fixed[i]=true;}
   if(offset<0||offset>parts.Length)throw new ArgumentException("Assinatura inválida: "+text);return s;}
  public static Sig Fn(string p,int o){return Pattern(Kind.Fn,p,o);}
  public static Sig Abs(string p,int o){var s=Pattern(Kind.Abs,p,o);if(o+4>s.Bytes.Length)throw new ArgumentException("Assinatura inválida: "+p);return s;}
  public static Sig Rel(string p,int o){var s=Pattern(Kind.Rel,p,o);if(o+4>s.Bytes.Length)throw new ArgumentException("Assinatura inválida: "+p);return s;}
  public static Sig Rtti(string type,int colOffset){return new Sig{Kind=Kind.Rtti,Type=type,Offset=colOffset,Text=type};}
 }
 // Prologue: primeiros 6 bytes esperados da função (com curingas), conferidos no endereço achado por qualquer assinatura.
 public sealed class Target {public readonly string Name;public readonly uint Rva;public readonly Sig[] Sigs;public readonly Sig Prologue;public Target(string name,uint rva,Sig[] sigs,string prologue=null){Name=name;Rva=rva;Sigs=sigs;Prologue=prologue==null?null:Sig.Fn(prologue,0);}}
 public sealed class Guard {public readonly string Name,Function;public readonly uint Value,Within;public readonly Sig Sig;public Guard(string name,uint value,string function,string pattern,int offset,uint within){Name=name;Value=value;Function=function;Sig=Sig.Fn(pattern,offset);Within=within;}}
}

// Imagem PE x86 mapeada por RVA a partir do arquivo (somente leitura; nada é executado).
public sealed class PeImage {
 public sealed class Section {public string Name;public uint Rva,Size,RawOffset,RawSize;public uint Characteristics;public bool Code{get{return (Characteristics&0x20000020)!=0;}}}
 public byte[] Img;public uint ImageBase,SizeOfImage;public ushort Machine;public readonly List<Section> Sections=new List<Section>();public readonly HashSet<uint> Relocs=new HashSet<uint>();
 public static PeImage Load(byte[] file){
  var pe=new PeImage();if(file.Length<0x400||BitConverter.ToUInt16(file,0)!=0x5A4D)throw new InvalidDataException("Executável não PE.");
  int nt=BitConverter.ToInt32(file,0x3C);if(nt<0||nt+0xF8>file.Length||BitConverter.ToUInt32(file,nt)!=0x4550)throw new InvalidDataException("PE inválido.");
  pe.Machine=BitConverter.ToUInt16(file,nt+4);if(pe.Machine!=0x14C)throw new InvalidDataException("Arquitetura diferente de x86.");
  int count=BitConverter.ToUInt16(file,nt+6),opt=nt+24,table=opt+BitConverter.ToUInt16(file,nt+20);
  if(BitConverter.ToUInt16(file,opt)!=0x10B)throw new InvalidDataException("PE32 esperado.");
  pe.ImageBase=BitConverter.ToUInt32(file,opt+28);pe.SizeOfImage=BitConverter.ToUInt32(file,opt+56);
  if(pe.SizeOfImage<0x1000||pe.SizeOfImage>256*1024*1024)throw new InvalidDataException("Tamanho de imagem inválido.");
  pe.Img=new byte[pe.SizeOfImage];Buffer.BlockCopy(file,0,pe.Img,0,Math.Min(0x1000,file.Length));
  for(int i=0;i<count;i++){int s=table+40*i;if(s+40>file.Length)throw new InvalidDataException("Tabela de seções inválida.");
   string name=Encoding.ASCII.GetString(file,s,8).Split('\0')[0];uint vs=BitConverter.ToUInt32(file,s+8),rva=BitConverter.ToUInt32(file,s+12),raw=BitConverter.ToUInt32(file,s+16),off=BitConverter.ToUInt32(file,s+20);
   if((ulong)rva+vs>pe.SizeOfImage)throw new InvalidDataException("Seção fora da imagem.");
   uint n=Math.Min(raw,vs);if((ulong)off+n>(ulong)file.LongLength)n=(uint)Math.Max(0L,file.LongLength-(long)off);
   if(n>0)Buffer.BlockCopy(file,(int)off,pe.Img,(int)rva,(int)n);
   pe.Sections.Add(new Section{Name=name,Rva=rva,Size=vs,RawOffset=off,RawSize=raw,Characteristics=BitConverter.ToUInt32(file,s+36)});}
  uint relRva=BitConverter.ToUInt32(file,opt+96+5*8),relSize=BitConverter.ToUInt32(file,opt+96+5*8+4);
  if(relRva!=0&&(ulong)relRva+relSize<=pe.SizeOfImage)for(uint p=relRva;p+8<=relRva+relSize;){uint page=pe.U32(p),size=pe.U32(p+4);if(size<8)break;for(uint q=p+8;q+2<=p+size;q+=2){ushort e=BitConverter.ToUInt16(pe.Img,(int)q);if((e>>12)==3)pe.Relocs.Add(page+(uint)(e&0xFFF));}p+=size;}
  return pe;}
 public uint U32(uint rva){if((ulong)rva+4>(ulong)Img.LongLength)throw new InvalidDataException("Leitura fora da imagem.");return BitConverter.ToUInt32(Img,(int)rva);}
 public Section Find(string name){foreach(var s in Sections)if(s.Name==name)return s;return null;}
 public Section At(uint rva){foreach(var s in Sections)if(rva>=s.Rva&&rva<s.Rva+s.Size)return s;return null;}
}

public static class SignatureScanner {
 // Posições (RVA) em [start,end) que casam com o padrão; para quando passar de limit.
 public static List<uint> Find(PeImage pe,uint start,uint end,GameSignatures.Sig s,int limit){
  var hits=new List<uint>();byte[] b=pe.Img,p=s.Bytes;bool[] f=s.Fixed;int n=p.Length;
  // âncora: os dois primeiros bytes fixos
  int a=-1,c=-1;for(int i=0;i<n;i++)if(f[i]){if(a<0)a=i;else{c=i;break;}}
  if(a<0||c<0)throw new InvalidDataException("Assinatura sem bytes fixos suficientes.");
  byte pa=p[a],pc=p[c];long last=(long)Math.Min(end,(uint)b.Length)-n;
  for(long q=start;q<=last;q++){if(b[q+a]!=pa||b[q+c]!=pc)continue;bool ok=true;for(int i=0;i<n;i++)if(f[i]&&b[q+i]!=p[i]){ok=false;break;}if(ok){hits.Add((uint)q);if(hits.Count>limit)break;}}
  return hits;}
 sealed class Outcome {public bool Found;public uint Rva;public string Error;}
 static Outcome One(PeImage pe,PeImage.Section text,GameSignatures.Sig s){
  var o=new Outcome();
  if(s.Kind==GameSignatures.Kind.Rtti){var v=Vtable(pe,s.Type,(uint)s.Offset);if(v.Count>1){o.Error="RTTI "+s.Type+" ambíguo ("+v.Count+")";return o;}if(v.Count==1){o.Found=true;o.Rva=v[0];}return o;}
  var hits=Find(pe,text.Rva,text.Rva+text.Size,s,1);if(hits.Count>1){o.Error="assinatura ambígua";return o;}if(hits.Count==0)return o;
  uint at=hits[0];
  if(s.Kind==GameSignatures.Kind.Fn)o.Rva=at+(uint)s.Offset;
  else if(s.Kind==GameSignatures.Kind.Abs){uint pos=at+(uint)s.Offset;if(!pe.Relocs.Contains(pos)){o.Error="referência absoluta sem relocação";return o;}uint va=pe.U32(pos);if(va<pe.ImageBase||va-pe.ImageBase>=pe.SizeOfImage){o.Error="endereço fora da imagem";return o;}o.Rva=va-pe.ImageBase;}
  else{uint pos=at+(uint)s.Offset;o.Rva=unchecked((uint)(pos+4+(int)pe.U32(pos)));}
  o.Found=true;return o;}
 // Vtable primária de um tipo pelo RTTI do MSVC: TypeDescriptor (nome) -> CompleteObjectLocator (offset) -> vtable.
 public static List<uint> Vtable(PeImage pe,string type,uint colOffset){
  var result=new List<uint>();byte[] name=Encoding.ASCII.GetBytes(type+"\0");var tds=new List<uint>();
  foreach(var sec in pe.Sections){if(sec.Code)continue;uint end=sec.Rva+sec.Size;for(uint q=sec.Rva+8;q+name.Length<=end;q+=4){if(pe.Img[q]!=name[0]||pe.Img[q+1]!=name[1])continue;bool ok=true;for(int i=0;i<name.Length;i++)if(pe.Img[q+i]!=name[i]){ok=false;break;}if(ok)tds.Add(q-8);}}
  if(tds.Count!=1)return tds.Count>1?new List<uint>{0,0}:result;
  uint td=pe.ImageBase+tds[0];var cols=new List<uint>();
  foreach(var sec in pe.Sections){if(sec.Code)continue;uint end=sec.Rva+sec.Size;for(uint q=sec.Rva;q+20<=end;q+=4)if(pe.U32(q+12)==td&&pe.U32(q)==0&&pe.U32(q+4)==colOffset)cols.Add(q);}
  foreach(uint col in cols){uint va=pe.ImageBase+col;foreach(var sec in pe.Sections){if(sec.Code)continue;uint end=sec.Rva+sec.Size;for(uint q=sec.Rva;q+8<=end;q+=4)if(pe.U32(q)==va)result.Add(q+4);}}
  return result;}
 public sealed class Resolution {public readonly Dictionary<string,uint> Rva=new Dictionary<string,uint>();public readonly List<string> Errors=new List<string>();public bool Ok{get{return Errors.Count==0;}}}
 public static Resolution Resolve(PeImage pe){
  var r=new Resolution();var text=pe.Find(".text");if(text==null||!text.Code){r.Errors.Add("seção .text ausente");return r;}
  foreach(var t in GameSignatures.Targets){
   uint? value=null;bool failed=false;
   foreach(var s in t.Sigs){var o=One(pe,text,s);if(o.Error!=null){r.Errors.Add(t.Name+": "+o.Error);failed=true;break;}if(!o.Found)continue;
    if(value.HasValue&&value.Value!=o.Rva){r.Errors.Add(t.Name+": assinaturas discordam ("+value.Value.ToString("X")+" × "+o.Rva.ToString("X")+")");failed=true;break;}value=o.Rva;}
   if(failed)continue;if(!value.HasValue){r.Errors.Add(t.Name+": não encontrado");continue;}
   var sec=pe.At(value.Value);bool code=t.Sigs[0].Kind==GameSignatures.Kind.Fn||t.Sigs[0].Kind==GameSignatures.Kind.Rel;
   if(sec==null||sec.Code!=code){r.Errors.Add(t.Name+": endereço "+value.Value.ToString("X")+" fora da seção esperada");continue;}
   if(r.Rva.ContainsKey(t.Name)){r.Errors.Add(t.Name+": duplicado");continue;}
   if(t.Prologue!=null){bool same=value.Value+t.Prologue.Bytes.Length<=pe.Img.Length;for(int k=0;same&&k<t.Prologue.Bytes.Length;k++)if(t.Prologue.Fixed[k]&&pe.Img[value.Value+k]!=t.Prologue.Bytes[k])same=false;if(!same){r.Errors.Add(t.Name+": prólogo inesperado em "+value.Value.ToString("X"));continue;}}
   r.Rva[t.Name]=value.Value;}
  foreach(var g in GameSignatures.Guards){uint fn;if(!r.Rva.TryGetValue(g.Function,out fn)){r.Errors.Add("guarda "+g.Name+": função "+g.Function+" sem endereço");continue;}
   var hits=Find(pe,text.Rva,text.Rva+text.Size,g.Sig,1);
   if(hits.Count!=1){r.Errors.Add("guarda "+g.Name+": "+(hits.Count==0?"não encontrada":"ambígua"));continue;}
   if(hits[0]<fn||hits[0]-fn>=g.Within){r.Errors.Add("guarda "+g.Name+": fora de "+g.Function);continue;}
   uint got=pe.U32(hits[0]+(uint)g.Sig.Offset);if(got!=g.Value)r.Errors.Add("guarda "+g.Name+": deslocamento "+got.ToString("X")+" em vez de "+g.Value.ToString("X"));}
  return r;}
}
