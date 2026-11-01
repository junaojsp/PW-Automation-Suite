using System;
using System.Threading;

internal static class QuestDelivery
{
	internal sealed class Outcome
	{
		public bool Confirmed;

		public bool AlreadyPresent;

		public int Attempts;

		public string CloseError;
	}

	internal static Outcome Execute(Func<bool> hasQuest, Action openNpc, Func<bool> npcReady, Action acceptQuest, Func<bool> waitQuest, Action closeWindows, CancellationToken token, Func<bool> canRetryError = null)
	{
		Outcome outcome = new Outcome();
		token.ThrowIfCancellationRequested();
		if (hasQuest())
		{
			outcome.Confirmed = true;
			outcome.AlreadyPresent = true;
			return outcome;
		}
		bool flag = false;
		try
		{
			for (int i = 1; i <= 3; i++)
			{
				token.ThrowIfCancellationRequested();
				try
				{
					if (hasQuest())
					{
						outcome.Confirmed = true;
						break;
					}
					outcome.Attempts = i;
					flag = true;
					openNpc();
					if (npcReady())
					{
						token.ThrowIfCancellationRequested();
						acceptQuest();
						if (waitQuest())
						{
							outcome.Confirmed = true;
							break;
						}
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception)
				{
					if (i == 3 || canRetryError == null || !canRetryError())
					{
						throw;
					}
				}
				if (i < 3 && token.WaitHandle.WaitOne(250))
				{
					token.ThrowIfCancellationRequested();
				}
			}
			if (!outcome.Confirmed)
			{
				outcome.Confirmed = hasQuest();
			}
			return outcome;
		}
		finally
		{
			if (flag)
			{
				try
				{
					closeWindows();
				}
				catch (Exception ex3)
				{
					outcome.CloseError = ex3.Message;
				}
			}
		}
	}
}
