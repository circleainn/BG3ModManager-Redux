using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>
/// Updates per-mod category choices and sidebar visibility independently.
/// Persistence failures restore the previous choices without changing definitions or styles.
/// </summary>
public static class ModCategoryAssignmentReset
{
	public static bool SetAutomaticClassificationEnabled(DivinityModManagerSettings settings, bool enabled)
	{
		ArgumentNullException.ThrowIfNull(settings);
		if (settings.EnableAutomaticModCategories == enabled) return false;

		settings.EnableAutomaticModCategories = enabled;
		return true;
	}

	public static int ClearManualAssignments(DivinityModManagerSettings settings, Func<bool> persist = null)
	{
		ArgumentNullException.ThrowIfNull(settings);

		var previousAssignments = settings.ModCategoryAssignments;
		var previousOverrides = settings.ModCategoryOverrides;
		var previousCleared = settings.ClearedAutomaticCategoryModIds;
		var affectedModCount = (previousAssignments?.Keys ?? Enumerable.Empty<string>())
			.Concat(previousOverrides?.Keys ?? Enumerable.Empty<string>())
			.Concat(previousCleared ?? [])
			.Where(uuid => !String.IsNullOrWhiteSpace(uuid))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count();
		if (affectedModCount == 0) return 0;

		settings.ModCategoryAssignments = new(StringComparer.OrdinalIgnoreCase);
		// Clear the legacy store as well or its migration path would restore old
		// assignments the next time settings are loaded.
		settings.ModCategoryOverrides = new(StringComparer.OrdinalIgnoreCase);
		settings.ClearedAutomaticCategoryModIds = [];
		PersistOrRestore(persist, () =>
		{
			settings.ModCategoryAssignments = previousAssignments;
			settings.ModCategoryOverrides = previousOverrides;
			settings.ClearedAutomaticCategoryModIds = previousCleared;
		});
		return affectedModCount;
	}

	public static bool IsAutomaticAssignmentCleared(DivinityModManagerSettings settings, string uuid) =>
		!String.IsNullOrWhiteSpace(uuid) &&
		settings.ClearedAutomaticCategoryModIds?.Contains(uuid, StringComparer.OrdinalIgnoreCase) == true;

	public static int ClearAutomaticAssignments(
		DivinityModManagerSettings settings, IEnumerable<DivinityModData> currentMods, Func<bool> persist)
	{
		var previous = settings.ClearedAutomaticCategoryModIds;
		var cleared = new HashSet<string>(previous ?? [], StringComparer.OrdinalIgnoreCase);
		var affected = 0;
		foreach (var mod in currentMods)
		{
			if (mod == null || mod.IsVisualDivider || String.IsNullOrWhiteSpace(mod.UUID)) continue;
			if (settings.ModCategoryAssignments?.TryGetValue(mod.UUID, out var manual) == true && manual?.Count > 0) continue;
			if (settings.ModCategoryOverrides?.TryGetValue(mod.UUID, out var legacy) == true && !String.IsNullOrWhiteSpace(legacy)) continue;
			if (cleared.Add(mod.UUID)) affected++;
		}
		if (affected == 0) return 0;
		settings.ClearedAutomaticCategoryModIds = cleared.ToList();
		PersistOrRestore(persist, () => settings.ClearedAutomaticCategoryModIds = previous);
		return affected;
	}

	public static int RestoreClearedAutomaticAssignments(DivinityModManagerSettings settings, Func<bool> persist)
	{
		var previous = settings.ClearedAutomaticCategoryModIds;
		if (previous == null || previous.Count == 0) return 0;
		settings.ClearedAutomaticCategoryModIds = [];
		PersistOrRestore(persist, () => settings.ClearedAutomaticCategoryModIds = previous);
		return previous.Count;
	}

	public static bool SetFiltersVisible(
		DivinityModManagerSettings settings, IEnumerable<string> categories, bool visible, Func<bool> persist)
	{
		var previous = settings.DisabledModCategories;
		var hidden = new HashSet<string>(previous ?? [], StringComparer.OrdinalIgnoreCase);
		var changed = false;
		foreach (var category in categories.Where(category => !String.IsNullOrWhiteSpace(category)))
			changed |= visible ? hidden.Remove(category) : hidden.Add(category);
		if (!changed) return false;
		settings.DisabledModCategories = hidden.ToList();
		PersistOrRestore(persist, () => settings.DisabledModCategories = previous);
		return true;
	}

	private static void PersistOrRestore(Func<bool> persist, Action restore)
	{
		try
		{
			if (persist != null && !persist()) throw new IOException("The category choices could not be saved.");
		}
		catch
		{
			restore();
			throw;
		}
	}
}
