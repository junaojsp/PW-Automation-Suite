// Adapted from the Bagaceira source supplied by the owner; validate in PW Avalon before publication.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using RotaAvalon;

internal sealed class DgBot
{
	public sealed class Account
	{
		public int Pid;

		public uint Id;

		public string Name;

		public int Class = -1;
	}

	private sealed class Step
	{
		public char Kind;

		public double X;

		public double Z;

		public string Data;

		public int Index;
	}

	private sealed class Mob
	{
		public uint Key;

		public uint Obj;

		public uint Template;

		public double X;

		public double Z;

		public string Name;
	}

	private const uint PortoId = 3803u;

	private const double PortoX = 1631.3;

	private const double PortoZ = 816.1;

	private readonly Account leader;

	private readonly List<Account> all;

	private readonly Action<string> note;

	private readonly Action<string> status;

	private readonly Func<bool> remount;

	private readonly List<Step> route = new List<Step>();

	private volatile bool stop;

	private Thread thread;
	private Thread followerThread;

	private volatile bool followOn;

	private readonly Dictionary<int, GameClient> clients = new Dictionary<int, GameClient>();

	private string lib;

	public int Cycles;

	public TimeSpan LastTime;

	public DateTime CycleStart;

	public string Current = "";

	private static readonly object navLock = new object();

	private readonly object learnLock = new object();

	private List<byte[]> learning;

	private Dictionary<string, List<byte[]>> learned;

	private static readonly string[] BuiltInLearned = new string[3] { "entrada=02-00-A6-48-10-80|23-00-A6-48-10-80|25-00-07-00-00-00-0C-00-00-00-33-25-00-00-00-00-00-00-00-00-00-00|31-00-03-00-00-00-01-33-25|56-00-02-00-00-00-81-00-00-00", "npc-19980=02-00-52-2C-B0-83|23-00-52-2C-B0-83|25-00-07-00-00-00-0C-00-00-00-0C-4E-00-00-00-00-00-00-00-00-00-00", "npc-19996=02-00-4B-34-B0-83|23-00-4B-34-B0-83|25-00-07-00-00-00-0C-00-00-00-1C-4E-00-00-00-00-00-00-00-00-00-00" };

	private uint replayTemplate;

	private readonly Dictionary<int, int> lastStep = new Dictionary<int, int>();

	private readonly Dictionary<int, DateTime> stepSince = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> stallWarn = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> attackWarned = new Dictionary<int, DateTime>();

	private volatile bool epHitArea;

	private readonly Dictionary<int, bool> epMode = new Dictionary<int, bool>();

	private readonly HashSet<int> wallHeld = new HashSet<int>();

	private readonly Dictionary<int, DateTime> followAsked = new Dictionary<int, DateTime>();

	private bool followWas;

	private readonly List<double[]> trail = new List<double[]>();

	private readonly Dictionary<int, int> trailIdx = new Dictionary<int, int>();

	private readonly Dictionary<int, DateTime> navAt = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> followAsked2 = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, double> bestLead = new Dictionary<int, double>();

	private readonly Dictionary<int, DateTime> progressAt = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> trailUntil = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, double[]> movedFrom = new Dictionary<int, double[]>();

	private readonly Dictionary<int, DateTime> movedAt = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, int> escTurn = new Dictionary<int, int>();

	private readonly Dictionary<int, DateTime> escUntil = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> escWarn = new Dictionary<int, DateTime>();

	private HashSet<uint> bossTplCache;

	private readonly HashSet<uint> skipMob = new HashSet<uint>();

	private bool inDefense;

	private double emX;

	private double emZ;

	private string emWho;

	private readonly Dictionary<int, DateTime> leftWarn = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> realMoveAt = new Dictionary<int, DateTime>();

	private readonly Dictionary<int, DateTime> trailWarn = new Dictionary<int, DateTime>();

	private double lastVeil = -999.0;

	private readonly Stopwatch clockAll = Stopwatch.StartNew();

	private double lastMeditateEnd = -999.0;

	private readonly HashSet<uint> lootSkip = new HashSet<uint>();

	private DateTime rezNext = DateTime.UtcNow;

	private DateTime prayUntil = DateTime.UtcNow;

	private readonly HashSet<uint> bossDone = new HashSet<uint>();

	private volatile bool inBoss;

	private bool loboPrepped;

	public bool Running => thread != null && thread.IsAlive;

	public static int RouteSteps => DgRouteData.Lines.Length;

	private static string LearnFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvalonInterno", "frost-dg-aprendido.txt");

	private HashSet<uint> bossTpl
	{
		get
		{
			if (bossTplCache == null)
			{
				bossTplCache = new HashSet<uint>();
				foreach (Step item in route)
				{
					if (item.Kind == 'B')
					{
						try
						{
							bossTplCache.Add(Convert.ToUInt32(item.Data.Split(' ')[0], 16));
						}
						catch
						{
						}
					}
				}
			}
			return bossTplCache;
		}
	}

	public DgBot(Account leader, List<Account> accounts, Action<string> note, Action<string> status, Func<bool> remount)
	{
		this.leader = leader;
		all = accounts;
		this.note = note;
		this.status = status;
		this.remount = remount;
		int num = 0;
		string[] lines = DgRouteData.Lines;
		foreach (string text in lines)
		{
			string[] array = text.Split(';');
			if (array.Length >= 4)
			{
				route.Add(new Step
				{
					Kind = array[0][0],
					X = double.Parse(array[1], CultureInfo.InvariantCulture),
					Z = double.Parse(array[2], CultureInfo.InvariantCulture),
					Data = array[3],
					Index = num++
				});
			}
		}
	}

	public bool SingleCycle = true;

	public void Start()
	{
		foreach (Account item in all)
		{
			RotaLock.Set(item.Pid, "Bot Frost DG");
			if (item.Pid != leader.Pid)
			{
				Esc(item);
			}
		}
		thread = new Thread(Run)
		{
			IsBackground = true,
			Name = "Bot Frost DG"
		};
		thread.Start();
	}

	public void Stop()
	{
		stop = true;
	}

	public void StopAndWait(int ms)
	{
		stop = true;
		if (thread != null)
		{
			thread.Join(ms);
		}
	}

	private void Say(string s)
	{
		note("Bot Frost DG: " + s);
	}

	private static double Dist(double ax, double az, double bx, double bz)
	{
		return Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
	}

	private GameClient C(Account a)
	{
		GameClient value;
		lock (clients)
		{
			if (!clients.TryGetValue(a.Pid, out value))
			{
				value = new GameClient(a.Pid, new Dictionary<string, bool>());
				clients[a.Pid] = value;
			}
		}
		return value;
	}

	private void ResetClient(Account a)
	{
		lock (clients)
		{
			if (clients.TryGetValue(a.Pid, out var value))
			{
				try
				{
					value.Dispose();
				}
				catch
				{
				}
				clients.Remove(a.Pid);
			}
		}
	}

	private ClientSnapshot Snap(Account a)
	{
		try
		{
			return C(a).Read();
		}
		catch
		{
			lock (clients)
			{
				if (clients.TryGetValue(a.Pid, out var value))
				{
					try
					{
						value.Dispose();
					}
					catch
					{
					}
					clients.Remove(a.Pid);
				}
			}
			return null;
		}
	}

	private void Nav(Account a, double x, double z)
	{
		try
		{
			ClientSnapshot clientSnapshot = Snap(a);
			if (clientSnapshot != null && clientSnapshot.Hp > 0)
			{
				lock (navLock)
				{
					NativeBridge.Navigate(C(a), x, clientSnapshot.Y, z, lib);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Say(a.Name + ": andar — " + ex.Message);
		}
	}

	private bool Wait(double seconds)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed.TotalSeconds < seconds)
		{
			if (stop)
			{
				return false;
			}
			Thread.Sleep(100);
		}
		return true;
	}

	private Dictionary<string, List<byte[]>> Learned()
	{
		if (learned != null)
		{
			return learned;
		}
		learned = new Dictionary<string, List<byte[]>>();
		string[] builtInLearned = BuiltInLearned;
		foreach (string text in builtInLearned)
		{
			try
			{
				int num = text.IndexOf('=');
				learned[text.Substring(0, num)] = (from h in text.Substring(num + 1).Split(new char[1] { '|' }, StringSplitOptions.RemoveEmptyEntries)
					select (from x in h.Split('-')
						select Convert.ToByte(x, 16)).ToArray()).ToList();
			}
			catch
			{
			}
		}
		try
		{
			if (File.Exists(LearnFile))
			{
				builtInLearned = File.ReadAllLines(LearnFile);
				foreach (string text in builtInLearned)
				{
					int num = text.IndexOf('=');
					if (num < 0)
					{
						continue;
					}
					learned[text.Substring(0, num)] = (from h in text.Substring(num + 1).Split(new char[1] { '|' }, StringSplitOptions.RemoveEmptyEntries)
						select (from x in h.Split('-')
							select Convert.ToByte(x, 16)).ToArray()).ToList();
				}
			}
		}
		catch
		{
		}
		return learned;
	}

