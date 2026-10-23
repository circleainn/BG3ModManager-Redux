namespace DivinityModManager.Models;

public sealed class NexusSkippedFileUpdate
{
	public string ModUuid { get; set; }
	public long ProjectId { get; set; }
	public long InstalledFileId { get; set; }
	public long ReplacementFileId { get; set; }
}
