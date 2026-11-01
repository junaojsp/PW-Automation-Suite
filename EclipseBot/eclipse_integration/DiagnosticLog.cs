using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

internal static class DiagnosticLog
{
	private const int MaxPending = 3000;

	private const int MaxLine = 480;

	private static readonly object gate = new object();

	private static readonly LinkedList<string> pending = new LinkedList<string>();

	internal static bool ToFile = true;

	public static int Pending
	{
		get
		{
			lock (gate)
			{
				return pending.Count;
			}
		}
	}

	public static void Write(string message)
	{
		try
		{
			lock (gate)
			{
				string text = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
				pending.AddLast((text.Length > 480) ? (text.Substring(0, 480) + "…") : text);
				while (pending.Count > 3000)
				{
					pending.RemoveFirst();
				}
				if (ToFile)
				{
					string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno", "logs");
					Directory.CreateDirectory(text2);
					string text3 = Path.Combine(text2, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
					if (!File.Exists(text3) || new FileInfo(text3).Length < 2097152)
					{
						File.AppendAllText(text3, text + Environment.NewLine, Encoding.UTF8);
					}
				}
			}
		}
		catch
		{
		}
	}

	public static List<string> Take(int maxLines, int maxBytes)
	{
		List<string> list = new List<string>();
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		int num = 0;
		lock (gate)
		{
			while (pending.Count > 0 && list.Count < maxLines)
			{
				int num2 = Encoding.UTF8.GetByteCount(javaScriptSerializer.Serialize(pending.First.Value)) + 1;
				if (list.Count > 0 && num + num2 > maxBytes)
				{
					break;
				}
				num += num2;
				list.Add(pending.First.Value);
				pending.RemoveFirst();
			}
		}
		return list;
	}

	public static void PutBack(List<string> batch)
	{
		lock (gate)
		{
			for (int num = batch.Count - 1; num >= 0; num--)
			{
				pending.AddFirst(batch[num]);
			}
			while (pending.Count > 3000)
			{
				pending.RemoveFirst();
			}
		}
	}
}
