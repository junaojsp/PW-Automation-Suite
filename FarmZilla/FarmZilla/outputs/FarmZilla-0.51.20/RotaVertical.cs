using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace RotaAvalon {
    // 0.45.0 (owner: climb and descend in a straight line, like holding Space / Z): the host player's vertical flight
    // direction (player+0xB78: 0x10 up, 0x20 down). 0.45.1: the game clears it about 33 times a second (seen live), so it
    // is held by a small thread that puts it back about every millisecond while a direction is wanted — live test on
    // 04/11/2026: a character descending 5,3/s climbed 7,3 in 1,5 s with 49 rewrites. Released at once when not wanted.
    sealed class VerticalControl : IDisposable {
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] bytes,int size,out IntPtr read);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr h,IntPtr address,byte[] bytes,int size,out IntPtr written);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
        const uint Offset=0xB78,Up=0x10,Down=0x20;
        readonly IntPtr handle;readonly object gate=new object();Thread worker;volatile int want;volatile uint player;volatile bool closed;
        public VerticalControl(int pid){handle=OpenProcess(0x0008|0x0010|0x0020,false,pid);if(handle==IntPtr.Zero)throw new InvalidOperationException("Controle vertical indisponível.");}
        uint Read(uint address){var b=new byte[4];IntPtr n;if(!ReadProcessMemory(handle,(IntPtr)unchecked((int)address),b,4,out n)||n.ToInt32()!=4)throw new InvalidOperationException("Leitura do controle vertical falhou.");return BitConverter.ToUInt32(b,0);}
        // Puts the wanted direction bits back if the game cleared them; only the two direction bits are touched.
        void Apply(uint at,int direction){
            if(at<0x10000)throw new InvalidOperationException("Personagem indisponível.");
            uint address=checked(at+Offset);uint value=Read(address);uint next=(value&~(Up|Down))|(direction>0?Up:direction<0?Down:0u);if(next==value)return;
            IntPtr n;var bytes=BitConverter.GetBytes(next);if(!WriteProcessMemory(handle,(IntPtr)unchecked((int)address),bytes,4,out n)||n.ToInt32()!=4)throw new InvalidOperationException("Escrita do controle vertical falhou.");}
        // direction: +1 up, -1 down, 0 released.
        public void Set(uint at,int direction){
            lock(gate){player=at;want=direction;if(direction==0){Apply(at,0);return;}Apply(at,direction);
                if(worker==null){worker=new Thread(Hold){IsBackground=true,Name="FarmZilla vertical",Priority=ThreadPriority.AboveNormal};worker.Start();}}}
        void Hold(){
            bool fine=false;
            try{while(!closed){int d=want;
                    if(d==0){if(fine){timeEndPeriod(1);fine=false;}Thread.Sleep(15);continue;}
                    if(!fine){timeBeginPeriod(1);fine=true;}
                    try{Apply(player,d);}catch{want=0;}Thread.Sleep(1);}}
            finally{if(fine)timeEndPeriod(1);}
        }
        public void Dispose(){closed=true;want=0;try{if(player!=0)Apply(player,0);}catch{}var w=worker;if(w!=null)w.Join(200);CloseHandle(handle);}
    }
}
