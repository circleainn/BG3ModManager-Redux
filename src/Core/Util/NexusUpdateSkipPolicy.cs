using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>Local notification choices, independent of source links and provider caches.</summary>
public static class NexusUpdateSkipPolicy
{
	public static long[] GetSkippedFiles(DivinityModManagerSettings settings, string uuid, long projectId, long installedFileId) =>
		(settings?.SkippedNexusFileUpdates ?? [])
			.Where(entry => Matches(entry, uuid, projectId, installedFileId))
			.Select(entry => entry.ReplacementFileId).Distinct().Order().ToArray();

	public static long[] GetNotifiableFiles(DivinityModManagerSettings settings, string uuid, long projectId,
		long installedFileId, IEnumerable<long> offered) =>
		(offered ?? []).Where(id => id > 0 && id != installedFileId)
			.Except(GetSkippedFiles(settings, uuid, projectId, installedFileId)).Order().ToArray();

	public static bool SetSkipped(DivinityModManagerSettings settings, string uuid, long projectId,
		long installedFileId, IEnumerable<long> replacements, bool skipped, Func<bool> persist)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(persist);
		if (String.IsNullOrWhiteSpace(uuid) || projectId <= 0 || installedFileId <= 0) return false;
		var targets = (replacements ?? []).Where(id => id > 0 && id != installedFileId).ToHashSet();
		if (targets.Count == 0) return false;
		var previous = settings.SkippedNexusFileUpdates;
		var next = (previous ?? []).Where(entry => !(Matches(entry, uuid, projectId, installedFileId)
			&& targets.Contains(entry.ReplacementFileId))).ToList();
		if (skipped)
			foreach (var id in targets.Order())
				next.Add(new() { ModUuid = uuid, ProjectId = projectId, InstalledFileId = installedFileId, ReplacementFileId = id });
		settings.SkippedNexusFileUpdates = next;
		try
		{
			if (persist()) return true;
		}
		catch
		{
			settings.SkippedNexusFileUpdates = previous;
			throw;
		}
		settings.SkippedNexusFileUpdates = previous;
		return false;
	}

	private static bool Matches(NexusSkippedFileUpdate entry, string uuid, long projectId, long installedFileId) =>
		entry != null && !String.IsNullOrWhiteSpace(uuid) && projectId > 0 && installedFileId > 0
		&& String.Equals(entry.ModUuid, uuid, StringComparison.OrdinalIgnoreCase)
		&& entry.ProjectId == projectId && entry.InstalledFileId == installedFileId
		&& entry.ReplacementFileId > 0 && entry.ReplacementFileId != installedFileId;
}
