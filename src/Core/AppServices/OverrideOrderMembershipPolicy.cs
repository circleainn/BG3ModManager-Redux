using DivinityModManager.Models;

namespace DivinityModManager.AppServices;

/// <summary>Chooses the physical Override PAKs needed by one load order.</summary>
public static class OverrideOrderMembershipPolicy
{
	public static IReadOnlyList<string> WantedFiles(
		IEnumerable<DivinityModData> availableMods,
		IEnumerable<string> pureOverrideSelection,
		IEnumerable<string> activeModUuids)
	{
		var mods = (availableMods ?? []).Where(mod => mod != null).ToArray();
		var pure = pureOverrideSelection ?? mods
			.Where(mod => mod.IsForceLoaded && !mod.IsForceLoadedMergedMod)
			.Select(mod => Path.GetFileName(mod.FilePath));
		var activeIds = (activeModUuids ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var mixed = mods.Where(mod => mod.IsForceLoadedMergedMod && activeIds.Contains(mod.UUID))
			.Select(mod => Path.GetFileName(mod.FilePath));
		return pure.Concat(mixed).Where(name => !String.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}
}
