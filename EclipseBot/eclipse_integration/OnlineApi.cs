using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

internal static class OnlineApi
{
	internal static string Service
	{
		get
		{
			string path = Path.Combine(LicenseCore.Home, "online-service.txt");
			return File.Exists(path) ? File.ReadAllText(path).Trim() : "https://bagaceira-control.viacaomimo.workers.dev";
		}
		set
		{
			ValidateService(value);
			Directory.CreateDirectory(LicenseCore.Home);
			File.WriteAllText(Path.Combine(LicenseCore.Home, "online-service.txt"), new Uri(value.Trim()).GetLeftPart(UriPartial.Authority));
		}
	}

	internal static void ValidateService(string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var result) || result.Scheme != "https" || !result.IsDefaultPort || result.UserInfo != "" || result.Query != "" || result.Fragment != "" || result.AbsolutePath != "/")
		{
			throw new InvalidDataException("Informe somente o endereço HTTPS do serviço de ativação, sem caminhos ou parâmetros.");
		}
	}

	internal static T Post<T>(string service, string path, object body, string bearer = null)
	{
		ValidateService(service);
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(service.TrimEnd('/') + path);
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		httpWebRequest.Method = "POST";
		httpWebRequest.ContentType = "application/json; charset=utf-8";
		httpWebRequest.UserAgent = "Bagaceira/0.19.19";
		httpWebRequest.Timeout = 12000;
		httpWebRequest.ReadWriteTimeout = 12000;
		httpWebRequest.AllowAutoRedirect = false;
		if (bearer != null)
		{
			httpWebRequest.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearer;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));
		httpWebRequest.ContentLength = bytes.Length;
		try
		{
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
			}
			using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			if (httpWebResponse.StatusCode != HttpStatusCode.OK)
			{
				throw new IOException("Resposta inesperada do serviço.");
			}
			using Stream responseStream = httpWebResponse.GetResponseStream();
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[4096];
			int num;
			while ((num = responseStream.Read(array, 0, array.Length)) > 0)
			{
				if (memoryStream.Length + num > 16384)
				{
					throw new InvalidDataException("Resposta excedeu o limite.");
				}
				memoryStream.Write(array, 0, num);
			}
			return new JavaScriptSerializer().Deserialize<T>(Encoding.UTF8.GetString(memoryStream.ToArray()));
		}
		catch (WebException ex)
		{
			if (ex.Response is HttpWebResponse httpWebResponse2)
			{
				int statusCode = (int)httpWebResponse2.StatusCode;
				httpWebResponse2.Dispose();
				if (statusCode == 401 || statusCode == 403)
				{
					throw new OnlineDenied("Computador bloqueado ou aguardando liberação no gerenciador.");
				}
			}
			throw;
		}
	}

	internal static string Acquire(OnlineIdentity identity, string service)
	{
		ChallengeReply challengeReply = Post<ChallengeReply>(service, "/v1/device/register", new
		{
			device = identity.Code,
			hardware = identity.Hardware,
			publicKey = identity.Public
		});
		if (challengeReply == null || string.IsNullOrEmpty(challengeReply.nonce) || challengeReply.nonce.Length > 100)
		{
			throw new InvalidDataException("Desafio do servidor inválido.");
		}
		LeaseReply leaseReply = Post<LeaseReply>(service, "/v1/device/lease", new
		{
			device = identity.Code,
			nonce = challengeReply.nonce,
			signature = identity.Sign(challengeReply.nonce)
		});
		if (leaseReply == null || string.IsNullOrEmpty(leaseReply.lease))
		{
			throw new InvalidDataException("Autorização não recebida.");
		}
		return leaseReply.lease;
	}
}
