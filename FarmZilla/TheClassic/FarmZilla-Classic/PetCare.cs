using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;using System.Runtime.InteropServices;using System.Threading;
// 0.51.2 (owner): the WF's scorpion is revived (F5) and called (F6) by itself when it is gone. Found 07/11/2026 (Limaozinho,
// read-only, Avalon): host+0x91C keeps the pet id; the pet is a CECPet object with that id at +0x118, outside the monster table; when it
// dies the object is destroyed (13:57:00.8: its type turned into A3DObject) while host+0x91C keeps the old id; a new call gives a
// new id and, so far, the same object address. Commands captured: F5 = 29 00 49 01 00 00 0B 01 <own id> (skill 329 on herself),
// F6 = 64 00 00 00 00 00. Rule: cached object still a CECPet with the id = alive; otherwise one full read-only scan; nothing
// found = F5, 2 s, F6, then 15 s before trying again. Runs in the 2 s cycle of BagAutoEquip.
// The Classic: the character keeps no pet id, so the pet is the CECPet whose master id (+0x254) is the character's own id.
partial class AvalonInterno {
 [StructLayout(LayoutKind.Sequential)] struct PetMbi {public IntPtr BaseAddress,AllocationBase;public uint AllocationProtect;public IntPtr RegionSize;public uint State,Protect,Type;}
 [DllImport("kernel32.dll")] static extern int VirtualQueryEx(IntPtr h,IntPtr addr,out PetMbi m,uint len);
 [DllImport("kernel32.dll",EntryPoint="ReadProcessMemory")] static extern bool PetRead(IntPtr h,IntPtr a,byte[] b,IntPtr size,out IntPtr read);
 [DllImport("kernel32.dll",EntryPoint="OpenProcess")] static extern IntPtr PetOpen(uint a,bool b,int pid);
 [DllImport("kernel32.dll",EntryPoint="CloseHandle")] static extern bool PetClose(IntPtr h);
 readonly Dictionary<int,uint> petObject=new Dictionary<int,uint>();readonly Dictionary<int,DateTime> petNext=new Dictionary<int,DateTime>();
 const uint PetRevive=329;
 static bool IsPet(RotaAvalon.ProcessMemory mem,uint obj,uint owner,uint baseA,uint end){if(obj==0)return false;try{return mem.UInt32(obj+ClassicLayout.PetMaster)==owner&&RotaAvalon.NearbyResources.NativeType(mem,obj,baseA,end)==".?AVCECPet@@";}catch{return false;}}
 static uint ScanPet(int pid,RotaAvalon.ProcessMemory mem,uint owner,uint baseA,uint end){IntPtr h=PetOpen(0x410,false,pid);if(h==IntPtr.Zero)return 0;
  try{long addr=0x10000;while(addr<0x7FFF0000){PetMbi m;if(VirtualQueryEx(h,new IntPtr(addr),out m,(uint)Marshal.SizeOf(typeof(PetMbi)))==0)break;long size=m.RegionSize.ToInt64();
    if(m.State==0x1000&&(m.Protect&0x66)!=0&&(m.Protect&0x100)==0&&size<64*1024*1024){var b=new byte[size];IntPtr read;if(PetRead(h,m.BaseAddress,b,new IntPtr(size),out read))
      for(int i=(int)ClassicLayout.PetMaster;i+4<=b.Length;i+=4){if(BitConverter.ToUInt32(b,i)!=owner)continue;uint obj=(uint)(m.BaseAddress.ToInt64()+i-ClassicLayout.PetMaster);if(IsPet(mem,obj,owner,baseA,end))return obj;}}
    addr=m.BaseAddress.ToInt64()+size;}}finally{PetClose(h);}return 0;}
 void PetCareWork(List<CharacterRef> wfs,List<string> notes){
  if(!foxOn)return;
  foreach(var c in wfs){DateTime next;if(petNext.TryGetValue(c.Pid,out next)&&DateTime.UtcNow<next)continue;
   try{using(var p=Process.GetProcessById(c.Pid))using(var mem=new RotaAvalon.ProcessMemory(c.Pid))using(var cm=new CharacterMemory(c.Pid)){
     if(cm.Id!=c.Id)continue;uint id=cm.Id;uint baseA=(uint)p.MainModule.BaseAddress.ToInt32(),end=baseA+(uint)p.MainModule.ModuleMemorySize;
     uint cached;petObject.TryGetValue(c.Pid,out cached);if(IsPet(mem,cached,id,baseA,end))continue;
     uint found=ScanPet(c.Pid,mem,id,baseA,end);if(found!=0){petObject[c.Pid]=found;continue;}
     petNext[c.Pid]=DateTime.UtcNow.AddSeconds(15);
     var revive=new byte[12];revive[0]=0x29;BitConverter.GetBytes(PetRevive).CopyTo(revive,2);revive[6]=0x0B;revive[7]=1;BitConverter.GetBytes(c.Id).CopyTo(revive,8);
     PetSend(c,revive);Thread.Sleep(2000);PetSend(c,new byte[]{0x64,0,0,0,0,0});Thread.Sleep(2500);
     uint after=ScanPet(c.Pid,mem,id,baseA,end);if(after!=0)petObject[c.Pid]=after;
     notes.Add(c.Name+": pet "+(after!=0?"revivido e invocado (F5 + F6).":"não voltou depois de F5 + F6; tento de novo em 15 s."));}}
   catch(Exception e){petNext[c.Pid]=DateTime.UtcNow.AddSeconds(15);notes.Add(c.Name+": pet: "+e.Message);}}
 }
 void PetSend(CharacterRef c,byte[] packet){TeleportBridge b=null;
  try{b=new TeleportBridge(c.Pid);if(b.CharacterId!=c.Id)throw new Exception("Personagem mudou.");b.SendPetCare(packet);}
  finally{if(b!=null){b.Dispose();if(!b.RestoreSucceeded){lock(blocked)blocked.Add(c.Pid);throw new Exception("ponte sem restauração; reinicie esse cliente.");}}}}
}
