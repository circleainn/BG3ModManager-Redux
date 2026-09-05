using DivinityModManager.Models;
using DivinityModManager.AppServices;

namespace DivinityModManager.Models.Health;

/// <summary>
/// A single read-only Mod Health check. Rules report findings only; they must
/// never change mods, load-order state, settings, provider metadata, or files.
/// </summary>
public interface IModHealthRule
{
	void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings);
}

/// <summary>
/// Immutable facts shared with registered Mod Health rules.
/// </summary>
public sealed class ModHealthAnalysisContext
{
	public DivinityModData Mod { get; }
	public IReadOnlyDictionary<string, DivinityModData> InstalledByUuid { get; }
	public IReadOnlySet<string> ActiveUuids { get; }
	public IReadOnlyDictionary<string, int> ActivePositions { get; }
	public IReadOnlySet<string> DuplicateUuids { get; }
	public ReduxLoadOrderAdvisorKnowledge LoadOrderAdvisorKnowledge { get; }

	public ModHealthAnalysisContext(
		DivinityModData mod,
		IReadOnlyDictionary<string, DivinityModData> installedByUuid,
		IReadOnlySet<string> activeUuids,
		IReadOnlyDictionary<string, int> activePositions,
		IReadOnlySet<string> duplicateUuids,
		ReduxLoadOrderAdvisorKnowledge loadOrderAdvisorKnowledge = null)
	{
		Mod = mod;
		InstalledByUuid = installedByUuid;
		ActiveUuids = activeUuids;
		ActivePositions = activePositions;
		DuplicateUuids = duplicateUuids;
		LoadOrderAdvisorKnowledge = loadOrderAdvisorKnowledge ?? ReduxLoadOrderAdvisorKnowledge.Empty;
	}
}
