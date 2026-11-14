using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

sealed class DeviceKeyRecord {public string Hardware {get;set;}public string PrivateXml {get;set;}}
sealed class DevicePublicKey {public string kty {get;set;}public string n {get;set;}public string e {get;set;}}
sealed class OnlineIdentity {
 public string Hardware,Code;public DevicePublicKey Public;string privateXml;
 static string Clean(object value){string s=Convert.ToString(value).Trim().ToUpperInvariant();if(s.Length<4||new[]{"UNKNOWN","NONE","DEFAULT STRING","TO BE FILLED BY O.E.M.","NOT SPECIFIED","SYSTEM SERIAL NUMBER"}.Contains(s)||s.All(c=>c=='0'||c=='F'||c=='-'||c==' '))return "";return Regex.Replace(s,@"\s+","");}
 static List<ManagementObject> Query(string sql){using(var search=new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"),new ObjectQuery(sql),new EnumerationOptions{Timeout=TimeSpan.FromSeconds(4),ReturnImmediately=false})){using(var results=search.Get())return results.Cast<ManagementObject>().ToList();}}
 static string Value(string sql,string property){var values=new List<string>();foreach(var row in Query(sql))using(row){string v=Clean(row[property]);if(v!="")values.Add(v);}return String.Join(",",values.Distinct().OrderBy(s=>s,StringComparer.Ordinal));}
 static string SystemDisk(){
  string drive=Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');var values=new List<string>();
  foreach(var partition in Query("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='"+drive+"'} WHERE AssocClass=Win32_LogicalDiskToPartition"))using(partition){foreach(var disk in Query("ASSOCIATORS OF {"+partition.Path.RelativePath+"} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))using(disk){string serial=Clean(disk["SerialNumber"]);if(serial!="")values.Add(serial);}}
  return String.Join(",",values.Distinct().OrderBy(s=>s,StringComparer.Ordinal));
 }
 public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
 internal static string HardwareFromParts(IDictionary<string,string> parts){var valid=parts.Where(p=>!String.IsNullOrEmpty(p.Value)).OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray();if(valid.Length<2)throw new InvalidOperationException("Não foi possível confirmar pelo menos dois identificadores de hardware. Verifique o serviço WMI do Windows.");return "BGH2-"+Hash("Bagaceira:hardware:v2|"+String.Join("|",valid.Select(p=>p.Key+"="+p.Value)));}
 internal static string ReadHardware(){var p=new Dictionary<string,string>();p["cpu"]=Value("SELECT ProcessorId FROM Win32_Processor","ProcessorId");p["board"]=Value("SELECT SerialNumber FROM Win32_BaseBoard","SerialNumber");p["uuid"]=Value("SELECT UUID FROM Win32_ComputerSystemProduct","UUID");p["disk"]=SystemDisk();return HardwareFromParts(p);}
 public static OnlineIdentity Load(){
  string hardware=ReadHardware(),path=Path.Combine(LicenseCore.Home,"device-v2.dat");DeviceKeyRecord record=null;
  if(File.Exists(path)){try{record=new JavaScriptSerializer().Deserialize<DeviceKeyRecord>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)));}catch{throw new InvalidOperationException("A chave deste PC não pôde ser aberta nesta conta do Windows. Solicite nova ativação ao administrador.");}}
  if(record!=null&&record.Hardware!=hardware)throw new InvalidOperationException("Os identificadores do hardware mudaram. É necessária uma nova identificação e liberação pelo administrador.");
  if(record==null){using(var rsa=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;record=new DeviceKeyRecord{Hardware=hardware,PrivateXml=rsa.ToXmlString(true)};}Directory.CreateDirectory(LicenseCore.Home);File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(record)),null,DataProtectionScope.CurrentUser));}
  using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(record.PrivateXml);RSAParameters p=rsa.ExportParameters(false);var pub=new DevicePublicKey{kty="RSA",n=LicenseCore.Encode(p.Modulus),e=LicenseCore.Encode(p.Exponent)};return new OnlineIdentity{Hardware=hardware,Public=pub,Code=BuildCode(hardware,pub),privateXml=record.PrivateXml};}
 }
 internal static string BuildCode(string hardware,DevicePublicKey pub){return "BGC2-"+Hash("Bagaceira:online:v2|"+hardware+"|"+pub.n+"|"+pub.e);}
 internal string Sign(string nonce){using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(privateXml);return LicenseCore.Encode(rsa.SignData(Encoding.UTF8.GetBytes("Bagaceira-lease-v2\n"+Code+"\n"+nonce),"SHA256"));}}
}
