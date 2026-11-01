using System.Collections.Generic;

internal static class RemoteLog
{
	public static void Upload(string service, string token)
	{
		if (string.IsNullOrEmpty(service) || string.IsNullOrEmpty(token))
		{
			return;
		}
		for (int i = 0; i < 5; i++)
		{
			List<string> list = DiagnosticLog.Take(200, 12000);
			if (list.Count == 0)
			{
				break;
			}
			try
			{
				OnlineApi.Post<Dictionary<string, object>>(service, "/v1/device/log", new
				{
					version = "0.19.19",
					lines = list
				}, token);
			}
			catch
			{
				DiagnosticLog.PutBack(list);
				break;
			}
		}
	}
}
