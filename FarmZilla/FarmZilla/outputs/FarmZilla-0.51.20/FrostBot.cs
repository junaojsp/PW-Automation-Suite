// Adapted from the Bagaceira source supplied by the owner; validate in PW Avalon before publication.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using RotaAvalon;

internal sealed class FrostBot
{
	public enum Mode
	{
		Dragon,
		Wolf
	}

	public sealed class Account
	{
		public int Pid;

		public string Name;
	}

	private sealed class Npc
	{
		public uint Key;

		public uint Obj;

		public double X;

		public double Z;
	}

	private const uint DragonTpl = 24816u;

	private const uint BallTpl = 24833u;

	private const uint WolfTpl = 24820u;

	private readonly Mode mode;

	private readonly Action<string> note;

	private readonly List<Thread> threads = new List<Thread>();

	private volatile bool stop;

	private long fixedBoss = 0L;

	private long bossLastSeen = 0L;

	private readonly object claimLock = new object();

	private readonly Dictionary<string, double[]> claims = new Dictionary<string, double[]>();

	private int alive;

	public bool ManageLock = true;

	private static List<double[]> room;

	public bool Running => Volatile.Read(ref alive) > 0;

	public event Action Finished;

	public FrostBot(Mode mode, Action<string> note)
	{
		this.mode = mode;
		this.note = note;
	}

	public static string Title(Mode m)
	{
		return (m == Mode.Dragon) ? "Boss 1 · Dragão da Tempestade" : "Boss 2 · Lobo de Sangue Oculto";
	}

	public void Start(IEnumerable<Account> accounts)
	{
		string lib = RotaPaths.Bridge();
		var selectedAccounts = accounts.ToList();
		Interlocked.Exchange(ref alive, selectedAccounts.Count);
		foreach (Account account in selectedAccounts)
		{
			Account acc = account;
			if (ManageLock)
			{
				RotaLock.Set(acc.Pid, "Bot Frost");
			}

			Thread thread = new Thread(() =>
			{
				try
				{
					Run(acc, lib);
				}
				catch (Exception ex)
				{
					Say(acc.Name, "parou com erro: " + ex.Message);
				}
				finally
				{
					if (ManageLock)
					{
						RotaLock.Clear(acc.Pid);
					}
					if (Interlocked.Decrement(ref alive) == 0)
					{
						Finished?.Invoke();
					}
				}
			});
			thread.IsBackground = true;
			thread.Name = "Frost " + acc.Name;
			Thread thread2 = thread;
			threads.Add(thread2);
			thread2.Start();
		}
	}

	public void Stop()
	{
		stop = true;
	}

	public void StopAndWait(int ms)
	{
		stop = true;
		Thread[] array = threads.ToArray();
		foreach (Thread thread in array)
		{
			thread.Join(ms);
		}
	}

	private void Say(string who, string s)
	{
		note("Bot Frost · " + who + ": " + s);
	}

	private static double Dist(double ax, double az, double bx, double bz)
	{
		return Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
	}

	private static void Esc(int pid)
	{
		TeleportBridge teleportBridge = null;
		try
		{
			teleportBridge = new TeleportBridge(pid);
			teleportBridge.SendControl(new byte[2] { 42, 0 });
			teleportBridge.SendControl(new byte[2] { 8, 0 });
		}
		finally
		{
			teleportBridge?.Dispose();
		}
	}

