using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Clears only per-mod category choices so the automatic classifier becomes
/// authoritative again. Category definitions and other presentation settings
/// are intentionally left untouched.
/// </summary>
public static class ModCategoryAssignmentReset
{
	public static int ClearManualAssignments(DivinityModManagerSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		settings.ModCategoryAssignments ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		settings.ModCategoryOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var affectedModCount = settings.ModCategoryAssignments.Keys
			.Concat(settings.ModCategoryOverrides.Keys)
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count();

		settings.ModCategoryAssignments.Clear();
		// Clear the legacy store as well or its migration path would restore old
		// assignments the next time settings are loaded.
		settings.ModCategoryOverrides.Clear();
		return affectedModCount;
	}
}
