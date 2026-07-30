namespace DivinityModManager.Models;

public enum LoadOrderChangeKind
{
	Activated,
	Deactivated,
	Repositioned,
	AutomaticallyAdded
}

/// <summary>
/// One read-only difference between the currently exported user-mod order and a proposed export.
/// Positions are one-based and refer only to user-managed mods; the campaign module is excluded.
/// </summary>
public sealed class LoadOrderChange
{
	public LoadOrderChangeKind Kind { get; }
	public string UUID { get; }
	public string Name { get; }
	public int? PreviousPosition { get; }
	public int? NextPosition { get; }

	public LoadOrderChange(
		LoadOrderChangeKind kind,
		string uuid,
		string name,
		int? previousPosition,
		int? nextPosition)
	{
		Kind = kind;
		UUID = uuid ?? String.Empty;
		Name = String.IsNullOrWhiteSpace(name) ? UUID : name;
		PreviousPosition = previousPosition;
		NextPosition = nextPosition;
	}
}

/// <summary>
/// Immutable result used by Review Export and future saved-order comparison features.
/// </summary>
public sealed class LoadOrderComparison
{
	public bool HasPreviousOrder { get; }
	public IReadOnlyList<LoadOrderChange> Changes { get; }
	public IReadOnlyList<LoadOrderChange> Activated { get; }
	public IReadOnlyList<LoadOrderChange> Deactivated { get; }
	public IReadOnlyList<LoadOrderChange> Repositioned { get; }
	public IReadOnlyList<LoadOrderChange> AutomaticallyAdded { get; }
	public int ProposedModCount { get; }
	public bool HasChanges => Changes.Count > 0;

	public LoadOrderComparison(
		bool hasPreviousOrder,
		IEnumerable<LoadOrderChange> changes,
		int proposedModCount)
	{
		HasPreviousOrder = hasPreviousOrder;
		Changes = (changes ?? Enumerable.Empty<LoadOrderChange>()).ToArray();
		Activated = Changes.Where(change => change.Kind == LoadOrderChangeKind.Activated).ToArray();
		Deactivated = Changes.Where(change => change.Kind == LoadOrderChangeKind.Deactivated).ToArray();
		Repositioned = Changes.Where(change => change.Kind == LoadOrderChangeKind.Repositioned).ToArray();
		AutomaticallyAdded = Changes.Where(change => change.Kind == LoadOrderChangeKind.AutomaticallyAdded).ToArray();
		ProposedModCount = Math.Max(0, proposedModCount);
	}
}
