using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace RotaAvalon {
// Read-only process memory used by Rota (from RotaAvalon ferramentas/MemoryProbe.cs, without the probe tool).
public sealed class ProcessMemory : IDisposable {
    [StructLayout(LayoutKind.Sequential)] public struct Region { public uint BaseAddress,AllocationBase,AllocationProtect,RegionSize,State,Protect,Type; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] bytes,int size,out IntPtr read);
    [DllImport("kernel32.dll")] static extern int VirtualQueryEx(IntPtr h,IntPtr address,out Region region,int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    readonly IntPtr handle;
    public ProcessMemory(int pid) { handle=OpenProcess(0x410,false,pid);if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
    public byte[] Read(uint address,int count) { byte[] bytes=new byte[count];IntPtr read;bool ok=ReadProcessMemory(handle,new IntPtr(unchecked((int)address)),bytes,count,out read);int n=read.ToInt32();if(!ok && n==0)return new byte[0];if(n!=count)Array.Resize(ref bytes,n);return bytes; }
    public uint UInt32(uint address) { byte[] bytes=Read(address,4);if(bytes.Length!=4)throw new IOException("Endereço ilegível: 0x"+address.ToString("X8"));return BitConverter.ToUInt32(bytes,0); }
    public float Float(uint address) {return BitConverter.ToSingle(BitConverter.GetBytes(UInt32(address)),0);}
    public IEnumerable<Region> Regions() { ulong at=0;while(at<0x100000000UL){Region r;if(VirtualQueryEx(handle,new IntPtr(unchecked((int)(uint)at)),out r,28)==0)yield break;ulong next=(ulong)r.BaseAddress+r.RegionSize;if(next<=at)yield break;at=next;if(r.State==0x1000 && (r.Protect&0x101)==0 && (r.Protect&0xff)!=0)yield return r;} }
    public void Dispose() {CloseHandle(handle);}
}
}
