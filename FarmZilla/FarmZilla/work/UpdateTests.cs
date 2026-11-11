using System;using System.IO;using System.Security.Cryptography;using System.Text;using System.Web.Script.Serialization;
static class UpdateTests {
 static int count;static JavaScriptSerializer json=new JavaScriptSerializer();
 static void Assert(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,name);}
 static string Sign(UpdateManifest m,RSACryptoServiceProvider rsa){byte[] payload=Encoding.UTF8.GetBytes(json.Serialize(m));return json.Serialize(new UpdateEnvelope{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(rsa.SignData(payload,"SHA256"))});}
 static void Main(){
  using(var rsa=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;string key=rsa.ToXmlString(false);
   byte[] exe=new byte[1024];exe[0]=0x4d;exe[1]=0x5a;Array.Copy(BitConverter.GetBytes(128),0,exe,60,4);Array.Copy(BitConverter.GetBytes(0x4550),0,exe,128,4);Array.Copy(BitConverter.GetBytes((ushort)0x14c),0,exe,132,2);Array.Copy(BitConverter.GetBytes((ushort)0x10b),0,exe,152,2);
   string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(exe)).Replace("-","");
   var m=new UpdateManifest{product="AvalonInterno",architecture="x86",version="0.14.0",url="https://github.com/example/AvalonInterno-Updates/releases/download/v0.14.0/Bagaceira.exe",sha256=hash,size=exe.Length};
   string signed=Sign(m,rsa);Assert(AppUpdate.Verify(signed,"example/AvalonInterno-Updates",key).version=="0.14.0","manifesto assinado válido");
   var bad=json.Deserialize<UpdateEnvelope>(signed);bad.payload=Convert.ToBase64String(Encoding.UTF8.GetBytes(json.Serialize(new UpdateManifest{product="changed"})));Reject(()=>AppUpdate.Verify(json.Serialize(bad),"example/AvalonInterno-Updates",key),"payload alterado bloqueado");
   using(var other=new RSACryptoServiceProvider(2048)){other.PersistKeyInCsp=false;Reject(()=>AppUpdate.Verify(signed,"example/AvalonInterno-Updates",other.ToXmlString(false)),"assinatura de outra chave bloqueada");}
   m.url="https://github.com/other/repo/releases/download/v1/Bagaceira.exe";Reject(()=>AppUpdate.Verify(Sign(m,rsa),"example/AvalonInterno-Updates",key),"outro repositório bloqueado");
   m.url="http://github.com/example/AvalonInterno-Updates/releases/download/v1/Bagaceira.exe";Reject(()=>AppUpdate.Verify(Sign(m,rsa),"example/AvalonInterno-Updates",key),"HTTP bloqueado");
   m.url="https://github.com/example/AvalonInterno-Updates/releases/download/v1/Bagaceira.exe";m.architecture="x64";Reject(()=>AppUpdate.Verify(Sign(m,rsa),"example/AvalonInterno-Updates",key),"arquitetura errada bloqueada");m.architecture="x86";
   AppUpdate.VerifyBinary(exe,m);Assert(true,"executável x86 e hash conferidos");exe[500]=1;Reject(()=>AppUpdate.VerifyBinary(exe,m),"binário adulterado bloqueado");exe[500]=0;m.size++;Reject(()=>AppUpdate.VerifyBinary(exe,m),"tamanho incorreto bloqueado");
   Reject(()=>AppUpdate.ManifestUrl("owner/repo/../other"),"repositório com caminho inválido bloqueado");
   m.size=exe.Length;m.url="https://updates.example/releases/v0.14.0/Bagaceira.exe";Assert(AppUpdate.Verify(Sign(m,rsa),"https://updates.example",key).version=="0.14.0","servidor privado e manifesto assinado aceitos");
   Reject(()=>AppUpdate.Verify(Sign(m,rsa),"https://outro.example",key),"servidor privado divergente bloqueado");
   Reject(()=>AppUpdate.CheckPrivateSource("https://github.com/example/repo"),"configuração pública recusada");
   Reject(()=>AppUpdate.CheckPrivateSource("http://updates.example"),"servidor privado sem TLS recusado");
   // Regression: parent PID can be gone before helper starts.
   string processFile=System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
   string originTest=Path.Combine(Path.GetTempPath(),"FarmZilla-update-parent-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(originTest);
   try {
    string helperExe=Path.Combine(originTest,"installer.exe"),originalExe=Path.Combine(originTest,"FarmZilla.exe");
    File.Copy(processFile,helperExe);File.Copy(processFile,originalExe);
    Assert(FarmZillaControlUpdate.SameOriginalBinary(helperExe,originalExe),
      "processo encerrado: helper e executavel original idênticos aceitos");
    using(var output=new FileStream(originalExe,FileMode.Open,FileAccess.Write,FileShare.None)){
     output.Position=output.Length-1;output.WriteByte(0x58);
    }
    Assert(!FarmZillaControlUpdate.SameOriginalBinary(helperExe,originalExe),
      "processo encerrado: executavel adulterado recusado");
    Assert(!FarmZillaControlUpdate.SameOriginalBinary(helperExe,Path.Combine(originTest,"missing.exe")),
      "processo encerrado: executavel inexistente recusado");
   }finally{Directory.Delete(originTest,true);}
   string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AvalonUpdateTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string target=Path.Combine(dir,"Bagaceira.exe");File.WriteAllText(target,"old");
   try {AppUpdate.ApplyFile(target,Encoding.UTF8.GetBytes("new"),p=>Assert(File.ReadAllText(p)=="new","substituição antes de reiniciar"));Assert(File.ReadAllText(target)=="new","instalação preservada");
    Reject(()=>AppUpdate.ApplyFile(target,Encoding.UTF8.GetBytes("bad"),p=>{throw new IOException("simulated launch failure");}),"falha no reinício reportada");Assert(File.ReadAllText(target)=="new","rollback preserva versão anterior");Assert(Directory.GetFiles(dir).Length==1,"sem executáveis antigos após instalação");
   }finally{foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}
  }
  Console.WriteLine(count+" verificações aprovadas.");
 }
}
