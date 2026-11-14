using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Text;
// Descoberta por janela/processo e diagnóstico somente por leitura; perfil validado continua obrigatório.
sealed class ClientCandidate {public int Pid;public bool ByName,ByClass;public string WindowTitle="";}
static class ClientDiscovery {
 const string Known=GameProfile.KnownSha256;
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int size);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr process);
 internal static ClientCandidate[] Find(Action<string> log){var all=new Dictionary<int,ClientCandidate>();
  foreach(var p in Process.GetProcessesByName("ElementClient"))using(p)all[p.Id]=new ClientCandidate{Pid=p.Id,ByName=true};
  IntPtr window=IntPtr.Zero;var seen=new HashSet<IntPtr>();while((window=FindWindowEx(IntPtr.Zero,window,"ElementClient Window",null))!=IntPtr.Zero&&seen.Add(window)){uint id;GetWindowThreadProcessId(window,out id);if(id==0)continue;ClientCandidate c;if(!all.TryGetValue((int)id,out c))all[(int)id]=c=new ClientCandidate{Pid=(int)id};c.ByClass=true;var text=new StringBuilder(256);GetWindowText(window,text,text.Capacity);c.WindowTitle=text.ToString();}
  log("Detecção: "+all.Values.Count(c=>c.ByName)+" por nome, "+all.Values.Count(c=>c.ByClass)+" por classe de janela; "+all.Count+" cliente(s) distinto(s).");return all.Values.OrderBy(c=>c.Pid).ToArray();
 }
 internal static bool Compatible(int pid,Action<string> log){IntPtr query=OpenProcess(0x1000,false,pid);if(query==IntPtr.Zero)throw new Exception("Consulta negada (Windows "+Marshal.GetLastWin32Error()+")");try{
  var path=new StringBuilder(32768);int size=path.Capacity;if(!QueryFullProcessImageName(query,0,path,ref size))throw new Exception("Caminho indisponível (Windows "+Marshal.GetLastWin32Error()+")");string hash;ushort machine;
  using(var f=File.OpenRead(path.ToString())){using(var binary=new BinaryReader(f,Encoding.UTF8,true)){if(binary.ReadUInt16()!=0x5A4D)throw new Exception("Executável não PE");f.Position=0x3C;int pe=binary.ReadInt32();f.Position=pe;if(binary.ReadUInt32()!=0x4550)throw new Exception("PE inválido");machine=binary.ReadUInt16();}f.Position=0;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  log("PID "+pid+": "+Path.GetFileName(path.ToString())+" · arquitetura "+machine.ToString("X4")+" · SHA-256 "+hash);
  if(machine!=0x14C){log("PID "+pid+": encontrado, mas arquitetura incompatível com o perfil x86.");return false;}
  if(hash==Known)return true;
  // 0.50.0: compilação nova só é aceita se o perfil completo por assinaturas resolver (GameProfile registra o motivo da recusa).
  string failure;var profile=GameProfile.ForFile(path.ToString(),out failure);
  if(profile==null){log("PID "+pid+": encontrado, mas não é o elementclient.exe do PW The Classic analisado.");return false;}
  log("PID "+pid+": compilação nova aceita pelo "+profile.Describe()+".");return true;
 }finally{CloseHandle(query);}}
}

