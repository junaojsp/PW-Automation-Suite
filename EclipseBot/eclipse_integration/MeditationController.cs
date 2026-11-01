using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class MeditationController
{
	private sealed class Pending
	{
		public string Key;

		public bool Desired;

		public bool Reported;

		public bool WasMatched;

		public int Attempts;

		public DateTime Next;
	}

	private readonly IMeditationRuntime runtime;

	private readonly HashSet<int> blocked;

	private readonly Action<string> note;

	private readonly Dictionary<int, Pending> attempts = new Dictionary<int, Pending>();

	private readonly HashSet<int> seated = new HashSet<int>();

	private string source = "";

	public bool PrimarySitting;

	public MeditationController(IMeditationRuntime runtime, HashSet<int> blocked, Action<string> note)
	{
		this.runtime = runtime;
		this.blocked = blocked;
		this.note = note;
	}

	public bool Holds(int pid)
	{
		Pending value;
		return seated.Contains(pid) || (attempts.TryGetValue(pid, out value) && value.Desired && PrimarySitting);
	}

	public void Tick(CharacterRef primary, CharacterRef[] selected, bool enabled)
	{
		if (primary == null)
		{
			source = "";
			attempts.Clear();
			seated.Clear();
			PrimarySitting = false;
			return;
		}
		if (source != primary.Key)
		{
			source = primary.Key;
			attempts.Clear();
		}
		try
		{
			PrimarySitting = runtime.Sitting(primary);
		}
		catch
		{
			PrimarySitting = false;
			return;
		}
		int[] array = attempts.Keys.Where((int id) => !selected.Any((CharacterRef c) => c.Pid == id)).ToArray();
		foreach (int key in array)
		{
			attempts.Remove(key);
		}
		seated.Clear();
		CharacterRef[] array2 = selected;
		foreach (CharacterRef characterRef in array2)
		{
			if (characterRef.Pid == primary.Pid || blocked.Contains(characterRef.Pid))
			{
				continue;
			}
			bool flag;
			try
			{
				flag = runtime.Sitting(characterRef);
			}
			catch
			{
				continue;
			}
			if (flag)
			{
				seated.Add(characterRef.Pid);
			}
			if (!enabled)
			{
				attempts.Remove(characterRef.Pid);
				continue;
			}
			if (!attempts.TryGetValue(characterRef.Pid, out var value) || value.Key != characterRef.Key || value.Desired != PrimarySitting)
			{
				Pending pending = new Pending();
				pending.Key = characterRef.Key;
				pending.Desired = PrimarySitting;
				value = pending;
				attempts[characterRef.Pid] = value;
			}
			if (flag == value.Desired)
			{
				value.WasMatched = true;
				if (value.Attempts > 0 && !value.Reported)
				{
					note(characterRef.Name + (flag ? ": meditação confirmada." : ": levantou; comandos liberados."));
					value.Reported = true;
				}
				continue;
			}
			if (value.WasMatched)
			{
				value.WasMatched = false;
				value.Attempts = 0;
				value.Reported = false;
				value.Next = DateTime.MinValue;
			}
			if (value.Attempts >= 3)
			{
				if (!value.Reported)
				{
					note(characterRef.Name + ": não confirmou " + (value.Desired ? "meditação" : "levantar") + " após 3 tentativas. Confira o estado do personagem.");
					value.Reported = true;
				}
			}
			else if (!(DateTime.UtcNow < value.Next))
			{
				if (runtime.Sitting(primary) != value.Desired)
				{
					break;
				}
				value.Attempts++;
				value.Next = DateTime.UtcNow.AddMilliseconds(1500.0);
				MeditationResult meditationResult = runtime.Set(characterRef, value.Desired);
				if (meditationResult.Blocked)
				{
					blocked.Add(characterRef.Pid);
					note(characterRef.Name + ": ponte de meditação não confirmou remoção; reinicie esse cliente.");
				}
				else if (meditationResult.Error != null)
				{
					note(characterRef.Name + ": meditação · " + meditationResult.Error);
				}
				else
				{
					note(characterRef.Name + (value.Desired ? ": acompanhando meditação do principal." : ": acompanhando o principal ao levantar."));
				}
			}
		}
	}
}
