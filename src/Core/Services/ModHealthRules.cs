using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Metadata;

namespace DivinityModManager.Models.Health;

/// <summary>
/// Conservative checks for package identity facts already known to Redux.
/// </summary>
public sealed class ModIdentityHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		if (mod.HasInvalidUUID)
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.InvalidUuid,
				ModHealthSeverity.Error,
				"Invalid mod UUID",
				"This package has an invalid UUID and may fail to load or reset the exported load order."));
		}

		if (!String.IsNullOrWhiteSpace(mod.UUID) && context.DuplicateUuids.Contains(mod.UUID))
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.DuplicateUuid,
				ModHealthSeverity.Error,
				"Duplicate mod UUID",
				"More than one installed package uses this UUID. Neither file was changed.",
				new[] { mod.UUID }));
		}
	}
}

/// <summary>
/// Checks explicit dependency and conflict metadata without interpreting order.
/// </summary>
public sealed class ModDependencyHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		foreach (var dependency in mod.Dependencies.Items)
		{
			if (String.IsNullOrWhiteSpace(dependency.UUID))
			{
				continue;
			}

			if (String.Equals(dependency.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase))
			{
				findings.Add(new ModHealthFinding(
					ModHealthFindingCode.SelfDependency,
					ModHealthSeverity.Error,
					"Invalid self-dependency",
					$"{mod.DisplayName} declares its own UUID as a dependency. The package was not changed.",
					new[] { dependency.UUID }));
				continue;
			}

			if (dependency.Version?.VersionInt > 0
				&& context.InstalledByUuid.TryGetValue(dependency.UUID, out var installedDependency)
				&& installedDependency.Version?.VersionInt < dependency.Version.VersionInt)
			{
				findings.Add(new ModHealthFinding(
					ModHealthFindingCode.DependencyVersionTooOld,
					ModHealthSeverity.Warning,
					"Dependency version is older than declared",
					$"{dependency.Name} {dependency.Version.Version} or newer is declared, but installed version {installedDependency.Version?.Version ?? "unknown"} was detected.",
					new[] { dependency.UUID }));
			}
		}

		foreach (var dependency in mod.MissingDependencies.Items)
		{
			if (String.Equals(dependency.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.MissingDependency,
				ModHealthSeverity.Error,
				"Missing dependency",
				$"{dependency.Name} is listed as a dependency but is not installed.",
				new[] { dependency.UUID }));
		}

		if (mod.IsActive)
		{
			AddInactiveDependencies(context, findings);
		}

		foreach (var conflict in mod.Conflicts.Items)
		{
			if (!mod.IsActive
				|| String.IsNullOrWhiteSpace(conflict.UUID)
				|| String.Equals(conflict.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase)
				|| !context.ActiveUuids.Contains(conflict.UUID))
			{
				continue;
			}

			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.DeclaredConflict,
				ModHealthSeverity.Warning,
				"Active declared conflict",
				$"{mod.DisplayName} and {conflict.Name} are both active, and this package declares them incompatible.",
				new[] { conflict.UUID }));
		}
	}

	private static void AddInactiveDependencies(
		ModHealthAnalysisContext context,
		ICollection<ModHealthFinding> findings)
	{
		foreach (var dependency in context.Mod.Dependencies.Items)
		{
			if (String.IsNullOrWhiteSpace(dependency.UUID)
				|| !context.InstalledByUuid.TryGetValue(dependency.UUID, out var installedDependency)
				|| context.ActiveUuids.Contains(dependency.UUID)
				|| installedDependency.IsForceLoaded
				|| installedDependency.IsLarianMod)
			{
				continue;
			}

			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.InactiveDependency,
				ModHealthSeverity.Warning,
				"Dependency is inactive",
				$"{dependency.Name} is installed but is not currently in the active load order.",
				new[] { dependency.UUID }));
		}
	}

}

