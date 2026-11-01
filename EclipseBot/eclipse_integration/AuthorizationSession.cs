using System;
using System.Diagnostics;

internal static class AuthorizationSession
{
	internal static string Token;

	private static Stopwatch clock;

	private static long remaining;

	private static long highUtc;

	internal static void Begin(string token)
	{
		if (!(Token == token) || clock == null)
		{
			long num = OnlineLicense.Millis(DateTime.UtcNow);
			OnlineLease onlineLease = OnlineLicense.Verify(token, LicenseGate.Identity.Code, "<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>", num);
			Token = token;
			remaining = Math.Min(86400000L, onlineLease.expires - num);
			clock = Stopwatch.StartNew();
			highUtc = num;
		}
	}

	internal static bool Valid()
	{
		long num = OnlineLicense.Millis(DateTime.UtcNow);
		if (clock == null || clock.ElapsedMilliseconds >= remaining || num + 300000 < highUtc)
		{
			return false;
		}
		highUtc = Math.Max(num, highUtc);
		try
		{
			OnlineLicense.Verify(Token, LicenseGate.Identity.Code, "<RSAKeyValue><Modulus>ndzL/Q6TJZE2B9z7wzoAU/KS/Nl3Cs2mfMKcKSqvSkwL4WWihjHiz2lJaQ1VB/jfM4A0+vBatepKYpfuLmchVSF2svHZFZJ17iK35B5WQQYfZDdWqgJMrEcnkEdCWWT/SCRrnFIQf7ErPGdAiQqOBLwuQmIJ5G+h7WQfl+B1bclSySa7fz57NmSe6xV1Or3PEDgoFH6LzTh6UU5CeuPX+n+rLrHF1jU6pVYPRdS0zdjd6DQdOvvpIPKS9RUSeId2D44FAMlS0VsRI7OpkWYVnAnwfd5pafyyXEHVznqr/OLu7BbUuNv/Fa2qLcXIR0BCugLV9vjLIgvBJR/FOj/k5Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>", num);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
