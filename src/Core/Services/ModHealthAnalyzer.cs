using DivinityModManager.Models;

namespace DivinityModManager.Models.Health;

/// <summary>
/// Builds read-only health snapshots from facts BG3MM has already detected.
/// It does not mutate mods, load orders, settings, files, or provider metadata.
/// Individual checks are registered as removable <see cref="IModHealthRule"/> extensions.
/// </summary>
public sealed class ModHealthAnalyzer : IModHealthAnalyzer
{
	private static readonly IReadOnlyList<IModHealthRule> DefaultHealthRules = new IModHealthRule[]
	{
		new ModIdentityHealthRule(),
		new ModDependencyHealthRule(),
		new CreatorManifestHealthRule(),
		new ScriptExtenderHealthRule(),
		new LegacyAndOverrideHealthRule(),
		new McmActivationHealthRule(),
		new ModSourceHealthRule()
	};
	private static readonly IReadOnlyList<IModHealthRule> DefaultAdvisorRules = new IModHealthRule[]
	{
		new LoadOrderAdvisorRule(),
		new LoadOrderDependencyCycleRule()
	};

	private readonly IReadOnlyList<IModHealthRule> _healthRules;
	private readonly IReadOnlyList<IModHealthRule> _advisorRules;

	public ModHealthAnalyzer(
		IEnumerable<IModHealthRule> healthRules = null,
		IEnumerable<IModHealthRule> advisorRules = null)
	{
		_healthRules = (healthRules ?? DefaultHealthRules).Where(rule => rule != null).ToArray();
		_advisorRules = (advisorRules ?? DefaultAdvisorRules).Where(rule => rule != null).ToArray();
	}

	public IReadOnlyList<ModHealthSnapshot> AnalyzeAll(
		IEnumerable<DivinityModData> installedMods,
		IEnumerable<DivinityModData> activeMods,
		IEnumerable<DivinityModData> duplicateMods = null,
		bool enableLoadOrderAdvisor = false,
		bool disableModioWarnings = false)
	{
		var installed = (installedMods ?? Enumerable.Empty<DivinityModData>())
			.Where(mod => mod != null && !mod.IsVisualDivider)
			.ToArray();
		var active = (activeMods ?? Enumerable.Empty<DivinityModData>())
			.Where(mod => mod != null && !mod.IsVisualDivider)
			.ToArray();
		var activeUuids = active
			.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
			.Select(mod => mod.UUID)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var activePositions = BuildActivePositions(active);
		var installedByUuid = installed
			.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var duplicateUuids = FindDuplicateUuids(installed, duplicateMods);

		return installed
			.Select(mod => Analyze(
				new ModHealthAnalysisContext(
					mod,
					installedByUuid,
					activeUuids,
					activePositions,
					duplicateUuids),
				enableLoadOrderAdvisor,
				disableModioWarnings))
			.ToArray();
	}

	private ModHealthSnapshot Analyze(
		ModHealthAnalysisContext context,
		bool enableLoadOrderAdvisor,
		bool disableModioWarnings)
	{
		var findings = new List<ModHealthFinding>();
		foreach (var rule in _healthRules)
		{
			rule.Evaluate(context, findings);
		}

		// Load-order advice is meaningful only for ordinary entries in the active
		// order. Inactive packages have no position, and force-loaded/override
		// packages load outside the normal modsettings.lsx ordering model.
		if (enableLoadOrderAdvisor
			&& !context.Mod.IsForceLoaded
			&& !String.IsNullOrWhiteSpace(context.Mod.UUID)
			&& context.ActiveUuids.Contains(context.Mod.UUID))
		{
			foreach (var rule in _advisorRules)
			{
				rule.Evaluate(context, findings);
			}
		}

		if (disableModioWarnings)
		{
			// Presentation-only suppression of a single advisory finding. The rules still run,
			// and nothing about mod.io metadata, source linking, labels or cached source data
			// changes - only this one notice is withheld from the snapshot.
			findings.RemoveAll(finding => finding.Code == ModHealthFindingCode.ModioManagedSource);
		}

		return new ModHealthSnapshot(context.Mod, findings);
	}

	private static HashSet<string> FindDuplicateUuids(
		IEnumerable<DivinityModData> installedMods,
		IEnumerable<DivinityModData> duplicateMods)
	{
		var duplicates = installedMods
			.Where(mod => !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (var duplicate in duplicateMods ?? Enumerable.Empty<DivinityModData>())
		{
			if (!String.IsNullOrWhiteSpace(duplicate?.UUID))
			{
				duplicates.Add(duplicate.UUID);
			}
		}

		return duplicates;
	}

	private static Dictionary<string, int> BuildActivePositions(IEnumerable<DivinityModData> activeMods)
	{
		var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var position = 0;
		foreach (var mod in activeMods ?? Enumerable.Empty<DivinityModData>())
		{
			if (mod == null || mod.IsVisualDivider || String.IsNullOrWhiteSpace(mod.UUID))
			{
				continue;
			}

			positions.TryAdd(mod.UUID, position);
			position++;
		}
		return positions;
	}
}
