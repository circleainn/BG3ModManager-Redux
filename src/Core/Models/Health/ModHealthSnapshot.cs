namespace DivinityModManager.Models.Health;

/// <summary>
/// Immutable result of evaluating one mod at one point in time.
/// </summary>
public sealed class ModHealthSnapshot
{
	public DivinityModData Mod { get; }
	public IReadOnlyList<ModHealthFinding> Findings { get; }
	public IReadOnlyList<ModHealthFinding> GeneralHealthFindings { get; }
	public bool HasFindings => Findings.Count > 0;
	public int ErrorCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Error);
	public int WarningCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Warning);
	public int InfoCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Info);
	public bool HasErrors => ErrorCount > 0;
	public bool HasWarnings => WarningCount > 0;
	public bool HasInfo => InfoCount > 0;
	public bool HasGeneralHealthAttention => GeneralHealthFindings.Count > 0;
	public bool HasGeneralHealthErrors => GeneralHealthFindings.Any(finding => finding.Severity == ModHealthSeverity.Error);
	public string GeneralHealthTooltip => String.Join(
		Environment.NewLine + Environment.NewLine,
		GeneralHealthFindings.Select(finding => $"{finding.Title}{Environment.NewLine}{finding.Message}"));
	public bool HasLoadOrderAdvice => Findings.Any(finding => finding.Code == ModHealthFindingCode.DependencyLoadsLater);
	public string LoadOrderAdviceTooltip
	{
		get
		{
			var advice = Findings
				.Where(finding => finding.Code == ModHealthFindingCode.DependencyLoadsLater)
				.Select(finding => finding.Message)
				.ToArray();
			var position = String.IsNullOrWhiteSpace(Mod.LoadOrderDisplayText)
				? Mod.Index.ToString()
				: Mod.LoadOrderDisplayText;
			return advice.Length == 0
				? $"Load Order Advisor{Environment.NewLine}Current position: {position}{Environment.NewLine}No evidence-based placement change is recommended."
				: $"Load Order Advisor{Environment.NewLine}Current position: {position}{Environment.NewLine}{String.Join(Environment.NewLine + Environment.NewLine, advice)}";
		}
	}
	public bool NeedsAttention => HasErrors || HasWarnings;
	public bool IsClear => !HasFindings;
	public ModHealthSeverity HighestSeverity => Findings.Count == 0
		? ModHealthSeverity.Info
		: Findings.Max(finding => finding.Severity);
	public string StatusTitle => HasErrors
		? "Action recommended"
		: HasWarnings
			? "Review recommended"
			: HasInfo
				? "Compatibility information"
				: "No issues detected";
	public string StatusDescription => HasErrors
		? "Redux found conditions likely to prevent this mod or one of its dependencies from working as expected."
		: HasWarnings
			? "Redux found conditions worth reviewing before exporting the load order or launching the game."
			: HasInfo
				? "Redux found loading or compatibility behavior worth knowing about."
				: "No issues were detected from the package metadata and runtime state currently available to Redux.";
	public string FindingCountSummary
	{
		get
		{
			if (!HasFindings) return "Read-only analysis is clear";
			var parts = new List<string>();
			if (ErrorCount > 0) parts.Add($"{ErrorCount} error{(ErrorCount == 1 ? String.Empty : "s")}");
			if (WarningCount > 0) parts.Add($"{WarningCount} warning{(WarningCount == 1 ? String.Empty : "s")}");
			if (InfoCount > 0) parts.Add($"{InfoCount} note{(InfoCount == 1 ? String.Empty : "s")}");
			return String.Join(" · ", parts);
		}
	}

	public ModHealthSnapshot(DivinityModData mod, IEnumerable<ModHealthFinding> findings)
	{
		Mod = mod ?? throw new ArgumentNullException(nameof(mod));
		Findings = (findings ?? Enumerable.Empty<ModHealthFinding>())
			.OrderByDescending(finding => finding.Severity)
			.ThenBy(finding => finding.Code)
			.ToArray();
		GeneralHealthFindings = Findings
			.Where(finding => finding.Code is
				ModHealthFindingCode.DuplicateUuid or
				ModHealthFindingCode.InactiveDependency or
				ModHealthFindingCode.DeclaredConflict)
			.ToArray();
	}
}
