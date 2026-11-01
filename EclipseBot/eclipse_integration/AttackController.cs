using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class AttackController
{
	private sealed class Session
	{
		public CharacterRef Character;

		public uint Target;

		public uint Combo;

		public bool Completed;

		public DateTime RetryAt;
	}

	private readonly IAttackRuntime runtime;

	private readonly Dictionary<int, Session> sessions = new Dictionary<int, Session>();

	private readonly HashSet<int> blocked;

	private readonly Action<string> note;

	private uint manualTarget;

	private uint lastTarget;

	private DateTime nextProbe;

	private bool targetValid;

	public AttackController(IAttackRuntime runtime, HashSet<int> blocked, Action<string> note)
	{
		this.runtime = runtime;
		this.blocked = blocked;
		this.note = note;
	}

	public uint ReadTarget(CharacterRef primary)
	{
		return runtime.Target(primary);
	}

	public static bool MonsterId(uint target)
	{
		return target != 0 && (target & 0xC0000000u) == 2147483648u;
	}

	public bool Engaged(int pid)
	{
		Session value;
		return sessions.TryGetValue(pid, out value) && ((value.Combo != 0 && !value.Completed) || DateTime.UtcNow < value.RetryAt);
	}

	public void StopAll()
	{
		Session[] array = sessions.Values.ToArray();
		foreach (Session s in array)
		{
			Stop(s);
		}
		sessions.Clear();
		manualTarget = (lastTarget = 0u);
		nextProbe = DateTime.MinValue;
		targetValid = false;
	}

	private void Stop(Session s)
	{
		if (s.Combo == 0 || s.Completed || blocked.Contains(s.Character.Pid))
		{
			return;
		}
		try
		{
			AttackResult attackResult = runtime.Stop(s.Character, s.Combo, s.Target);
			if (attackResult.Blocked)
			{
				blocked.Add(s.Character.Pid);
			}
			if (attackResult.Message != null)
			{
				note(attackResult.Message);
			}
		}
		catch (Exception ex)
		{
			blocked.Add(s.Character.Pid);
			note(s.Character.Name + ": falha encerrando ataque; reinicie esse cliente. " + ex.Message);
		}
	}

	public void Tick(CharacterRef primary, CharacterRef[] selected, bool enabled, bool automatic, bool request)
	{
		if (!enabled || primary == null || blocked.Contains(primary.Pid))
		{
			StopAll();
			return;
		}
		uint num;
		try
		{
			num = runtime.Target(primary);
		}
		catch
		{
			StopAll();
			return;
		}
		if (!MonsterId(num))
		{
			StopAll();
			return;
		}
		if (num != lastTarget)
		{
			manualTarget = 0u;
			nextProbe = DateTime.MinValue;
			targetValid = false;
			lastTarget = num;
		}
		if (request && !automatic)
		{
			manualTarget = num;
		}
		bool flag = automatic || manualTarget == num;
		KeyValuePair<int, Session>[] array = sessions.ToArray();
		foreach (KeyValuePair<int, Session> pair in array)
		{
			KeyValuePair<int, Session> keyValuePair;
			if (flag)
			{
				keyValuePair = pair;
				if (keyValuePair.Value.Target == num && selected.Any((CharacterRef c) =>
				{
					string key = c.Key;
					KeyValuePair<int, Session> keyValuePair2 = pair;
					return key == keyValuePair2.Value.Character.Key;
				}))
				{
					continue;
				}
			}
			keyValuePair = pair;
			Stop(keyValuePair.Value);
			Dictionary<int, Session> dictionary = sessions;
			keyValuePair = pair;
			dictionary.Remove(keyValuePair.Key);
		}
		if (!flag || selected.Length == 0)
		{
			return;
		}
		if (DateTime.UtcNow >= nextProbe)
		{
			nextProbe = DateTime.UtcNow.AddSeconds(1.0);
			try
			{
				targetValid = runtime.ValidTarget(primary, num);
			}
			catch (AttackBlocked attackBlocked)
			{
				blocked.Add(attackBlocked.Pid);
				note(attackBlocked.Message);
				targetValid = false;
			}
			catch (Exception ex)
			{
				note("Ataque: " + ex.Message);
				targetValid = false;
			}
		}
		if (!targetValid)
		{
			Session[] array2 = sessions.Values.ToArray();
			foreach (Session s in array2)
			{
				Stop(s);
			}
			sessions.Clear();
			manualTarget = 0u;
			if (request)
			{
				note("Ataque: o alvo do principal não está disponível ou não é atacável.");
			}
			return;
		}
		foreach (CharacterRef characterRef in selected)
		{
			if (blocked.Contains(characterRef.Pid))
			{
				continue;
			}
			bool flag2 = false;
			if (sessions.TryGetValue(characterRef.Pid, out var s))
			{
				if (!s.Completed)
				{
					try
					{
						if (!runtime.Running(characterRef, s.Combo, s.Target))
						{
							s.Completed = true;
						}
					}
					catch
					{
						Stop(s);
						s.Completed = true;
					}
				}
				if (!s.Completed)
				{
					continue;
				}
				if (automatic)
				{
					if (DateTime.UtcNow < s.RetryAt)
					{
						continue;
					}
					flag2 = true;
				}
				else if (!request)
				{
					continue;
				}
				sessions.Remove(characterRef.Pid);
			}
			if (runtime.Target(primary) != num)
			{
				break;
			}
			try
			{
				AttackResult attackResult = runtime.Start(characterRef, num);
				if (attackResult.Blocked)
				{
					blocked.Add(characterRef.Pid);
				}
				if (attackResult.Message != null && (!flag2 || attackResult.Combo == 0 || attackResult.Blocked))
				{
					note(attackResult.Message);
				}
				sessions[characterRef.Pid] = new Session
				{
					Character = characterRef,
					Target = num,
					Combo = attackResult.Combo,
					Completed = (attackResult.Combo == 0),
					RetryAt = ((attackResult.Combo == 0) ? DateTime.UtcNow.AddSeconds(3.0) : DateTime.MinValue)
				};
			}
			catch (Exception ex)
			{
				if (!flag2)
				{
					note(characterRef.Name + ": " + ex.Message);
				}
				sessions[characterRef.Pid] = new Session
				{
					Character = characterRef,
					Target = num,
					Completed = true,
					RetryAt = DateTime.UtcNow.AddSeconds(3.0)
				};
			}
		}
	}
}
