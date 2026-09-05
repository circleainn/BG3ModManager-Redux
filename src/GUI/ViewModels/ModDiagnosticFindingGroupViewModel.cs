using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;

namespace DivinityModManager.ViewModels;

/// <summary>
/// Presentation-only grouping for the unified Mod Diagnostics menu. Analyzer snapshots and
/// rule boundaries remain unchanged; identical findings are displayed once with affected mods.
/// </summary>
public sealed class ModDiagnosticFindingGroupViewModel
{
	public ModHealthFindingCode Code { get; }
	public ModHealthSeverity Severity { get; }
	public string Title { get; }
	public string Message { get; }
	public IReadOnlyList<ModDiagnosticAffectedModViewModel> AffectedMods { get; }
	public ModHealthSnapshot PrimarySnapshot => AffectedMods[0].Snapshot;
	public int AffectedCount => AffectedMods.Count;
	public string AffectedCountText => $"{AffectedCount} mod{(AffectedCount == 1 ? String.Empty : "s")}";
	public bool HasMultipleAffectedMods => AffectedCount > 1;
	public bool HasErrors => Severity == ModHealthSeverity.Error;
	public DivinityModData PrimaryRelatedMod { get; }
	public string PrimaryRelatedModUuid { get; }
	public string PrimaryRelatedSourceUrl { get; }
	public bool CanRevealRelatedDependency { get; }
	public bool CanActivateRelatedDependency { get; }
	public bool CanOpenRelatedDependencySource { get; }
	public bool CanCopyRelatedDependencyUuid { get; }
	public bool CanOpenAffectedModSource { get; }
	public bool HasDiagnosticActions =>
		CanRevealRelatedDependency
		|| CanActivateRelatedDependency
		|| CanOpenRelatedDependencySource
		|| CanCopyRelatedDependencyUuid
		|| CanOpenAffectedModSource;
	public string RelatedDependencyActionText => "Show in list";

	public ModDiagnosticFindingGroupViewModel(
		ModHealthFinding finding,
		IEnumerable<ModHealthSnapshot> affectedSnapshots,
		IEnumerable<DivinityModData> installedMods,
		bool sourceIntegrationsEnabled)
	{
		if (finding == null) throw new ArgumentNullException(nameof(finding));

		Code = finding.Code;
		Severity = finding.Severity;
		Title = finding.Title;
		Message = finding.Message;
		AffectedMods = (affectedSnapshots ?? Enumerable.Empty<ModHealthSnapshot>())
			.Where(snapshot => snapshot?.Mod != null)
			.Distinct()
			.Select(snapshot => new ModDiagnosticAffectedModViewModel(snapshot))
			.OrderBy(item => item.Mod.Index)
			.ThenBy(item => item.Mod.DisplayName, StringComparer.CurrentCultureIgnoreCase)
			.ToArray();

		if (AffectedMods.Count == 0)
			throw new ArgumentException("A finding group must contain at least one affected mod.", nameof(affectedSnapshots));

		var installedByUuid = (installedMods ?? Enumerable.Empty<DivinityModData>())
			.Where(mod => mod != null && !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		PrimaryRelatedModUuid = finding.RelatedModUuids.FirstOrDefault() ?? String.Empty;
		PrimaryRelatedMod = finding.RelatedModUuids
			.Select(uuid => installedByUuid.TryGetValue(uuid, out var mod) ? mod : null)
			.FirstOrDefault(mod => mod != null);
		PrimaryRelatedSourceUrl = sourceIntegrationsEnabled
			? ResolveRelatedSourceUrl(PrimaryRelatedMod, PrimaryRelatedModUuid)
			: String.Empty;
		CanRevealRelatedDependency = PrimaryRelatedMod != null && Code is
			ModHealthFindingCode.InactiveDependency or
			ModHealthFindingCode.DependencyVersionTooOld or
			ModHealthFindingCode.DependencyLoadsLater or
			ModHealthFindingCode.RecommendedPredecessorLoadsLater;
		CanActivateRelatedDependency = PrimaryRelatedMod != null
			&& !PrimaryRelatedMod.IsActive
			&& Code == ModHealthFindingCode.InactiveDependency;
		CanOpenRelatedDependencySource = !String.IsNullOrWhiteSpace(PrimaryRelatedSourceUrl)
			&& Code is ModHealthFindingCode.InactiveDependency
				or ModHealthFindingCode.DependencyVersionTooOld
				or ModHealthFindingCode.DependencyLoadsLater
				or ModHealthFindingCode.RecommendedPredecessorLoadsLater
				or ModHealthFindingCode.MissingDependency;
		CanCopyRelatedDependencyUuid = !String.IsNullOrWhiteSpace(PrimaryRelatedModUuid)
			&& Code is ModHealthFindingCode.MissingDependency
				or ModHealthFindingCode.InactiveDependency
				or ModHealthFindingCode.DependencyVersionTooOld
				or ModHealthFindingCode.DependencyLoadsLater
				or ModHealthFindingCode.RecommendedPredecessorLoadsLater;
		CanOpenAffectedModSource = sourceIntegrationsEnabled
			&& Code == ModHealthFindingCode.MissingDependency
			&& AffectedMods.Count == 1
			&& !String.IsNullOrWhiteSpace(PrimarySnapshot.Mod.Metadata?.SourcePageUrl);
	}

	private static string ResolveRelatedSourceUrl(DivinityModData installedMod, string relatedUuid)
	{
		var installedUrl = installedMod?.Metadata?.SourcePageUrl;
		if (!String.IsNullOrWhiteSpace(installedUrl)) return installedUrl;

		return ReduxModDatabaseService.TryResolveModuleUuid(relatedUuid)
			?.CreateMetadata(relatedUuid)
			.SourcePageUrl
			?? String.Empty;
	}
}

public sealed class ModDiagnosticAffectedModViewModel
{
	public ModHealthSnapshot Snapshot { get; }
	public DivinityModData Mod => Snapshot.Mod;

	public ModDiagnosticAffectedModViewModel(ModHealthSnapshot snapshot)
	{
		Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
	}
}
