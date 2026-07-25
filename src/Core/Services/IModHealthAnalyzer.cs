using DivinityModManager.Models;

namespace DivinityModManager.Models.Health;

/// <summary>
/// Optional, read-only analysis boundary for Redux Mod Health.
/// Implementations must not mutate mods, load orders, settings, or files.
/// </summary>
public interface IModHealthAnalyzer
{
	IReadOnlyList<ModHealthSnapshot> AnalyzeAll(
		IEnumerable<DivinityModData> installedMods,
		IEnumerable<DivinityModData> activeMods,
		IEnumerable<DivinityModData> duplicateMods = null,
		bool enableLoadOrderAdvisor = false);
}
