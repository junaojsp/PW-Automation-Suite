using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal static class OnlineLicense
{
	internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	internal static string CacheFileOverride = null;

	private static string CachePath => CacheFileOverride ?? Path.Combine(LicenseCore.Home, "lease-v2.dat");

	internal static long Millis(DateTime utc)
	{
		return (long)(utc.ToUniversalTime() - Epoch).TotalMilliseconds;
	}

	internal static OnlineLease Verify(string token, string code, string publicKey, long now)
	{
		if (token == null || token.Length > 8192)
		{
			throw new InvalidDataException("Autorização inválida.");
		}
		string[] array = token.Split('.');
		if (array.Length != 3 || array[0] != "BGL2")
		{
			throw new InvalidDataException("Autorização antiga não permite controle remoto. Solicite liberação no gerenciador.");
		}
		byte[] array2 = LicenseCore.Decode(array[1]);
		using (RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider())
		{
			rSACryptoServiceProvider.PersistKeyInCsp = false;
			rSACryptoServiceProvider.FromXmlString(publicKey);
			if (!rSACryptoServiceProvider.VerifyData(array2, "SHA256", LicenseCore.Decode(array[2])))
			{
				throw new InvalidDataException("Assinatura de autorização inválida.");
			}
		}
		OnlineLease onlineLease = new JavaScriptSerializer().Deserialize<OnlineLease>(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array2));
		if (onlineLease == null || onlineLease.product != "Bagaceira" || onlineLease.device != code || onlineLease.issued > now + 300000 || onlineLease.expires <= now || onlineLease.expires - onlineLease.issued != 86400000)
		{
			throw new InvalidDataException("Autorização expirada, inválida ou de outro PC. Conecte-se à internet.");
		}
		return onlineLease;
	}

	internal static LeaseCache Load()
	{
		if (!File.Exists(CachePath))
		{
			return null;
		}
		return new JavaScriptSerializer().Deserialize<LeaseCache>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CachePath), null, DataProtectionScope.CurrentUser)));
	}

	internal static void Save(LeaseCache cache)
	{
		Directory.CreateDirectory(LicenseCore.Home);
		File.WriteAllBytes(CachePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(cache)), null, DataProtectionScope.CurrentUser));
	}

	internal static bool FallbackEligible(Exception e)
	{
		if (!(e is WebException ex))
		{
			return false;
		}
		if (ex.Response is HttpWebResponse httpWebResponse)
		{
			return httpWebResponse.StatusCode >= HttpStatusCode.InternalServerError;
		}
		return ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ConnectFailure || ex.Status == WebExceptionStatus.NameResolutionFailure || ex.Status == WebExceptionStatus.ProxyNameResolutionFailure || ex.Status == WebExceptionStatus.ReceiveFailure;
	}

	internal static AccessResult Evaluate(OnlineIdentity identity, string service, Func<string> acquire)
	{
		return Evaluate(identity, service, acquire, "<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
	}

	internal static AccessResult Evaluate(OnlineIdentity identity, string service, Func<string> acquire, string publicKey)
	{
		long num = Millis(DateTime.UtcNow);
		LeaseCache leaseCache = null;
		try
		{
			leaseCache = Load();
		}
		catch
		{
		}
		try
		{
			string token = acquire();
			Verify(token, identity.Code, publicKey, num);
			LeaseCache leaseCache2 = new LeaseCache();
			leaseCache2.Token = token;
			leaseCache2.LastUtc = num;
			leaseCache2.Service = service;
			Save(leaseCache2);
			AccessResult accessResult = new AccessResult();
			accessResult.Allowed = true;
			accessResult.Token = token;
			accessResult.Reason = "Autorizado pelo gerenciador";
			return accessResult;
		}
		catch (OnlineDenied onlineDenied)
		{
			LeaseCache leaseCache3 = new LeaseCache();
			leaseCache3.Token = null;
			leaseCache3.LastUtc = num;
			leaseCache3.Service = service;
			Save(leaseCache3);
			AccessResult accessResult2 = new AccessResult();
			accessResult2.Reason = onlineDenied.Message;
			return accessResult2;
		}
		catch (Exception e)
		{
			if (FallbackEligible(e) && leaseCache != null && leaseCache.Token != null && leaseCache.Service == service && num + 300000 >= leaseCache.LastUtc)
			{
				try
				{
					Verify(leaseCache.Token, identity.Code, publicKey, num);
					leaseCache.LastUtc = Math.Max(num, leaseCache.LastUtc);
					Save(leaseCache);
					AccessResult accessResult3 = new AccessResult();
					accessResult3.Allowed = true;
					accessResult3.Token = leaseCache.Token;
					accessResult3.Reason = "Sem conexão: usando tolerância offline de até 24 horas";
					return accessResult3;
				}
				catch
				{
				}
			}
			AccessResult accessResult4 = new AccessResult();
			accessResult4.Reason = "Não foi possível confirmar autorização. Conecte-se à internet ou contate o administrador.";
			return accessResult4;
		}
	}
}
