using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal sealed class CharacterMemory : IDisposable
{
	private struct Region
	{
		public uint Base;

		public uint Allocation;

		public uint Protect;

		public uint Size;

		public uint State;

		public uint Protection;

		public uint Type;
	}

	private static readonly Dictionary<int, List<uint>> dialogCache = new Dictionary<int, List<uint>>();

	private static readonly Dictionary<int, DateTime> lastScan = new Dictionary<int, DateTime>();

	private static readonly ConcurrentDictionary<string, List<uint>> questWindowCache = new ConcurrentDictionary<string, List<uint>>();

	private IntPtr handle;

	public uint Module;

	public uint Host;

	public uint Id;

	public string Name;

	public float X;

	public float Y;

	public float Z;

	public uint SelectedTarget => U(Host + 1532);

	public bool Sitting => (U(Host + 2004) & 0x20) != 0;

	public uint Team => U(Host + 2072);

	public int Profession
	{
		get
		{
			try
			{
				int num = (int)U(Host + 1868);
				return (num >= 0 && num < 16) ? num : (-1);
			}
			catch
			{
				return -1;
			}
		}
	}

	public bool AttackConfigured()
	{
		uint num = U(U(Module + 10192652) + 24);
		byte[] b = Read(num + 258, 17);
		return Enumerable.Range(0, 8).Any((int i) =>
		{
			short num2 = BitConverter.ToInt16(b, 1 + 2 * i);
			return num2 > 0 || num2 == -1;
		});
	}

	public bool OwnedAttackRunning(uint combo, uint target)
	{
		return combo != 0 && RunningMacro() == combo && U(combo + 28) == 0 && U(combo + 44) == target && Read(combo + 36, 1)[0] == 0;
	}

	public short[] BuffMacroSkills()
	{
		uint num = U(U(Module + 10192652) + 24);
		byte[] bytes = Read(num + 258 + 17, 17);
		return (from i in Enumerable.Range(0, 8)
			select BitConverter.ToInt16(bytes, 1 + 2 * i)).ToArray();
	}

	public bool BuffMacroConfigured()
	{
		return BuffMacroSkills().Any((short id) => id > 0 || id == -1);
	}

	public uint RunningMacro()
	{
		uint num = U(Host + 7484);
		if (num == 0)
		{
			return 0u;
		}
		if (U(num) != Module + 9328264 || U(num + 4) != Host)
		{
			throw new Exception("Estado da macro indisponível.");
		}
		return num;
	}

	public bool AnyMacroRunning()
	{
		uint num = RunningMacro();
		return num != 0 && Read(num + 36, 1)[0] == 0;
	}

	public bool BuffMacroRunning()
	{
		uint num = RunningMacro();
		return num != 0 && U(num + 28) == 1 && Read(num + 36, 1)[0] == 0;
	}

	[DllImport("kernel32.dll")]
	private static extern uint VirtualQueryEx(IntPtr h, IntPtr a, out Region m, uint n);

	public PartySnapshot ReadParty()
	{
		uint team = Team;
		if (team == 0)
		{
			return null;
		}
		if (U(team) != Module + 9356216)
		{
			throw new Exception("Estrutura da PT indisponível.");
		}
		PartySnapshot partySnapshot = new PartySnapshot();
		partySnapshot.Leader = U(team + 4);
		PartySnapshot partySnapshot2 = partySnapshot;
		uint num = U(team + 24);
		uint num2 = U(team + 20);
		if (num > 64)
		{
			throw new Exception("Lista da PT inválida.");
		}
		for (uint num3 = 0u; num3 < num; num3++)
		{
			uint num4 = U(num2 + 4 * num3);
			if (num4 == 0 || U(num4) != Module + 9356208)
			{
				throw new Exception("Membro da PT indisponível.");
			}
			partySnapshot2.Members.Add(U(num4 + 12));
		}
		return partySnapshot2;
	}

	private PartyInvitation CheckInvite(uint address, uint inviter)
	{
		try
		{
			byte[] array = Read(address, 260);
			if (BitConverter.ToUInt32(array, 0) != Module + 9291332 || array[144] == 0 || BitConverter.ToUInt32(array, 252) != inviter)
			{
				return null;
			}
			string text = Encoding.ASCII.GetString(Read(BitConverter.ToUInt32(array, 76), 32));
			char[] separator = new char[1];
			string text2 = text.Split(separator)[0];
			if (text2 != "Game_InviteGroup")
			{
				return null;
			}
			PartyInvitation partyInvitation = new PartyInvitation();
			partyInvitation.Address = address;
			partyInvitation.Inviter = inviter;
			partyInvitation.Ticket = BitConverter.ToUInt32(array, 256);
			return partyInvitation;
		}
		catch
		{
			return null;
		}
	}

	public PartyInvitation FindPartyInvitation(int pid, uint inviter)
	{
		if (!dialogCache.TryGetValue(pid, out var value))
		{
			value = new List<uint>();
			dialogCache[pid] = value;
		}
		foreach (uint item in value)
		{
			PartyInvitation partyInvitation = CheckInvite(item, inviter);
			if (partyInvitation != null)
			{
				return partyInvitation;
			}
		}
		if (lastScan.TryGetValue(pid, out var value2) && (DateTime.UtcNow - value2).TotalSeconds < 1.0)
		{
			return null;
		}
		lastScan[pid] = DateTime.UtcNow;
		List<uint> list = new List<uint>();
		uint num = Module + 9291332;
		uint num2 = 65536u;
		Region m;
		while (num2 < 2147418112 && VirtualQueryEx(handle, new IntPtr((int)num2), out m, 28u) != 0)
		{
			ulong num3 = (ulong)m.Base + (ulong)m.Size;
			if (num3 <= num2 || num3 > 2147418112)
			{
				break;
			}
			num2 = (uint)num3;
			if (m.State != 4096 || m.Type != 131072 || (m.Protection & 0x101) != 0)
			{
				continue;
			}
			for (uint num4 = 0u; num4 < m.Size; num4 += 1048576)
			{
				int size = (int)Math.Min(1048576u, m.Size - num4);
				byte[] array;
				try
				{
					array = Read(m.Base + num4, size);
				}
				catch
				{
					continue;
				}
				for (int i = 0; i + 4 <= array.Length; i += 4)
				{
					if (BitConverter.ToUInt32(array, i) == num)
					{
						list.Add(m.Base + num4 + (uint)i);
					}
				}
			}
		}
		dialogCache[pid] = list;
		foreach (uint item2 in list)
		{
			PartyInvitation partyInvitation = CheckInvite(item2, inviter);
			if (partyInvitation != null)
			{
				return partyInvitation;
			}
		}
		return null;
	}

	public ushort[] ActiveQuestIds()
	{
		uint num = U(Host + 7460);
		if (U(num) != Module + 9384568 || U(num + 4) != Host)
		{
			throw new Exception("Interface de missoes invalida");
		}
		uint at = U(num + 8);
		int num2 = Read(at, 8)[0];
		if (num2 > 175)
		{
			throw new Exception("Contagem de missoes invalida");
		}
		byte[] value = Read(at, 8 + 32 * num2);
		if (Read(at, 1)[0] != num2)
		{
			throw new Exception("Lista de missoes atualizando");
		}
		List<ushort> list = new List<ushort>();
		for (int i = 0; i < num2; i++)
		{
			list.Add(BitConverter.ToUInt16(value, 8 + 32 * i));
		}
		return list.ToArray();
	}

	private static bool QuestWindowType(uint v)
	{
		return v == 9293192 || v == 9312760 || v == 9312916 || v == 9313072;
	}

	public List<uint> QuestWindows(CancellationToken token)
	{
		string key = Host.ToString("X8") + ":" + Module + ":" + Id;
		if (questWindowCache.TryGetValue(key, out var value))
		{
			List<uint> list = new List<uint>();
			foreach (uint item in value)
			{
				try
				{
					if (QuestWindowType(U(item) - Module))
					{
						list.Add(item);
					}
				}
				catch
				{
				}
			}
			if (list.Count > 0)
			{
				return list;
			}
		}
		List<uint> list2 = new List<uint>();
		uint num = 65536u;
		while (num < 2147418112)
		{
			token.ThrowIfCancellationRequested();
			if (VirtualQueryEx(handle, new IntPtr((int)num), out var m, 28u) == 0)
			{
				break;
			}
			ulong num2 = (ulong)m.Base + (ulong)m.Size;
			if (num2 <= num || num2 > 2147418112)
			{
				break;
			}
			num = (uint)num2;
			if (m.State != 4096 || m.Type != 131072 || (m.Protection & 0x101) != 0)
			{
				continue;
			}
			for (uint num3 = 0u; num3 < m.Size; num3 += 1048576)
			{
				token.ThrowIfCancellationRequested();
				int num4 = (int)Math.Min(1048576u, m.Size - num3);
				byte[] value2;
				try
				{
					value2 = Read(m.Base + num3, num4);
				}
				catch
				{
					continue;
				}
				for (int i = 0; i + 4 <= num4; i += 4)
				{
					if (!QuestWindowType(BitConverter.ToUInt32(value2, i) - Module))
					{
						continue;
					}
					uint current = m.Base + num3 + (uint)i;
					try
					{
						uint at = U(current + 76);
						string text = Encoding.ASCII.GetString(Read(at, 48));
						char[] separator = new char[1];
						string text2 = text.Split(separator)[0];
						if (text2.StartsWith("Win_"))
						{
							list2.Add(current);
						}
					}
					catch
					{
					}
				}
			}
		}
		questWindowCache[key] = list2;
		return list2;
	}

	public List<uint> NpcDialogs(CancellationToken token)
	{
		return (from d in QuestWindows(token)
			where U(d) == Module + 9293192
			select d).ToList();
	}

	public bool NpcVisible(uint dialog)
	{
		return U(dialog) == Module + 9293192 && QuestWindowVisible(dialog);
	}

	public bool IsNpcDialog(uint dialog)
	{
		return U(dialog) == Module + 9293192;
	}

	public bool NpcInteractionActive(uint dialog)
	{
		if (!IsNpcDialog(dialog))
		{
			return false;
		}
		return U(U(dialog + 52) + 2644) != 0 || U(Host + 3836) != 0 || Read(Host + 3850, 1)[0] != 0;
	}

	public bool QuestWindowVisible(uint dialog)
	{
		return QuestWindowType(U(dialog) - Module) && Read(dialog + 144, 1)[0] != 0;
	}

	public CharacterMemory(int pid)
	{
		try
		{
			using Process process = Process.GetProcessById(pid);
			Module = InternalBridge.Addr(process.MainModule.BaseAddress);
			handle = Win.OpenProcess(1040u, inherit: false, pid);
			if (handle == IntPtr.Zero)
			{
				throw new Exception("Acesso ao personagem indisponível.");
			}
			uint num = U(Module + 10192652);
			uint num2 = U(num + 28);
			Host = U(num2 + 52);
			Id = U(Host + 1264);
			if (Id < 2)
			{
				throw new Exception("Personagem ainda não entrou no mundo.");
			}
			uint at = U(Host + 1864);
			string text = Encoding.Unicode.GetString(Read(at, 64));
			char[] separator = new char[1];
			Name = text.Split(separator)[0];
			if (Name.Length == 0 || Name.Any((char c) => char.IsControl(c)))
			{
				throw new Exception("Nome do personagem indisponível.");
			}
			X = F(Host + 60);
			Y = F(Host + 64);
			Z = F(Host + 68);
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	public byte[] Read(uint at, int size)
	{
		byte[] array = new byte[size];
		if (!Win.ReadProcessMemory(handle, new IntPtr((int)at), array, new UIntPtr((uint)size), out var read) || read.ToUInt32() != size)
		{
			throw new Exception("Personagem carregando ou desconectado.");
		}
		return array;
	}

	public uint U(uint at)
	{
		return BitConverter.ToUInt32(Read(at, 4), 0);
	}

	private float F(uint at)
	{
		return BitConverter.ToSingle(Read(at, 4), 0);
	}

	public Stone FindStone()
	{
		uint num = U(Host + 4344);
		uint num2 = U(num + 16);
		uint num3 = U(num + 12);
		if (num2 > 256)
		{
			throw new Exception("Inventário indisponível.");
		}
		for (uint num4 = 0u; num4 < num2; num4++)
		{
			uint num5 = U(num3 + 4 * num4);
			if (num5 != 0 && U(num5 + 12) == 36905 && U(num5) == Module + 9340204)
			{
				uint num6 = U(num5 + 20);
				if (num6 != 0)
				{
					Stone stone = new Stone();
					stone.Slot = (ushort)num4;
					stone.Count = num6;
					return stone;
				}
			}
		}
		throw new Exception("Sem Pedra de Teleporte no inventário.");
	}

	public string DescribeSlot(int slot)
	{
		uint num = U(Host + 4344);
		uint num2 = U(num + 16);
		uint num3 = U(num + 12);
		if (slot < 0 || slot >= num2 || num2 > 256)
		{
			return "slot fora do inventário";
		}
		uint num4 = U(num3 + (uint)(4 * slot));
		if (num4 == 0)
		{
			return "slot vazio";
		}
		return "item " + U(num4 + 12) + ", tipo +" + (U(num4) - Module).ToString("X") + ", campo " + U(num4 + 20);
	}

	public ushort FindSpecialTeleport()
	{
		uint num = U(Host + 4344);
		uint num2 = U(num + 16);
		uint num3 = U(num + 12);
		if (num2 > 256)
		{
			throw new Exception("Inventário indisponível.");
		}
		for (uint num4 = 0u; num4 < num2; num4++)
		{
			uint num5 = U(num3 + 4 * num4);
			if (num5 != 0 && U(num5 + 12) == 52256 && U(num5) == Module + 9345412)
			{
				return (ushort)num4;
			}
		}
		throw new Exception("Sem Teleporte Especial no inventário.");
	}

	public Destination FindDestination(uint id)
	{
		uint num = U(Module + 10230016);
		uint num2 = U(Module + 10230028);
		if (num2 > 4096)
		{
			throw new Exception("Tabela de destinos inválida.");
		}
		for (uint num3 = 0u; num3 < num2; num3++)
		{
			byte[] array = Read(num + num3 * 88, 88);
			if (BitConverter.ToUInt32(array, 0) == id)
			{
				Destination destination = new Destination();
				destination.Id = id;
				string text = Encoding.Unicode.GetString(array, 4, 64);
				char[] separator = new char[1];
				destination.Name = text.Split(separator)[0];
				destination.X = BitConverter.ToSingle(array, 72);
				destination.Y = BitConverter.ToSingle(array, 76);
				destination.Z = BitConverter.ToSingle(array, 80);
				Destination destination2 = destination;
				if (float.IsNaN(destination2.X) || float.IsInfinity(destination2.X) || float.IsNaN(destination2.Y) || float.IsInfinity(destination2.Y) || float.IsNaN(destination2.Z) || float.IsInfinity(destination2.Z))
				{
					throw new Exception("Coordenadas inválidas.");
				}
				return destination2;
			}
		}
		throw new Exception("Destino não encontrado na tabela deste cliente.");
	}

	public bool At(Destination d)
	{
		return Math.Abs(X - d.X) < 10f && Math.Abs(Z - d.Z) < 10f && Math.Abs(Y - d.Y) < 20f;
	}

	public void Dispose()
	{
		if (handle != IntPtr.Zero)
		{
			Win.CloseHandle(handle);
			handle = IntPtr.Zero;
		}
	}
}
