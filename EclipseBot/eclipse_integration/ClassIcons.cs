using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class ClassIcons
{
	public const int Warrior = 0;

	public const int Mage = 1;

	public const int Venomancer = 3;

	public const int Barbarian = 4;

	public const int Archer = 6;

	public const int Cleric = 7;

	private const uint K1 = 2828235874u;

	private const uint K2 = 4054070867u;

	private static readonly Dictionary<int, int> fileOf = new Dictionary<int, int>
	{
		{ 0, 1 },
		{ 1, 2 },
		{ 3, 3 },
		{ 4, 4 },
		{ 6, 5 },
		{ 7, 6 }
	};

	private static readonly Dictionary<int, Image> icons = new Dictionary<int, Image>();

	private static readonly object gate = new object();

	private static bool tried;

	public static bool Known(int c)
	{
		lock (gate)
		{
			return icons.ContainsKey(c);
		}
	}

	public static string Name(int c)
	{
		return c switch
		{
			0 => "Guerreiro", 
			1 => "Mago", 
			3 => "Feiticeira", 
			4 => "Bárbaro", 
			6 => "Arqueiro", 
			7 => "Sacerdote", 
			_ => "", 
		};
	}

	public static void Draw(Graphics g, RectangleF r, int c)
	{
		Image value;
		lock (gate)
		{
			if (!icons.TryGetValue(c, out value))
			{
				return;
			}
		}
		GraphicsState gstate = g.Save();
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.DrawImage(value, r);
		g.Restore(gstate);
	}

	public static string Load(string gameExe)
	{
		bool lockTaken = false;
		object obj = default;
		try
		{
			Monitor.Enter(obj = gate, ref lockTaken);
			if (tried)
			{
				return null;
			}
			tried = true;
		}
		finally
		{
			if (lockTaken)
			{
				Monitor.Exit(obj);
			}
		}
		try
		{
			string text = Path.Combine(Path.GetDirectoryName(gameExe), "surfaces.pck");
			if (!File.Exists(text))
			{
				return "Ícones de classe: surfaces.pck não encontrado; nomes sem ícone.";
			}
			Dictionary<int, Image> dictionary = Read(text);
			bool lockTaken2 = false;
			try
			{
				Monitor.Enter(obj = gate, ref lockTaken2);
				foreach (KeyValuePair<int, Image> item in dictionary)
				{
					icons[item.Key] = item.Value;
				}
			}
			finally
			{
				if (lockTaken2)
				{
					Monitor.Exit(obj);
				}
			}
			return (dictionary.Count == fileOf.Count) ? null : ("Ícones de classe: " + dictionary.Count + "/" + fileOf.Count + " encontrados no jogo.");
		}
		catch (Exception ex)
		{
			return "Ícones de classe indisponíveis: " + ex.Message;
		}
	}

	public static Bitmap Refine(Bitmap src)
	{
		int width = src.Width;
		int height = src.Height;
		Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.DrawImage(src, 0, 0, width, height);
		}
		src.Dispose();
		Rectangle rect = new Rectangle(0, 0, width, height);
		BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
		byte[] array = new byte[bitmapData.Stride * height];
		Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
		byte[] array2 = (byte[])array.Clone();
		float num = (float)(width - 1) / 2f;
		float num2 = (float)(height - 1) / 2f;
		float num3 = (float)Math.Min(width, height) / 2f;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num4 = i * bitmapData.Stride + j * 4;
				if (array2[num4 + 3] == 0)
				{
					continue;
				}
				float num5 = (float)Math.Sqrt(((float)j - num) * ((float)j - num) + ((float)i - num2) * ((float)i - num2)) / num3;
				for (int k = 0; k < 3; k++)
				{
					float num6 = (int)array2[num4 + k];
					if (num5 < 0.76f && j > 0 && i > 0 && j < width - 1 && i < height - 1)
					{
						float num7 = (float)(array2[num4 + k - 4] + array2[num4 + k + 4] + array2[num4 + k - bitmapData.Stride] + array2[num4 + k + bitmapData.Stride]) / 4f;
						num6 += 0.9f * (num6 - num7);
						num6 = (num6 - 128f) * 1.12f + 128f;
					}
					array[num4 + k] = (byte)Math.Max(0f, Math.Min(255f, num6));
				}
				if (num5 >= 0.74f)
				{
					float num8 = (int)array[num4];
					float num9 = (int)array[num4 + 1];
					float num10 = (int)array[num4 + 2];
					float num11 = 0.299f * num10 + 0.587f * num9 + 0.114f * num8;
					float num12 = Math.Min(1f, (num5 - 0.74f) / 0.12f) * 0.8f;
					array[num4] = (byte)(num8 + (num11 * 0.8f - num8) * num12);
					array[num4 + 1] = (byte)(num9 + (num11 * 0.8f - num9) * num12);
					array[num4 + 2] = (byte)(num10 + (num11 * 0.82f - num10) * num12);
				}
			}
		}
		Marshal.Copy(array, 0, bitmapData.Scan0, array.Length);
		bitmap.UnlockBits(bitmapData);
		return bitmap;
	}

	private static byte[] Inflate(byte[] data, int size)
	{
		if (data.Length >= size)
		{
			return data;
		}
		using MemoryStream stream = new MemoryStream(data, 2, data.Length - 2);
		using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
		byte[] array = new byte[size];
		int i;
		int num;
		for (i = 0; i < size; i += num)
		{
			if ((num = deflateStream.Read(array, i, size - i)) <= 0)
			{
				break;
			}
		}
		if (i != size)
		{
			throw new InvalidDataException("Entrada incompleta");
		}
		return array;
	}

	private static Dictionary<int, Image> Read(string pck)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<int, int> item in fileOf)
		{
			dictionary["surfaces\\new\\classes\\" + item.Value + ".png"] = item.Key;
		}
		Dictionary<int, Image> dictionary2 = new Dictionary<int, Image>();
		Encoding encoding = Encoding.GetEncoding(936);
		using (FileStream fileStream = new FileStream(pck, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		{
			using BinaryReader binaryReader = new BinaryReader(fileStream);
			long length = fileStream.Length;
			fileStream.Seek(-8L, SeekOrigin.End);
			int num = binaryReader.ReadInt32();
			uint num2 = binaryReader.ReadUInt32();
			if (num2 != 131075)
			{
				throw new InvalidDataException("formato de pacote 0x" + num2.ToString("X") + " não suportado");
			}
			if (num < 1 || num > 200000)
			{
				throw new InvalidDataException("índice inválido");
			}
			fileStream.Seek(-280L, SeekOrigin.End);
			long num3 = (uint)((int)binaryReader.ReadUInt32() ^ -1466731422);
			if (num3 <= 0 || num3 >= length)
			{
				throw new InvalidDataException("índice fora do arquivo");
			}
			fileStream.Seek(num3, SeekOrigin.Begin);
			List<Tuple<int, long, int, int>> list = new List<Tuple<int, long, int, int>>();
			for (int i = 0; i < num; i++)
			{
				if (list.Count >= dictionary.Count)
				{
					break;
				}
				int num4 = (int)binaryReader.ReadUInt32() ^ -1466731422;
				binaryReader.ReadUInt32();
				if (num4 <= 0 || num4 > 4096)
				{
					throw new InvalidDataException("entrada inválida");
				}
				byte[] array = binaryReader.ReadBytes(num4);
				if (num4 < 288)
				{
					array = Inflate(array, 288);
				}
				string text = encoding.GetString(array, 0, 260);
				char[] separator = new char[1];
				string key = text.Split(separator)[0];
				if (dictionary.TryGetValue(key, out var value))
				{
					list.Add(Tuple.Create(value, (long)BitConverter.ToUInt64(array, 264), BitConverter.ToInt32(array, 272), BitConverter.ToInt32(array, 276)));
				}
			}
			foreach (Tuple<int, long, int, int> item2 in list)
			{
				if (item2.Item3 <= 0 || item2.Item3 > 1048576 || item2.Item4 <= 0 || item2.Item2 + item2.Item4 > length)
				{
					continue;
				}
				fileStream.Seek(item2.Item2, SeekOrigin.Begin);
				byte[] buffer = Inflate(binaryReader.ReadBytes(item2.Item4), item2.Item3);
				using MemoryStream stream = new MemoryStream(buffer);
				using Image original = Image.FromStream(stream);
				dictionary2[item2.Item1] = Refine(new Bitmap(original));
			}
		}
		return dictionary2;
	}
}
