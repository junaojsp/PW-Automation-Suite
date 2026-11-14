using System;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;
sealed class PartyInvitation {public uint Address,Inviter,Ticket;}
sealed class PartySnapshot {public uint Leader;public readonly HashSet<uint> Members=new HashSet<uint>();}
sealed partial class CharacterMemory {
 [StructLayout(LayoutKind.Sequential)]struct Region {public uint Base,Allocation,Protect,Size,State,Protection,Type;}
 [DllImport("kernel32.dll")]static extern uint VirtualQueryEx(IntPtr h,IntPtr a,out Region m,uint n);
 static readonly Dictionary<int,List<uint>> dialogCache=new Dictionary<int,List<uint>>();
 static readonly Dictionary<int,DateTime> lastScan=new Dictionary<int,DateTime>();
 public uint Team {get{return U(Host+0x818);}}
 public PartySnapshot ReadParty(){uint team=Team;if(team==0)return null;if(U(team)!=Module+G.Team)throw new Exception("Estrutura da PT indisponível.");var party=new PartySnapshot{Leader=U(team+4)};uint count=U(team+0x18),items=U(team+0x14);if(count>64)throw new Exception("Lista da PT inválida.");for(uint i=0;i<count;i++){uint member=U(items+4*i);if(member==0||U(member)!=Module+G.TeamMember)throw new Exception("Membro da PT indisponível.");party.Members.Add(U(member+0xC));}return party;}
 PartyInvitation CheckInvite(uint address,uint inviter){try{byte[] b=Read(address,0x104);if(BitConverter.ToUInt32(b,0)!=Module+G.DlgMessageBox||b[0x90]==0||BitConverter.ToUInt32(b,0xFC)!=inviter)return null;string name=Encoding.ASCII.GetString(Read(BitConverter.ToUInt32(b,0x4C),32)).Split('\0')[0];if(name!="Game_InviteGroup")return null;return new PartyInvitation{Address=address,Inviter=inviter,Ticket=BitConverter.ToUInt32(b,0x100)};}catch{return null;}}
 public PartyInvitation FindPartyInvitation(int pid,uint inviter){List<uint> addresses;if(!dialogCache.TryGetValue(pid,out addresses)){addresses=new List<uint>();dialogCache[pid]=addresses;}foreach(uint address in addresses){var found=CheckInvite(address,inviter);if(found!=null)return found;}
  DateTime last;if(lastScan.TryGetValue(pid,out last)&&(DateTime.UtcNow-last).TotalSeconds<1)return null;lastScan[pid]=DateTime.UtcNow;var nextAddresses=new List<uint>();uint vtable=Module+G.DlgMessageBox;
  for(uint at=0x10000;at<0x7FFF0000;){Region m;if(VirtualQueryEx(handle,new IntPtr(unchecked((int)at)),out m,28)==0)break;ulong next=(ulong)m.Base+m.Size;if(next<=at||next>0x7FFF0000)break;at=(uint)next;if(m.State!=0x1000||m.Type!=0x20000||(m.Protection&0x101)!=0)continue;
   for(uint off=0;off<m.Size;off+=1048576){int n=(int)Math.Min(1048576,m.Size-off);byte[] b;try{b=Read(m.Base+off,n);}catch{continue;}for(int i=0;i+4<=b.Length;i+=4)if(BitConverter.ToUInt32(b,i)==vtable)nextAddresses.Add(m.Base+off+(uint)i);}
  }dialogCache[pid]=nextAddresses;foreach(uint address in nextAddresses){var found=CheckInvite(address,inviter);if(found!=null)return found;}return null;
 }
}

