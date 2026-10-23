using DivinityModManager.Models.NexusMods;

using System.Runtime.Serialization;

namespace DivinityModManager.Models.Cache;

public class NexusModsCachedData : BaseModCacheData<NexusModsModData>
{
	[DataMember]
	public Dictionary<string, NexusVerifiedFileUpdate> VerifiedFileUpdates { get; set; } =
		new(StringComparer.OrdinalIgnoreCase);

	public bool HasVerifiedUpdate(string uuid, long projectId, long fileId, DateTimeOffset now) =>
		projectId > 0 && fileId > 0
		&& VerifiedFileUpdates?.TryGetValue(uuid, out var update) == true
		&& update.ProjectId == projectId && update.FileId == fileId
		&& update.EligibilityVersion == NexusVerifiedFileUpdate.CurrentEligibilityVersion
		&& update.CheckedAt > 0
		&& now.ToUnixTimeSeconds() - update.CheckedAt is >= 0 and <= 604800;

	public void RecordFileUpdateCheck(string uuid, long projectId, long fileId, bool available, DateTimeOffset now)
	{
		VerifiedFileUpdates ??= new(StringComparer.OrdinalIgnoreCase);
		if (available)
			VerifiedFileUpdates[uuid] = new NexusVerifiedFileUpdate
			{
				ProjectId = projectId, FileId = fileId, CheckedAt = now.ToUnixTimeSeconds(),
				EligibilityVersion = NexusVerifiedFileUpdate.CurrentEligibilityVersion
			};
		else
			VerifiedFileUpdates.Remove(uuid);
	}
}

public sealed class NexusVerifiedFileUpdate
{
	// Older cached warnings checked history alone and must be reverified.
	public const int CurrentEligibilityVersion = 1;
	public int EligibilityVersion { get; set; }
	public long ProjectId { get; set; }
	public long FileId { get; set; }
	public long CheckedAt { get; set; }
}
