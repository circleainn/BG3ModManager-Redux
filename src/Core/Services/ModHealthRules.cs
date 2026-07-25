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
				"More than one installed package uses this UUID. Redux is only reporting the duplicate; it has not changed either file.",
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
					$"{mod.DisplayName} declares its own UUID as a dependency. Redux is reporting the package metadata and has not altered it.",
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

		foreach (var dependency in mod.Dependencies.Items)
		{
			if (String.IsNullOrWhiteSpace(dependency.UUID)
				|| String.Equals(dependency.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase)
				|| !context.InstalledByUuid.TryGetValue(dependency.UUID, out var installedDependency)
				|| installedDependency.IsForceLoaded
				|| installedDependency.IsLarianMod
				|| !context.ActivePositions.TryGetValue(dependency.UUID, out var dependencyPosition)
				|| dependencyPosition < modPosition)
			{
				continue;
			}

			findings.Add(new ModHealthFinding(
				ModHealthFindingCode.DependencyLoadsLater,
				ModHealthSeverity.Warning,
				"Dependency loads later",
				$"{dependency.Name} is positioned after {mod.DisplayName}. Declared dependencies should normally load earlier; review the mod author's instructions before moving it.",
				new[] { dependency.UUID }));
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

		foreach (var dependency in mod.Dependencies.Items)
		{
			if (String.IsNullOrWhiteSpace(dependency.UUID)
				|| !context.ActiveUuids.Contains(dependency.UUID)
				|| !HasDependencyPathTo(
					dependency.UUID,
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
				new[] { dependency.UUID }));
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

		return current.Dependencies.Items.Any(dependency =>
			!String.IsNullOrWhiteSpace(dependency.UUID)
			&& HasDependencyPathTo(dependency.UUID, targetUuid, context, visited));
	}
}

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
				? "This package contains a redux.mod.json file that could not be validated. Redux did not apply its claims or change any existing source association."
				: $"{manifest.Diagnostic} Redux did not apply its claims or change any existing source association."));
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
				"Limited mod.io support",
				"A subscribed mod.io mod can be restored or redownloaded by Baldur's Gate 3 after its local file is removed."));
		}
	}
}
