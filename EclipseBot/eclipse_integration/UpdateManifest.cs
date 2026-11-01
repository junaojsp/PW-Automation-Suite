internal sealed class UpdateManifest
{
	public string product { get; set; }

	public string architecture { get; set; }

	public string version { get; set; }

	public string url { get; set; }

	public string sha256 { get; set; }

	public long size { get; set; }

	public string notes { get; set; }
}
