using System;
using System.Diagnostics;
using System.Threading;

internal static class BuffService
{
	public static BuffResult Start(CharacterRef character)
	{
		BuffResult buffResult = new BuffResult();
		BuffBridge buffBridge = null;
		bool flag = false;
		try
		{
			using (CharacterMemory characterMemory = new CharacterMemory(character.Pid))
			{
				if (characterMemory.Id != character.Id)
				{
					throw new Exception("Personagem mudou; atualize a lista.");
				}
				if (!characterMemory.BuffMacroConfigured())
				{
					throw new Exception("Cadastre os buffs na macro 1 (segundo slot) da janela R.");
				}
				flag = characterMemory.AnyMacroRunning();
			}
			if (flag)
			{
				StopRunningMacro(character, buffResult);
			}
			buffBridge = new BuffBridge(character.Pid);
			if (buffBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou antes do comando.");
			}
			buffBridge.BuffOnce();
			if (buffBridge.LastResult != 1)
			{
				throw new Exception("Cliente não aceitou o início da macro.");
			}
			buffResult.Started = true;
			buffResult.Message = character.Name + (flag ? ": macro anterior interrompida; macro 1 acionada." : ": macro 1 acionada uma vez.");
		}
		catch (Exception ex)
		{
			buffResult.Message = character.Name + ": " + ex.Message;
		}
		finally
		{
			if (buffBridge != null)
			{
				try
				{
					buffBridge.Dispose();
				}
				catch (Exception ex)
				{
					buffResult.Message = buffResult.Message + " " + ex.Message;
				}
				buffResult.Blocked = !buffBridge.RestoreSucceeded;
				if (buffResult.Blocked)
				{
					buffResult.Message += " Ponte sem restauração confirmada; reinicie esse cliente.";
				}
			}
		}
		return buffResult;
	}

	public static bool MacroBusy(CharacterRef character)
	{
		using CharacterMemory characterMemory = new CharacterMemory(character.Pid);
		if (characterMemory.Id != character.Id)
		{
			throw new Exception("Personagem mudou; atualize a lista.");
		}
		return characterMemory.AnyMacroRunning();
	}

	private static void StopRunningMacro(CharacterRef character, BuffResult result)
	{
		AttackBridge attackBridge = null;
		try
		{
			attackBridge = new AttackBridge(character.Pid);
			if (attackBridge.CharacterId != character.Id)
			{
				throw new Exception("Personagem mudou antes do comando.");
			}
			attackBridge.StopMacro(-1);
		}
		finally
		{
			if (attackBridge != null)
			{
				attackBridge.Dispose();
				if (!attackBridge.RestoreSucceeded)
				{
					result.Blocked = true;
					throw new Exception("Ponte sem restauração confirmada; reinicie esse cliente.");
				}
			}
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < 1500)
		{
			using (CharacterMemory characterMemory = new CharacterMemory(character.Pid))
			{
				if (!characterMemory.AnyMacroRunning())
				{
					return;
				}
			}
			Thread.Sleep(100);
		}
		throw new Exception("A macro em execução não parou; macro 1 não acionada.");
	}

	public static bool Running(CharacterRef character)
	{
		using CharacterMemory characterMemory = new CharacterMemory(character.Pid);
		if (characterMemory.Id != character.Id)
		{
			return false;
		}
		return characterMemory.BuffMacroRunning();
	}
}
