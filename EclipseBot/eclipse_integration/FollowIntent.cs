using System;

internal sealed class FollowIntent
{
	public readonly int Pid;

	public readonly uint Id;

	public readonly uint Target;

	public bool Enabled;

	public bool StopPending;

	public DateTime NextAttempt;

	public FollowIntent(int pid, uint id, uint target, bool enabled)
	{
		Pid = pid;
		Id = id;
		Target = target;
		Enabled = enabled;
	}

	public void ToggleTo(bool enabled)
	{
		Enabled = enabled;
		StopPending = !enabled;
		NextAttempt = DateTime.MinValue;
	}
}