/// <summary>
/// Experimental, opt-in interpretation of declared dependency placement.
/// Kept separate from general dependency health so it can be omitted entirely.
/// </summary>
public sealed class LoadOrderAdvisorRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		if (!mod.IsActive
			|| String.IsNullOrWhiteSpace(mod.UUID)
			|| !context.ActivePositions.TryGetValue(mod.UUID, out var modPosition))
		{
			return;
		}

		var reportedUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var dependency in LoadOrderAdvisorRelationships.GetResolvedDependencies(context, mod))
		{
			if (String.Equals(dependency.Uuid, mod.UUID, StringComparison.OrdinalIgnoreCase)
				|| context.LoadOrderAdvisorKnowledge.SuppressesDependencyOrdering(dependency.Uuid)
				|| !context.InstalledByUuid.TryGetValue(dependency.Uuid, out var installedDependency)
				|| installedDependency.IsForceLoaded
				|| installedDependency.IsLarianMod
				|| !context.ActivePositions.TryGetValue(dependency.Uuid, out var dependencyPosition)
				|| dependencyPosition < modPosition)
			{
				continue;
			}

			reportedUuids.Add(dependency.Uuid);
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.DependencyLoadsLater,
				ModHealthSeverity.Warning,
				"Dependency loads later",
				$"{dependency.DisplayName} is positioned after {mod.DisplayName}. This dependency should normally load earlier; review the mod author's instructions before moving it.",
				new[] { dependency.Uuid }));
		}

		if (!context.LoadOrderAdvisorKnowledge.TryGetEntry(mod.UUID, out var knowledgeEntry)) return;
		foreach (var predecessor in knowledgeEntry.LoadAfter ?? new List<ReduxLoadAfterKnowledge>())
		{
			if (!context.LoadOrderAdvisorKnowledge.TryResolveInstalledDependency(
					predecessor.Uuid,
					predecessor.Name,
					context.InstalledByUuid,
					out var predecessorUuid)
				|| reportedUuids.Contains(predecessorUuid)
				|| !context.InstalledByUuid.TryGetValue(predecessorUuid, out var installedPredecessor)
				|| installedPredecessor.IsForceLoaded
				|| installedPredecessor.IsLarianMod
				|| !context.ActivePositions.TryGetValue(predecessorUuid, out var predecessorPosition)
				|| predecessorPosition < modPosition)
			{
				continue;
			}

			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.RecommendedPredecessorLoadsLater,
				ModHealthSeverity.Warning,
				"Recommended predecessor loads later",
				$"{installedPredecessor.DisplayName} is positioned after {mod.DisplayName}, but documented mod-author guidance places {mod.DisplayName} after it.",
				new[] { predecessorUuid }));
		}
	}
}

/// <summary>
/// Reports exact cycles in active, declared dependency metadata. A cycle has no
/// linear arrangement that can satisfy every declaration, so Redux only reports it.
/// </summary>
public sealed class LoadOrderDependencyCycleRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		if (!mod.IsActive
			|| String.IsNullOrWhiteSpace(mod.UUID)
			|| !context.ActiveUuids.Contains(mod.UUID))
		{
			return;
		}

		foreach (var dependency in LoadOrderAdvisorRelationships.GetResolvedDependencies(context, mod))
		{
			if (!context.ActiveUuids.Contains(dependency.Uuid)
				|| context.LoadOrderAdvisorKnowledge.SuppressesDependencyOrdering(dependency.Uuid)
				|| !HasDependencyPathTo(
					dependency.Uuid,
					mod.UUID,
					context,
					new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
			{
				continue;
			}

			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.DependencyCycle,
				ModHealthSeverity.Warning,
				"Declared dependency cycle",
				$"{mod.DisplayName} and its active dependency chain refer back to one another. No linear load order can satisfy every declaration; review the mod authors' instructions.",
				new[] { dependency.Uuid }));
			return;
		}
	}

	private static bool HasDependencyPathTo(
		string currentUuid,
		string targetUuid,
		ModHealthAnalysisContext context,
		ISet<string> visited)
	{
		if (String.Equals(currentUuid, targetUuid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!visited.Add(currentUuid)
			|| !context.ActiveUuids.Contains(currentUuid)
			|| !context.InstalledByUuid.TryGetValue(currentUuid, out var current))
		{
			return false;
		}

		return LoadOrderAdvisorRelationships.GetResolvedDependencies(context, current).Any(dependency =>
			!context.LoadOrderAdvisorKnowledge.SuppressesDependencyOrdering(dependency.Uuid)
			&& HasDependencyPathTo(dependency.Uuid, targetUuid, context, visited));
	}
}

internal static class LoadOrderAdvisorRelationships
{
	public static IEnumerable<ResolvedAdvisorDependency> GetResolvedDependencies(
		ModHealthAnalysisContext context,
		DivinityModData mod)
	{
		var declared = mod.Dependencies.Items
			.Select(dependency => new ReduxLoadOrderDependencyKnowledge
			{
				Uuid = dependency.UUID,
				Name = dependency.Name
			})
			.ToList();
		if (context.LoadOrderAdvisorKnowledge.TryGetEntry(mod.UUID, out var knowledgeEntry))
		{
			declared.AddRange(knowledgeEntry.Dependencies ?? new List<ReduxLoadOrderDependencyKnowledge>());
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var dependency in declared)
		{
			if (!context.LoadOrderAdvisorKnowledge.TryResolveInstalledDependency(
					dependency.Uuid,
					dependency.Name,
					context.InstalledByUuid,
					out var installedUuid)
				|| !seen.Add(installedUuid))
			{
				continue;
			}

			var displayName = context.InstalledByUuid.TryGetValue(installedUuid, out var installed)
				? installed.DisplayName
				: dependency.Name;
			yield return new ResolvedAdvisorDependency(installedUuid, displayName);
		}
	}
}

