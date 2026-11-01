internal sealed class LeaseCache
{
	public string Token { get; set; }

	public long LastUtc { get; set; }

	public string Service { get; set; }
}
