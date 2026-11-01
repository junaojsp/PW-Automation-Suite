using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal sealed class OnlineIdentity
{
	public string Hardware;

	public string Code;

	public DevicePublicKey Public;

	private string privateXml;

	private static string Clean(object value)
	{
		string text = Convert.ToString(value).Trim().ToUpperInvariant();
		if (text.Length < 4 || new string[6] { "UNKNOWN", "NONE", "DEFAULT STRING", "TO BE FILLED BY O.E.M.", "NOT SPECIFIED", "SYSTEM SERIAL NUMBER" }.Contains(text) || text.All((char c) => c == '0' || c == 'F' || c == '-' || c == ' '))
		{
			return "";
		}
		return Regex.Replace(text, "\\s+", "");
	}

	private static List<ManagementObject> Query(string sql)
	{
		using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(new ManagementScope("\\\\.\\root\\cimv2"), new ObjectQuery(sql), new EnumerationOptions
		{
			Timeout = TimeSpan.FromSeconds(4.0),
			ReturnImmediately = false
		});
		using ManagementObjectCollection source = managementObjectSearcher.Get();
		return source.Cast<ManagementObject>().ToList();
	}

	private static string Value(string sql, string property)
	{
		List<string> list = new List<string>();
		foreach (ManagementObject item in Query(sql))
		{
			using (item)
			{
				string text = Clean(item[property]);
				if (text != "")
				{
					list.Add(text);
				}
			}
		}
		return string.Join(",", list.Distinct().OrderBy((string s) => s, StringComparer.Ordinal));
	}

	private static string SystemDisk()
	{
		string text = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
		List<string> list = new List<string>();
		foreach (ManagementObject item in Query("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + text + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
		{
			using (item)
			{
				foreach (ManagementObject item2 in Query("ASSOCIATORS OF {" + item.Path.RelativePath + "} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
				{
					using (item2)
					{
						string text2 = Clean(item2["SerialNumber"]);
						if (text2 != "")
						{
							list.Add(text2);
						}
					}
				}
			}
		}
		return string.Join(",", list.Distinct().OrderBy((string s) => s, StringComparer.Ordinal));
	}

	public static string Hash(string value)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
	}

	internal static string HardwareFromParts(IDictionary<string, string> parts)
	{
		KeyValuePair<string, string>[] array = parts.Where((KeyValuePair<string, string> p) => !string.IsNullOrEmpty(p.Value)).OrderBy((KeyValuePair<string, string> p) => p.Key, StringComparer.Ordinal).ToArray();
		if (array.Length < 2)
		{
			throw new InvalidOperationException("Não foi possível confirmar pelo menos dois identificadores de hardware. Verifique o serviço WMI do Windows.");
		}
		return "BGH2-" + Hash("Bagaceira:hardware:v2|" + string.Join("|", array.Select((KeyValuePair<string, string> p) => p.Key + "=" + p.Value)));
	}

	internal static string ReadHardware()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary["cpu"] = Value("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");
		dictionary["board"] = Value("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber");
		dictionary["uuid"] = Value("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");
		dictionary["disk"] = SystemDisk();
		return HardwareFromParts(dictionary);
	}

	public static OnlineIdentity Load()
	{
		string text = ReadHardware();
		string path = Path.Combine(LicenseCore.Home, "device-v2.dat");
		DeviceKeyRecord deviceKeyRecord = null;
		if (File.Exists(path))
		{
			try
			{
				deviceKeyRecord = new JavaScriptSerializer().Deserialize<DeviceKeyRecord>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)));
			}
			catch
			{
				throw new InvalidOperationException("A chave deste PC não pôde ser aberta nesta conta do Windows. Solicite nova ativação ao administrador.");
			}
		}
		if (deviceKeyRecord != null && deviceKeyRecord.Hardware != text)
		{
			throw new InvalidOperationException("Os identificadores do hardware mudaram. É necessária uma nova identificação e liberação pelo administrador.");
		}
		if (deviceKeyRecord == null)
		{
			using (RSACryptoServiceProvider temporaryKeyProvider = new RSACryptoServiceProvider(2048))
			{
				temporaryKeyProvider.PersistKeyInCsp = false;
				DeviceKeyRecord deviceKeyRecord2 = new DeviceKeyRecord();
				deviceKeyRecord2.Hardware = text;
				deviceKeyRecord2.PrivateXml = temporaryKeyProvider.ToXmlString(includePrivateParameters: true);
				deviceKeyRecord = deviceKeyRecord2;
			}
			Directory.CreateDirectory(LicenseCore.Home);
			File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(deviceKeyRecord)), null, DataProtectionScope.CurrentUser));
		}
		using RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
		rSACryptoServiceProvider.PersistKeyInCsp = false;
		rSACryptoServiceProvider.FromXmlString(deviceKeyRecord.PrivateXml);
		RSAParameters rSAParameters = rSACryptoServiceProvider.ExportParameters(includePrivateParameters: false);
		DevicePublicKey devicePublicKey = new DevicePublicKey();
		devicePublicKey.kty = "RSA";
		devicePublicKey.n = LicenseCore.Encode(rSAParameters.Modulus);
		devicePublicKey.e = LicenseCore.Encode(rSAParameters.Exponent);
		DevicePublicKey devicePublicKey2 = devicePublicKey;
		OnlineIdentity onlineIdentity = new OnlineIdentity();
		onlineIdentity.Hardware = text;
		onlineIdentity.Public = devicePublicKey2;
		onlineIdentity.Code = BuildCode(text, devicePublicKey2);
		onlineIdentity.privateXml = deviceKeyRecord.PrivateXml;
		return onlineIdentity;
	}

	internal static string BuildCode(string hardware, DevicePublicKey pub)
	{
		return "BGC2-" + Hash("Bagaceira:online:v2|" + hardware + "|" + pub.n + "|" + pub.e);
	}

	internal string Sign(string nonce)
	{
		using RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
		rSACryptoServiceProvider.PersistKeyInCsp = false;
		rSACryptoServiceProvider.FromXmlString(privateXml);
		return LicenseCore.Encode(rSACryptoServiceProvider.SignData(Encoding.UTF8.GetBytes("Bagaceira-lease-v2\n" + Code + "\n" + nonce), "SHA256"));
	}
}
