using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Text;
// Investigação isolada: somente enumeração, leitura de arquivo e ReadProcessMemory.
// Não chama nem altera o FarmZilla, não injeta e não envia comandos às janelas.
class DiscoveryProbe {
 const string Known="34fca82cafa1f38825228626aa3e8f98c56274c095cb46e317b2d665aaf69684";
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int size);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] data,UIntPtr size,out UIntPtr read);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr process);
 sealed class Candidate {public int Pid;public bool ByName,ByClass;public string Title="";}
 static byte[] Read(IntPtr handle,uint address,int size){var data=new byte[size];UIntPtr got;if(!ReadProcessMemory(handle,new IntPtr(unchecked((int)address)),data,new UIntPtr((uint)size),out got)||got.ToUInt32()!=size)throw new Exception("Leitura indisponível (Windows "+Marshal.GetLastWin32Error()+")");return data;}
 static uint U(IntPtr handle,uint address){return BitConverter.ToUInt32(Read(handle,address,4),0);}
 static int Main(){try{
  var all=new Dictionary<int,Candidate>();foreach(var p in Process.GetProcessesByName("ElementClient"))using(p)all[p.Id]=new Candidate{Pid=p.Id,ByName=true};
  IntPtr window=IntPtr.Zero;var seen=new HashSet<IntPtr>();while((window=FindWindowEx(IntPtr.Zero,window,"ElementClient Window",null))!=IntPtr.Zero&&seen.Add(window)){uint id;GetWindowThreadProcessId(window,out id);Candidate c;if(!all.TryGetValue((int)id,out c))all[(int)id]=c=new Candidate{Pid=(int)id};c.ByClass=true;var title=new StringBuilder(256);GetWindowText(window,title,title.Capacity);c.Title=title.ToString();}
  Console.WriteLine("Por nome="+all.Values.Count(c=>c.ByName)+"; por classe="+all.Values.Count(c=>c.ByClass)+"; candidatos únicos="+all.Count);
  int ready=0;foreach(var c in all.Values.OrderBy(c=>c.Pid)){
   Console.WriteLine("PID="+c.Pid+" nome="+c.ByName+" classe="+c.ByClass+" título="+c.Title);
   IntPtr query=IntPtr.Zero,read=IntPtr.Zero;try{
    query=OpenProcess(0x1000,false,c.Pid);if(query==IntPtr.Zero)throw new Exception("Consulta negada (Windows "+Marshal.GetLastWin32Error()+")");var path=new StringBuilder(32768);int capacity=path.Capacity;if(!QueryFullProcessImageName(query,0,path,ref capacity))throw new Exception("Caminho indisponível (Windows "+Marshal.GetLastWin32Error()+")");
    string file=path.ToString(),hash;ushort machine;using(var f=File.OpenRead(file)){using(var binary=new BinaryReader(f,Encoding.UTF8,true)){if(binary.ReadUInt16()!=0x5A4D)throw new Exception("Arquivo não PE");f.Position=0x3C;int pe=binary.ReadInt32();f.Position=pe;if(binary.ReadUInt32()!=0x4550)throw new Exception("PE inválido");machine=binary.ReadUInt16();}f.Position=0;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    Console.WriteLine("Executável="+Path.GetFileName(file)+" arquitetura="+machine.ToString("X4")+" SHA256="+hash);
    if(machine!=0x14C){Console.WriteLine("INCOMPATÍVEL: perfil atual exige x86; não ler offsets nem habilitar ações.");continue;}if(hash!=Known){Console.WriteLine("INCOMPATÍVEL: compilação diferente; não ler offsets nem habilitar ações.");continue;}
    read=OpenProcess(0x410,false,c.Pid);if(read==IntPtr.Zero)throw new Exception("Acesso de leitura negado (Windows "+Marshal.GetLastWin32Error()+")");uint module;using(var p=Process.GetProcessById(c.Pid))module=unchecked((uint)p.MainModule.BaseAddress.ToInt32());uint root=U(read,module+0x9B870C),run=U(read,root+0x1C),host=U(read,run+0x34),character=U(read,host+0x4F0);if(character<2)throw new Exception("Personagem não entrou no mundo");uint name=U(read,host+0x748);string text=Encoding.Unicode.GetString(Read(read,name,64)).Split('\0')[0].Trim();if(String.IsNullOrEmpty(text)||text.Any(Char.IsControl))throw new Exception("Nome do personagem indisponível");ready++;Console.WriteLine("RECONHECIDO: "+text);
   }catch(Exception e){Console.WriteLine("DIAGNÓSTICO: "+e.GetType().Name+": "+e.Message);}finally{if(read!=IntPtr.Zero)CloseHandle(read);if(query!=IntPtr.Zero)CloseHandle(query);}
  }
  Console.WriteLine("Total reconhecido="+ready+". Nenhum comando enviado e nenhuma configuração alterada.");return 0;
 }catch(Exception e){Console.WriteLine(e.Message);return 1;}}
}
