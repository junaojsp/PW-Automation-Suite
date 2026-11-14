using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace RotaAvalon {
    public sealed class ClientSnapshot {
        public int Pid; public string Name; public int Level,Hp,MaxHp,Mp,MaxMp;
        public float X,Y,Z; public DateTime ReadAt;public bool IsFlying,FlightStateKnown;public uint MovementMode,MovementFlags;
        public double MapX { get { return 400+X/10.0; } }
        public double MapZ { get { return 550+Z/10.0; } }
        public double MapHeight { get { return Y/10.0; } }
        public override string ToString() {return Name;}
    }
    public sealed class GameClient : IDisposable {
        public const string SupportedHash="34FCA82CAFA1F38825228626AA3E8F98C56274C095CB46E317B2D665AAF69684";
        // SupportedHash continua sendo a etiqueta do formato das rotas salvas (coordenadas do mundo), não a trava do executável.
        public readonly uint RootRva;public readonly global::GameProfile Profile;
        readonly Process process; readonly ProcessMemory memory; readonly uint imageBase;
        public int Pid {get {return process.Id;}}
        public string GameDirectory {get {return Path.GetDirectoryName(process.MainModule.FileName);}}
        public GameClient(int pid, IDictionary<string,bool> verifiedFiles) {
            process=Process.GetProcessById(pid);
            if(!String.Equals(process.ProcessName,"ElementClient",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Selecione um processo ElementClient.");
            string path=process.MainModule.FileName;
            // 0.50.0: perfil validado (hash conhecido) ou perfil completo por assinaturas; GameProfile guarda o resultado por arquivo.
            string failure;var profile=global::GameProfile.TryFor(process,out failure);verifiedFiles[path]=profile!=null;
            if(profile==null)throw new InvalidDataException("Versão do cliente diferente do perfil The Classic validado.");
            RootRva=profile.RotaRoot;Profile=profile;
            imageBase=unchecked((uint)process.MainModule.BaseAddress.ToInt32());
            memory=new ProcessMemory(pid);
        }
        public ClientSnapshot Read() {
            if(process.HasExited)throw new IOException("O cliente foi fechado.");
            uint root=memory.UInt32(checked(imageBase+RootRva)); CheckPointer(root);
            uint player=memory.UInt32(checked(root+ClassicLayout.RunHost)); CheckPointer(player);
            uint vtable=memory.UInt32(player);
            if(vtable<imageBase || vtable>=imageBase+(uint)process.MainModule.ModuleMemorySize)throw new InvalidDataException("Estrutura do personagem indisponível.");
            byte[] data=memory.Read(player,ClassicLayout.HostReadSize);if(data.Length!=ClassicLayout.HostReadSize)throw new IOException("Leitura incompleta do personagem.");
            var s=new ClientSnapshot {Pid=process.Id,Level=I(data,(int)ClassicLayout.HostLevel),Hp=I(data,(int)ClassicLayout.HostHp),Mp=I(data,(int)ClassicLayout.HostMp),MaxHp=I(data,(int)ClassicLayout.HostMaxHp),MaxMp=I(data,(int)ClassicLayout.HostMaxMp),X=F(data,0x3c),Y=F(data,0x40),Z=F(data,0x44),ReadAt=DateTime.Now};
            if(s.Level<1 || s.Level>200 || s.MaxHp<1 || s.MaxHp>100000000 || s.Hp<0 || s.Hp>s.MaxHp || s.MaxMp<0 || s.MaxMp>100000000 || s.Mp<0 || s.Mp>s.MaxMp || !ValidCoordinate(s.X)||!ValidCoordinate(s.Y)||!ValidCoordinate(s.Z))throw new InvalidDataException("Atributos inconsistentes; leitura interrompida.");
            s.MovementMode=BitConverter.ToUInt32(data,(int)ClassicLayout.HostMoveMode);s.MovementFlags=BitConverter.ToUInt32(data,(int)ClassicLayout.HostFlags);
            s.FlightStateKnown=(s.MovementMode==0||(s.MovementMode==2))&&((s.MovementMode==2)==((s.MovementFlags&0x10)!=0));
            s.IsFlying=s.FlightStateKnown&&s.MovementMode==2;
            uint namePointer=BitConverter.ToUInt32(data,(int)ClassicLayout.HostName);CheckPointer(namePointer);
            byte[] nameBytes=memory.Read(namePointer,64);if(nameBytes.Length<2)throw new IOException("Nome indisponível.");
            string name=Encoding.Unicode.GetString(nameBytes,0,nameBytes.Length-(nameBytes.Length%2));int zero=name.IndexOf('\0');if(zero<0)throw new InvalidDataException("Nome sem terminador.");s.Name=name.Substring(0,zero);
            if(String.IsNullOrWhiteSpace(s.Name)||s.Name.Length>31)throw new InvalidDataException("Nome inválido.");
            foreach(char c in s.Name)if(Char.IsControl(c))throw new InvalidDataException("Nome inválido.");
            return s;
        }
        public ResourceFrame ReadResources(ResourceCatalog catalog,ClientSnapshot snapshot){
            if(snapshot==null||snapshot.Pid!=Pid)throw new InvalidDataException("Personagem incompatível com a leitura.");
            uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);
            return NearbyResources.Read(memory,root,imageBase,imageBase+(uint)process.MainModule.ModuleMemorySize,catalog,snapshot);
        }
        public List<MonsterMarker> ReadMonsters(ClientSnapshot snapshot){uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);return NearbyMonsters.Read(memory,root,imageBase,imageBase+(uint)process.MainModule.ModuleMemorySize,snapshot);}
        public InventorySnapshot ReadInventory(){uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);uint player=memory.UInt32(root+ClassicLayout.RunHost);CheckPointer(player);var result=GameInventory.Read(memory,player,imageBase,imageBase+(uint)process.MainModule.ModuleMemorySize);if(memory.UInt32(root+ClassicLayout.RunHost)!=player)throw new IOException("Personagem mudou durante a leitura.");return result;}
        public ActivitySnapshot ReadActivity(){uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);uint player=memory.UInt32(root+ClassicLayout.RunHost);CheckPointer(player);var result=GameActivity.Read(memory,player,imageBase,imageBase+(uint)process.MainModule.ModuleMemorySize);if(memory.UInt32(root+ClassicLayout.RunHost)!=player)throw new IOException("Personagem mudou durante a leitura.");return result;}
        static int I(byte[] b,int o){return BitConverter.ToInt32(b,o);}
        static float F(byte[] b,int o){return BitConverter.ToSingle(b,o);}
        // 0.51.7 (read-only): task ids of the rows in the game's "missões para concluir" window (CDlgTaskConfirm: gameui+0xD54,
        // rows +0x250/+0x26C, id at label+0xF4 — the same fields the native bridge picks the row by). Empty when not available.
        public List<uint> ReadConfirmRows(){var rows=new List<uint>();try{uint end=imageBase+(uint)process.MainModule.ModuleMemorySize;
            uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);uint ui=memory.UInt32(root+ClassicLayout.RunUi),gameui=ui<0x10000?0:memory.UInt32(ui+8),dialog=gameui<0x10000?0:memory.UInt32(gameui+ClassicLayout.UiTaskConfirm);
            if(dialog<0x10000||NearbyResources.NativeType(memory,dialog,imageBase,end)!=".?AVCDlgTaskConfirm@@")return rows;
            uint count=memory.UInt32(dialog+ClassicLayout.ConfirmCount),labels=memory.UInt32(dialog+ClassicLayout.ConfirmLabels);if(count>18||labels<0x10000)return rows;
            for(uint i=0;i<count;i++){uint label=memory.UInt32(labels+i*4);if(label<0x10000)continue;try{if(NearbyResources.NativeType(memory,label,imageBase,end)!=".?AVAUILabel@@")continue;}catch{continue;}uint task=memory.UInt32(label+0xF4);if(task!=0&&!rows.Contains(task))rows.Add(task);}}
            catch{}return rows;}
        static void CheckPointer(uint p){if(p<0x10000 || (p&3)!=0)throw new InvalidDataException("Personagem ainda não carregado.");}
        public static bool ValidCoordinate(double v){return !Double.IsNaN(v)&&!Double.IsInfinity(v)&&Math.Abs(v)<=100000;}
        VerticalControl vertical;
        // Holds (or releases) the vertical flight direction of the character: +1 up, -1 down, 0 released.
        public void Vertical(int direction){uint root=memory.UInt32(checked(imageBase+RootRva));CheckPointer(root);uint player=memory.UInt32(checked(root+ClassicLayout.RunHost));CheckPointer(player);if(vertical==null)vertical=new VerticalControl(process.Id);vertical.Set(player,direction);}
        public void Dispose(){if(vertical!=null){try{Vertical(0);}catch{}vertical.Dispose();}memory.Dispose();process.Dispose();}
    }
}
