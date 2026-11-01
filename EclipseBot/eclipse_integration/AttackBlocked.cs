using System;

internal sealed class AttackBlocked : Exception
{
	public readonly int Pid;

	public AttackBlocked(int pid, string message)
		: base(message)
	{
		Pid = pid;
	}
}
