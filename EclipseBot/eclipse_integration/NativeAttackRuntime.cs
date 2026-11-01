using System;

internal sealed class NativeAttackRuntime : IAttackRuntime
{
	private static CharacterMemory Open(CharacterRef c)
	{
		CharacterMemory characterMemory = new CharacterMemory(c.Pid);
		if (characterMemory.Id != c.Id)
		{
			characterMemory.Dispose();
			throw new Exception("Personagem mudou; atualize a lista.");
		}
		return characterMemory;
	}

	public uint Target(CharacterRef c)
	{
		using CharacterMemory characterMemory = Open(c);
		return characterMemory.SelectedTarget;
	}

	private static AttackResult Use(CharacterRef c, Action<AttackBridge, AttackResult> action)
	{
		AttackResult attackResult = new AttackResult();
		AttackBridge attackBridge = null;
		try
		{
			attackBridge = new AttackBridge(c.Pid);
			if (attackBridge.CharacterId != c.Id)
			{
				throw new Exception("Personagem mudou.");
			}
			action(attackBridge, attackResult);
		}
		catch (Exception ex)
		{
			attackResult.Message = c.Name + ": " + ex.Message;
		}
		finally
		{
			if (attackBridge != null)
			{
				try
				{
					attackBridge.Dispose();
				}
				catch (Exception ex)
				{
					attackResult.Message = c.Name + ": " + ex.Message;
				}
				attackResult.Blocked = !attackBridge.RestoreSucceeded;
				if (attackResult.Blocked)
				{
					attackResult.Message = c.Name + ": ponte de ataque sem restauração; reinicie esse cliente.";
				}
			}
		}
		return attackResult;
	}

	public bool ValidTarget(CharacterRef c, uint target)
	{
		if (!AttackController.MonsterId(target))
		{
			return false;
		}
		AttackResult attackResult = Use(c, (AttackBridge b, AttackResult r) =>
		{
			b.Command(0u, target);
			r.Combo = ((b.LastResult == 1) ? 1u : 0u);
		});
		if (attackResult.Blocked)
		{
			throw new AttackBlocked(c.Pid, attackResult.Message);
		}
		if (attackResult.Message != null)
		{
			throw new Exception(attackResult.Message);
		}
		return attackResult.Combo == 1;
	}

	public bool Running(CharacterRef c, uint combo, uint target)
	{
		using CharacterMemory characterMemory = Open(c);
		return characterMemory.OwnedAttackRunning(combo, target);
	}

	public AttackResult Start(CharacterRef c, uint target)
	{
		bool macroRunning;
		using (CharacterMemory characterMemory = Open(c))
		{
			if (!characterMemory.AttackConfigured())
			{
				AttackResult attackResult = new AttackResult();
				attackResult.Message = c.Name + ": cadastre habilidades na macro 0.";
				return attackResult;
			}
			macroRunning = characterMemory.AnyMacroRunning();
		}
		return Use(c, (AttackBridge b, AttackResult r) =>
		{
			if (macroRunning && !b.StopMacro(-1) && b.Read(b.Host + 7484) != 0)
			{
				throw new Exception("Macro em execução não parou.");
			}
			b.Command(0u, target);
			if (b.LastResult != 1)
			{
				throw new Exception("Alvo indisponível ou não atacável.");
			}
			b.Command(1u, target);
			if (b.LastResult != 1 || b.Read(b.Host + 1532) != target)
			{
				throw new Exception("Alvo não confirmado.");
			}
			b.Command(2u, target);
			if (b.LastResult != 1 || b.Combo == 0)
			{
				throw new Exception("Macro 0 não iniciou.");
			}
			r.Combo = b.Combo;
			r.Message = c.Name + (macroRunning ? ": macro anterior interrompida; atacando o alvo do principal com a macro 0." : ": atacando o alvo do principal com a macro 0.");
		});
	}

	public AttackResult Stop(CharacterRef c, uint combo, uint target)
	{
		return Use(c, (AttackBridge b, AttackResult r) =>
		{
			b.Command(3u, target, combo);
			r.Message = ((b.LastResult == 1) ? (c.Name + ": ataque encerrado.") : null);
		});
	}
}
