using System.Collections.Generic;

internal sealed class PartySnapshot
{
	public uint Leader;

	public readonly HashSet<uint> Members = new HashSet<uint>();
}
