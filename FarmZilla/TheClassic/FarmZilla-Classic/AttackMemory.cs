using System;using System.Linq;
sealed partial class CharacterMemory {
 public uint SelectedTarget {get{return U(Host+ClassicLayout.HostTarget);}}
 public bool AttackConfigured(){uint config=U(U(Module+G.Root)+0x18);byte[] b=Read(config+ClassicLayout.MacroConfig,17);return Enumerable.Range(0,8).Any(i=>{short id=BitConverter.ToInt16(b,1+2*i);return id>0||id==-1;});}
 // Macro N lives at config+ClassicLayout.MacroConfig+17*N (macro 0 attack, 1 buff, 2 support).
 public bool MacroConfigured(int n){uint config=U(U(Module+G.Root)+0x18);byte[] b=Read(config+ClassicLayout.MacroConfig+(uint)(17*n),17);return Enumerable.Range(0,8).Any(i=>{short id=BitConverter.ToInt16(b,1+2*i);return id>0||id==-1;});}
 public bool OwnedMacroRunning(uint combo,int index){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==(uint)index&&Read(combo+0x24,1)[0]==0;}
 public bool OwnedAttackRunning(uint combo,uint target){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==0&&U(combo+0x2C)==target&&Read(combo+0x24,1)[0]==0;}
}
