using DivinityModManager.Models;
using DivinityModManager.Models.Health;

namespace DivinityModManager.ViewModels;

/// <summary>
/// Presentation-only grouping for the toolbar Health menu. Analyzer snapshots and finding
/// counts remain unchanged; identical findings are displayed once with their affected mods.
/// </summary>
public sealed class ModHealthFindingGroupViewModel
{
	public ModHealthFindingCode Code { get; }
	public ModHealthSeverity Severity { get; }
	public string Title { get; }
	public string Message { get; }
	public IReadOnlyList<ModHealthAffectedModViewModel> AffectedMods { get; }
	public ModHealthSnapshot PrimarySnapshot => AffectedMods[0].Snapshot;
	public int AffectedCount => AffectedMods.Count;
	public string AffectedCountText => $"{AffectedCount} mod{(AffectedCount == 1 ? String.Empty : "s")}";
	public bool HasMultipleAffectedMods => AffectedCount > 1;
	public bool HasErrors => Severity == ModHealthSeverity.Error;

	public ModHealthFindingGroupViewModel(
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
			.Select(snapshot => new ModHealthAffectedModViewModel(snapshot))
			.OrderBy(item => item.Mod.Index)
			.ThenBy(item => item.Mod.DisplayName, StringComparer.CurrentCultureIgnoreCase)
			.ToArray();

		if (AffectedMods.Count == 0)
			throw new ArgumentException("A finding group must contain at least one affected mod.", nameof(affectedSnapshots));
	}
}

public sealed class ModHealthAffectedModViewModel
{
	public ModHealthSnapshot Snapshot { get; }
	public DivinityModData Mod => Snapshot.Mod;

	public ModHealthAffectedModViewModel(ModHealthSnapshot snapshot)
	{
		Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
	}
}
