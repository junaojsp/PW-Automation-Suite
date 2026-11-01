public sealed class CharacterRef
{
	public int Pid;

	public uint Id;

	public string Name;

	public string Key => Pid + ":" + Id;
}
