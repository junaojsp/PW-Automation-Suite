using System;using System.Collections.Generic;using System.Collections.Concurrent;using System.Text;using System.Threading;using System.Linq;
sealed partial class CharacterMemory {
 public ushort[] ActiveQuestIds(){uint task=U(Host+ClassicLayout.HostTasks);if(U(task)!=Module+G.TaskInterface||U(task+4)!=Host)throw new Exception("Interface de missoes invalida");uint list=U(task+8);int count=Read(list,8)[0];if(count>175)throw new Exception("Contagem de missoes invalida");byte[] data=Read(list,8+32*count);if(Read(list,1)[0]!=count)throw new Exception("Lista de missoes atualizando");var ids=new List<ushort>();for(int i=0;i<count;i++)ids.Add(BitConverter.ToUInt16(data,8+32*i));return ids.ToArray();}
 static readonly ConcurrentDictionary<string,List<uint>> questWindowCache=new ConcurrentDictionary<string,List<uint>>();
 bool QuestWindowType(uint v){return v==G.DlgNpc||v==G.DlgTask||v==G.DlgTaskConfirm||v==G.DlgTaskList;}
 public List<uint> QuestWindows(CancellationToken token){string key=Host.ToString("X8")+":"+Module+":"+Id;List<uint> cached;if(questWindowCache.TryGetValue(key,out cached)){var valid=new List<uint>();foreach(uint address in cached)try{if(QuestWindowType(U(address)-Module))valid.Add(address);}catch{}if(valid.Count>0)return valid;}var found=new List<uint>();
  for(uint at=0x10000;at<0x7FFF0000;){token.ThrowIfCancellationRequested();Region region;if(VirtualQueryEx(handle,new IntPtr(unchecked((int)at)),out region,28)==0)break;ulong next=(ulong)region.Base+region.Size;if(next<=at||next>0x7FFF0000)break;at=(uint)next;if(region.State!=0x1000||region.Type!=0x20000||(region.Protection&0x101)!=0)continue;
   for(uint off=0;off<region.Size;off+=1048576){token.ThrowIfCancellationRequested();int size=(int)Math.Min(1048576,region.Size-off);byte[] b;try{b=Read(region.Base+off,size);}catch{continue;}for(int i=0;i+4<=size;i+=4)if(QuestWindowType(BitConverter.ToUInt32(b,i)-Module)){uint address=region.Base+off+(uint)i;try{uint name=U(address+0x4C);string text=Encoding.ASCII.GetString(Read(name,48)).Split('\0')[0];if(text.StartsWith("Win_"))found.Add(address);}catch{}}}
  }questWindowCache[key]=found;return found;
 }
 public List<uint> NpcDialogs(CancellationToken token){return QuestWindows(token).Where(d=>U(d)==Module+G.DlgNpc).ToList();}
 public bool NpcVisible(uint dialog){return U(dialog)==Module+G.DlgNpc&&QuestWindowVisible(dialog);}
 public bool IsNpcDialog(uint dialog){return U(dialog)==Module+G.DlgNpc;}
 public bool NpcInteractionActive(uint dialog){if(!IsNpcDialog(dialog))return false;return U(U(dialog+0x34)+ClassicLayout.UiNpcTalk)!=0||U(Host+ClassicLayout.HostNpcTalk)!=0||Read(Host+ClassicLayout.HostNpcBusy,1)[0]!=0;}
 public bool QuestWindowVisible(uint dialog){return QuestWindowType(U(dialog)-Module)&&Read(dialog+0x90,1)[0]!=0;}
}
