using System.Collections.Generic;

internal sealed class QuestChoice
{
	public byte[] Packet;

	public ushort Quest;

	public ushort Next;

	public byte Kind;

	public bool WasActive;

	public HashSet<ushort> Before;
}
