internal sealed class UpdateRequest
{
	public string repository { get; set; }

	public string envelope { get; set; }

	public string target { get; set; }

	public int parentId { get; set; }

	public long parentStart { get; set; }
}
