namespace DivinityModManager.ViewModels;

using ReactiveUI;

/// <summary>
/// A category shown in the Redux category navigator. This is view metadata only;
/// it never changes a mod's position in the underlying load order.
/// </summary>
public sealed class ModCategoryFilterItem : ReactiveObject
{
	private bool _hasNewMods;

	public string Name { get; }
	public string DisplayCategory => Name;
	public int Count { get; }
	public string Color { get; }
	public string IconId { get; }
	public string Description { get; }
	public bool HasIcon => !String.IsNullOrWhiteSpace(IconId);
	public bool HasDescription => !String.IsNullOrWhiteSpace(Description);
	public string SoftColor => String.IsNullOrWhiteSpace(Color) ? "#243A3346" : $"#33{Color.TrimStart('#')}";
	public string HoverColor => String.IsNullOrWhiteSpace(Color) ? "#183A3346" : $"#1F{Color.TrimStart('#')}";
	public string SelectionColor => String.IsNullOrWhiteSpace(Color) ? "#283A3346" : $"#2E{Color.TrimStart('#')}";
	public string CountHoverColor => String.IsNullOrWhiteSpace(Color) ? "#183A3346" : $"#24{Color.TrimStart('#')}";
	public string CountSelectionColor => String.IsNullOrWhiteSpace(Color) ? "#203A3346" : $"#24{Color.TrimStart('#')}";
	public bool HasNewMods
	{
		get => _hasNewMods;
		set => this.RaiseAndSetIfChanged(ref _hasNewMods, value);
	}

	public ModCategoryFilterItem(string name, int count, string color, string iconId = "",
		bool hasNewMods = false, string description = "")
	{
		Name = name;
		Count = count;
		Color = color;
		IconId = iconId ?? String.Empty;
		Description = description?.Trim() ?? String.Empty;
		_hasNewMods = hasNewMods;
	}
}
