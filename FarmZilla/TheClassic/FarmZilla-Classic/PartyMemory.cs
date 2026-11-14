using System;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text;
sealed class PartyInvitation {public uint Address,Inviter,Ticket;}
sealed class PartySnapshot {public uint Leader;public readonly HashSet<uint> Members=new HashSet<uint>();}
sealed partial class CharacterMemory {
 [StructLayout(LayoutKind.Sequential)]struct Region {public uint Base,Allocation,Protect,Size,State,Protection,Type;}
 [DllImport("kernel32.dll")]static extern uint VirtualQueryEx(IntPtr h,IntPtr a,out Region m,uint n);
 static readonly Dictionary<int,List<uint>> dialogCache=new Dictionary<int,List<uint>>();
 static readonly Dictionary<int,DateTime> lastScan=new Dictionary<int,DateTime>();
 public uint Team {get{return U(Host+ClassicLayout.HostTeam);}}
 public PartySnapshot ReadParty(){uint team=Team;if(team==0)return null;if(U(team)!=Module+G.Team)throw new Exception("Estrutura da PT indisponível.");var party=new PartySnapshot{Leader=U(team+4)};uint count=U(team+0x18),items=U(team+0x14);if(count>64)throw new Exception("Lista da PT inválida.");for(uint i=0;i<count;i++){uint member=U(items+4*i);if(member==0||U(member)!=Module+G.TeamMember)throw new Exception("Membro da PT indisponível.");party.Members.Add(U(member+0xC));}return party;}
 PartyInvitation CheckInvite(uint address,uint inviter,bool knownSlot){try{byte[] b=Read(address,0x104);if(!knownSlot&&BitConverter.ToUInt32(b,0)!=Module+G.DlgMessageBox)return null;string name="";try{name=Encoding.ASCII.GetString(Read(BitConverter.ToUInt32(b,0x4C),32)).Split('\0')[0];}catch{if(!knownSlot)return null;}var found=PartyInvite.Match(b,name,inviter,knownSlot);if(found!=null)found.Address=address;return found;}catch{return null;}}
 PartyInvitation QueuedInvite(uint inviter){try{uint vec=Module+ClassicLayout.InviteQueueRva;uint begin=U(vec),count=U(vec+0xC);if(begin<0x10000||count==0||count>8)return null;for(uint i=0;i<count;i++){byte[] rec=Read(begin+i*ClassicLayout.InviteRecord,(int)ClassicLayout.InviteRecord);if(rec.Length<(int)ClassicLayout.InviteRecord)continue;string name=Encoding.ASCII.GetString(Read(BitConverter.ToUInt32(rec,0),32)).Split('\0')[0];var found=PartyInvite.FromRecord(rec,name,inviter);if(found!=null)return found;}}catch{return null;}return null;}
 PartyInvitation DirectPop(uint inviter){try{uint game=U(Module+G.Root),run=U(game+0x1C),ui=U(run+ClassicLayout.RunUi),gameui=U(ui+8),dialog=U(gameui+ClassicLayout.UiPop);return CheckInvite(dialog,inviter,true);}catch{return null;}}
 public PartyInvitation FindPartyInvitation(int pid,uint inviter){var queued=QueuedInvite(inviter);if(queued!=null)return queued;var direct=DirectPop(inviter);if(direct!=null)return direct;List<uint> addresses;if(!dialogCache.TryGetValue(pid,out addresses)){addresses=new List<uint>();dialogCache[pid]=addresses;}foreach(uint address in addresses){var found=CheckInvite(address,inviter,false);if(found!=null)return found;}
  DateTime last;if(lastScan.TryGetValue(pid,out last)&&(DateTime.UtcNow-last).TotalSeconds<1)return null;lastScan[pid]=DateTime.UtcNow;var nextAddresses=new List<uint>();uint vtable=Module+G.DlgMessageBox;
  for(uint at=0x10000;at<0x7FFF0000;){Region m;if(VirtualQueryEx(handle,new IntPtr(unchecked((int)at)),out m,28)==0)break;ulong next=(ulong)m.Base+m.Size;if(next<=at||next>0x7FFF0000)break;at=(uint)next;if(m.State!=0x1000||m.Type!=0x20000||(m.Protection&0x101)!=0)continue;
   for(uint off=0;off<m.Size;off+=1048576){int n=(int)Math.Min(1048576,m.Size-off);byte[] b;try{b=Read(m.Base+off,n);}catch{continue;}for(int i=0;i+4<=b.Length;i+=4)if(BitConverter.ToUInt32(b,i)==vtable)nextAddresses.Add(m.Base+off+(uint)i);}
  }dialogCache[pid]=nextAddresses;foreach(uint address in nextAddresses){var found=CheckInvite(address,inviter,false);if(found!=null)return found;}return null;
 }
}
// The Classic message box is created as "Win_Pop". While an invite is on screen the client copies the inviter id to +0xFC
// and the ticket to +0x100 — the same two dwords the Yes button puts in packet 0x1C. Requiring the Avalon name
// "Game_InviteGroup" made the invite visible in game and invisible to the app.
static class PartyInvite {
 public const string BoxName="Win_Pop",InviteName="Game_InviteGroup";
 public static PartyInvitation Match(byte[] dialog,string name,uint inviter){return Match(dialog,name,inviter,false);}
 public static PartyInvitation Match(byte[] dialog,string name,uint inviter,bool knownSlot){
  if(dialog==null||dialog.Length<0x104||dialog[0x90]==0)return null;
  uint data=BitConverter.ToUInt32(dialog,0xFC);
  if(data!=inviter)return null;
  if(!knownSlot&&name!=BoxName&&name!=InviteName)return null;
  return new PartyInvitation{Inviter=data,Ticket=BitConverter.ToUInt32(dialog,0x100)};
 }
 public static PartyInvitation FromRecord(byte[] record,string name,uint inviter){
  if(record==null||record.Length<(int)ClassicLayout.InviteRecord||name!=InviteName)return null;
  uint who=BitConverter.ToUInt32(record,(int)ClassicLayout.InviteInviter);
  if(who!=inviter)return null;
  return new PartyInvitation{Inviter=who,Ticket=BitConverter.ToUInt32(record,(int)ClassicLayout.InviteTicket)};
 }
 public static byte[] Accept(PartyInvitation invitation){var answer=new byte[10];answer[0]=0x1C;Buffer.BlockCopy(BitConverter.GetBytes(invitation.Inviter),0,answer,2,4);Buffer.BlockCopy(BitConverter.GetBytes(invitation.Ticket),0,answer,6,4);return answer;}
}

