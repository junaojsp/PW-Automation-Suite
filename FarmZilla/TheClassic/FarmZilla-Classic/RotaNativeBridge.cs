using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace RotaAvalon {
    // A private notification wakes the game's thread. No mouse or keyboard messages.
    public static class NativeBridge {
        [StructLayout(LayoutKind.Sequential,Pack=4)]public struct Result {
            public uint Magic,Version,Operation,Pid,Thread,Nonce;public int State;public uint ExecutedThread,Error;public float X,Y,Z;public uint Reserved;
            // 0.50.0 (versão 2): endereços do perfil do jogo na ordem de GameProfile.NativeOrder e tamanho da imagem.
            public uint ImageSize;[MarshalAs(UnmanagedType.ByValArray,SizeConst=24)]public uint[] Rva;
        }
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr address,UIntPtr size,uint allocation,uint protection);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool VirtualFreeEx(IntPtr h,IntPtr address,UIntPtr size,uint free);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr address,byte[] data,int size,out IntPtr written);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr address,byte[] data,int size,out IntPtr read);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true,SetLastError=true)]static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int kind,IntPtr callback,IntPtr module,uint thread);
        [DllImport("user32.dll",SetLastError=true)]static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll",SetLastError=true)]static extern bool PostMessage(IntPtr window,uint message,UIntPtr wparam,IntPtr lparam);
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        public static Result Ping(int pid,IntPtr window,string library){return Send(pid,window,library,0,0,0,0);}
        public static Result Collect(GameClient client,ResourceCatalog catalog,uint instance,string library){var s=client.Read();if(s.Hp<=0)throw new InvalidOperationException("Personagem sem vida.");ResourceMarker target=null;foreach(var r in client.ReadResources(catalog,s).Resources)if(r.InstanceId==instance)target=r;if(target==null||!target.LevelAllowed||target.Distance>50)throw new InvalidOperationException("Material indisponível ou fora do alcance do teste.");using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,2,0,0,0,instance);}
        public static Result PrepareMaterialReward(GameClient client,string library,uint expected=0){client.Read();using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,3,0,0,0,expected);}
        public static Result InspectMaterialReward(GameClient client,string library){client.Read();using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,5,0,0,0);}
        public static Result ConfirmSecondReward(GameClient client,uint quest,string library){client.Read();using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,4,0,0,0,quest);}
        // FarmZilla 0.27.0 (operation 6): the game's own auto-move with height control. FlyTo = straight flight to (x,z) with the
        // height control on; FlightHeight = height target in game units (Y/10), which also takes off when not flying.
        public static Result FlyTo(GameClient client,double x,double z,string library){
            var s=client.Read();if(s.Hp<=0)throw new InvalidOperationException("Personagem sem vida.");
            if(!GameClient.ValidCoordinate(x)||!GameClient.ValidCoordinate(z))throw new ArgumentException("Destino inválido.");
            double dx=x-s.X,dz=z-s.Z;if(dx*dx+dz*dz>400*400)throw new InvalidOperationException("Trecho de voo limitado a 400 por comando.");
            using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,6,(float)x,1,(float)z,0);
        }
        public static Result FlightHeight(GameClient client,int height,string library){
            if(height<1||height>1000)throw new ArgumentException("Altura inválida.");client.Read();
            using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,6,0,height,0,1);
        }
        // FarmZilla 0.45.6 (operation 7): the game's own Space (up) / Z (down) — starts the keyboard flight move with no destination.
        public static Result KeyMove(GameClient client,bool up,string library){client.Read();using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,7,0,0,0,up?8u:9u);}
        // FarmZilla 0.46.0 (operation 8): the game's own auto-cultivation (F12) on or off for one client, like the game's button.
        public static Result AutoPolicy(int pid,bool on,string library){using(var process=Process.GetProcessById(pid))return Send(pid,process.MainWindowHandle,library,8,0,0,0,on?1u:0u);}
        public static Result Navigate(GameClient client,double x,double y,double z,string library){
            var s=client.Read();if(s.Hp<=0)throw new InvalidOperationException("Personagem sem vida.");
            if(!GameClient.ValidCoordinate(x)||!GameClient.ValidCoordinate(y)||!GameClient.ValidCoordinate(z)||y<0||y>10000)throw new ArgumentException("Destino inválido.");
            double dx=x-s.X,dz=z-s.Z;if(dx*dx+dz*dz>150*150||Math.Abs(y-s.Y)>120)throw new InvalidOperationException("Protótipo limitado a 150 unidades horizontais e 12 de altura no mapa por comando.");
            using(var process=Process.GetProcessById(client.Pid))return Send(client.Pid,process.MainWindowHandle,library,1,(float)x,(float)y,(float)z);
        }
        static byte[] Bytes(Result value){int n=Marshal.SizeOf(typeof(Result));IntPtr local=Marshal.AllocHGlobal(n);try{Marshal.StructureToPtr(value,local,false);var bytes=new byte[n];Marshal.Copy(local,bytes,0,n);return bytes;}finally{Marshal.FreeHGlobal(local);}}
        static Result Decode(byte[] bytes){IntPtr local=Marshal.AllocHGlobal(bytes.Length);try{Marshal.Copy(bytes,0,local,bytes.Length);return (Result)Marshal.PtrToStructure(local,typeof(Result));}finally{Marshal.FreeHGlobal(local);}}
        static Result Send(int pid,IntPtr window,string library,uint operation,float x,float y,float z,uint reserved=0){
            if(IntPtr.Size!=4)throw new InvalidOperationException("A ponte deve executar em x86.");if(!File.Exists(library))throw new FileNotFoundException("Ponte nativa ausente.",library);RotaPaths.Verify(library);
            uint owner;uint thread=GetWindowThreadProcessId(window,out owner);if(owner!=(uint)pid||thread==0)throw new InvalidOperationException("Janela incompatível.");
            IntPtr process=IntPtr.Zero,module=IntPtr.Zero,remote=IntPtr.Zero,hook=IntPtr.Zero;bool completed=false;
            try{
                module=LoadLibrary(Path.GetFullPath(library));if(module==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr callback=GetProcAddress(module,"NativeHookProc");if(callback==IntPtr.Zero)callback=GetProcAddress(module,"NativeHookProc@12");if(callback==IntPtr.Zero)callback=GetProcAddress(module,"_NativeHookProc@12");if(callback==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                process=OpenProcess(0x438,false,pid);if(process==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                var request=new Result{Magic=0x52415631,Version=2,Operation=operation,Pid=(uint)pid,Thread=thread,Nonce=unchecked((uint)Guid.NewGuid().GetHashCode()),X=x,Y=y,Z=z,Reserved=reserved,Rva=new uint[24]};
                if(operation!=0){var profile=global::GameProfile.Require(pid);var table=profile.NativeTable();Array.Copy(table,request.Rva,table.Length);request.ImageSize=profile.SizeOfImage;}
                var bytes=Bytes(request);
                remote=VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)4096,0x3000,4);if(remote==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr written;if(!WriteProcessMemory(process,remote,bytes,bytes.Length,out written)||written.ToInt32()!=bytes.Length)throw new Win32Exception(Marshal.GetLastWin32Error());
                hook=SetWindowsHookEx(3,callback,module,thread);if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                if(!PostMessage(window,0x85A7,(UIntPtr)request.Nonce,remote))throw new Win32Exception(Marshal.GetLastWin32Error());
                var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<5000){IntPtr read;if(!ReadProcessMemory(process,remote,bytes,bytes.Length,out read)||read.ToInt32()!=bytes.Length)throw new Win32Exception(Marshal.GetLastWin32Error());var result=Decode(bytes);if(result.State==2||result.State==-1){completed=true;if(result.ExecutedThread!=thread)throw new InvalidOperationException("Contexto de thread incorreto.");if(result.Error!=0)throw new Win32Exception((int)result.Error);return result;}Thread.Sleep(10);}
                throw new TimeoutException("O cliente não confirmou a chamada nativa. Nenhuma repetição automática foi enviada.");
            }finally{if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);if(remote!=IntPtr.Zero&&completed)VirtualFreeEx(process,remote,UIntPtr.Zero,0x8000);if(process!=IntPtr.Zero)CloseHandle(process);if(module!=IntPtr.Zero)FreeLibrary(module);}
        }
    }
}


