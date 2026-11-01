using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal static class LicenseCore
{
	internal const string PublicKey = "<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

	internal static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno");

	internal static string DeviceCode()
	{
		using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
		using RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography");
		string value = ((registryKey2 == null) ? null : (registryKey2.GetValue("MachineGuid") as string));
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException("Não foi possível identificar esta instalação do Windows.");
		}
		return CodeFromIdentity(value);
	}

	internal static string CodeFromIdentity(string value)
	{
		using SHA256 sHA = SHA256.Create();
		return "BGC1-" + BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes("Bagaceira:device:v1:" + value.Trim().ToLowerInvariant()))).Replace("-", "");
	}

	internal static string NormalizeCode(string value)
	{
		string text = (value ?? "").Trim().ToUpperInvariant();
		if (!Regex.IsMatch(text, "\\ABGC1-[A-F0-9]{64}\\z"))
		{
			throw new InvalidDataException("Código do PC inválido. Cole o código completo que a pessoa copiou do Bagaceira.");
		}
		return text;
	}

	internal static string Encode(byte[] b)
	{
		return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	internal static byte[] Decode(string s)
	{
		s = s.Replace('-', '+').Replace('_', '/');
		return Convert.FromBase64String(s.PadRight((s.Length + 3) / 4 * 4, '='));
	}

	internal static string Issue(LicensePayload payload, RSACryptoServiceProvider rsa)
	{
		payload.device = NormalizeCode(payload.device);
		if (payload.product != "Bagaceira" || string.IsNullOrEmpty(payload.id))
		{
			throw new InvalidDataException("Dados de licença inválidos.");
		}
		byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(payload));
		return "BGL1." + Encode(bytes) + "." + Encode(rsa.SignData(bytes, "SHA256"));
	}

	internal static LicensePayload Verify(string token, string device, string publicKey)
	{
		if (token == null || token.Length > 8192)
		{
			throw new InvalidDataException("Chave de ativação inválida.");
		}
		string[] array = token.Trim().Split('.');
		if (array.Length != 3 || array[0] != "BGL1")
		{
			throw new InvalidDataException("Cole a chave de ativação completa enviada pelo administrador.");
		}
		byte[] array2 = Decode(array[1]);
		byte[] signature = Decode(array[2]);
		using (RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider())
		{
			rSACryptoServiceProvider.PersistKeyInCsp = false;
			rSACryptoServiceProvider.FromXmlString(publicKey);
			if (!rSACryptoServiceProvider.VerifyData(array2, "SHA256", signature))
			{
				throw new InvalidDataException("Esta chave não foi emitida pelo administrador do Bagaceira.");
			}
		}
		LicensePayload licensePayload = new JavaScriptSerializer().Deserialize<LicensePayload>(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array2));
		if (licensePayload == null || licensePayload.product != "Bagaceira" || string.IsNullOrEmpty(licensePayload.id))
		{
			throw new InvalidDataException("Licença inválida.");
		}
		if (NormalizeCode(licensePayload.device) != NormalizeCode(device))
		{
			throw new InvalidDataException("Esta chave foi liberada para outro computador.");
		}
		return licensePayload;
	}

	internal static void Save(string token, string code)
	{
		Verify(token, code, "<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
		Directory.CreateDirectory(Home);
		byte[] bytes = Encoding.UTF8.GetBytes(token.Trim());
		File.WriteAllBytes(Path.Combine(Home, "license.dat"), ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
	}

	internal static string Load()
	{
		string path = Path.Combine(Home, "license.dat");
		if (!File.Exists(path))
		{
			return null;
		}
		return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
	}
}
