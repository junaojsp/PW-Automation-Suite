using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;
// Autoteste 0.50.0 sem jogo aberto: só LÊ o ElementClient.exe (argumento 1, padrão = perfil do dono) e cópias temporárias dele.
// 1) com o exe atual, cada assinatura resolve exatamente para o RVA validado e todas as guardas conferem;
// 2) hash conhecido = perfil validado sem procurar; 3) mesmo código com hash diferente = perfil por assinaturas com os mesmos endereços;
// 4) código carregado conferido contra o arquivo (exe mapeado só como imagem de recurso, nada executado);
// 5) recusas: assinatura ambígua, tipo RTTI ausente, deslocamento de estrutura mudado, arquivo que não é PE x86.
// Nada é executado no jogo; as cópias ficam na pasta temporária e são apagadas.
class GameSignatureTest {
 static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
 static uint FileOffset(PeImage pe,uint rva){foreach(var s in pe.Sections)if(rva>=s.Rva&&rva<s.Rva+Math.Min(s.Size,s.RawSize))return s.RawOffset+rva-s.Rva;throw new Exception("RVA sem arquivo "+rva.ToString("X"));}
 static string Temp(string dir,string name,byte[] bytes){string p=Path.Combine(dir,name);File.WriteAllBytes(p,bytes);return p;}
 static int Main(string[] args){
  string exe=args.Length>0?args[0]:@"D:\Avalon-Classico-Chave-12\Avalon Clássico Ofc\element\ElementClient.exe";
  string dir=Path.Combine(Path.GetTempPath(),"FarmZilla-Sig-Test-"+Guid.NewGuid().ToString("N"));
  try{DiagnosticLog.ToFile=false;var log=new List<string>();GameProfile.Log=delegate(string s){log.Add(s);};
   if(!File.Exists(exe)){Console.WriteLine("FAIL exe do jogo não encontrado: "+exe);return 1;}
   byte[] file=File.ReadAllBytes(exe);var pe=PeImage.Load(file);
   var clock=Stopwatch.StartNew();var r=SignatureScanner.Resolve(pe);clock.Stop();
   foreach(var e in r.Errors)Console.WriteLine("  erro: "+e);
   Check(r.Ok,"todas as "+GameSignatures.Targets.Length+" assinaturas e "+GameSignatures.Guards.Length+" guardas resolvem no exe atual ("+clock.ElapsedMilliseconds+" ms)");
   int sigs=0;foreach(var t in GameSignatures.Targets){sigs+=t.Sigs.Length;if(r.Rva[t.Name]!=t.Rva)throw new Exception(t.Name+" resolveu "+r.Rva[t.Name].ToString("X")+" em vez de "+t.Rva.ToString("X"));}
   Check(true,"cada alvo resolve exatamente para o RVA validado ("+sigs+" assinaturas)");
   // Cada assinatura isolada também é única e aponta para o mesmo RVA (nenhuma está só "de carona").
   var text=pe.Find(".text");
   foreach(var t in GameSignatures.Targets)foreach(var s in t.Sigs){if(s.Kind==GameSignatures.Kind.Rtti){var v=SignatureScanner.Vtable(pe,s.Type,(uint)s.Offset);if(v.Count!=1||v[0]!=t.Rva)throw new Exception(t.Name+" RTTI");continue;}
    var h=SignatureScanner.Find(pe,text.Rva,text.Rva+text.Size,s,2);if(h.Count!=1)throw new Exception(t.Name+": "+h.Count+" ocorrências de "+s.Text);}
   Check(true,"cada assinatura isolada tem exatamente uma ocorrência");
   var gp=GameProfile.ForFile(exe,out _unused);Check(gp!=null&&gp.Known&&gp.Root==0x9B870C&&gp.SendData==0x514240,"hash conhecido usa o perfil validado (sem procurar)");
   var known=gp.NativeTable();Check(known.Length==17&&known[0]==0x116E55C&&known[2]==0x3FA070&&known[16]==0x4DFF80,"tabela da ponte nativa na ordem de NativeOrder");
   Directory.CreateDirectory(dir);
   // Mesmo código, hash diferente (1 byte a mais no fim do arquivo, fora da imagem).
   var same=new byte[file.Length+1];Buffer.BlockCopy(file,0,same,0,file.Length);same[file.Length]=0x5A;
   string f1=Temp(dir,"ElementClient.exe",same);string failure;var sp=GameProfile.ForFile(f1,out failure);
   Check(sp!=null&&!sp.Known,"hash desconhecido com o mesmo código: perfil por assinaturas aceito");
   foreach(var t in GameSignatures.Targets)if(sp.Rva(t.Name)!=t.Rva)throw new Exception("perfil por assinaturas: "+t.Name);
   Check(sp.NativeTable().SequenceEqual(known)&&sp.SizeOfImage==GameProfile.KnownSizeOfImage,"perfil por assinaturas = endereços validados (C# e ponte nativa)");
   Check(log.Any(l=>l.Contains("por assinaturas aceito")),"aceite registrado no log");
   // Conferência no módulo carregado: o exe é mapeado neste processo só como imagem de recurso (LOAD_LIBRARY_AS_IMAGE_RESOURCE:
   // seções no lugar, nada executado, sem relocação) e o perfil confere os bytes de código de cada função contra o arquivo.
   IntPtr mapped=LoadLibraryEx(f1,IntPtr.Zero,0x20);Check(mapped!=IntPtr.Zero,"exe mapeado como imagem de recurso (sem executar)");
   try{uint b=unchecked((uint)mapped.ToInt32())&~3u;int me=Process.GetCurrentProcess().Id;
    Check(sp.ConfirmLoaded(me,b,out failure),"código mapeado confere com o arquivo nas "+GameSignatures.Targets.Count(t=>t.Sigs[0].Kind!=GameSignatures.Kind.Rtti&&t.Sigs[0].Kind!=GameSignatures.Kind.Abs)+" funções");
    uint patch=b+sp.Rva("KeyMove")+8,old;Check(VirtualProtect(new IntPtr(unchecked((int)patch)),new UIntPtr(1),0x04,out old),"página de teste liberada para escrita");
    byte keep=System.Runtime.InteropServices.Marshal.ReadByte(new IntPtr(unchecked((int)patch)));System.Runtime.InteropServices.Marshal.WriteByte(new IntPtr(unchecked((int)patch)),(byte)(keep^0xFF));
    Check(!sp.ConfirmLoaded(me,b,out failure)&&failure.Contains("KeyMove"),"código carregado diferente do arquivo é recusado");
   }finally{FreeLibrary(mapped);}
   // Ambígua: copia o padrão de KeyMove para um trecho de preenchimento (0xCC) do .text.
   var key=GameSignatures.Targets.First(t=>t.Name=="KeyMove").Sigs[0];int n=key.Bytes.Length;uint pad=0;
   for(uint q=text.Rva+0x1000;q+n+8<text.Rva+text.Size;q++){bool all=true;for(int k=0;k<n+8;k++)if(pe.Img[q+k]!=0xCC){all=false;break;}if(all){pad=q+4;break;}}
   Check(pad!=0,"trecho de preenchimento para o teste de ambiguidade");
   var amb=(byte[])same.Clone();uint fo=FileOffset(pe,pad);for(int k=0;k<n;k++)amb[fo+k]=key.Fixed[k]?key.Bytes[k]:(byte)0;
   GameProfile.ResetCache();var ap=GameProfile.ForFile(Temp(dir,"amb.exe",amb),out failure);
   Check(ap==null&&failure.Contains("KeyMove")&&failure.Contains("ambígua"),"assinatura ambígua recusa o perfil inteiro");
   // RTTI ausente: troca uma letra do nome do tipo do time.
   var rt=(byte[])same.Clone();byte[] name=System.Text.Encoding.ASCII.GetBytes(".?AVCECTeam@@\0");int at=-1;
   for(int i=0;i+name.Length<rt.Length&&at<0;i++){bool ok=true;for(int k=0;k<name.Length;k++)if(rt[i+k]!=name[k]){ok=false;break;}if(ok)at=i;}
   Check(at>0,"nome RTTI localizado no arquivo");rt[at+10]=(byte)'X';
   var rp=GameProfile.ForFile(Temp(dir,"rtti.exe",rt),out failure);Check(rp==null&&failure.Contains("Team: não encontrado"),"tipo RTTI ausente recusa o perfil");
   // Deslocamento de estrutura mudado: host+0x1D3C vira 0x1D40 no ponto da guarda (o jogo reorganizou o personagem).
   var g=GameSignatures.Guards.First(x=>x.Name=="HostMacro");var gh=SignatureScanner.Find(pe,text.Rva,text.Rva+text.Size,g.Sig,1);Check(gh.Count==1,"guarda HostMacro localizada");
   var gd=(byte[])same.Clone();uint go=FileOffset(pe,gh[0]+(uint)g.Sig.Offset);gd[go]=0x40;
   var dp=GameProfile.ForFile(Temp(dir,"guard.exe",gd),out failure);Check(dp==null&&failure.Contains("HostMacro"),"deslocamento de estrutura diferente recusa o perfil");
   // Arquivo que não é PE x86.
   var bad=(byte[])same.Clone();int nt=BitConverter.ToInt32(bad,0x3C);bad[nt+4]=0x64;bad[nt+5]=0x86;
   var bp=GameProfile.ForFile(Temp(dir,"x64.exe",bad),out failure);Check(bp==null&&failure.Contains("x86"),"executável não x86 recusado");
   Check(log.Count(l=>l.Contains("recusado"))>=4,"recusas registradas no log");
   Console.WriteLine("PASS: assinaturas do perfil Avalon ("+GameSignatures.Targets.Length+" endereços, "+GameSignatures.Guards.Length+" guardas).");return 0;}
  catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
  finally{try{Directory.Delete(dir,true);}catch{}}}
 static string _unused;
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool VirtualProtect(IntPtr a,UIntPtr n,uint p,out uint old);
}
