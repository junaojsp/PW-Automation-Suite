using System;using System.Linq;
sealed partial class CharacterMemory {
 public uint SelectedTarget {get{return U(Host+ClassicLayout.HostTarget);}}
 public bool AttackConfigured(){uint config=U(U(Module+G.Root)+0x18);return MacroSlot.Filled(Read(config+ClassicLayout.MacroConfig,17));}
 // Macro N lives at config+ClassicLayout.MacroConfig+17*N (macro 0 attack, 1 buff, 2 support).
 public bool MacroConfigured(int n){uint config=U(U(Module+G.Root)+0x18);return MacroSlot.Filled(Read(config+ClassicLayout.MacroConfig+(uint)(17*n),17));}
 public string MacroIds(int n){try{uint config=U(U(Module+G.Root)+0x18);return MacroSlot.Dump(Read(config+ClassicLayout.MacroConfig+(uint)(17*n),17));}catch{return "sem leitura";}}
 public bool OwnedMacroRunning(uint combo,int index){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==(uint)index&&Read(combo+0x24,1)[0]==0;}
 public bool OwnedAttackRunning(uint combo,uint target){return combo!=0&&RunningMacro()==combo&&U(combo+0x1C)==0&&U(combo+0x2C)==target&&Read(combo+0x24,1)[0]==0;}
}
// Skills are int16s starting at byte 1 of the 17-byte slot (the client's own loader at 0xD23C0).
static class MacroSlot {
 public static bool Filled(byte[] slot){if(slot==null||slot.Length<17)return false;return Enumerable.Range(0,8).Any(i=>{short id=BitConverter.ToInt16(slot,1+2*i);return id>0||id==-1;});}
 public static string Dump(byte[] slot){if(slot==null||slot.Length<17)return "sem leitura";return string.Join(",",Enumerable.Range(0,8).Select(i=>BitConverter.ToInt16(slot,1+2*i).ToString()));}
}
static class AttackHotkey {
 // The Atacar key is registered in both modes. Automatic keeps attacking; the key restarts a stuck combo.
 public static string Refusal(bool shortcutsOn,int modeIndex){return !shortcutsOn||modeIndex<0?"Ligue os atalhos para usar a tecla Atacar.":null;}
}
