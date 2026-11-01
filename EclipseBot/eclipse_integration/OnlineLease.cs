internal sealed class OnlineLease
{
	public string product { get; set; }

	public string device { get; set; }

	public long issued { get; set; }

	public long expires { get; set; }
}