	private void SaveLearned()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(LearnFile));
			File.WriteAllLines(LearnFile, learned.Select((KeyValuePair<string, List<byte[]>> kv) => kv.Key + "=" + string.Join("|", kv.Value.Select((byte[] b) => BitConverter.ToString(b)).ToArray())).ToArray());
		}
		catch
		{
		}
	}

	public void Observe(byte[] p)
	{
		if (p == null || p.Length < 2 || ((p[0] != 35 || p.Length != 6) && (p[0] != 37 || p.Length < 10 || p.Length > 32) && (p[0] != 49 || p.Length != 9) && (p[0] != 86 || p.Length != 10) && (p[0] != 2 || p.Length != 6 || (p[5] & 0x80) == 0)))
		{
			return;
		}
		lock (learnLock)
		{
			if (learning != null && learning.Count < 12)
			{
				learning.Add(p);
			}
		}
	}

	private bool Replay(string key, Func<bool> done, double waitAfter)
	{
		return ReplayOn(leader, key, done, waitAfter);
	}

	private bool ReplayOn(Account who, string key, Func<bool> done, double waitAfter)
	{
		if (!Learned().TryGetValue(key, out var value) || value.Count == 0)
		{
			return false;
		}
		if (replayTemplate != 0)
		{
			uint now = NpcKeyByTemplate(who, replayTemplate);
			if (now != 0)
			{
				value = value.Select((byte[] p) =>
				{
					byte[] array = (byte[])p.Clone();
					if ((array[0] == 2 || array[0] == 35) && array.Length == 6 && (BitConverter.ToUInt32(array, 2) & 0xFF000000u) == 2197815296u)
					{
						BitConverter.GetBytes(now).CopyTo(array, 2);
					}
					return array;
				}).ToList();
			}
		}
		Say(who.Name + ": repetindo o que foi aprendido em \"" + key + "\" (" + value.Count + " comandos).");
		for (int num = 1; num <= 2; num++)
		{
			if (stop)
			{
				break;
			}
			foreach (byte[] item in value)
			{
				if (stop)
				{
					return false;
				}
				TeleportBridge teleportBridge = null;
				try
				{
					teleportBridge = new TeleportBridge(who.Pid);
					teleportBridge.SendDgPacket(item);
				}
				catch (Exception ex)
				{
					Say("comando aprendido recusado: " + ex.Message);
				}
				finally
				{
					teleportBridge?.Dispose();
				}
				Wait((item[0] == 35 || item[0] == 2) ? 1.5 : 1.0);
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed.TotalSeconds < waitAfter && !stop)
			{
				if (done())
				{
					return true;
				}
				Thread.Sleep(300);
			}
		}
		return done();
	}

	private bool ManualAndLearn(string key, string ask, Func<bool> done, double maxMinutes)
	{
		if (done())
		{
			return true;
		}
		if (Replay(key, done, 12.0))
		{
			return true;
		}
		Say("PASSO MANUAL — " + ask + " (o bot aprende e faz sozinho nas próximas DGs; o líder tem de ser o principal do Espelho).");
		status("Aguardando você: " + ask);
		bool lockTaken = false;
		object obj = default;
		try
		{
			Monitor.Enter(obj = learnLock, ref lockTaken);
			learning = new List<byte[]>();
		}
		finally
		{
			if (lockTaken)
			{
				Monitor.Exit(obj);
			}
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		bool flag = false;
		while (!stop && stopwatch.Elapsed.TotalMinutes < maxMinutes)
		{
			if (done())
			{
				flag = true;
				break;
			}
			Thread.Sleep(500);
		}
		bool lockTaken2 = false;
		List<byte[]> list;
		try
		{
			Monitor.Enter(obj = learnLock, ref lockTaken2);
			list = learning;
			learning = null;
		}
		finally
		{
			if (lockTaken2)
			{
				Monitor.Exit(obj);
			}
		}
		if (flag && list != null && list.Count > 0)
		{
			Learned()[key] = list;
			SaveLearned();
			Say("aprendido \"" + key + "\": " + string.Join("  ", list.Select((byte[] b) => BitConverter.ToString(b)).ToArray()));
		}
		else if (flag)
		{
			Say("passo feito, mas nenhum comando do líder foi capturado (confira se o líder é o principal do Espelho).");
		}
		return flag;
	}

	private List<Mob> Mobs(Account a)
	{
		List<Mob> list = new List<Mob>();
		try
		{
			GameClient gameClient = C(a);
			using Process process = Process.GetProcessById(a.Pid);
			using ProcessMemory processMemory = new ProcessMemory(a.Pid);
			uint num = (uint)process.MainModule.BaseAddress.ToInt32();
			uint imageEnd = num + (uint)process.MainModule.ModuleMemorySize;
			uint num2 = processMemory.UInt32(num + gameClient.RootRva);
			uint num3 = processMemory.UInt32(num2 + 32);
			uint num4 = processMemory.UInt32(num3 + 36);
			uint num5 = processMemory.UInt32(num4 + 28);
			uint address = processMemory.UInt32(num4 + 32);
			if (num5 < 1 || num5 > 65536)
			{
				return list;
			}
			byte[] array = processMemory.Read(address, (int)(num5 * 4));
			HashSet<uint> hashSet = new HashSet<uint>();
			for (int i = 0; i + 4 <= array.Length; i += 4)
			{
				uint num6 = BitConverter.ToUInt32(array, i);
				int num7 = 0;
				while (num6 != 0 && num7++ < 1000 && hashSet.Add(num6))
				{
					byte[] array2;
					try
					{
						array2 = processMemory.Read(num6, 12);
					}
					catch
					{
						break;
					}
					if (array2 == null || array2.Length < 12)
					{
						break;
					}
					num6 = BitConverter.ToUInt32(array2, 0);
					uint num8 = BitConverter.ToUInt32(array2, 4);
					uint num9 = BitConverter.ToUInt32(array2, 8);
					if ((num9 & 0xC0000000u) != 2147483648u)
					{
						continue;
					}
					string text;
					try
					{
						text = NearbyResources.NativeType(processMemory, num8, num, imageEnd);
					}
					catch
					{
						continue;
					}
					if (text != ".?AVCECMonster@@")
					{
						continue;
					}
					Mob mob = new Mob();
					mob.Key = num9;
					mob.Obj = num8;
					Mob mob2 = mob;
					try
					{
						mob2.Template = processMemory.UInt32(num8 + 284);
						mob2.X = processMemory.Float(num8 + 60);
						mob2.Z = processMemory.Float(num8 + 68);
						uint num10 = processMemory.UInt32(num8 + 644);
						if (num10 > 65536)
						{
							byte[] array3 = processMemory.Read(num10, 64);
							if (array3 != null && array3.Length == 64)
							{
								string text2 = Encoding.Unicode.GetString(array3);
								int num11 = text2.IndexOf('\0');
								mob2.Name = ((num11 > 0) ? text2.Substring(0, num11) : text2);
							}
						}
					}
					catch
					{
					}
					if (mob2.Template != 24833)
					{
						list.Add(mob2);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private void Esc(Account a)
	{
		TeleportBridge teleportBridge = null;
		try
		{
			teleportBridge = new TeleportBridge(a.Pid);
			teleportBridge.SendControl(new byte[2] { 42, 0 });
		}
		catch
		{
		}
		finally
		{
			teleportBridge?.Dispose();
		}
	}

	private void StopMacro(Account a)
	{
		AttackBridge attackBridge = null;
		try
		{
			attackBridge = new AttackBridge(a.Pid);
			attackBridge.StopMacro(-1);
		}
		catch
		{
		}
		finally
		{
			attackBridge?.Dispose();
		}
	}

	private bool MacroOn(Account a)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(a.Pid);
			uint num = characterMemory.RunningMacro();
			if (num == 0)
			{
				return false;
			}
			using ProcessMemory processMemory = new ProcessMemory(a.Pid);
			return processMemory.Read(num + 36, 1)[0] == 0;
		}
		catch
		{
			return false;
		}
	}

	private int MacroStep(Account a)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(a.Pid);
			uint num = characterMemory.RunningMacro();
			if (num == 0)
			{
				return -1;
			}
			return (int)characterMemory.U(num + 32);
		}
		catch
		{
			return -1;
		}
	}

	private void Unstick(Account a, uint target)
	{
		ClientSnapshot clientSnapshot = Snap(a);
		Mob mob = Mobs(leader).FirstOrDefault((Mob q) => q.Key == target);
		double num = ((clientSnapshot != null && mob != null) ? Dist(clientSnapshot.X, clientSnapshot.Z, mob.X, mob.Z) : 0.0);
		bool flag = !stallWarn.TryGetValue(a.Pid, out var value) || (DateTime.UtcNow - value).TotalSeconds > 30.0;
		if (flag)
		{
			stallWarn[a.Pid] = DateTime.UtcNow;
		}
		StopMacro(a);
		if (mob != null && ((num > 20.0 && !IsFuria(mob)) || (a.Pid == leader.Pid && !IsFuria(mob) && num > 12.0)))
		{
			if (flag)
			{
				Say(a.Name + ": macro parada com o alvo a " + num.ToString("F0") + " — não vou atrás (não puxar mais mobs/boss); reiniciando.");
			}
		}
		else if (num > 8.0)
		{
			if (flag)
			{
				Say(a.Name + ": macro parada com o alvo a " + num.ToString("F0") + " (parede?) — andando até ele.");
			}
			Nav(a, mob.X, mob.Z);
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (!stop && stopwatch.Elapsed.TotalSeconds < 6.0)
			{
				Thread.Sleep(300);
				ClientSnapshot clientSnapshot2 = Snap(a);
				Mob mob2 = Mobs(leader).FirstOrDefault((Mob x) => x.Key == target);
				if (clientSnapshot2 == null || mob2 == null || Dist(clientSnapshot2.X, clientSnapshot2.Z, mob2.X, mob2.Z) <= 6.0)
				{
					break;
				}
			}
		}
		else if (flag)
		{
			Say(a.Name + ": macro parada (stun/sono?) — reiniciando.");
		}
		Attack(a, target);
	}

	private bool MacroStalled(Account a)
	{
		int num = MacroStep(a);
		DateTime utcNow = DateTime.UtcNow;
		if (num < 0 || !lastStep.TryGetValue(a.Pid, out var value) || value != num)
		{
			lastStep[a.Pid] = num;
			stepSince[a.Pid] = utcNow;
			return false;
		}
		if (stepSince.TryGetValue(a.Pid, out var value2) && (utcNow - value2).TotalSeconds > 4.0)
		{
			stepSince[a.Pid] = utcNow;
			return true;
		}
		return false;
	}

	private uint TargetOf(Account a)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(a.Pid);
			return characterMemory.SelectedTarget;
		}
		catch
		{
			return 0u;
		}
	}

	private void Gather()
	{
		StopFollowNow();
		ClientSnapshot l = Snap(leader);
		if (l == null)
		{
			return;
		}
		Say("Fúria da parede: juntando todas perto do líder antes de bater.");
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!stop && stopwatch.Elapsed.TotalSeconds < 12.0)
		{
			List<string> list = new List<string>();
			foreach (Account item in all)
			{
				if (item.Pid != leader.Pid)
				{
					ClientSnapshot clientSnapshot = Snap(item);
					if (clientSnapshot != null && clientSnapshot.Hp > 0 && !LeftBehind(item) && Dist(clientSnapshot.X, clientSnapshot.Z, l.X, l.Z) > 3.0)
					{
						list.Add(item.Name);
						Nav(item, l.X, l.Z);
					}
				}
			}
			if (list.Count == 0)
			{
				Say("todas juntas (" + stopwatch.Elapsed.TotalSeconds.ToString("F0") + " s).");
				return;
			}
			Thread.Sleep(700);
		}
		string[] array = (from a in all.Where((Account a) =>
			{
				ClientSnapshot clientSnapshot2 = Snap(a);
				return a.Pid != leader.Pid && clientSnapshot2 != null && clientSnapshot2.Hp > 0 && Dist(clientSnapshot2.X, clientSnapshot2.Z, l.X, l.Z) > 3.0;
			})
			select a.Name).ToArray();
		if (array.Length > 0)
		{
			Say("não juntaram em 12 s: " + string.Join(", ", array) + " — batendo assim.");
		}
	}

	private static bool EpArea(double x, double z)
	{
		return Math.Abs(x - 136.7) < 3.0 && Math.Abs(z + 302.0) < 3.0;
	}

	private bool EpHits(uint monster)
	{
		return false;
	}

	private bool EpHitsOld(uint monster)
	{
		if (!epHitArea)
		{
			return false;
		}
		Mob mob = Mobs(leader).FirstOrDefault((Mob q) => q.Key == monster);
		if (mob == null || !IsFuria(mob))
		{
			return false;
		}
		foreach (Account item in all)
		{
			ClientSnapshot clientSnapshot = Snap(item);
			if (clientSnapshot != null && clientSnapshot.Hp > 0 && clientSnapshot.MaxHp > 0 && clientSnapshot.Hp * 2 < clientSnapshot.MaxHp)
			{
				return false;
			}
		}
		return true;
	}

	private bool EpModeChanged(Account a, uint monster)
	{
		if (a.Pid == leader.Pid)
		{
			return false;
		}
		epMode.TryGetValue(a.Pid, out var value);
		return value != EpHits(monster);
	}

	private static bool Ranged(Account a)
	{
		return a.Class == 6 || a.Class == 3 || a.Class == 1;
	}

	private bool WallHold(Account a, uint cur)
	{
		if (!epHitArea || a.Class == 7 || Ranged(a))
		{
			return false;
		}
		Mob mob = Mobs(leader).FirstOrDefault((Mob q) => q.Key == cur);
		if (mob == null || !IsFuria(mob))
		{
			return false;
		}
		if (wallHeld.Add(a.Pid))
		{
			StopMacro(a);
			Say(a.Name + ": Fúria da parede — fica junto do líder (só WF/arqueiro batem nele).");
		}
		ClientSnapshot l = Snap(leader);
		Mob mob2 = ((l == null) ? null : (from q in Mobs(leader)
			where !IsFuria(q) && !bossTpl.Contains(q.Template) && Dist(q.X, q.Z, l.X, l.Z) <= 12.0
			orderby Dist(q.X, q.Z, l.X, l.Z)
			select q).FirstOrDefault());
		if (mob2 != null && (!MacroOn(a) || TargetOf(a) != mob2.Key))
		{
			Attack(a, mob2.Key);
		}
		return true;
	}

	private void Attack(Account a, uint monster)
	{
		bool flag = false;
		string text = null;
		bool flag2 = a.Class == 7 && a.Pid != leader.Pid;
		bool value;
		if (flag2 && EpHits(monster))
		{
			flag2 = false;
			if (!epMode.TryGetValue(a.Pid, out value) || !value)
			{
				epMode[a.Pid] = true;
				Say(a.Name + ": saiu do suporte — batendo no Fúria da parede.");
			}
		}
		else if (a.Class == 7 && epMode.TryGetValue(a.Pid, out value) && value)
		{
			epMode[a.Pid] = false;
			Say(a.Name + ": voltou ao suporte.");
		}
		for (int i = 1; i <= 2 && !flag && !stop; i++)
		{
			AttackBridge attackBridge = null;
			try
			{
				attackBridge = new AttackBridge(a.Pid);
				if (flag2)
				{
					attackBridge.StartMacroOn(leader.Id, 2);
					flag = true;
				}
				else
				{
					flag = attackBridge.StartMacroOnMonster(monster, 0);
				}
				if (!flag)
				{
					text = "macro não começou";
				}
			}
			catch (Exception ex)
			{
				text = ex.Message;
			}
			finally
			{
				attackBridge?.Dispose();
			}
			if (flag || i == 2)
			{
				break;
			}
			try
			{
				NativeMeditationRuntime nativeMeditationRuntime = new NativeMeditationRuntime();
				CharacterRef c = Ref(a);
				if (nativeMeditationRuntime.Sitting(c))
				{
					nativeMeditationRuntime.Set(c, sitting: false);
					text += " (estava sentada — levantou)";
					Wait(0.6);
					continue;
				}
			}
			catch
			{
			}
			ClientSnapshot clientSnapshot = Snap(a);
			Mob mob = Mobs(leader).FirstOrDefault((Mob q) => q.Key == monster);
			if (clientSnapshot != null && mob != null && Dist(clientSnapshot.X, clientSnapshot.Z, mob.X, mob.Z) > 10.0)
			{
				text = text + " (longe, a " + Dist(clientSnapshot.X, clientSnapshot.Z, mob.X, mob.Z).ToString("F0") + " — andando até o alvo)";
				Nav(a, mob.X, mob.Z);
				Wait(1.2);
			}
		}
		if (!flag && (!attackWarned.TryGetValue(a.Pid, out var value2) || (DateTime.UtcNow - value2).TotalSeconds > 10.0))
		{
			attackWarned[a.Pid] = DateTime.UtcNow;
			Say(a.Name + ": não atacou — " + text + ".");
		}
	}

	private static bool FollowingOf(int pid, uint target)
	{
		try
		{
			using State state = new State(pid);
			return state.Following && state.Target == target;
		}
		catch
		{
			return false;
		}
	}

	private void GameFollow(Account a, bool on)
	{
		InternalBridge internalBridge = null;
		try
		{
			internalBridge = new InternalBridge(a.Pid);
			internalBridge.Follow((!on) ? 1u : leader.Id);
		}
		catch
		{
		}
		finally
		{
			internalBridge?.Dispose();
		}
	}

	private void AddTrail(double x, double z)
	{
		lock (trail)
		{
			if (trail.Count > 0)
			{
				double[] array = trail[trail.Count - 1];
				if (Dist(array[0], array[1], x, z) < 3.0)
				{
					return;
				}
			}
			trail.Add(new double[2] { x, z });
		}
	}

	private void SkipTrail()
	{
		lock (trail)
		{
			foreach (Account item in all)
			{
				trailIdx[item.Pid] = trail.Count;
			}
		}
	}

	private bool FollowersCaughtUp()
	{
		ClientSnapshot clientSnapshot = Snap(leader);
		if (clientSnapshot == null)
		{
			return true;
		}
		foreach (Account item in all)
		{
			if (item.Pid != leader.Pid)
			{
				ClientSnapshot clientSnapshot2 = Snap(item);
				if (clientSnapshot2 != null && clientSnapshot2.Hp > 0 && !LeftBehind(item) && Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z) > 10.0)
				{
					return false;
				}
			}
		}
		return true;
	}

	private void WaitFollowers(double seconds)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		followOn = true;
		while (!stop && stopwatch.Elapsed.TotalSeconds < seconds && !FollowersCaughtUp())
		{
			Thread.Sleep(400);
		}
	}

	private void FollowLoop()
	{
		bool flag = false;
		List<double[]> obj = default;
		while (!stop)
		{
			bool flag2 = followOn;
			if (!flag2 && flag)
			{
				foreach (Account item in all)
				{
					if (item.Pid != leader.Pid && FollowingOf(item.Pid, leader.Id))
					{
						GameFollow(item, on: false);
					}
				}
				trailUntil.Clear();
			}
			flag = flag2;
			if (flag2)
			{
				ClientSnapshot clientSnapshot = Snap(leader);
				if (clientSnapshot != null)
				{
					foreach (Account item2 in all)
					{
						if (stop || !followOn || item2.Pid == leader.Pid)
						{
							continue;
						}
						ClientSnapshot clientSnapshot2 = Snap(item2);
						if (clientSnapshot2 == null || clientSnapshot2.Hp <= 0)
						{
							continue;
						}
						double num = Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z);
						DateTime utcNow = DateTime.UtcNow;
						if (!movedFrom.TryGetValue(item2.Pid, out var value) || Dist(value[0], value[1], clientSnapshot2.X, clientSnapshot2.Z) >= 1.5 || num <= 8.0)
						{
							movedFrom[item2.Pid] = new double[2] { clientSnapshot2.X, clientSnapshot2.Z };
							movedAt[item2.Pid] = utcNow;
							realMoveAt[item2.Pid] = utcNow;
						}
						bool flag3 = trailUntil.TryGetValue(item2.Pid, out var value2) && utcNow < value2;
						int i;
						if (!flag3 && num > 8.0 && movedAt.TryGetValue(item2.Pid, out var value3) && (utcNow - value3).TotalSeconds > 3.0)
						{
							flag3 = true;
							trailUntil[item2.Pid] = utcNow.AddSeconds(6.0);
							movedAt[item2.Pid] = utcNow;
							bool lockTaken = false;
							try
							{
								Monitor.Enter(obj = trail, ref lockTaken);
								i = -1;
								double num2 = 1000000000.0;
								for (int j = Math.Max(0, trail.Count - 40); j < trail.Count; j++)
								{
									double num3 = Dist(trail[j][0], trail[j][1], clientSnapshot2.X, clientSnapshot2.Z);
									if (num3 < num2)
									{
										num2 = num3;
										i = j;
									}
								}
								trailIdx[item2.Pid] = Math.Max(0, i);
							}
							finally
							{
								if (lockTaken)
								{
									Monitor.Exit(obj);
								}
							}
							if (FollowingOf(item2.Pid, leader.Id))
							{
								GameFollow(item2, on: false);
							}
							try
							{
								NativeMeditationRuntime nativeMeditationRuntime = new NativeMeditationRuntime();
								if (nativeMeditationRuntime.Sitting(Ref(item2)))
								{
									nativeMeditationRuntime.Set(Ref(item2), sitting: false);
								}
							}
							catch
							{
							}
							Esc(item2);
							if (!trailWarn.TryGetValue(item2.Pid, out var value4) || (utcNow - value4).TotalSeconds > 60.0)
							{
								trailWarn[item2.Pid] = utcNow;
								Say(item2.Name + ": parada a " + num.ToString("F0") + " do líder; ESC/levantar e andando o caminho dele.");
							}
						}
						DateTime value10;
						if (flag3)
						{
							double[] array = null;
							bool lockTaken2 = false;
							try
							{
								Monitor.Enter(obj = trail, ref lockTaken2);
								trailIdx.TryGetValue(item2.Pid, out i);
								for (; i < trail.Count - 1 && Dist(trail[i][0], trail[i][1], clientSnapshot2.X, clientSnapshot2.Z) <= 4.0; i++)
								{
								}
								trailIdx[item2.Pid] = i;
								if (i < trail.Count)
								{
									array = trail[i];
								}
							}
							finally
							{
								if (lockTaken2)
								{
									Monitor.Exit(obj);
								}
							}
							if (array == null || Dist(array[0], array[1], clientSnapshot2.X, clientSnapshot2.Z) <= 4.0)
							{
								trailUntil.Remove(item2.Pid);
							}
							else
							{
								if (escUntil.TryGetValue(item2.Pid, out var value5) && utcNow < value5)
								{
									continue;
								}
								DateTime value9;
								if (movedAt.TryGetValue(item2.Pid, out var value6) && (utcNow - value6).TotalSeconds > 2.5)
								{
									escTurn.TryGetValue(item2.Pid, out var value7);
									escTurn[item2.Pid] = value7 + 1;
									int[] array2 = new int[7] { 2, -2, 3, -3, 4, 1, -1 };
									double num4 = Math.Atan2(array[1] - (double)clientSnapshot2.Z, array[0] - (double)clientSnapshot2.X) + (double)array2[value7 % array2.Length] * Math.PI / 4.0;
									movedAt[item2.Pid] = utcNow;
									escUntil[item2.Pid] = utcNow.AddSeconds(1.5);
									trailUntil[item2.Pid] = utcNow.AddSeconds(6.0);
									navAt[item2.Pid] = utcNow;
									if (!escWarn.TryGetValue(item2.Pid, out var value8) || (utcNow - value8).TotalSeconds > 30.0)
									{
										escWarn[item2.Pid] = utcNow;
										Say(item2.Name + ": presa (não se mexe) — tentando sair pelo lado.");
									}
									Nav(item2, (double)clientSnapshot2.X + Math.Cos(num4) * 8.0, (double)clientSnapshot2.Z + Math.Sin(num4) * 8.0);
								}
								else if (!navAt.TryGetValue(item2.Pid, out value9) || !((utcNow - value9).TotalSeconds < 0.5))
								{
									navAt[item2.Pid] = utcNow;
									Nav(item2, array[0], array[1]);
								}
							}
						}
						else if (!FollowingOf(item2.Pid, leader.Id) && (!followAsked2.TryGetValue(item2.Pid, out value10) || !((utcNow - value10).TotalSeconds < 1.5)))
						{
							followAsked2[item2.Pid] = utcNow;
							if (num > 140.0)
							{
								Nav(item2, clientSnapshot.X, clientSnapshot.Z);
							}
							else
							{
								GameFollow(item2, on: true);
							}
						}
					}
				}
			}
			Thread.Sleep(250);
		}
	}

	private void FollowLoopOld()
	{
		while (!stop)
		{
			bool flag = followOn;
			if (!flag && followWas)
			{
				foreach (Account item in all)
				{
					if (item.Pid != leader.Pid && FollowingOf(item.Pid, leader.Id))
					{
						GameFollow(item, on: false);
					}
				}
			}
			followWas = flag;
			if (flag)
			{
				ClientSnapshot clientSnapshot = Snap(leader);
				if (clientSnapshot != null)
				{
					foreach (Account item2 in all)
					{
						if (stop || !followOn || item2.Pid == leader.Pid)
						{
							continue;
						}
						ClientSnapshot clientSnapshot2 = Snap(item2);
						if (clientSnapshot2 == null || clientSnapshot2.Hp <= 0 || FollowingOf(item2.Pid, leader.Id) || (followAsked.TryGetValue(item2.Pid, out var value) && (DateTime.UtcNow - value).TotalSeconds < 3.0))
						{
							continue;
						}
						followAsked[item2.Pid] = DateTime.UtcNow;
						if (Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z) > 140.0)
						{
							lock (navLock)
							{
								try
								{
									NativeBridge.Navigate(C(item2), clientSnapshot.X, clientSnapshot2.Y, clientSnapshot.Z, lib);
								}
								catch
								{
								}
							}
						}
						else
						{
							GameFollow(item2, on: true);
						}
					}
				}
			}
			Thread.Sleep(500);
		}
	}

	private static bool Careful(double x, double z)
	{
		return (x >= -190.0 && x <= -145.0 && z >= -35.0 && z <= 200.0) || (x >= 100.0 && x <= 170.0 && z >= 272.0 && z <= 366.0) || (x >= 0.0 && x <= 152.0 && z >= -218.0 && z <= -204.0) || (x >= -105.0 && x <= -50.0 && z >= 262.0 && z <= 372.0);
	}

	private void OneByOne(double cx, double cz, double radius, string where)
	{
		int num = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		bool flag = false;
		bool flag2 = false;
		while (!stop && stopwatch.Elapsed.TotalSeconds < 300.0)
		{
			ClientSnapshot l = Snap(leader);
			if (l == null || l.Hp <= 0)
			{
				ClientSnapshot clientSnapshot = all.Select((Account a2) => Snap(a2)).FirstOrDefault((ClientSnapshot q) => q != null && q.Hp > 0);
				if (clientSnapshot == null)
				{
					break;
				}
				l = clientSnapshot;
			}
			if (BossNearby())
			{
				continue;
			}
			List<Mob> list = (from m in Mobs(leader)
				where !bossTpl.Contains(m.Template) && !skipMob.Contains(m.Key)
				where Dist(m.X, m.Z, l.X, l.Z) <= 25.0 || Dist(m.X, m.Z, cx, cz) <= radius
				select m).ToList();
			if (list.Count == 0)
			{
				break;
			}
			if (!flag)
			{
				flag = true;
				Say("corredor mob por mob (" + where + "): " + list.Count + " à frente.");
			}
			HoldForGroup();
			StopFollowNow();
			Mob mob = (from m in list
				orderby (m.Name == null || m.Name.IndexOf("sumo", StringComparison.OrdinalIgnoreCase) < 0) ? 1 : 0, Dist(m.X, m.Z, l.X, l.Z)
				select m).First();
			uint cur = mob.Key;
			bool flag3 = false;
			bool flag4 = where != "caminho" && where != "área";
			if (!flag2 && (flag3 || flag4))
			{
				flag2 = true;
				Account a = Wb();
				ClientSnapshot clientSnapshot2 = Snap(a);
				if (clientSnapshot2 != null && clientSnapshot2.Hp > 0)
				{
					Say("Véu do WB (" + (flag3 ? "boss" : where) + ").");
					WbOpen(cur);
				}
			}
			foreach (Account item in all)
			{
				ClientSnapshot clientSnapshot3 = Snap(item);
				if (clientSnapshot3 != null && clientSnapshot3.Hp > 0)
				{
					Attack(item, cur);
				}
			}
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			while (!stop && stopwatch2.Elapsed.TotalSeconds < 45.0 && Mobs(leader).Any((Mob m) => m.Key == cur))
			{
				ClientSnapshot clientSnapshot4 = Snap(leader);
				Mob mob2 = Mobs(leader).FirstOrDefault((Mob m) => m.Key == cur);
				if (clientSnapshot4 != null && clientSnapshot4.Hp > 0 && mob2 != null && Dist(mob2.X, mob2.Z, clientSnapshot4.X, clientSnapshot4.Z) > 30.0)
				{
					skipMob.Add(cur);
					Say("mob a " + Dist(mob2.X, mob2.Z, clientSnapshot4.X, clientSnapshot4.Z).ToString("F0") + " (fugiu/parede): deixando para trás.");
					break;
				}
				RezTick();
				foreach (Account item2 in all)
				{
					ClientSnapshot clientSnapshot3 = Snap(item2);
					if (clientSnapshot3 != null && clientSnapshot3.Hp > 0 && (item2.Class != 7 || !(DateTime.UtcNow < prayUntil)))
					{
						if (!MacroOn(item2) || ((item2.Class != 7 || EpHits(cur)) && TargetOf(item2) != cur) || (item2.Class == 7 && EpModeChanged(item2, cur)))
						{
							Attack(item2, cur);
						}
						else if (MacroStalled(item2))
						{
							Unstick(item2, cur);
						}
					}
				}
				Thread.Sleep(500);
			}
			foreach (Account item3 in all)
			{
				StopMacro(item3);
			}
			num++;
			if (!inDefense && all.Any((Account a2) =>
			{
				ClientSnapshot clientSnapshot5 = Snap(a2);
				return clientSnapshot5 != null && clientSnapshot5.Hp <= 0;
			}))
			{
				AfterDeaths();
			}
		}
		if (num > 0)
		{
			Say("corredor: " + num + " mob(s) um por um (" + stopwatch.Elapsed.TotalSeconds.ToString("F0") + " s).");
		}
	}

	private Step EntryOf(Step st)
	{
		return route.LastOrDefault((Step r) => r.Index < st.Index && r.Kind == 'W');
	}

	private void BackToEntry(Step st)
	{
		Step step = EntryOf(st);
		if (step == null || stop)
		{
			return;
		}
		ClientSnapshot clientSnapshot = Snap(leader);
		if (clientSnapshot != null)
		{
			double num = Dist(clientSnapshot.X, clientSnapshot.Z, step.X, step.Z);
			if (!(num <= 5.0) && !(num > 60.0))
			{
				Say("voltando por onde entrou na área (" + num.ToString("F0") + ").");
				WalkOnce(step.X, step.Z, 4.0);
			}
		}
	}

	private bool WalkTo(double x, double z, double near)
	{
		if (WalkOnce(x, z, near))
		{
			return true;
		}
		ClientSnapshot l = Snap(leader);
		if (l == null)
		{
			return false;
		}
		Mob mob = (from m in Mobs(leader).Where(IsFuria)
			orderby Dist(m.X, m.Z, l.X, l.Z)
			select m).FirstOrDefault();
		if (mob != null && Dist(mob.X, mob.Z, l.X, l.Z) <= 70.0)
		{
			Say("caminho travado e há um Fúria vivo a " + Dist(mob.X, mob.Z, l.X, l.Z).ToString("F0") + " (" + mob.Name + "): matando para abrir o portal.");
			Fight(mob.X, mob.Z, 25.0, 0u, 240);
			return WalkOnce(x, z, near);
		}
		return false;
	}

	private bool Emergency()
	{
		if (inDefense)
		{
			return false;
		}
		ClientSnapshot l = Snap(leader);
		if (l == null || l.Hp <= 0 || l.MaxHp <= 0)
		{
			return false;
		}
		List<Mob> list = (from m in Mobs(leader)
			where !bossTpl.Contains(m.Template)
			select m).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		if ((long)l.Hp * 100L < (long)l.MaxHp * 70L && list.Any((Mob m) => Dist(m.X, m.Z, l.X, l.Z) <= 12.0))
		{
			emX = l.X;
			emZ = l.Z;
			emWho = leader.Name;
			return true;
		}
		foreach (Account item in all)
		{
			if (item.Pid != leader.Pid)
			{
				ClientSnapshot q = Snap(item);
				if (q != null && q.Hp > 0 && q.MaxHp > 0 && (long)q.Hp * 100L < (long)q.MaxHp * 40L && list.Any((Mob m) => Dist(m.X, m.Z, q.X, q.Z) <= 8.0))
				{
					emX = q.X;
					emZ = q.Z;
					emWho = item.Name;
					return true;
				}
			}
		}
		return false;
	}

	private bool Attacked()
	{
		if (inDefense)
		{
			return false;
		}
		ClientSnapshot l = Snap(leader);
		if (l == null || l.MaxHp <= 0 || l.Hp <= 0)
		{
			return false;
		}
		if ((long)l.Hp * 100L >= (long)l.MaxHp * 85L)
		{
			return false;
		}
		return Mobs(leader).Any((Mob m) => Dist(m.X, m.Z, l.X, l.Z) <= 12.0);
	}

	private void Defend()
	{
		inDefense = true;
		try
		{
			Say("emergência no caminho (" + emWho + " com vida baixa e mob perto): mob por mob ali.");
			followOn = false;
			OneByOne(emX, emZ, 15.0, "emergência");
		}
		finally
		{
			inDefense = false;
		}
	}

	private bool LeftBehind(Account a)
	{
		if (!realMoveAt.TryGetValue(a.Pid, out var value))
		{
			return false;
		}
		ClientSnapshot clientSnapshot = Snap(leader);
		ClientSnapshot clientSnapshot2 = Snap(a);
		if (clientSnapshot == null || clientSnapshot2 == null || Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z) <= 22.0)
		{
			return false;
		}
		if ((DateTime.UtcNow - value).TotalSeconds < 30.0)
		{
			return false;
		}
		if (!leftWarn.TryGetValue(a.Pid, out var value2) || (DateTime.UtcNow - value2).TotalSeconds > 60.0)
		{
			leftWarn[a.Pid] = DateTime.UtcNow;
			Say(a.Name + ": parada há " + (int)(DateTime.UtcNow - value).TotalSeconds + " s (sentada/presa?) — o grupo segue sem esperar; levante/solte ela no jogo.");
		}
		return true;
	}

	private double Lag()
	{
		ClientSnapshot clientSnapshot = Snap(leader);
		if (clientSnapshot == null)
		{
			return 0.0;
		}
		double num = 0.0;
		foreach (Account item in all)
		{
			if (item.Pid != leader.Pid)
			{
				ClientSnapshot clientSnapshot2 = Snap(item);
				if (clientSnapshot2 != null && clientSnapshot2.Hp > 0 && !LeftBehind(item))
				{
					num = Math.Max(num, Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z));
				}
			}
		}
		return num;
	}

	private void HoldForGroup()
	{
		if (!(Lag() <= 22.0))
		{
			ClientSnapshot clientSnapshot = Snap(leader);
			if (clientSnapshot != null)
			{
				Nav(leader, clientSnapshot.X, clientSnapshot.Z);
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			followOn = true;
			while (!stop && stopwatch.Elapsed.TotalSeconds < 12.0 && Lag() > 12.0)
			{
				Thread.Sleep(300);
			}
		}
	}

	private bool WalkOnce(double x, double z, double near)
	{
		followOn = true;
		AddTrail(x, z);
		HoldForGroup();
		Stopwatch stopwatch = Stopwatch.StartNew();
		double num = -9.0;
		double num2 = 1000000000.0;
		Stopwatch stopwatch2 = Stopwatch.StartNew();
		while (!stop && stopwatch.Elapsed.TotalSeconds < 25.0)
		{
			ClientSnapshot clientSnapshot = Snap(leader);
			if (clientSnapshot == null)
			{
				Thread.Sleep(300);
				continue;
			}
			if (clientSnapshot.Hp <= 0)
			{
				return false;
			}
			double num3 = Dist(clientSnapshot.X, clientSnapshot.Z, x, z);
			if (num3 <= near)
			{
				return true;
			}
			if (BossNearby())
			{
				followOn = true;
				stopwatch.Restart();
				continue;
			}
			if (Emergency())
			{
				Defend();
				followOn = true;
				stopwatch.Restart();
				continue;
			}
			if (Lag() > 28.0)
			{
				HoldForGroup();
				num = -9.0;
				stopwatch2.Restart();
			}
			if (num3 < num2 - 0.5)
			{
				num2 = num3;
				stopwatch2.Restart();
			}
			if (stopwatch.Elapsed.TotalSeconds - num >= 1.5 || stopwatch2.Elapsed.TotalSeconds > 2.0)
			{
				Nav(leader, x, z);
				num = stopwatch.Elapsed.TotalSeconds;
				if (stopwatch2.Elapsed.TotalSeconds > 2.0)
				{
					stopwatch2.Restart();
				}
			}
			Thread.Sleep(150);
		}
		return false;
	}

	private Account Wb()
	{
		return all.FirstOrDefault((Account a) => a.Class == 4) ?? leader;
	}

	private void WbApproach(Account wb, uint target)
	{
		List<Mob> source = Mobs(wb);
		Func<Mob, bool> predicate = (Mob q) => q.Key == target;
		Mob mob = source.FirstOrDefault(predicate);
		ClientSnapshot clientSnapshot = Snap(wb);
		if (mob != null && clientSnapshot != null && Dist(mob.X, mob.Z, clientSnapshot.X, clientSnapshot.Z) > 15.0)
		{
			Say(wb.Name + ": Fúria a " + Dist(mob.X, mob.Z, clientSnapshot.X, clientSnapshot.Z).ToString("F0") + " (parede?) — Véu aqui, sem correr atrás.");
			return;
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!stop && stopwatch.Elapsed.TotalSeconds < 8.0)
		{
			Mob mob2 = Mobs(wb).FirstOrDefault((Mob q) => q.Key == target);
			ClientSnapshot clientSnapshot2 = Snap(wb);
			if (mob2 == null || clientSnapshot2 == null)
			{
				break;
			}
			double num = Dist(mob2.X, mob2.Z, clientSnapshot2.X, clientSnapshot2.Z);
			if (num <= 3.0)
			{
				break;
			}
			Nav(wb, mob2.X - ((double)clientSnapshot2.X - mob2.X) / num * 2.0, mob2.Z - ((double)clientSnapshot2.Z - mob2.Z) / num * 2.0);
			Thread.Sleep(400);
		}
	}

	private string WbDist(Account wb, uint target)
	{
		Mob mob = Mobs(wb).FirstOrDefault((Mob q) => q.Key == target);
		ClientSnapshot clientSnapshot = Snap(wb);
		return (mob == null || clientSnapshot == null) ? "?" : Dist(mob.X, mob.Z, clientSnapshot.X, clientSnapshot.Z).ToString("F1");
	}

	private void WbOpen(uint target)
	{
		Account account = Wb();
		ClientSnapshot clientSnapshot = Snap(account);
		if (clientSnapshot == null || clientSnapshot.Hp <= 0)
		{
			return;
		}
		WbApproach(account, target);
		TeleportBridge teleportBridge = null;
		try
		{
			teleportBridge = new TeleportBridge(account.Pid);
			teleportBridge.SendSelfSkill(41, 186u);
			lastVeil = clockAll.Elapsed.TotalSeconds;
		}
		catch
		{
		}
		finally
		{
			teleportBridge?.Dispose();
		}
		Wait(0.6);
		TeleportBridge teleportBridge2 = null;
		try
		{
			teleportBridge2 = new TeleportBridge(account.Pid);
			teleportBridge2.SendSelfSkill(80, 188u);
		}
		catch
		{
		}
		finally
		{
			teleportBridge2?.Dispose();
		}
		Say(account.Name + ": Véu + Fúria Bestial (a " + WbDist(account, target) + " do alvo).");
		Attack(account, target);
		Wait(0.8);
	}

	private List<double[]> Drops(Account a, double radius)
	{
		List<double[]> list = new List<double[]>();
		try
		{
			GameClient gameClient = C(a);
			ClientSnapshot clientSnapshot = gameClient.Read();
			using Process process = Process.GetProcessById(a.Pid);
			using ProcessMemory processMemory = new ProcessMemory(a.Pid);
			uint num = (uint)process.MainModule.BaseAddress.ToInt32();
			uint imageEnd = num + (uint)process.MainModule.ModuleMemorySize;
			uint num2 = processMemory.UInt32(num + gameClient.RootRva);
			uint num3 = processMemory.UInt32(num2 + 32);
			uint num4 = processMemory.UInt32(num3 + 40);
			uint num5 = processMemory.UInt32(num4 + 24);
			uint num6 = processMemory.UInt32(num4 + 28);
			if (num6 == 0 || num5 < 1 || num5 > 65536)
			{
				return list;
			}
			byte[] array = processMemory.Read(num6, (int)(num5 * 4));
			HashSet<uint> hashSet = new HashSet<uint>();
			for (int i = 0; i + 4 <= array.Length; i += 4)
			{
				uint num7 = BitConverter.ToUInt32(array, i);
				int num8 = 0;
				while (num7 != 0 && num8++ < 1000 && hashSet.Add(num7))
				{
					byte[] array2;
					try
					{
						array2 = processMemory.Read(num7, 12);
					}
					catch
					{
						break;
					}
					if (array2 == null || array2.Length < 12)
					{
						break;
					}
					num7 = BitConverter.ToUInt32(array2, 0);
					uint num9 = BitConverter.ToUInt32(array2, 4);
					string text;
					try
					{
						text = NearbyResources.NativeType(processMemory, num9, num, imageEnd);
					}
					catch
					{
						continue;
					}
					if (!(text != ".?AVCECMatter@@") || !(text != ".?AVCECTracedMatter@@"))
					{
						double num10 = processMemory.Float(num9 + 60);
						double num11 = processMemory.Float(num9 + 68);
						uint num12 = BitConverter.ToUInt32(array2, 8);
						uint num13 = processMemory.UInt32(num9 + 276);
						if (Dist(num10, num11, clientSnapshot.X, clientSnapshot.Z) <= radius && (num12 & 0xC0000000u) == 3221225472u && num13 != 0 && num13 < 1000000)
						{
							list.Add(new double[4] { num10, num11, num12, num13 });
						}
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	private void WallDone()
	{
		Say("Fúria da parede: luta terminou — sem andar atrás de drops; todas de volta ao líder e seguindo a rota.");
		Loot(walk: false);
		ClientSnapshot clientSnapshot = Snap(leader);
		if (clientSnapshot == null)
		{
			return;
		}
		foreach (Account item in all)
		{
			if (item.Pid != leader.Pid)
			{
				ClientSnapshot clientSnapshot2 = Snap(item);
				if (clientSnapshot2 != null && clientSnapshot2.Hp > 0 && Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z) > 3.0)
				{
					Nav(item, clientSnapshot.X, clientSnapshot.Z);
				}
			}
		}
	}

	private void Loot()
	{
		Loot(walk: true);
	}

	private void Loot(bool walk)
	{
		int num = 0;
		int num2 = 0;
		Stopwatch stopwatch = Stopwatch.StartNew();
		ClientSnapshot clientSnapshot = Snap(leader);
		while (!stop && num2 < 8 && stopwatch.Elapsed.TotalSeconds < 45.0)
		{
			List<double[]> list = Drops(leader, 40.0);
			if (list.Count == 0)
			{
				break;
			}
			num2++;
			followOn = false;
			List<double[]> list2 = Drops(leader, 8.0);
			if (list2.Count == 0 && !walk)
			{
				break;
			}
			if (list2.Count == 0)
			{
				ClientSnapshot l = Snap(leader);
				if (l == null)
				{
					break;
				}
				List<double[]> list3 = (from q in list
					where !lootSkip.Contains((uint)q[2]) && Dist(q[0], q[1], l.X, l.Z) <= 25.0
					orderby Dist(q[0], q[1], l.X, l.Z)
					select q).ToList();
				if (list3.Count == 0)
				{
					break;
				}
				double[] array = list3[0];
				lootSkip.Add((uint)array[2]);
				WalkOnce(array[0], array[1], 2.0);
				list2 = Drops(leader, 8.0);
				if (list2.Count == 0)
				{
					continue;
				}
			}
			List<KeyValuePair<uint, uint>> list4 = (from d in list2.Take(40)
				select new KeyValuePair<uint, uint>((uint)d[2], (uint)d[3])).ToList();
			TeleportBridge teleportBridge = null;
			try
			{
				teleportBridge = new TeleportBridge(leader.Pid);
				teleportBridge.SendLoot(list4);
				num += list4.Count;
			}
			catch (Exception ex)
			{
				Say("Ganância: " + ex.Message);
			}
			finally
			{
				teleportBridge?.Dispose();
			}
			Wait(1.2);
		}
		if (walk && clientSnapshot != null && num2 > 0)
		{
			ClientSnapshot clientSnapshot2 = Snap(leader);
			if (clientSnapshot2 != null && Dist(clientSnapshot2.X, clientSnapshot2.Z, clientSnapshot.X, clientSnapshot.Z) > 5.0)
			{
				WalkOnce(clientSnapshot.X, clientSnapshot.Z, 3.0);
			}
		}
		int count = Drops(leader, 40.0).Count;
		if (num2 > 0)
		{
			Say("Ganância: " + num2 + " vez(es), " + num + " drop(s) pedidos" + ((count > 0) ? (", " + count + " ainda no chão") : "") + ".");
		}
	}

	private bool FuriaAlive(double x, double z)
	{
		return Mobs(leader).Any((Mob m) => IsFuria(m) && Dist(m.X, m.Z, x, z) <= 70.0);
	}

	private static bool IsFuria(Mob m)
	{
		return m.Name != null && m.Name.StartsWith("Fúria", StringComparison.OrdinalIgnoreCase);
	}

	private void StopFollowNow()
	{
		if (!inDefense)
		{
			WaitFollowers(20.0);
		}
		followOn = false;
		foreach (Account item in all)
		{
			if (item.Pid != leader.Pid && FollowingOf(item.Pid, leader.Id))
			{
				GameFollow(item, on: false);
			}
		}
	}

	private void Fight(double x, double z, double radius, uint onlyTemplate, int maxSeconds)
	{
		StopFollowNow();
		Stopwatch stopwatch = Stopwatch.StartNew();
		Stopwatch stopwatch2 = Stopwatch.StartNew();
		uint current = 0u;
		int num = 0;
		DateTime utcNow = DateTime.UtcNow;
		while (!stop && (stopwatch.Elapsed.TotalSeconds < (double)maxSeconds || (onlyTemplate == 0 && FuriaAlive(x, z))))
		{
			ClientSnapshot l = Snap(leader);
			if (l == null)
			{
				Thread.Sleep(300);
				continue;
			}
			if (stopwatch.Elapsed.TotalSeconds >= (double)maxSeconds && (DateTime.UtcNow - utcNow).TotalSeconds > 60.0)
			{
				utcNow = DateTime.UtcNow;
				Say("Fúria ainda vivo (" + stopwatch.Elapsed.TotalSeconds.ToString("F0") + " s): continuando até ele morrer (sem ele a porta não abre).");
			}
			if (l.Hp <= 0)
			{
				Say("líder morto na luta.");
				break;
			}
			List<Mob> list = (from m in Mobs(leader)
				where Dist(m.X, m.Z, x, z) <= radius || Dist(m.X, m.Z, l.X, l.Z) <= 12.0 || (IsFuria(m) && Dist(m.X, m.Z, x, z) <= 70.0)
				where onlyTemplate == 0 || m.Template == onlyTemplate || Dist(m.X, m.Z, l.X, l.Z) <= 12.0
				where onlyTemplate != 0 || !bossTpl.Contains(m.Template)
				select m).ToList();
			RezTick();
			if (list.Count == 0)
			{
				current = 0u;
				if (stopwatch2.Elapsed.TotalSeconds >= 3.0 && (onlyTemplate != 0 || !FuriaAlive(x, z)))
				{
					break;
				}
				Thread.Sleep(250);
				continue;
			}
			stopwatch2.Restart();
			if (onlyTemplate != 0 && current != 0 && list.Any((Mob m) => m.Key == current && m.Template == onlyTemplate) && list.Any((Mob m) => m.Template != onlyTemplate))
			{
				current = 0u;
			}
			if (current == 0 || !list.Any((Mob m) => m.Key == current))
			{
				if (current != 0 && !list.Any((Mob m) => m.Key == current))
				{
					num++;
				}
				Func<Mob, bool> isFuria = (Mob m) => m.Name != null && m.Name.StartsWith("Fúria", StringComparison.OrdinalIgnoreCase);
				Mob mob = (from m in list
					orderby (m.Name == null || m.Name.IndexOf("sumo", StringComparison.OrdinalIgnoreCase) < 0) ? (isFuria(m) ? 1 : ((onlyTemplate != 0 && m.Template == onlyTemplate) ? 3 : 2)) : 0, Dist(m.X, m.Z, l.X, l.Z)
					select m).First();
				current = mob.Key;
				if (num == 0 && !epHitArea)
				{
					WbOpen(list.FirstOrDefault(isFuria)?.Key ?? current);
				}
				foreach (Account item in all)
				{
					ClientSnapshot clientSnapshot = Snap(item);
					if (clientSnapshot != null && clientSnapshot.Hp > 0 && !WallHold(item, current))
					{
						Attack(item, current);
					}
				}
			}
			else
			{
				foreach (Account item2 in all)
				{
					ClientSnapshot clientSnapshot = Snap(item2);
					if (clientSnapshot != null && clientSnapshot.Hp > 0 && (item2.Class != 7 || !(DateTime.UtcNow < prayUntil)) && !WallHold(item2, current))
					{
						if (!MacroOn(item2) || ((item2.Class != 7 || EpHits(current)) && TargetOf(item2) != current) || (item2.Class == 7 && EpModeChanged(item2, current)))
						{
							Attack(item2, current);
						}
						else if (MacroStalled(item2))
						{
							Unstick(item2, current);
						}
					}
				}
			}
			Thread.Sleep(500);
		}
		foreach (Account item3 in all)
		{
			StopMacro(item3);
		}
		Say("área limpa (" + (num + ((current != 0) ? 1 : 0)) + " alvo(s), " + stopwatch.Elapsed.TotalSeconds.ToString("F0") + " s).");
		Wait(1.0);
		if (all.Any((Account a) =>
		{
			ClientSnapshot clientSnapshot2 = Snap(a);
			return clientSnapshot2 != null && clientSnapshot2.Hp <= 0;
		}))
		{
			AfterDeaths();
		}
		if (epHitArea)
		{
			WallDone();
		}
		else
		{
			Loot();
		}
	}

	private void RezTick()
	{
		if (DateTime.UtcNow < rezNext)
		{
			return;
		}
		rezNext = DateTime.UtcNow.AddSeconds(8.0);
		List<Account> list = all.Where((Account a) =>
		{
			ClientSnapshot clientSnapshot2 = Snap(a);
			return clientSnapshot2 != null && clientSnapshot2.Hp <= 0;
		}).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Account account = all.FirstOrDefault((Account a) => a.Class == 7);
		ClientSnapshot clientSnapshot = ((account == null) ? null : Snap(account));
		foreach (Account item in list)
		{
			TeleportBridge teleportBridge = null;
			try
			{
				teleportBridge = new TeleportBridge(item.Pid);
				teleportBridge.SendReviveAccept();
			}
			catch
			{
			}
			finally
			{
				teleportBridge?.Dispose();
			}
		}
		if (account == null || clientSnapshot == null || clientSnapshot.Hp <= 0 || list.Contains(account))
		{
			return;
		}
		Account account2 = list.FirstOrDefault((Account a) => a.Pid == leader.Pid) ?? list[0];
		Esc(account);
		AttackBridge attackBridge = null;
		try
		{
			attackBridge = new AttackBridge(account.Pid);
			attackBridge.StartMacroOn(account2.Id, 3);
			prayUntil = DateTime.UtcNow.AddSeconds(6.0);
			Say(account.Name + ": Prece da Vida em " + account2.Name + " (durante a luta).");
		}
		catch
		{
		}
		finally
		{
			attackBridge?.Dispose();
		}
	}

	private void AfterDeaths()
	{
		ClientSnapshot l = Snap(leader);
		if (l != null && l.Hp <= 0)
		{
			ClientSnapshot clientSnapshot = (from a in all
				where a.Pid != leader.Pid
				select Snap(a)).FirstOrDefault((ClientSnapshot q) => q != null && q.Hp > 0);
			if (clientSnapshot != null)
			{
				l = clientSnapshot;
			}
		}
		if (!inDefense && l != null && l.Hp > 0 && Mobs(leader).Any((Mob m) => Dist(m.X, m.Z, l.X, l.Z) <= 15.0))
		{
			inDefense = true;
			try
			{
				Say("mob batendo perto dos mortos: lutando antes da ressurreição.");
				OneByOne(l.X, l.Z, 15.0, "antes da ressurreição");
			}
			finally
			{
				inDefense = false;
			}
		}
		if (stop)
		{
			return;
		}
		Account account = all.FirstOrDefault((Account a) => a.Class == 7);
		ClientSnapshot clientSnapshot2 = ((account == null) ? null : Snap(account));
		if (account == null || (clientSnapshot2 != null && clientSnapshot2.Hp <= 0))
		{
			ClientSnapshot clientSnapshot3 = Snap(leader);
			Account account2 = all.FirstOrDefault((Account a) =>
			{
				ClientSnapshot clientSnapshot5 = Snap(a);
				return clientSnapshot5 != null && clientSnapshot5.Hp > 0;
			});
			ClientSnapshot clientSnapshot4;
			if (clientSnapshot3 != null && clientSnapshot3.Hp > 0)
			{
				clientSnapshot4 = clientSnapshot3;
			}
			else
			{
				clientSnapshot4 = ((account2 != null) ? Snap(account2) : null);
			}
			if (clientSnapshot4 != null && !inDefense)
			{
				inDefense = true;
				try
				{
					Say("EP morta: as vivas terminam os mobs perto antes de parar.");
					OneByOne(clientSnapshot4.X, clientSnapshot4.Z, 20.0, "EP morta");
				}
				finally
				{
					inDefense = false;
				}
			}
			Say("EP morta (ou sem EP): ninguém para levantar — parando. Levante na mão e clique Iniciar: o bot continua do ponto mais perto.");
			stop = true;
			return;
		}
		WaitRez();
		if (all.Any((Account a) =>
		{
			ClientSnapshot clientSnapshot5 = Snap(a);
			return clientSnapshot5 != null && clientSnapshot5.Hp <= 0;
		}) && Snap(Wb()) != null && Snap(Wb()).Hp <= 0)
		{
			return;
		}
		ClientSnapshot l2 = Snap(leader);
		if (!inDefense && l2 != null && l2.Hp > 0 && Mobs(leader).Any((Mob m) => Dist(m.X, m.Z, l2.X, l2.Z) <= 15.0))
		{
			inDefense = true;
			try
			{
				OneByOne(l2.X, l2.Z, 15.0, "depois da ressurreição");
			}
			finally
			{
				inDefense = false;
			}
		}
		if (!stop && !all.Any((Account a) =>
		{
			ClientSnapshot clientSnapshot5 = Snap(a);
			return clientSnapshot5 != null && clientSnapshot5.Hp <= 0;
		}))
		{
			Say("rebuff depois das mortes.");
			WbStand();
			Buff();
			WbLie();
		}
	}

	private void Meditate(int seconds)
	{
		Loot();
		StopFollowNow();
		Say("meditando " + seconds + " s.");
		try
		{
			NativeMeditationRuntime rt = new NativeMeditationRuntime();
			foreach (Account item in all)
			{
				ClientSnapshot clientSnapshot = Snap(item);
				if (clientSnapshot != null && clientSnapshot.Hp > 0)
				{
					try
					{
						rt.Set(Ref(item), sitting: true);
					}
					catch
					{
					}
				}
			}
			Wait(seconds);
			foreach (Account item2 in all)
			{
				try
				{
					rt.Set(Ref(item2), sitting: false);
				}
				catch
				{
				}
			}
			Wait(1.0);
			for (int i = 0; i < 3; i++)
			{
				if (stop)
				{
					break;
				}
				List<Account> list = all.Where((Account a) =>
				{
					try
					{
						ClientSnapshot clientSnapshot2 = Snap(a);
						return clientSnapshot2 != null && clientSnapshot2.Hp > 0 && rt.Sitting(Ref(a));
					}
					catch
					{
						return false;
					}
				}).ToList();
				if (list.Count == 0)
				{
					break;
				}
				if (i == 2)
				{
					Say("ainda sentada(s): " + string.Join(", ", list.Select((Account a) => a.Name).ToArray()) + ".");
				}
				foreach (Account item3 in list)
				{
					try
					{
						rt.Set(Ref(item3), sitting: false);
					}
					catch
					{
					}
				}
				Wait(1.0);
			}
		}
		finally
		{
			lastMeditateEnd = clockAll.Elapsed.TotalSeconds;
		}
	}

	private static CharacterRef Ref(Account a)
	{
		CharacterRef characterRef = new CharacterRef();
		characterRef.Pid = a.Pid;
		characterRef.Id = a.Id;
		characterRef.Name = a.Name;
		return characterRef;
	}

	private void Buff()
	{
		foreach (Account item in all)
		{
			ClientSnapshot clientSnapshot = Snap(item);
			if (clientSnapshot != null && clientSnapshot.Hp > 0)
			{
				try
				{
					BuffService.Start(Ref(item));
				}
				catch
				{
				}
			}
		}
		Wait(8.0);
	}

	private int WbFormRaw(Account wb)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(wb.Pid);
			if (characterMemory.Id != wb.Id)
			{
				return -1;
			}
			return characterMemory.Read(characterMemory.Host + 237, 1)[0];
		}
		catch
		{
			return -1;
		}
	}

	private void WbSet(bool form, string label)
	{
		Account account = Wb();
		ClientSnapshot clientSnapshot = Snap(account);
		if (clientSnapshot == null || clientSnapshot.Hp <= 0)
		{
			Say(account.Name + ": " + label + " — morto.");
			return;
		}
		int num = WbFormRaw(account);
		if (num == (form ? 1 : 0))
		{
			Say(account.Name + ": " + label + " (já estava; forma lida = " + num + ").");
			return;
		}
		Say(string.Concat(account.Name, ": forma lida = ", num, " (", num switch
		{
			0 => (object)"em pé", 
			1 => "deitado", 
			_ => "?", 
		}, ") → ", label, "."));
		for (int i = 0; i < 2; i++)
		{
			if (stop)
			{
				break;
			}
			TeleportBridge teleportBridge = null;
			try
			{
				teleportBridge = new TeleportBridge(account.Pid);
				teleportBridge.SendSelfSkill(80, 112u);
			}
			catch (Exception ex)
			{
				Say(account.Name + ": " + label + " — " + ex.Message);
				return;
			}
			finally
			{
				teleportBridge?.Dispose();
			}
			if (num != 0 && num != 1)
			{
				Say(account.Name + ": " + label + " (estado da forma não lido).");
				Wait(1.5);
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed.TotalSeconds < 3.0 && !stop)
			{
				if (WbFormRaw(account) == (form ? 1 : 0))
				{
					Say(account.Name + ": " + label + ".");
					Wait(0.5);
					return;
				}
				Thread.Sleep(150);
			}
		}
		Say(account.Name + ": " + label + " não confirmou.");
	}

	private void WbStand()
	{
		WbSet(form: false, "em pé");
	}

	private void WbLie()
	{
		WbSet(form: true, "deitado (forma)");
	}

	private void Skill(string data)
	{
		string[] array = data.Split(' ');
		uint num = uint.Parse(array[0]);
		byte type = Convert.ToByte(array[1], 16);
		if (num == 112)
		{
			if (array.Length > 2 && array[2].IndexOf("pé", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				WbStand();
			}
			else
			{
				WbLie();
			}
			return;
		}
		Account account = all.FirstOrDefault((Account a) => a.Class == 4) ?? leader;
		TeleportBridge teleportBridge = null;
		try
		{
			teleportBridge = new TeleportBridge(account.Pid);
			teleportBridge.SendSelfSkill(type, num);
			Say(account.Name + ": " + ((array.Length > 2) ? array[2] : ("skill " + num)) + ".");
		}
		catch (Exception ex)
		{
			Say(account.Name + ": skill " + num + " — " + ex.Message);
		}
		finally
		{
			teleportBridge?.Dispose();
		}
		Wait(1.5);
	}

	private void WaitRez()
	{
		followOn = false;
		Stopwatch stopwatch = Stopwatch.StartNew();
		Account account = all.FirstOrDefault((Account a) => a.Class == 7);
		double num = 0.0;
		double num2 = 0.0;
		while (!stop && stopwatch.Elapsed.TotalSeconds < 90.0)
		{
			List<Account> list = all.Where((Account a) =>
			{
				ClientSnapshot clientSnapshot = Snap(a);
				return clientSnapshot != null && clientSnapshot.Hp <= 0;
			}).ToList();
			if (list.Count == 0)
			{
				return;
			}
			double totalSeconds = stopwatch.Elapsed.TotalSeconds;
			if (totalSeconds >= num2)
			{
				num2 = totalSeconds + 2.0;
				foreach (Account item in list)
				{
					TeleportBridge teleportBridge = null;
					try
					{
						teleportBridge = new TeleportBridge(item.Pid);
						teleportBridge.SendReviveAccept();
					}
					catch
					{
					}
					finally
					{
						teleportBridge?.Dispose();
					}
				}
			}
			if (account != null && totalSeconds >= num && !list.Contains(account))
			{
				num = totalSeconds + 8.0;
				Account current = list[0];
				Esc(account);
				AttackBridge attackBridge = null;
				try
				{
					attackBridge = new AttackBridge(account.Pid);
					attackBridge.StartMacroOn(current.Id, 3);
					Say(account.Name + ": Prece da Vida em " + current.Name + ".");
				}
				catch
				{
				}
				finally
				{
					attackBridge?.Dispose();
				}
			}
			Thread.Sleep(500);
		}
		Say("ainda há conta morta depois de 90 s; seguindo.");
	}

	private bool BossNearby()
	{
		if (inBoss)
		{
			return false;
		}
		ClientSnapshot l = Snap(leader);
		if (l == null || l.Hp <= 0)
		{
			return false;
		}
		Mob b = Mobs(leader).FirstOrDefault((Mob m) => bossTpl.Contains(m.Template) && !bossDone.Contains(m.Template) && Dist(m.X, m.Z, l.X, l.Z) <= (double)((m.Template == 24820) ? 15 : 30));
		if (b == null)
		{
			return false;
		}
		Step step = route.FirstOrDefault((Step r) => r.Kind == 'B' && Convert.ToUInt32(r.Data.Split(' ')[0], 16) == b.Template);
		if (step == null)
		{
			return false;
		}
		Say("boss " + b.Template.ToString("X") + " puxado antes do passo dele: começando a mecânica do boss agora.");
		Boss(step);
		return true;
	}

	private static bool InWolfBox(double x, double z)
	{
		return x >= 115.0 && x <= 243.0 && z >= 322.0 && z <= 387.0;
	}

	private void LoboPrep()
	{
		Mob mob = Mobs(leader).FirstOrDefault((Mob m) => m.Template == 24820);
		double lx = mob?.X ?? 179.9;
		double lz = mob?.Z ?? 357.6;
		Say("salão do Lobo: limpando os mobs da sala antes do Lobo.");
		int num = 0;
		for (int num2 = 0; num2 < 8; num2++)
		{
			if (stop)
			{
				break;
			}
			ClientSnapshot l = Snap(leader);
			if (l == null || l.Hp <= 0)
			{
				break;
			}
			Mob mob2 = Mobs(leader).FirstOrDefault((Mob m) => m.Template == 24820);
			if (mob2 != null)
			{
				lx = mob2.X;
				lz = mob2.Z;
				if (Dist(mob2.X, mob2.Z, l.X, l.Z) <= 15.0)
				{
					BossNearby();
					return;
				}
			}
			Mob mob3 = (from m in Mobs(leader)
				where !bossTpl.Contains(m.Template) && InWolfBox(m.X, m.Z) && Dist(m.X, m.Z, lx, lz) > 18.0
				orderby Dist(m.X, m.Z, l.X, l.Z)
				select m).FirstOrDefault();
			if (mob3 == null)
			{
				break;
			}
			num++;
			Say("salão do Lobo: grupo " + num + " em " + mob3.X.ToString("F0") + "/" + mob3.Z.ToString("F0") + " (a " + Dist(mob3.X, mob3.Z, lx, lz).ToString("F0") + " do Lobo).");
			if (Dist(mob3.X, mob3.Z, l.X, l.Z) > 8.0)
			{
				Nav(leader, mob3.X, mob3.Z);
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (!stop && stopwatch.Elapsed.TotalSeconds < 10.0)
				{
					Thread.Sleep(300);
					ClientSnapshot clientSnapshot = Snap(leader);
					Mob mob4 = Mobs(leader).FirstOrDefault((Mob x) => x.Key == mob3.Key);
					if (clientSnapshot == null || mob4 == null || Dist(clientSnapshot.X, clientSnapshot.Z, mob4.X, mob4.Z) <= 7.0)
					{
						break;
					}
					Mob mob5 = Mobs(leader).FirstOrDefault((Mob m) => m.Template == 24820);
					if (mob5 != null && Dist(mob5.X, mob5.Z, clientSnapshot.X, clientSnapshot.Z) <= 15.0)
					{
						BossNearby();
						return;
					}
				}
			}
			HoldForGroup();
			Fight(mob3.X, mob3.Z, 12.0, 0u, 120);
		}
		Say("salão do Lobo: " + num + " grupo(s) limpos — agora o Lobo.");
	}

	private void Boss(Step st)
	{
		string[] array = st.Data.Split(' ');
		uint tpl = Convert.ToUInt32(array[0], 16);
		string mode = ((array.Length > 1) ? array[1] : "COMBO");
		if (bossDone.Contains(tpl) && !Mobs(leader).Any((Mob m) => m.Template == tpl))
		{
			Say("boss " + array[0] + " já feito.");
		}
		else
		{
			if (inBoss)
			{
				return;
			}
			inBoss = true;
			try
			{
				BossRun(st, array, tpl, mode);
			}
			finally
			{
				inBoss = false;
				if (!Mobs(leader).Any((Mob m) => m.Template == tpl))
				{
					bossDone.Add(tpl);
				}
			}
		}
	}

	private void BossRun(Step st, string[] f, uint tpl, string mode)
	{
		ClientSnapshot clientSnapshot = Snap(leader);
		List<Mob> source = Mobs(leader);
		Func<Mob, bool> predicate = (Mob m) => m.Template == tpl;
		Mob mob = source.FirstOrDefault(predicate);
		if (mob == null || clientSnapshot == null || !(Dist(mob.X, mob.Z, clientSnapshot.X, clientSnapshot.Z) <= 30.0))
		{
			WalkTo(st.X, st.Z, 8.0);
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (!stop && stopwatch.Elapsed.TotalSeconds < 60.0 && !Mobs(leader).Any((Mob m) => m.Template == tpl))
			{
				Thread.Sleep(500);
			}
			if (!Mobs(leader).Any((Mob m) => m.Template == tpl))
			{
				Say("boss " + f[0] + " não apareceu em 60 s; seguindo.");
				return;
			}
		}
		if (mode == "DRAGAO" || mode == "LOBO")
		{
			followOn = false;
			FrostBot frostBot = new FrostBot((!(mode == "DRAGAO")) ? FrostBot.Mode.Wolf : FrostBot.Mode.Dragon, note);
			frostBot.ManageLock = false;
			FrostBot frostBot2 = frostBot;
			Mob mob2 = Mobs(leader).FirstOrDefault((Mob m) => m.Template == tpl);
			if (mob2 != null)
			{
				WbOpen(mob2.Key);
			}
			frostBot2.Start(all.Select((Account a) => new FrostBot.Account
			{
				Pid = a.Pid,
				Name = a.Name
			}).ToList());
			try {
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			while (!stop && frostBot2.Running && stopwatch2.Elapsed.TotalMinutes < 15.0)
			{
				Thread.Sleep(500);
			}
			} finally { frostBot2.StopAndWait(Timeout.Infinite); }
			if (!stop) Loot();
		}
		else
		{
			for (int num = 0; num < 3; num++)
			{
				if (stop)
				{
					break;
				}
				Fight(st.X, st.Z, 40.0, tpl, 900);
				ClientSnapshot clientSnapshot2 = Snap(leader);
				if (stop || clientSnapshot2 == null || clientSnapshot2.Hp <= 0)
				{
					break;
				}
				Mob mob3 = Mobs(leader).FirstOrDefault((Mob m) => m.Template == tpl);
				if (mob3 == null)
				{
					break;
				}
				Say("boss " + f[0] + " ainda vivo: voltando à luta (" + (num + 2) + "ª vez).");
				WalkTo(mob3.X, mob3.Z, 8.0);
			}
		}
		Wait(3.0);
	}

	private void CloseWindows(Account a)
	{
		try
		{
			CharacterRef characterRef = new CharacterRef();
			characterRef.Pid = a.Pid;
			characterRef.Id = a.Id;
			characterRef.Name = a.Name;
			string text = QuestService.CloseWindows(characterRef, CancellationToken.None, out var _);
			if (!string.IsNullOrEmpty(text) && text.IndexOf("Nenhuma", StringComparison.OrdinalIgnoreCase) < 0)
			{
				Say(a.Name + ": " + text);
			}
		}
		catch
		{
		}
	}

	private void EquipBag(Account a)
	{
		for (int i = 1; i <= 3; i++)
		{
			if (stop)
			{
				break;
			}
			InventoryItem inventoryItem;
			try
			{
				inventoryItem = C(a).ReadInventory().Items.FirstOrDefault((InventoryItem inventoryItem2) => inventoryItem2.Id == 52287);
			}
			catch
			{
				return;
			}
			if (inventoryItem == null)
			{
				return;
			}
			TeleportBridge teleportBridge = null;
			try
			{
				teleportBridge = new TeleportBridge(a.Pid);
				teleportBridge.SendEquip(new byte[4]
				{
					17,
					0,
					(byte)inventoryItem.Slot,
					22
				});
			}
			catch (Exception ex)
			{
				Say(a.Name + ": bolsa tática: " + ex.Message);
				return;
			}
			finally
			{
				teleportBridge?.Dispose();
			}
			Wait(1.5);
			try
			{
				if (!C(a).ReadInventory().Items.Any((InventoryItem inventoryItem2) => inventoryItem2.Id == 52287))
				{
					Say(a.Name + ": bolsa tática equipada.");
					return;
				}
			}
			catch
			{
				return;
			}
		}
		Say(a.Name + ": a bolsa tática continua na mochila.");
	}

	private bool InRoute(double x, double z)
	{
		if (Math.Abs(x) >= 1500.0)
		{
			return false;
		}
		foreach (Step item in route)
		{
			if (item.Kind == 'E' || !(Dist(x, z, item.X, item.Z) < 70.0))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	private bool InsideOf(Account a)
	{
		ClientSnapshot clientSnapshot = Snap(a);
		return clientSnapshot != null && InRoute(clientSnapshot.X, clientSnapshot.Z);
	}

	private uint NpcKeyByTemplate(Account a, uint template)
	{
		try
		{
			GameClient gameClient = C(a);
			using ProcessMemory processMemory = new ProcessMemory(a.Pid);
			using Process process = Process.GetProcessById(a.Pid);
			uint num = (uint)process.MainModule.BaseAddress.ToInt32();
			uint num2 = processMemory.UInt32(num + gameClient.RootRva);
			uint num3 = processMemory.UInt32(num2 + 32);
			uint num4 = processMemory.UInt32(num3 + 36);
			uint num5 = processMemory.UInt32(num4 + 28);
			uint address = processMemory.UInt32(num4 + 32);
			if (num5 < 1 || num5 > 65536)
			{
				return 0u;
			}
			byte[] array = processMemory.Read(address, (int)(num5 * 4));
			HashSet<uint> hashSet = new HashSet<uint>();
			for (int i = 0; i + 4 <= array.Length; i += 4)
			{
				uint num6 = BitConverter.ToUInt32(array, i);
				int num7 = 0;
				while (num6 != 0 && num7++ < 1000 && hashSet.Add(num6))
				{
					byte[] array2 = processMemory.Read(num6, 12);
					if (array2 == null || array2.Length < 12)
					{
						break;
					}
					num6 = BitConverter.ToUInt32(array2, 0);
					uint num8 = BitConverter.ToUInt32(array2, 4);
					if (processMemory.UInt32(num8 + 284) == template)
					{
						return BitConverter.ToUInt32(array2, 8);
					}
				}
			}
		}
		catch
		{
		}
		return 0u;
	}

	private bool NpcPos(Account a, uint key, out double x, out double z)
	{
		x = (z = 0.0);
		try
		{
			GameClient gameClient = C(a);
			using ProcessMemory processMemory = new ProcessMemory(a.Pid);
			using Process process = Process.GetProcessById(a.Pid);
			uint num = (uint)process.MainModule.BaseAddress.ToInt32();
			uint num2 = processMemory.UInt32(num + gameClient.RootRva);
			uint num3 = processMemory.UInt32(num2 + 32);
			uint num4 = processMemory.UInt32(num3 + 36);
			uint num5 = processMemory.UInt32(num4 + 28);
			uint address = processMemory.UInt32(num4 + 32);
			if (num5 < 1 || num5 > 65536)
			{
				return false;
			}
			byte[] array = processMemory.Read(address, (int)(num5 * 4));
			HashSet<uint> hashSet = new HashSet<uint>();
			for (int i = 0; i + 4 <= array.Length; i += 4)
			{
				uint num6 = BitConverter.ToUInt32(array, i);
				int num7 = 0;
				while (num6 != 0 && num7++ < 1000 && hashSet.Add(num6))
				{
					byte[] array2 = processMemory.Read(num6, 12);
					if (array2 == null || array2.Length < 12)
					{
						break;
					}
					num6 = BitConverter.ToUInt32(array2, 0);
					if (BitConverter.ToUInt32(array2, 8) != key)
					{
						continue;
					}
					uint num8 = BitConverter.ToUInt32(array2, 4);
					x = processMemory.Float(num8 + 60);
					z = processMemory.Float(num8 + 68);
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private bool TeleportToPorto2(Account a)
	{
		return TeleportTo(a, 3803u, 1631.3, 816.1, "a Cidade Sul do Dragão", force: true);
	}

	private bool TeleportToPorto(Account a)
	{
		return TeleportTo(a, 3803u, 1631.3, 816.1, "a Cidade Sul do Dragão");
	}

	private bool Unstick(Account a)
	{
		Say(a.Name + ": presa no chão; teleportando para outro ponto e voltando.");
		TeleportTo(a, 3801u, 1470.5, 1180.9, "a Cidade Norte do Dragão", force: true);
		Wait(2.0);
		return TeleportTo(a, 3803u, 1631.3, 816.1, "a Cidade Sul do Dragão", force: true);
	}

	private bool TeleportTo(Account a, uint destId, double dx, double dz, string name, bool force = false)
	{
		for (int i = 1; i <= 2; i++)
		{
			if (stop)
			{
				break;
			}
			ClientSnapshot clientSnapshot = Snap(a);
			if (!force && clientSnapshot != null && Dist(clientSnapshot.X, clientSnapshot.Z, dx, dz) < 150.0)
			{
				return true;
			}
			force = false;
			TeleportBridge teleportBridge = null;
			try
			{
				Stone stone;
				using (CharacterMemory characterMemory = new CharacterMemory(a.Pid))
				{
					stone = characterMemory.FindAnyStone(TeleportService.StoneIds[0]);
				}
				byte[] array = new byte[14];
				BitConverter.GetBytes(16777325u).CopyTo(array, 0);
				BitConverter.GetBytes(stone.Slot).CopyTo(array, 4);
				BitConverter.GetBytes(stone.Id).CopyTo(array, 6);
				BitConverter.GetBytes(destId).CopyTo(array, 10);
				teleportBridge = new TeleportBridge(a.Pid);
				teleportBridge.SendTeleport(array);
				Say(a.Name + ": pedra de teleporte para " + name + ".");
			}
			catch (Exception ex)
			{
				Say(a.Name + ": teleporte — " + ex.Message);
				return false;
			}
			finally
			{
				teleportBridge?.Dispose();
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed.TotalSeconds < 25.0 && !stop)
			{
				ClientSnapshot clientSnapshot2 = Snap(a);
				if (clientSnapshot2 != null && Dist(clientSnapshot2.X, clientSnapshot2.Z, dx, dz) < 150.0)
				{
					Wait(2.0);
					ResetClient(a);
					return true;
				}
				Thread.Sleep(500);
			}
		}
		return false;
	}

	private bool EnterAccount(Account a)
	{
		if (InsideOf(a))
		{
			return true;
		}
		Esc(a);
		Wait(0.3);
		if (!Learned().TryGetValue("entrada", out var value) || value.Count == 0)
		{
			return false;
		}
		uint key = 0u;
		foreach (byte[] item in value)
		{
			if ((item[0] == 2 || item[0] == 35) && item.Length == 6)
			{
				key = BitConverter.ToUInt32(item, 2);
				break;
			}
		}
		for (int i = 1; i <= 2 && !stop; i++)
		{
			if (!NpcPos(a, key, out var x, out var z))
			{
				if (!TeleportToPorto(a))
				{
					continue;
				}
				Stopwatch stopwatch = Stopwatch.StartNew();
				while (!NpcPos(a, key, out x, out z) && stopwatch.Elapsed.TotalSeconds < 15.0 && !stop)
				{
					Thread.Sleep(400);
				}
				if (!NpcPos(a, key, out x, out z))
				{
					Say(a.Name + ": NPC da Terra do Sonho não visível na Cidade Sul do Dragão.");
					continue;
				}
			}
			ResetClient(a);
			ClientSnapshot clientSnapshot = Snap(a);
			double x2 = x;
			double z2 = z;
			if (clientSnapshot != null)
			{
				double num = Dist(clientSnapshot.X, clientSnapshot.Z, x, z);
				if (num > 0.1)
				{
					x2 = x + ((double)clientSnapshot.X - x) / num * 2.5;
					z2 = z + ((double)clientSnapshot.Z - z) / num * 2.5;
				}
			}
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			double num2 = 1000000000.0;
			Stopwatch stopwatch3 = Stopwatch.StartNew();
			int num3 = 0;
			ClientSnapshot clientSnapshot2;
			while (!stop && stopwatch2.Elapsed.TotalSeconds < 30.0)
			{
				clientSnapshot2 = Snap(a);
				if (clientSnapshot2 == null)
				{
					Thread.Sleep(500);
					continue;
				}
				double num4 = Dist(clientSnapshot2.X, clientSnapshot2.Z, x, z);
				if (num4 <= 4.5)
				{
					break;
				}
				if (num4 < num2 - 0.5)
				{
					num2 = num4;
					stopwatch3.Restart();
				}
				else if (stopwatch3.Elapsed.TotalSeconds > 5.0 && num3 < 2)
				{
					Esc(a);
					ResetClient(a);
					num3++;
					stopwatch3.Restart();
					Say(a.Name + ": parada a " + num4.ToString("F1") + " do NPC; ESC e andando de novo.");
				}
				Nav(a, x2, z2);
				Thread.Sleep(1000);
			}
			clientSnapshot2 = Snap(a);
			if (clientSnapshot2 != null && Dist(clientSnapshot2.X, clientSnapshot2.Z, x, z) > 6.0)
			{
				Say(a.Name + ": não chegou perto do NPC (a " + Dist(clientSnapshot2.X, clientSnapshot2.Z, x, z).ToString("F1") + "); tentando falar daqui.");
				if (ReplayOn(a, "entrada", () => InsideOf(a), 10.0))
				{
					Wait(1.5);
					CloseWindows(a);
					Say(a.Name + ": entrou na DG.");
					return true;
				}
				Unstick(a);
			}
			else if (ReplayOn(a, "entrada", () => InsideOf(a), 15.0))
			{
				Wait(1.5);
				CloseWindows(a);
				Say(a.Name + ": entrou na DG.");
				return true;
			}
		}
		Say(a.Name + ": não conseguiu entrar na DG.");
		return false;
	}

	private bool Inside()
	{
		return InsideOf(leader);
	}

	private bool HasQuest(ushort q)
	{
		try
		{
			using CharacterMemory characterMemory = new CharacterMemory(leader.Pid);
			return characterMemory.ActiveQuestIds().Contains(q);
		}
		catch
		{
			return false;
		}
	}

	private void SideSteps()
	{
		Wait(2.0);
		Say("3 passos para o lado (desbugar a posição).");
		foreach (Account item in all)
		{
			ClientSnapshot clientSnapshot = Snap(item);
			if (clientSnapshot == null || clientSnapshot.Hp <= 0)
			{
				continue;
			}
			Step step = route.First((Step r) => r.Kind == 'E');
			double num = (double)clientSnapshot.X - step.X;
			double num2 = (double)clientSnapshot.Z - step.Z;
			double num3 = Math.Sqrt(num * num + num2 * num2);
			double num4 = ((num3 > 0.5) ? ((0.0 - num2) / num3) : 1.0);
			double num5 = ((num3 > 0.5) ? (num / num3) : 0.0);
			for (int num6 = 1; num6 <= 3; num6++)
			{
				if (stop)
				{
					break;
				}
				Nav(item, (double)clientSnapshot.X + num4 * 2.0 * (double)num6, (double)clientSnapshot.Z + num5 * 2.0 * (double)num6);
				Wait(0.8);
			}
		}
		Wait(1.0);
	}

	private void Run()
	{
		try
		{
			lib = RotaPaths.Bridge();
			followerThread = new Thread(() => { try { FollowLoop(); } catch (Exception ex) { Say("Falha no seguir Frost: " + ex); stop = true; } });
			followerThread.IsBackground = true;
			followerThread.Start();
			while (!stop)
			{
				Cycles++;
				CycleStart = DateTime.Now;
				bossDone.Clear();
				skipMob.Clear();
				loboPrepped = false;
				Say("DG nº " + Cycles + " começando.");
				int num = 0;
				ClientSnapshot clientSnapshot = Snap(leader);
				Step step = route.First((Step r) => r.Kind == 'N');
				if (clientSnapshot != null && Inside() && Dist(clientSnapshot.X, clientSnapshot.Z, step.X, step.Z) > 60.0)
				{
					double num2 = 1000000000.0;
					foreach (Step item in route)
					{
						if (item.Kind != 'E')
						{
							double num3 = Dist(clientSnapshot.X, clientSnapshot.Z, item.X, item.Z);
							if (num3 < num2)
							{
								num2 = num3;
								num = item.Index;
							}
						}
					}
					Say("já dentro da DG, longe do portão: continuando do passo " + (num + 1) + "/" + route.Count + ".");
				}
				foreach (Step item2 in route)
				{
					if (stop)
					{
						break;
					}
					if (item2.Index < num)
					{
						continue;
					}
					if (item2.Kind != 'R' && item2.Kind != 'E' && all.Any((Account a2) =>
					{
						ClientSnapshot clientSnapshot4 = Snap(a2);
						return clientSnapshot4 != null && clientSnapshot4.Hp <= 0;
					}))
					{
						Say("conta morta antes do passo " + (item2.Index + 1) + ": esperando a ressurreição.");
						AfterDeaths();
						ClientSnapshot clientSnapshot2 = Snap(leader);
						if (clientSnapshot2 != null && clientSnapshot2.Hp <= 0)
						{
							Say("líder morto e ninguém levantou em 90 s; parando.");
							stop = true;
							break;
						}
					}
					Current = item2.Index + 1 + "/" + route.Count + " " + Describe(item2);
					if (item2.Kind != 'W')
					{
						status("DG " + Cycles + " · " + Current + " · " + (DateTime.Now - CycleStart).ToString("h\\:mm\\:ss"));
					}
					switch (item2.Kind)
					{
					case 'E':
					{
						followOn = false;
						bool flag2 = Learned().ContainsKey("entrada");
						if (!Inside())
						{
							if (flag2)
							{
								EnterAccount(leader);
							}
							if (!Inside() && !ManualAndLearn("entrada", "entre na DG Cidade do Gelo pela Terra do Sonho", Inside, 15.0))
							{
								Say("não entrou na DG; parando.");
								stop = true;
								break;
							}
						}
						List<Account> list2 = all.Where((Account account) => account.Pid != leader.Pid && !InsideOf(account)).ToList();
						if (list2.Count > 0)
						{
							Say("levando " + list2.Count + " conta(s) para a DG: " + string.Join(", ", list2.Select((Account account) => account.Name).ToArray()) + ".");
							foreach (Account item3 in list2)
							{
								if (stop)
								{
									break;
								}
								EnterAccount(item3);
							}
						}
						foreach (Account item4 in all)
						{
							CloseWindows(item4);
						}
						for (int num5 = 2; num5 <= 3; num5++)
						{
							if (stop)
							{
								break;
							}
							List<Account> list3 = all.Where((Account a2) => !InsideOf(a2)).ToList();
							if (list3.Count == 0)
							{
								break;
							}
							Say("ainda fora da DG: " + string.Join(", ", list3.Select((Account account) => account.Name).ToArray()) + " (rodada " + num5 + "/3).");
							foreach (Account item5 in list3)
							{
								if (stop)
								{
									break;
								}
								EnterAccount(item5);
							}
						}
						List<string> list4 = (from account in all
							where !InsideOf(account)
							select account.Name).ToList();
						if (list4.Count > 0)
						{
							Say("fora da DG: " + string.Join(", ", list4.ToArray()) + " — parando sem bufar nem abrir o portão.");
							stop = true;
							break;
						}
						lock (trail)
						{
							trail.Clear();
							trailIdx.Clear();
						}
						Say("buff geral antes do portão.");
						WbStand();
						foreach (Account item6 in all)
						{
							if (InsideOf(item6))
							{
								EquipBag(item6);
							}
						}
						Buff();
						WbLie();
						break;
					}
					case 'N':
					{
						string[] array = item2.Data.Split(' ');
						ushort q = ushort.Parse(array[0]);
						if (!HasQuest(q))
						{
							WalkTo(item2.X, item2.Z, 5.0);
							Say("NPC " + ((array.Length > 2) ? array[2] : "") + ": missão " + q + ".");
							replayTemplate = ((array.Length > 1) ? Convert.ToUInt32(array[1], 16) : 0u);
							if (!ManualAndLearn("npc-" + q, "fale com o NPC (" + ((array.Length > 2) ? array[2] : "") + ") e aceite a missão " + q, () => HasQuest(q), 10.0))
							{
								Say("missão " + q + " não aceita; parando.");
								stop = true;
							}
							replayTemplate = 0u;
							Wait(1.0);
							CloseWindows(leader);
						}
						break;
					}
					case 'W':
						if (!loboPrepped && InWolfBox(item2.X, item2.Z) && item2.X >= 145.0 && !bossDone.Contains(24820u))
						{
							WalkTo(item2.X, item2.Z, 4.0);
							loboPrepped = true;
							LoboPrep();
							break;
						}
						if (Careful(item2.X, item2.Z))
						{
							OneByOne(item2.X, item2.Z, 15.0, "caminho");
						}
						WalkTo(item2.X, item2.Z, 4.0);
						break;
					case 'F':
					{
						if (Careful(item2.X, item2.Z))
						{
							OneByOne(item2.X, item2.Z, 15.0, "antes da área");
							WalkTo(item2.X, item2.Z, 6.0);
							OneByOne(item2.X, item2.Z, 30.0, "área");
							if (FuriaAlive(item2.X, item2.Z))
							{
								Say("Fúria vivo na área mob por mob: lutando até ele morrer.");
								Fight(item2.X, item2.Z, 30.0, 0u, 240);
							}
							Loot();
							break;
						}
						WalkTo(item2.X, item2.Z, 6.0);
						double radius = double.Parse(item2.Data, CultureInfo.InvariantCulture);
						epHitArea = EpArea(item2.X, item2.Z);
						wallHeld.Clear();
						try
						{
							if (epHitArea)
							{
								Gather();
							}
							Fight(item2.X, item2.Z, radius, 0u, 240);
							for (int num4 = 0; num4 < 5; num4++)
							{
								if (stop)
								{
									break;
								}
								if (!FuriaAlive(item2.X, item2.Z))
								{
									break;
								}
								ClientSnapshot clientSnapshot3 = Snap(leader);
								if (clientSnapshot3 == null || clientSnapshot3.Hp <= 0)
								{
									break;
								}
								Say("Fúria ainda vivo depois das mortes: voltando à luta.");
								Fight(item2.X, item2.Z, radius, 0u, 240);
							}
						}
						finally
						{
							epHitArea = false;
						}
						BackToEntry(item2);
						break;
					}
					case 'M':
						WalkTo(item2.X, item2.Z, 6.0);
						Meditate(int.Parse(item2.Data));
						break;
					case 'B':
						Boss(item2);
						break;
					case 'R':
						WaitRez();
						break;
					case 'U':
						WbStand();
						Buff();
						WbLie();
						break;
					case 'S':
						Skill(item2.Data);
						break;
					case 'X':
					{
						LastTime = DateTime.Now - CycleStart;
						Say("DG nº " + Cycles + " concluída em " + LastTime.ToString("h\\:mm\\:ss") + ".");
						followOn = false;
						if (SingleCycle) { Say("Teste de ciclo único concluído; remontagem automática não habilitada nesta PR."); stop = true; break; }
						Thread.Sleep(800);
						bool flag = remount == null;
						for (int num4 = 0; num4 < 3; num4++)
						{
							if (stop)
							{
								break;
							}
							if (flag)
							{
								break;
							}
							if (num4 > 0)
							{
								Say("remontar PT de novo (" + (num4 + 1) + "ª tentativa).");
								Thread.Sleep(5000);
							}
							try
							{
								flag = remount();
							}
							catch (Exception ex)
							{
								Say("remontar PT: " + ex.Message);
							}
						}
						if (stop)
						{
							break;
						}
						if (!flag)
						{
							Say("a PT não remontou — sem remontar a próxima DG fica sem monstros; parando. Remonte a PT na mão e clique Iniciar.");
							stop = true;
							break;
						}
						Say("PT remontada: esperando 1 min 10 s antes de sair.");
						Stopwatch stopwatch = Stopwatch.StartNew();
						while (!stop && stopwatch.Elapsed.TotalSeconds < 70.0)
						{
							status("DG " + Cycles + " concluída · saindo em " + (70 - (int)stopwatch.Elapsed.TotalSeconds) + " s");
							Thread.Sleep(500);
						}
						if (stop)
						{
							break;
						}
						Say("esperando o jogo tirar da DG (fim da contagem).");
						Stopwatch stopwatch2 = Stopwatch.StartNew();
						while (!stop && stopwatch2.Elapsed.TotalSeconds < 90.0 && all.Any((Account a2) => InsideOf(a2)))
						{
							Thread.Sleep(500);
						}
						if (stop)
						{
							break;
						}
						if (!all.Any((Account a2) => InsideOf(a2)))
						{
							Say("saíram da DG sozinhas (" + stopwatch2.Elapsed.TotalSeconds.ToString("F0") + " s).");
						}
						if (InsideOf(leader) && (!Learned().ContainsKey("saida") || !ReplayOn(leader, "saida", () => !InsideOf(leader), 12.0)) && InsideOf(leader))
						{
							TeleportToPorto2(leader);
						}
						if (InsideOf(leader))
						{
							Say("o líder não saiu da DG; parando.");
							stop = true;
							break;
						}
						foreach (Account a in all)
						{
							if (!stop && a.Pid != leader.Pid && InsideOf(a) && (!Learned().ContainsKey("saida") || !ReplayOn(a, "saida", () => !InsideOf(a), 12.0)) && InsideOf(a))
							{
								TeleportToPorto2(a);
							}
						}
						List<string> list = (from account in all
							where InsideOf(account)
							select account.Name).ToList();
						if (list.Count > 0)
						{
							Say("ainda dentro da DG: " + string.Join(", ", list.ToArray()) + " — parando antes da próxima.");
							stop = true;
						}
						if (!stop)
						{
							SideSteps();
						}
						if (!stop)
						{
							Say("começando a próxima DG.");
						}
						break;
					}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Say("parou com erro: " + ex.Message);
		}
		finally
		{
			stop = true;
			followOn = false;
			if(followerThread != null && followerThread.IsAlive) followerThread.Join();
			foreach (Account item7 in all)
			{
				StopMacro(item7);
				RotaLock.Clear(item7.Pid);
			}
			lock (clients)
			{
				foreach (GameClient value in clients.Values)
				{
					try
					{
						value.Dispose();
					}
					catch
					{
					}
				}
				clients.Clear();
			}
			status("Parado. DGs: " + Cycles + ((LastTime > TimeSpan.Zero) ? (" · última " + LastTime.ToString("h\\:mm\\:ss")) : ""));
			Say("parado.");
		}
	}

	private static string Describe(Step s)
	{
		return s.Kind switch
		{
			'E' => "entrada", 
			'N' => "NPC " + s.Data, 
			'W' => "andando", 
			'F' => "matando mobs", 
			'M' => "meditando " + s.Data + " s", 
			'B' => "boss " + s.Data, 
			'R' => "esperando ressurreição", 
			'U' => "buff", 
			'S' => "skill " + s.Data, 
			'X' => "fim da DG", 
			_ => s.Kind.ToString(), 
		};
	}
}
