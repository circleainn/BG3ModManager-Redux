namespace DivinityModManager.Models;

/// <summary>
/// Presentation-only category metadata. Categories never alter a mod package or load-order position.
/// </summary>
public sealed class ModCategoryDisplayData
{
	public string Name { get; }
	public string Color { get; }
	public string IconId { get; }
	public string Description { get; }
	public bool ShowInterfaceIcons { get; }
	public bool UseIconsOnly { get; }
	public bool UseCategoryColorsForText { get; }
	public bool HasIcon => !String.IsNullOrWhiteSpace(IconId);
	public bool HasDescription => !String.IsNullOrWhiteSpace(Description);
	public string SoftColor => String.IsNullOrWhiteSpace(Color) ? "#243A3346" : $"#33{Color.TrimStart('#')}";

	public ModCategoryDisplayData(
		string name,
		string color,
		string iconId = "",
		string description = "",
		bool showInterfaceIcons = true,
		bool useIconsOnly = false,
		bool useCategoryColorsForText = false)
	{
		Name = name;
		Color = color;
		IconId = iconId ?? String.Empty;
		Description = description?.Trim() ?? String.Empty;
		ShowInterfaceIcons = showInterfaceIcons;
		UseIconsOnly = useIconsOnly;
		UseCategoryColorsForText = useCategoryColorsForText;
	}
}