internal readonly record struct ResolvedAdvisorDependency(string Uuid, string DisplayName);

public sealed class CreatorManifestHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var manifest = context.Mod.CreatorManifest;
		if (manifest?.State != ReduxCreatorManifestState.Invalid)
		{
			return;
		}

		findings.Add(new ModHealthFinding(
			ModHealthFindingCode.InvalidCreatorManifest,
			ModHealthSeverity.Warning,
			"Embedded creator manifest ignored",
			String.IsNullOrWhiteSpace(manifest.Diagnostic)
				? "This package contains a redux.mod.json file that could not be validated. Its mod-page information was ignored."
				: $"{manifest.Diagnostic} Its mod-page information was ignored."));
	}
}

public sealed class ScriptExtenderHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		var status = mod.ExtenderModStatus;
		if (status.HasFlag(DivinityExtenderModStatus.DisabledFromConfig)
			|| status.HasFlag(DivinityExtenderModStatus.MissingUpdater))
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.ScriptExtenderUnavailable,
				ModHealthSeverity.Error,
				"Script Extender unavailable",
				mod.ScriptExtenderSupportToolTipText));
		}
		else if (status.HasFlag(DivinityExtenderModStatus.MissingRequiredVersion)
			|| status.HasFlag(DivinityExtenderModStatus.MissingAppData))
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.ScriptExtenderVersionMismatch,
				ModHealthSeverity.Warning,
				"Script Extender needs attention",
				mod.ScriptExtenderSupportToolTipText));
		}
	}
}

public sealed class LegacyAndOverrideHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		if (mod.OsirisModStatus == DivinityOsirisModStatus.MODFIXER)
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.LegacyModFixerIncluded,
				ModHealthSeverity.Info,
				"Contains Mod Fixer",
				"Mod Fixer files were detected inside this package. BG3 Patch 7 and newer generally do not require Mod Fixer, and it does not need to be installed separately."));
		}

		if (!mod.IsForceLoaded)
		{
			return;
		}

		if (mod.ForceAllowInLoadOrder)
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.AlwaysLoadedWithLoadOrderEntry,
				ModHealthSeverity.Info,
				"Always Loaded + Load Order Entry",
				"This package overrides game files and is also explicitly allowed in the normal load order."));
		}
		else if (mod.IsForceLoadedMergedMod)
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.ContainsFileOverrides,
				ModHealthSeverity.Info,
				"Contains File Overrides",
				"Disabling this mod's load-order entry may not disable the files it directly overrides."));
		}
		else
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.AlwaysLoaded,
				ModHealthSeverity.Info,
				"Always Loaded",
				"This package is loaded because the .pak exists and usually is not written to modsettings.lsx."));
		}
	}
}

/// <summary>
/// Explains MCM's otherwise confusing in-game load-order warning when its
/// normal module entry has not been activated and exported.
/// </summary>
public sealed class McmActivationHealthRule : IModHealthRule
{
	private const string McmUuid = "755a8a72-407f-4f0d-9a33-274ac0f0b53d";

	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		var mod = context.Mod;
		if (!String.Equals(mod.UUID, McmUuid, StringComparison.OrdinalIgnoreCase)
			|| context.ActiveUuids.Contains(McmUuid))
		{
			return;
		}

		findings.Add(new ModHealthFinding(
			ModHealthFindingCode.McmNotActive,
			ModHealthSeverity.Warning,
			"Mod Configuration Menu is not active",
			"MCM includes files that can load before its normal module entry is active. That can make MCM appear in game while it warns that the load order was reset. Move MCM into the active pane and use Export to Game. Its reference to BG3MM also applies to compatible managers such as Redux."));
	}
}

/// <summary>
/// Provider-specific safety notes. In Local-only mode the provider is masked,
/// so this rule naturally produces no findings.
/// </summary>
public sealed class ModSourceHealthRule : IModHealthRule
{
	public void Evaluate(ModHealthAnalysisContext context, ICollection<ModHealthFinding> findings)
	{
		if (context.Mod.Metadata.SourceType == ModSourceType.MODIO)
		{
			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.ModioManagedSource,
				ModHealthSeverity.Warning,
				"BG3 or Steam may restore this mod",
				"Removing the local file does not unsubscribe from it. BG3 can reinstall subscribed mod.io mods, and Steam Cloud may retain a cached copy even after you unsubscribe. For predictable load-order control, avoid mixing the in-game/mod.io manager with Redux."));
		}
	}
}
