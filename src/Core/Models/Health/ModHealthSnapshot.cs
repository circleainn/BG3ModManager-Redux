namespace DivinityModManager.Models.Health;

/// <summary>
/// Immutable result of evaluating one mod at one point in time.
/// </summary>
public sealed class ModHealthSnapshot
{
	private readonly string _loadOrderPositionSignature;
	public DivinityModData Mod { get; }
	public IReadOnlyList<ModHealthFinding> Findings { get; }
	public IReadOnlyList<ModHealthFinding> GeneralHealthFindings { get; }
	public IReadOnlyList<ModHealthFinding> HealthAttentionFindings { get; }
	public IReadOnlyList<ModHealthFinding> LoadOrderAdviceFindings { get; }
	public IReadOnlyList<ModHealthFinding> AttentionFindings { get; }
	public IReadOnlyList<ModHealthFinding> PackageBehaviorFindings { get; }
	public bool HasFindings => Findings.Count > 0;
	public int ErrorCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Error);
	public int WarningCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Warning);
	public int InfoCount => Findings.Count(finding => finding.Severity == ModHealthSeverity.Info);
	public int HealthErrorCount => HealthAttentionFindings.Count(finding => finding.Severity == ModHealthSeverity.Error);
	public int HealthWarningCount => HealthAttentionFindings.Count(finding => finding.Severity == ModHealthSeverity.Warning);
	public int LoadOrderAdviceCount => LoadOrderAdviceFindings.Count;
	public bool HasErrors => ErrorCount > 0;
	public bool HasWarnings => WarningCount > 0;
	public bool HasInfo => InfoCount > 0;
	public bool HasHealthErrors => HealthErrorCount > 0;
	public bool HasHealthWarnings => HealthWarningCount > 0;
	public bool HasOnlyLoadOrderAdvice =>
		LoadOrderAdviceCount > 0
		&& HealthErrorCount == 0
		&& HealthWarningCount == 0;
	public bool HasGeneralHealthAttention => GeneralHealthFindings.Count > 0;
	public bool HasGeneralHealthErrors => GeneralHealthFindings.Any(finding => finding.Severity == ModHealthSeverity.Error);
	public string GeneralHealthTooltip => String.Join(
		Environment.NewLine + Environment.NewLine,
		GeneralHealthFindings.Select(finding => $"{finding.Title}{Environment.NewLine}{finding.Message}"));
	public bool HasLoadOrderAdvice => LoadOrderAdviceCount > 0;
	public string LoadOrderAdviceTooltip
	{
		get
		{
			var advice = LoadOrderAdviceFindings
				.Select(finding => finding.Message)
				.ToArray();
			var position = String.IsNullOrWhiteSpace(Mod.LoadOrderDisplayText)
				? Mod.Index.ToString()
				: Mod.LoadOrderDisplayText;
			return advice.Length == 0
				? $"Load Order Advisor{Environment.NewLine}Current position: {position}{Environment.NewLine}No placement change is recommended."
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
		? "This mod or one of its dependencies may not work as expected."
		: HasWarnings
			? "Review these warnings before exporting or launching the game."
			: HasInfo
				? "This mod has loading or compatibility information."
				: "No issues found.";
	public string FindingCountSummary
	{
		get
		{
			if (!HasFindings) return "No issues found";
			var parts = new List<string>();
			if (ErrorCount > 0) parts.Add($"{ErrorCount} error{(ErrorCount == 1 ? String.Empty : "s")}");
			if (WarningCount > 0) parts.Add($"{WarningCount} warning{(WarningCount == 1 ? String.Empty : "s")}");
			if (InfoCount > 0) parts.Add($"{InfoCount} note{(InfoCount == 1 ? String.Empty : "s")}");
			return String.Join(" · ", parts);
		}
	}
	public string AttentionCountSummary
	{
		get
		{
			var parts = new List<string>();
			if (HealthErrorCount > 0)
				parts.Add($"{HealthErrorCount} error{(HealthErrorCount == 1 ? String.Empty : "s")}");
			if (HealthWarningCount > 0)
				parts.Add($"{HealthWarningCount} warning{(HealthWarningCount == 1 ? String.Empty : "s")}");
			if (LoadOrderAdviceCount > 0)
				parts.Add($"{LoadOrderAdviceCount} guidance note{(LoadOrderAdviceCount == 1 ? String.Empty : "s")}");
			return parts.Count == 0 ? FindingCountSummary : String.Join(" · ", parts);
		}
	}

	public ModHealthSnapshot(DivinityModData mod, IEnumerable<ModHealthFinding> findings)
	{
		Mod = mod ?? throw new ArgumentNullException(nameof(mod));
		_loadOrderPositionSignature = $"{mod.Index}|{mod.LoadOrderDisplayText}";
		Findings = (findings ?? Enumerable.Empty<ModHealthFinding>())
			.OrderByDescending(finding => finding.Severity)
			.ThenBy(finding => finding.Code)
			.ToArray();
		LoadOrderAdviceFindings = Findings
			.Where(IsLoadOrderAdvice)
			.ToArray();
		AttentionFindings = Findings
			.Where(finding =>
				IsLoadOrderAdvice(finding)
				|| finding.Severity is ModHealthSeverity.Error or ModHealthSeverity.Warning)
			.ToArray();
		HealthAttentionFindings = Findings
			.Where(finding =>
				!IsLoadOrderAdvice(finding)
				&& finding.Severity is ModHealthSeverity.Error or ModHealthSeverity.Warning)
			.ToArray();
		GeneralHealthFindings = Findings
			.Where(finding => finding.Code is
				ModHealthFindingCode.DuplicateUuid or
				ModHealthFindingCode.InactiveDependency or
				ModHealthFindingCode.SelfDependency or
				ModHealthFindingCode.DependencyVersionTooOld or
				ModHealthFindingCode.DeclaredConflict or
				ModHealthFindingCode.InvalidCreatorManifest or
				ModHealthFindingCode.McmNotActive)
			.ToArray();
		PackageBehaviorFindings = Findings
			.Where(finding => finding.Code is
				ModHealthFindingCode.LegacyModFixerIncluded or
				ModHealthFindingCode.AlwaysLoaded or
				ModHealthFindingCode.ContainsFileOverrides or
				ModHealthFindingCode.AlwaysLoadedWithLoadOrderEntry)
			.ToArray();
	}

	/// <summary>
	/// Returns true when replacing this snapshot would not change anything presented
	/// to the user. Retaining equivalent snapshots avoids invalidating health bindings
	/// on every mod row after an unrelated library change.
	/// </summary>
	public bool HasEquivalentFindings(ModHealthSnapshot other)
	{
		if (other == null || !ReferenceEquals(Mod, other.Mod) || Findings.Count != other.Findings.Count)
			return false;
		if ((HasLoadOrderAdvice || other.HasLoadOrderAdvice)
			&& !String.Equals(_loadOrderPositionSignature, other._loadOrderPositionSignature, StringComparison.Ordinal))
			return false;

		for (var index = 0; index < Findings.Count; index++)
		{
			var left = Findings[index];
			var right = other.Findings[index];
			if (left.Code != right.Code
				|| left.Severity != right.Severity
				|| !String.Equals(left.Title, right.Title, StringComparison.Ordinal)
				|| !String.Equals(left.Message, right.Message, StringComparison.Ordinal)
				|| !left.RelatedModUuids.SequenceEqual(right.RelatedModUuids, StringComparer.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsLoadOrderAdvice(ModHealthFinding finding) =>
		finding.Code is
			ModHealthFindingCode.DependencyLoadsLater or
			ModHealthFindingCode.RecommendedPredecessorLoadsLater or
			ModHealthFindingCode.DependencyCycle;
}
