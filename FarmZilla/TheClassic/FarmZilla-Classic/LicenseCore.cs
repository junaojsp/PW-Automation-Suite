using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

sealed class LicensePayload {
 public string product {get;set;} public string device {get;set;} public string id {get;set;}
 public string customer {get;set;} public string issuedUtc {get;set;}
}
sealed class LicenseEnvelope {public string payload {get;set;}public string signature {get;set;}}
static class LicenseCore {
 internal const string PublicKey="<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
 internal static string Home {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmZillaClassic");}}
 internal static string DeviceCode(){
  using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32))
  using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")){
   string guid=key==null?null:key.GetValue("MachineGuid") as string;
   if(String.IsNullOrWhiteSpace(guid))throw new InvalidOperationException("Não foi possível identificar esta instalação do Windows.");
   return CodeFromIdentity(guid);
  }
 }
 internal static string CodeFromIdentity(string value){using(var sha=SHA256.Create())return "BGC1-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("Bagaceira:device:v1:"+value.Trim().ToLowerInvariant()))).Replace("-","");}
 internal static string NormalizeCode(string value){string code=(value??"").Trim().ToUpperInvariant();if(!Regex.IsMatch(code,@"\ABGC1-[A-F0-9]{64}\z"))throw new InvalidDataException("Código do PC inválido. Cole o código completo que a pessoa copiou do Bagaceira.");return code;}
 internal static string Encode(byte[] b){return Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');}
 internal static byte[] Decode(string s){s=s.Replace('-','+').Replace('_','/');return Convert.FromBase64String(s.PadRight((s.Length+3)/4*4,'='));}
 internal static string Issue(LicensePayload payload,RSACryptoServiceProvider rsa){
  payload.device=NormalizeCode(payload.device);if(payload.product!="Bagaceira"||String.IsNullOrEmpty(payload.id))throw new InvalidDataException("Dados de licença inválidos.");
  byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(payload));
  return "BGL1."+Encode(bytes)+"."+Encode(rsa.SignData(bytes,"SHA256"));
 }
 internal static LicensePayload Verify(string token,string device,string publicKey){
  if(token==null||token.Length>8192)throw new InvalidDataException("Chave de ativação inválida.");string[] parts=token.Trim().Split('.');
  if(parts.Length!=3||parts[0]!="BGL1")throw new InvalidDataException("Cole a chave de ativação completa enviada pelo administrador.");
  byte[] payload=Decode(parts[1]),signature=Decode(parts[2]);
  using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(!rsa.VerifyData(payload,"SHA256",signature))throw new InvalidDataException("Esta chave não foi emitida pelo administrador do Bagaceira.");}
  var data=new JavaScriptSerializer().Deserialize<LicensePayload>(new UTF8Encoding(false,true).GetString(payload));
  if(data==null||data.product!="Bagaceira"||String.IsNullOrEmpty(data.id))throw new InvalidDataException("Licença inválida.");
  if(NormalizeCode(data.device)!=NormalizeCode(device))throw new InvalidDataException("Esta chave foi liberada para outro computador.");return data;
 }
 internal static void Save(string token,string code){Verify(token,code,PublicKey);Directory.CreateDirectory(Home);byte[] raw=Encoding.UTF8.GetBytes(token.Trim());File.WriteAllBytes(Path.Combine(Home,"license.dat"),ProtectedData.Protect(raw,null,DataProtectionScope.CurrentUser));}
 internal static string Load(){string path=Path.Combine(Home,"license.dat");if(!File.Exists(path))return null;return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser));}
}
