using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class AppUpdate
{
	internal const int MaxExe = 33554432;

	internal const int MaxManifest = 65536;

	internal static string ClientAuthorization = null;

	internal static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno");

	internal static string Repository
	{
		get
		{
			return OnlineApi.Service;
		}
		set
		{
			OnlineApi.Service = value;
		}
	}

	internal static void CheckPrivateSource(string source)
	{
		if (!Uri.TryCreate(source, UriKind.Absolute, out var result) || result.Scheme != "https" || !result.IsDefaultPort || result.Host == "github.com" || result.UserInfo != "" || result.Query != "" || result.Fragment != "" || result.AbsolutePath != "/")
		{
			throw new InvalidDataException("Informe a origem HTTPS do servidor privado de atualizações, sem caminhos ou parâmetros.");
		}
	}

	internal static bool PrivateSource(string source)
	{
		return source?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? false;
	}

	internal static void CheckRepository(string repo)
	{
		if (PrivateSource(repo))
		{
			CheckPrivateSource(repo);
		}
		else if (repo == null || !Regex.IsMatch(repo, "\\A[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}\\z"))
		{
			throw new InvalidDataException("Endereço de atualização inválido.");
		}
	}

	internal static string ManifestUrl(string repo)
	{
		CheckRepository(repo);
		return PrivateSource(repo) ? (repo.TrimEnd('/') + "/update.json") : ("https://github.com/" + repo + "/releases/latest/download/update.json");
	}

	internal static UpdateManifest Verify(string envelope, string repo, string publicKey)
	{
		CheckRepository(repo);
		if (envelope == null || Encoding.UTF8.GetByteCount(envelope) > 65536)
		{
			throw new InvalidDataException("Manifesto muito grande.");
		}
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		UpdateEnvelope updateEnvelope = javaScriptSerializer.Deserialize<UpdateEnvelope>(envelope);
		if (updateEnvelope == null || updateEnvelope.payload == null || updateEnvelope.signature == null)
		{
			throw new InvalidDataException("Manifesto incompleto.");
		}
		byte[] array = Convert.FromBase64String(updateEnvelope.payload);
		byte[] signature = Convert.FromBase64String(updateEnvelope.signature);
		using (RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider())
		{
			rSACryptoServiceProvider.PersistKeyInCsp = false;
			rSACryptoServiceProvider.FromXmlString(publicKey);
			if (!rSACryptoServiceProvider.VerifyData(array, "SHA256", signature))
			{
				throw new InvalidDataException("Assinatura da atualização inválida.");
			}
		}
		UpdateManifest updateManifest = javaScriptSerializer.Deserialize<UpdateManifest>(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array));
		if (updateManifest == null || updateManifest.product != "AvalonInterno" || updateManifest.architecture != "x86" || !Version.TryParse(updateManifest.version, out var result) || result.Major < 0 || updateManifest.size < 1024 || updateManifest.size > 33554432 || updateManifest.sha256 == null || !Regex.IsMatch(updateManifest.sha256, "\\A[0-9a-fA-F]{64}\\z"))
		{
			throw new InvalidDataException("Versão ou metadados inválidos.");
		}
		if (!Uri.TryCreate(updateManifest.url, UriKind.Absolute, out var result2) || result2.Scheme != "https" || !result2.IsDefaultPort || result2.UserInfo != "" || result2.Query != "" || result2.Fragment != "" || result2.AbsolutePath.Contains("%"))
		{
			throw new InvalidDataException("Endereço de download inválido.");
		}
		if (PrivateSource(repo))
		{
			if (updateManifest.url != repo.TrimEnd('/') + "/releases/v" + updateManifest.version + "/Bagaceira.exe")
			{
				throw new InvalidDataException("Download fora do servidor privado configurado.");
			}
		}
		else if (result2.Host != "github.com" || !result2.AbsolutePath.StartsWith("/" + repo + "/releases/download/", StringComparison.Ordinal) || !result2.AbsolutePath.EndsWith("/Bagaceira.exe", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Download fora do repositório configurado.");
		}
		return updateManifest;
	}

	internal static byte[] Download(string url, int limit)
	{
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
		httpWebRequest.UserAgent = "Bagaceira/0.19.19";
		httpWebRequest.Timeout = 20000;
		httpWebRequest.ReadWriteTimeout = 20000;
		httpWebRequest.AllowAutoRedirect = false;
		if (!string.IsNullOrEmpty(ClientAuthorization))
		{
			httpWebRequest.Headers[HttpRequestHeader.Authorization] = "Bearer " + ClientAuthorization;
		}
		using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
		if (httpWebResponse.StatusCode != HttpStatusCode.OK || httpWebResponse.ResponseUri.Scheme != "https" || httpWebResponse.ContentLength > limit)
		{
			throw new InvalidDataException("Download inválido ou muito grande.");
		}
		using Stream stream = httpWebResponse.GetResponseStream();
		using MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[16384];
		int num;
		while ((num = stream.Read(array, 0, array.Length)) != 0)
		{
			if (memoryStream.Length + num > limit)
			{
				throw new InvalidDataException("Download excedeu o limite.");
			}
			memoryStream.Write(array, 0, num);
		}
		return memoryStream.ToArray();
	}

	internal static void VerifyBinary(byte[] bytes, UpdateManifest m)
	{
		if (bytes.LongLength != m.size)
		{
			throw new InvalidDataException("Tamanho da atualização não confere.");
		}
		string a;
		using (SHA256 sHA = SHA256.Create())
		{
			a = BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "");
		}
		if (!string.Equals(a, m.sha256, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("SHA-256 da atualização não confere.");
		}
		if (bytes.Length < 64 || bytes[0] != 77 || bytes[1] != 90)
		{
			throw new InvalidDataException("Executável inválido.");
		}
		int num = BitConverter.ToInt32(bytes, 60);
		if (num < 64 || num > bytes.Length - 26 || BitConverter.ToUInt32(bytes, num) != 17744 || BitConverter.ToUInt16(bytes, num + 4) != 332 || BitConverter.ToUInt16(bytes, num + 24) != 267)
		{
			throw new InvalidDataException("A atualização precisa ser um executável x86.");
		}
	}

	internal static string Stage(string repo, string envelope, UpdateManifest m, string target)
	{
		byte[] bytes = Download(m.url, 33554432);
		VerifyBinary(bytes, m);
		string text = Path.Combine(Home, "updates", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		File.WriteAllBytes(Path.Combine(text, "Bagaceira.exe"), bytes);
		UpdateRequest updateRequest = new UpdateRequest();
		updateRequest.repository = repo;
		updateRequest.envelope = envelope;
		updateRequest.target = Path.GetFullPath(target);
		UpdateRequest updateRequest2 = updateRequest;
		using (Process process = Process.GetCurrentProcess())
		{
			updateRequest2.parentId = process.Id;
			updateRequest2.parentStart = process.StartTime.ToUniversalTime().Ticks;
		}
		string text2 = Path.Combine(text, "request.json");
		File.WriteAllText(text2, new JavaScriptSerializer().Serialize(updateRequest2));
		return text2;
	}

	internal static void LaunchInstaller(string requestPath, string currentExe)
	{
		string text = Path.Combine(Path.GetDirectoryName(requestPath), "installer.exe");
		File.Copy(currentExe, text, overwrite: false);
		ProcessStartInfo processStartInfo = new ProcessStartInfo(text, "--apply-update \"" + requestPath + "\"");
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		Process.Start(processStartInfo);
	}

	internal static bool RunInstaller(string[] args)
	{
		if (args.Length == 0 || args[0] != "--apply-update")
		{
			return false;
		}
		try
		{
			if (args.Length != 2)
			{
				throw new InvalidDataException("Pedido de atualização inválido.");
			}
			string fullPath = Path.GetFullPath(args[1]);
			string directoryName = Path.GetDirectoryName(fullPath);
			string value = Path.GetFullPath(Path.Combine(Home, "updates")) + Path.DirectorySeparatorChar;
			if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || new FileInfo(fullPath).Length > 131072)
			{
				throw new InvalidDataException("Pedido fora da pasta de atualizações.");
			}
			UpdateRequest updateRequest = new JavaScriptSerializer().Deserialize<UpdateRequest>(File.ReadAllText(fullPath));
			if (updateRequest == null || Path.GetFileName(updateRequest.target) != "Bagaceira.exe" || !Path.IsPathRooted(updateRequest.target) || !File.Exists(updateRequest.target))
			{
				throw new InvalidDataException("Destino inválido.");
			}
			UpdateManifest updateManifest = Verify(updateRequest.envelope, updateRequest.repository, "<RSAKeyValue><Modulus>wsBKXnprkMV7G0sTgnnrnOj3zWnCPpoVmq20PyrVZOlpfvM/N3wDMM/ym/RiLJv4xhVn5BPvUv6khxv1n6MfM56REJQA+GW7HDAZPThfHWV9qeHvYHOwmBiPHSIw5kBtEq6cU0eNT3buJfYHadVtCKmvKnt6Mb1AwTxxT2VfJ91gT23RbxwlnunKTf/F7Ot9KAFUt17sW++c9wttLJBhNWNhC7qQPKHOiIO4/2t9F6oG8rGa0JLz6kcfwKC+jW+acXmQp/gr36RmRGyHaPhoUZutlqnOc0Optu3ZGfpUcp63OCBJSGTIIWO7m5bi6x5J/oZWRwaICJAhWHykKO93KQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
			if (new Version(updateManifest.version) <= new Version("0.19.19"))
			{
				throw new InvalidDataException("Atualização precisa ser mais recente.");
			}
			byte[] bytes = File.ReadAllBytes(Path.Combine(directoryName, "Bagaceira.exe"));
			VerifyBinary(bytes, updateManifest);
			Process process = null;
			try
			{
				process = Process.GetProcessById(updateRequest.parentId);
			}
			catch (ArgumentException)
			{
			}
			if (process != null)
			{
				using (process)
				{
					if (process.StartTime.ToUniversalTime().Ticks != updateRequest.parentStart || !string.Equals(Path.GetFullPath(process.MainModule.FileName), Path.GetFullPath(updateRequest.target), StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidDataException("Aplicativo de origem não confere.");
					}
					if (!process.WaitForExit(60000))
					{
						throw new IOException("O aplicativo ainda está encerrando. Tente novamente.");
					}
				}
			}
			ApplyFile(updateRequest.target, bytes, (string path) =>
			{
				Process.Start(new ProcessStartInfo(path)
				{
					UseShellExecute = true,
					WorkingDirectory = Path.GetDirectoryName(path)
				});
			});
			File.Delete(Path.Combine(directoryName, "Bagaceira.exe"));
			File.Delete(fullPath);
		}
		catch (Exception ex2)
		{
			DiagnosticLog.Write("Atualização não aplicada: " + ex2.Message);
			MessageBox.Show("Não foi possível aplicar a atualização: " + ex2.Message + "\nA versão anterior foi preservada.", "Atualização do FarmZilla");
		}
		return true;
	}

	internal static void ApplyFile(string target, byte[] bytes, Action<string> launch)
	{
		string text = target + ".update-" + Guid.NewGuid().ToString("N");
		string text2 = text + ".bak";
		bool flag = false;
		try
		{
			File.WriteAllBytes(text, bytes);
			File.Replace(text, target, text2);
			flag = true;
			launch(target);
		}
		catch
		{
			if (flag && File.Exists(text2))
			{
				File.Replace(text2, target, null);
			}
			throw;
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
		if (File.Exists(text2))
		{
			File.Delete(text2);
		}
	}
}