	private static void StopMacro(int pid)
	{
		AttackBridge attackBridge = null;
		try
		{
			attackBridge = new AttackBridge(pid);
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

	private List<Npc> Scan(ProcessMemory m, uint world, uint wantBoss, out Npc boss)
	{
		List<Npc> list = new List<Npc>();
		boss = null;
		uint num = m.UInt32(world + 36);
		uint num2 = m.UInt32(num + 28);
		uint address = m.UInt32(num + 32);
		if (num2 < 1 || num2 > 65536)
		{
			return list;
		}
		byte[] array = m.Read(address, (int)(num2 * 4));
		HashSet<uint> hashSet = new HashSet<uint>();
		for (int i = 0; i < array.Length; i += 4)
		{
			uint num3 = BitConverter.ToUInt32(array, i);
			int num4 = 0;
			while (num3 != 0 && num4++ < 1000 && hashSet.Add(num3))
			{
				byte[] value;
				try
				{
					value = m.Read(num3, 12);
				}
				catch
				{
					break;
				}
				num3 = BitConverter.ToUInt32(value, 0);
				uint num5 = BitConverter.ToUInt32(value, 4);
				uint num6 = BitConverter.ToUInt32(value, 8);
				uint num7;
				try
				{
					num7 = m.UInt32(num5 + 284);
				}
				catch
				{
					continue;
				}
				if (num7 == wantBoss)
				{
					long num8 = Interlocked.Read(ref fixedBoss);
					if (num8 == 0 || num8 == num6)
					{
						Interlocked.CompareExchange(ref fixedBoss, num6, 0L);
						boss = new Npc
						{
							Key = num6,
							Obj = num5,
							X = m.Float(num5 + 60),
							Z = m.Float(num5 + 68)
						};
					}
				}
				else if (num7 == 24833 && mode == Mode.Dragon)
				{
					list.Add(new Npc
					{
						Key = num6,
						Obj = num5,
						X = m.Float(num5 + 60),
						Z = m.Float(num5 + 68)
					});
				}
			}
		}
		return list;
	}

	private void Run(Account a, string lib)
	{
		Dictionary<string, bool> verifiedFiles = new Dictionary<string, bool>();
		using GameClient gameClient = new GameClient(a.Pid, verifiedFiles);
		using ProcessMemory processMemory = new ProcessMemory(a.Pid);
		using Process process = Process.GetProcessById(a.Pid);
		using CharacterMemory characterMemory = new CharacterMemory(a.Pid);
		uint num = (uint)process.MainModule.BaseAddress.ToInt32();
		uint num2 = processMemory.UInt32(num + gameClient.RootRva);
		uint wantBoss = ((mode == Mode.Dragon) ? 24816u : 24820u);
		Stopwatch stopwatch = Stopwatch.StartNew();
		int hp = gameClient.Read().Hp;
		bool flag = false;
		bool flag2 = false;
		double num3 = 0.0;
		double arg = 0.0;
		double arg2 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 0.0;
		int num8 = 0;
		int num9 = 0;
		uint num10 = 0u;
		uint num11 = 0u;
		double num12 = 0.0;
		double bx = 0.0;
		double bz = 0.0;
		Dictionary<uint, double> dictionary = new Dictionary<uint, double>();
		bool flag3 = false;
		bool flag4 = false;
		double num13 = -1.0;
		double num14 = 0.0;
		int num15 = 0;
		double num16 = 0.0;
		double num17 = 0.0;
		bool flag5 = false;
		double num18 = 1000000000.0;
		double num19 = 0.0;
		double num20 = 0.0;
		double num21 = -100.0;
		int num22 = 0;
		Say(a.Name, "pronta (" + Title(mode) + "), vida " + hp);
		object obj = default;
		while (!stop)
		{
			ClientSnapshot clientSnapshot = gameClient.Read();
			double totalSeconds = stopwatch.Elapsed.TotalSeconds;
			if (hp - clientSnapshot.Hp >= 3500)
			{
				num9++;
				Say(a.Name, "levou " + (hp - clientSnapshot.Hp) + " de uma vez, vida " + clientSnapshot.Hp + (flag ? " (fugindo)" : ""));
			}
			hp = clientSnapshot.Hp;
			List<Npc> list = Scan(processMemory, processMemory.UInt32(num2 + 32), wantBoss, out var boss);
			if (boss != null)
			{
				Interlocked.Exchange(ref bossLastSeen, DateTime.UtcNow.Ticks);
				num16 = boss.X;
				num17 = boss.Z;
				flag5 = true;
			}
			long num23 = Interlocked.Read(ref bossLastSeen);
			if (num23 != 0 && (DateTime.UtcNow.Ticks - num23) / 10000000 > 8)
			{
				Say(a.Name, "boss sumiu em todas as contas (morto) - fim");
				break;
			}
			if (clientSnapshot.Hp <= 0)
			{
				if (!flag2)
				{
					Say(a.Name, "morta, esperando ressuscitar");
				}
				flag2 = true;
				flag = false;
				bool lockTaken = false;
				try
				{
					Monitor.Enter(obj = claimLock, ref lockTaken);
					claims.Remove(a.Name);
				}
				finally
				{
					if (lockTaken)
					{
						Monitor.Exit(obj);
					}
				}
				Thread.Sleep(300);
				continue;
			}
			if (flag2)
			{
				flag2 = false;
				Say(a.Name, "viva de novo");
			}
			uint num24 = (uint)Interlocked.Read(ref fixedBoss);
			double num25 = (flag5 ? Dist(num16, num17, clientSnapshot.X, clientSnapshot.Z) : 0.0);
			if (mode == Mode.Dragon)
			{
				List<Npc> live = new List<Npc>();
				bool flag6 = false;
				foreach (Npc item in list)
				{
					if (!dictionary.ContainsKey(item.Key))
					{
						dictionary[item.Key] = totalSeconds;
						flag6 = true;
					}
					if (totalSeconds - dictionary[item.Key] < 7.6)
					{
						live.Add(item);
					}
				}
				Func<double, double, bool> func = (double bx3, double bz3) => live.Any((Npc o) => Dist(o.X, o.Z, bx3, bz3) < 8.0);
				if ((flag6 || !flag) && (func(clientSnapshot.X, clientSnapshot.Z) || (flag && func(num4, num5))))
				{
					if (!flag)
					{
						arg = clientSnapshot.X;
						arg2 = clientSnapshot.Z;
						num8++;
					}
					flag = true;
					num3 = totalSeconds;
					double num26 = -1.0;
					num4 = clientSnapshot.X + 15f;
					num5 = clientSnapshot.Z;
					for (int num27 = 0; num27 < 16; num27++)
					{
						double num28 = (double)num27 * Math.PI / 8.0;
						double num29 = (double)clientSnapshot.X + Math.Cos(num28) * 15.0;
						double num30 = (double)clientSnapshot.Z + Math.Sin(num28) * 15.0;
						double num31 = 1000000000.0;
						foreach (Npc item2 in live)
						{
							num31 = Math.Min(num31, Dist(item2.X, item2.Z, num29, num30));
						}
						if (num31 > num26)
						{
							num26 = num31;
							num4 = num29;
							num5 = num30;
						}
					}
					try
					{
						Esc(a.Pid);
					}
					catch (Exception ex)
					{
						Say(a.Name, "ESC: " + ex.Message);
					}
					StopMacro(a.Pid);
					Stopwatch stopwatch2 = Stopwatch.StartNew();
					double num32 = 0.0;
					while (stopwatch2.Elapsed.TotalSeconds < 4.5 && !stop)
					{
						ClientSnapshot clientSnapshot2 = gameClient.Read();
						try
						{
							NativeBridge.Navigate(gameClient, num4, clientSnapshot2.Y, num5, lib);
						}
						catch
						{
						}
						Thread.Sleep(400);
						clientSnapshot2 = gameClient.Read();
						num32 = 1000000000.0;
						foreach (Npc item3 in live)
						{
							num32 = Math.Min(num32, Dist(item3.X, item3.Z, clientSnapshot2.X, clientSnapshot2.Z));
						}
						if (num32 >= 12.0 || Dist(num4, num5, clientSnapshot2.X, clientSnapshot2.Z) < 2.0)
						{
							break;
						}
					}
					Say(a.Name, "fugiu da bola: a " + num32.ToString("F1") + " em " + stopwatch2.Elapsed.TotalSeconds.ToString("F1") + " s");
					num6 = 1000000000.0;
				}
				else if (flag && totalSeconds - num3 >= 1.0 && !func(arg, arg2) && (!flag5 || !func(num16, num17)))
				{
					flag = false;
					num6 = totalSeconds;
				}
			}
			else
			{
				byte[] array = null;
				if (boss != null)
				{
					try
					{
						array = processMemory.Read(boss.Obj, 1024);
					}
					catch
					{
					}
				}
				bool flag7 = false;
				bool flag8 = false;
				if (array != null)
				{
					flag8 = (BitConverter.ToUInt32(array, 572) & 2) != 0;
					flag7 = BitConverter.ToUInt32(array, 788) == boss.Key;
				}
				if (flag8 && !flag4)
				{
					num13 = totalSeconds;
				}
				if (!flag8 && !flag)
				{
					num13 = -1.0;
				}
				bool flag9 = (flag7 && !flag3) || (flag8 && !flag4);
				flag3 = flag7;
				flag4 = flag8;
				if (flag9 && !flag && boss != null)
				{
					flag = true;
					num3 = totalSeconds;
					num8++;
					try
					{
						Esc(a.Pid);
					}
					catch (Exception ex)
					{
						Say(a.Name, "ESC: " + ex.Message);
					}
					double[] array2 = PickSpot(a.Name, num16, num17, clientSnapshot.X, clientSnapshot.Z);
					if (array2 != null)
					{
						try
						{
							NativeBridge.Navigate(gameClient, array2[0], clientSnapshot.Y, array2[1], lib);
						}
						catch
						{
						}
					}
					StopMacro(a.Pid);
					Stopwatch stopwatch2 = Stopwatch.StartNew();
					double bx2 = clientSnapshot.X;
					double bz2 = clientSnapshot.Z;
					double num33 = 0.0;
					bool flag10 = false;
					int num34 = 0;
					int[] array3 = new int[5] { 0, 1, -1, 2, -2 };
					ClientSnapshot clientSnapshot2;
					while (stopwatch2.Elapsed.TotalSeconds < 4.5 && !stop)
					{
						clientSnapshot2 = gameClient.Read();
						double x;
						double z;
						if (array2 != null && num34 == 0)
						{
							x = array2[0];
							z = array2[1];
						}
						else
						{
							double num35 = Math.Atan2((double)clientSnapshot2.Z - num17, (double)clientSnapshot2.X - num16);
							double num28 = num35 + (double)array3[num34 % array3.Length] * Math.PI / 4.0;
							x = (double)clientSnapshot2.X + Math.Cos(num28) * 18.0;
							z = (double)clientSnapshot2.Z + Math.Sin(num28) * 18.0;
						}
						if (!flag10)
						{
							try
							{
								Esc(a.Pid);
							}
							catch
							{
							}
						}
						try
						{
							NativeBridge.Navigate(gameClient, x, clientSnapshot2.Y, z, lib);
						}
						catch
						{
						}
						Thread.Sleep(flag10 ? 400 : 200);
						clientSnapshot2 = gameClient.Read();
						double num36 = Dist(clientSnapshot2.X, clientSnapshot2.Z, num16, num17);
						if (num36 >= 22.0)
						{
							break;
						}
						double num37 = Dist(clientSnapshot2.X, clientSnapshot2.Z, bx2, bz2);
						bx2 = clientSnapshot2.X;
						bz2 = clientSnapshot2.Z;
						double totalSeconds2 = stopwatch2.Elapsed.TotalSeconds;
						if (num37 >= 0.3)
						{
							flag10 = true;
							num33 = totalSeconds2;
						}
						else if (flag10 && totalSeconds2 - num33 >= 0.6)
						{
							num34++;
							num33 = totalSeconds2;
						}
						else if (!flag10 && totalSeconds2 >= 1.5)
						{
							num34++;
							num33 = totalSeconds2;
							flag10 = true;
						}
					}
					clientSnapshot2 = gameClient.Read();
					Say(a.Name, (flag7 ? "grito" : "aviso") + " do lobo: fugiu para " + Dist(clientSnapshot2.X, clientSnapshot2.Z, num16, num17).ToString("F1") + " em " + stopwatch2.Elapsed.TotalSeconds.ToString("F1") + " s");
					num6 = 1000000000.0;
				}
				if (flag && boss != null && num25 < 22.0 && totalSeconds - num14 >= 0.8 && (!(num13 >= 0.0) || !(totalSeconds - num13 >= 6.5)) && (!(num13 < 0.0) || !(totalSeconds - num3 >= 22.0)))
				{
					num14 = totalSeconds;
					double[] array4 = PickSpot(a.Name, num16, num17, clientSnapshot.X, clientSnapshot.Z);
					double x2;
					double z2;
					if (array4 != null)
					{
						x2 = array4[0];
						z2 = array4[1];
					}
					else
					{
						double num35 = Math.Atan2((double)clientSnapshot.Z - num17, (double)clientSnapshot.X - num16);
						x2 = (double)clientSnapshot.X + Math.Cos(num35) * 18.0;
						z2 = (double)clientSnapshot.Z + Math.Sin(num35) * 18.0;
					}
					try
					{
						NativeBridge.Navigate(gameClient, x2, clientSnapshot.Y, z2, lib);
					}
					catch
					{
					}
					num15++;
					if (num15 <= 3 || num15 % 10 == 0)
					{
						Say(a.Name, "lobo chegou a " + num25.ToString("F1") + ", fugindo de novo");
					}
				}
				if (flag && ((num13 >= 0.0 && totalSeconds - num13 >= 6.5) || (num13 < 0.0 && totalSeconds - num3 >= 22.0)))
				{
					flag = false;
					num6 = totalSeconds;
					bool lockTaken2 = false;
					try
					{
						Monitor.Enter(obj = claimLock, ref lockTaken2);
						claims.Remove(a.Name);
					}
					finally
					{
						if (lockTaken2)
						{
							Monitor.Exit(obj);
						}
					}
				}
			}
			if (!flag && num24 != 0 && flag5)
			{
				bool flag11 = false;
				uint num38 = 0u;
				try
				{
					num38 = characterMemory.RunningMacro();
					flag11 = num38 != 0 && processMemory.Read(num38 + 36, 1)[0] == 0;
				}
				catch
				{
				}
				if (num25 > 12.0 && !flag11 && totalSeconds >= num7)
				{
					num7 = totalSeconds + 0.5;
					if (num25 < num18 - 0.5)
					{
						num18 = num25;
						num19 = totalSeconds;
					}
					if (!(totalSeconds < num20))
					{
						if (totalSeconds - num19 >= 2.5)
						{
							double num39 = Math.Atan2(num17 - (double)clientSnapshot.Z, num16 - (double)clientSnapshot.X);
							int[] array5 = new int[7] { 2, -2, 3, -3, 4, 1, -1 };
							double num28 = num39 + (double)array5[num22 % array5.Length] * Math.PI / 4.0;
							num22++;
							try
							{
								NativeBridge.Navigate(gameClient, (double)clientSnapshot.X + Math.Cos(num28) * 8.0, clientSnapshot.Y, (double)clientSnapshot.Z + Math.Sin(num28) * 8.0, lib);
							}
							catch
							{
							}
							num20 = totalSeconds + 1.2;
							num19 = totalSeconds;
							num18 = num25;
							if (totalSeconds - num21 >= 20.0)
							{
								num21 = totalSeconds;
								Say(a.Name, "presa voltando para o lobo (a " + num25.ToString("F0") + "): tentando sair pelo lado");
							}
						}
						else
						{
							try
							{
								NativeBridge.Navigate(gameClient, num16 + ((double)clientSnapshot.X - num16) / num25 * 3.0, clientSnapshot.Y, num17 + ((double)clientSnapshot.Z - num17) / num25 * 3.0, lib);
							}
							catch
							{
							}
						}
					}
				}
				else if (num25 <= 12.0 || flag11)
				{
					num18 = 1000000000.0;
					num19 = totalSeconds;
				}
				if (boss != null && num25 <= 40.0 && totalSeconds >= num6)
				{
					num6 = totalSeconds + 0.15;
					string text = null;
					if (!flag11)
					{
						text = "sem macro";
					}
					else if (characterMemory.SelectedTarget != num24)
					{
						text = "TG";
					}
					else
					{
						uint num40 = 0u;
						try
						{
							num40 = processMemory.UInt32(num38 + 32);
						}
						catch
						{
						}
						bool flag12 = Dist(clientSnapshot.X, clientSnapshot.Z, bx, bz) > 0.3;
						bx = clientSnapshot.X;
						bz = clientSnapshot.Z;
						if (num38 != num10 || num40 != num11 || flag12)
						{
							num10 = num38;
							num11 = num40;
							num12 = totalSeconds;
						}
						else if (totalSeconds - num12 >= 3.0)
						{
							text = "parada";
						}
					}
					if (text != null)
					{
						AttackBridge attackBridge = null;
						try
						{
							attackBridge = new AttackBridge(a.Pid);
							if (text != "sem macro")
							{
								attackBridge.StopMacro(-1);
							}
							attackBridge.StartMacroOnMonster(num24, 0);
							num12 = totalSeconds;
						}
						catch
						{
						}
						finally
						{
							attackBridge?.Dispose();
						}
						num6 = totalSeconds + 0.5;
					}
				}
			}
			Thread.Sleep(25);
		}
		StopMacro(a.Pid);
		Say(a.Name, "fim: " + num8 + " fuga(s), " + num9 + " golpe(s) de 3500+, vida " + gameClient.Read().Hp);
	}

	private static List<double[]> Room()
	{
		if (room != null)
		{
			return room;
		}
		List<double[]> list = new List<double[]>();
		string[] array = "152.9,358.1;150.5,358.0;148.3,358.0;145.8,357.9;143.7,357.8;141.5,357.7;139.2,357.6;137.0,357.6;134.8,357.6;132.7,357.5;130.5,357.5;128.3,357.4;126.0,357.4;123.8,357.4;121.6,357.3;119.5,357.3;117.1,357.2;115.1,357.2;115.1,360.1;115.2,362.1;116.9,363.4;119.0,363.7;121.2,363.9;123.3,363.8;125.2,362.7;127.1,361.7;129.2,362.2;131.4,362.9;133.0,364.4;134.5,365.9;135.9,367.5;137.4,369.1;138.8,371.0;139.6,373.0;138.9,375.0;138.1,377.0;137.2,379.2;136.6,381.3;136.9,383.4;138.2,385.4;139.9,386.4;141.6,386.4;143.4,386.4;145.3,386.4;147.0,386.4;150.8,386.4;152.7,386.4;154.6,386.4;156.7,386.4;158.6,386.4;160.5,386.4;162.6,386.4;164.5,386.4;166.4,386.4;168.5,386.4;170.4,386.4;172.3,386.4;174.2,386.4;176.2,386.4;178.2,386.4;180.2,386.4;182.1,386.4;184.0,386.4;185.9,386.4;187.8,386.4;189.7,386.4;191.8,386.4;193.7,386.4;195.6,386.4;197.5,386.4;199.5,386.4;201.5,386.4;203.4,386.4;205.4,386.4;207.4,386.4;209.4,386.4;211.3,386.4;213.2,386.4;215.3,386.4;217.2,386.4;219.1,386.4;221.1,386.4;223.0,386.4;227.0,386.4;230.9,386.4;233.0,386.4;234.9,386.4;236.3,386.1;236.1,384.3;236.0,382.4;236.1,380.9;236.2,378.9;238.0,379.2;240.2,380.0;241.6,380.8;242.6,381.0;242.5,378.4;242.5,376.2;242.5,373.3;242.5,371.2;242.5,369.8;242.6,368.7;242.5,366.9;240.3,366.8;238.8,366.7;236.9,366.1;235.4,364.6;234.5,362.6;234.5,360.4;234.6,358.3;235.3,356.2;235.6,354.2;235.7,352.0;235.7,349.7;235.7,347.5;236.5,345.5;237.8,343.9;239.9,344.5;241.9,345.4;242.5,343.9;242.5,342.3;242.5,340.6;242.6,338.8;242.4,336.7;241.8,334.5;240.9,332.8;237.6,332.4;236.6,333.2;234.1,334.1;232.4,333.0;229.9,330.4;228.6,328.7;226.9,327.4;226.4,325.5;226.6,323.3;224.1,323.1;222.1,323.1;219.7,323.4;217.6,323.7;215.4,323.9;213.1,323.7;211.0,323.3;208.9,323.1;206.8,323.1;204.8,323.1;202.8,323.1;200.8,323.1;198.7,323.1;196.7,323.1;194.7,323.1;192.6,323.1;190.6,323.1;188.7,323.1;186.5,323.1;184.5,323.1;182.8,324.3;181.4,325.9;179.9,327.6;179.5,329.2;178.5,330.5;176.3,330.6;174.2,330.6;172.0,330.5;170.8,330.4;169.6,331.7;167.3,331.8;165.2,331.5;163.2,330.5;161.3,329.3;159.4,328.3;157.2,327.9;154.9,327.6;152.7,327.5;150.4,327.5;148.1,327.4;145.9,327.2;143.6,327.0;141.2,326.8;139.1,326.5;137.1,325.7;135.1,324.4;133.2,323.3;131.4,323.1;129.5,323.1;127.4,323.1;125.2,323.1;123.0,323.4;122.0,325.2;121.7,327.2;121.4,329.1;120.3,331.7;119.2,333.6;117.8,335.1;117.2,337.2;115.7,338.7;115.1,340.2;115.6,342.3;115.7,344.3;115.1,346.2;115.1,348.1;115.8,349.9;116.6,351.9;117.0,353.9;118.7,355.3;120.5,356.5;128.4,359.9;130.9,363.7;132.1,365.4;133.5,367.0;136.4,370.4;138.2,374.0;139.1,376.1;139.9,378.2;140.7,380.2;141.6,382.1;142.9,384.1;150.2,384.1;151.1,382.1;151.9,380.1;152.8,377.9;153.6,375.9;154.3,373.9;155.1,371.9;156.0,369.7;155.8,367.7;155.1,365.6;154.3,363.4;153.6,361.4;153.0,359.3;151.6,355.0;150.9,352.9;151.1,350.8;152.1,348.9;153.1,346.9;154.2,344.9;155.2,342.9;156.3,340.9;157.4,338.8;158.6,337.0;160.0,335.3;161.5,333.7;163.4,333.1;165.6,333.1;172.4,332.8;174.6,332.9;176.8,333.1;178.9,333.4;181.0,334.1;182.8,335.2;184.2,336.8;184.9,338.9;185.4,341.0;187.3,342.3;189.1,343.5;192.7,346.0;194.6,347.4;196.3,348.6;198.1,349.9;199.8,351.2;201.7,352.6;203.5,353.8;204.7,355.5;205.0,357.8;205.6,360.1;206.8,361.9;208.6,362.9;210.7,363.5;212.9,363.8;215.0,363.9;217.2,363.7;219.3,363.3;221.7,362.9;223.9,362.4;226.1,361.9;228.3,361.2;229.4,359.4;228.8,357.4;227.1,355.8;225.4,354.4;223.7,353.1;221.9,351.8;220.0,350.6;217.9,350.0;215.8,349.4;213.7,349.0;211.3,348.7;209.2,348.9;207.2,349.7;205.2,350.7;203.1,351.7;199.2,353.6;197.3,354.5;195.3,355.5;194.1,356.9;192.8,358.1;189.8,358.9;188.1,359.2;186.6,359.4;185.0,359.7;181.7,360.2;180.2,360.4;178.6,360.7;176.9,360.7;173.8,360.2;172.1,359.8;170.6,359.4;169.1,358.9;167.2,358.3;165.2,357.4;163.3,356.1;161.1,355.8;159.1,355.1;157.0,354.4;155.0,354.2;150.8,355.9;145.5,355.9;144.8,353.9;144.1,351.8;144.8,349.8;145.9,347.8;146.9,345.9;148.0,344.0;149.2,342.1;150.5,340.2;151.8,338.4".Split(';');
		foreach (string text in array)
		{
			string[] array2 = text.Split(',');
			if (array2.Length == 2)
			{
				list.Add(new double[2]
				{
					double.Parse(array2[0], CultureInfo.InvariantCulture),
					double.Parse(array2[1], CultureInfo.InvariantCulture)
				});
			}
		}
		double num = list.Average((double[] q) => q[0]);
		double num2 = list.Average((double[] q) => q[1]);
		foreach (double[] item in list)
		{
			item[0] = num + (item[0] - num) * 0.85;
			item[1] = num2 + (item[1] - num2) * 0.85;
		}
		return room = list;
	}

	private double[] PickSpot(string who, double wx, double wz, double mx, double mz)
	{
		double[] array = null;
		double num = 1000000000.0;
		lock (claimLock)
		{
			for (int i = 0; i < 2; i++)
			{
				if (array != null)
				{
					break;
				}
				foreach (double[] item in Room())
				{
					if (Dist(item[0], item[1], wx, wz) < 23.5)
					{
						continue;
					}
					if (i == 0)
					{
						double num2 = (item[0] - wx) * (mx - wx) + (item[1] - wz) * (mz - wz);
						if (num2 < 0.0 && Dist(mx, mz, wx, wz) > 2.0)
						{
							continue;
						}
					}
					bool flag = false;
					foreach (KeyValuePair<string, double[]> claim in claims)
					{
						if (claim.Key != who && Dist(claim.Value[0], claim.Value[1], item[0], item[1]) < 5.0)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						double num3 = Dist(item[0], item[1], mx, mz);
						if (num3 < num)
						{
							num = num3;
							array = item;
						}
					}
				}
			}
			if (array != null)
			{
				claims[who] = array;
			}
		}
		return array;
	}
}
