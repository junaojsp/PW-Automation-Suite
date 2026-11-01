using System;

internal sealed class NativeMeditationRuntime : IMeditationRuntime
{
	public bool Sitting(CharacterRef c)
	{
		using CharacterMemory characterMemory = new CharacterMemory(c.Pid);
		if (characterMemory.Id != c.Id)
		{
			throw new Exception("Personagem mudou; atualize a lista.");
		}
		return characterMemory.Sitting;
	}

	public MeditationResult Set(CharacterRef c, bool sitting)
	{
		MeditationResult meditationResult = new MeditationResult();
		MeditationBridge meditationBridge = null;
		try
		{
			meditationBridge = new MeditationBridge(c.Pid);
			if (meditationBridge.CharacterId != c.Id)
			{
				throw new Exception("Personagem mudou antes da meditação.");
			}
			meditationBridge.SetSitting(sitting);
			if (meditationBridge.LastSendResult != 1)
			{
				throw new Exception("Cliente não aceitou o comando.");
			}
		}
		catch (Exception ex)
		{
			meditationResult.Error = ex.Message;
		}
		finally
		{
			if (meditationBridge != null)
			{
				try
				{
					meditationBridge.Dispose();
				}
				catch (Exception ex)
				{
					meditationResult.Error = ex.Message;
				}
				meditationResult.Blocked = !meditationBridge.RestoreSucceeded;
			}
		}
		return meditationResult;
	}
}
