using DivinityModManager.Models.NexusMods;

using System.Runtime.Serialization;

namespace DivinityModManager.Models.Cache;

public class NexusModsCachedData : BaseModCacheData<NexusModsModData>
{
	[DataMember]
	public Dictionary<string, NexusVerifiedFileUpdate> VerifiedFileUpdates { get; set; } =
		new(StringComparer.OrdinalIgnoreCase);

	public bool HasVerifiedUpdate(string uuid, long projectId, long fileId, DateTimeOffset now) =>
		GetVerifiedReplacements(uuid, projectId, fileId, now).Length > 0;

	public long[] GetVerifiedReplacements(string uuid, long projectId, long fileId, DateTimeOffset now) =>
		!String.IsNullOrWhiteSpace(uuid) && projectId > 0 && fileId > 0
		&& VerifiedFileUpdates?.TryGetValue(uuid, out var update) == true
		&& update != null && update.ProjectId == projectId && update.FileId == fileId
		&& update.EligibilityVersion == NexusVerifiedFileUpdate.CurrentEligibilityVersion
		&& update.CheckedAt > 0
		&& now.ToUnixTimeSeconds() - update.CheckedAt is >= 0 and <= 604800
			? (update.ReplacementFileIds ?? []).Where(id => id > 0 && id != fileId).Distinct().Order().ToArray() : [];

	public void RecordFileUpdateCheck(string uuid, long projectId, long fileId, IEnumerable<long> replacements, DateTimeOffset now)
	{
		VerifiedFileUpdates ??= new(StringComparer.OrdinalIgnoreCase);
		var offered = (replacements ?? []).Where(id => id > 0 && id != fileId).Distinct().Order().ToArray();
		if (offered.Length > 0)
			VerifiedFileUpdates[uuid] = new NexusVerifiedFileUpdate
			{
				ProjectId = projectId, FileId = fileId, CheckedAt = now.ToUnixTimeSeconds(),
				ReplacementFileIds = offered,
				EligibilityVersion = NexusVerifiedFileUpdate.CurrentEligibilityVersion
			};
		else
			VerifiedFileUpdates.Remove(uuid);
	}
}

public sealed class NexusVerifiedFileUpdate
{
	// Older warnings lack current-file eligibility or replacement IDs needed for skip choices.
	public const int CurrentEligibilityVersion = 2;
	public int EligibilityVersion { get; set; }
	public long[] ReplacementFileIds { get; set; } = [];
	public long ProjectId { get; set; }
	public long FileId { get; set; }
	public long CheckedAt { get; set; }
}
