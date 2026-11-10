using System;using System.Linq;
sealed partial class CharacterMemory {
 public short[] BuffMacroSkills(){uint config=U(U(Module+G.Root)+0x18);byte[] bytes=Read(config+0x102+17,17);return Enumerable.Range(0,8).Select(i=>BitConverter.ToInt16(bytes,1+2*i)).ToArray();}
 public bool BuffMacroConfigured(){return BuffMacroSkills().Any(id=>id>0||id==-1);}
 public uint RunningMacro(){uint combo=U(Host+0x1D3C);if(combo==0)return 0;if(U(combo)!=Module+G.ComboSkill||U(combo+4)!=Host)throw new Exception("Estado da macro indisponível.");return combo;}
 public bool AnyMacroRunning(){uint combo=RunningMacro();return combo!=0&&Read(combo+0x24,1)[0]==0;}
 public bool BuffMacroRunning(){uint combo=RunningMacro();return combo!=0&&U(combo+0x1C)==1&&Read(combo+0x24,1)[0]==0;}
}
