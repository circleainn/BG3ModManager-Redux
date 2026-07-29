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

	public ModDiagnosticFindingGroupViewModel(
		ModHealthFinding finding,
		IEnumerable<ModHealthSnapshot> affectedSnapshots)
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
