namespace DivinityModManager.Models;

/// <summary>
/// Presentation-only category metadata. Categories never alter a mod package or load-order position.
/// </summary>
public sealed class ModCategoryDisplayData
{
	public string Name { get; }
	public string Color { get; }
	public string IconId { get; }
	public bool HasIcon => !String.IsNullOrWhiteSpace(IconId);
	public string SoftColor => String.IsNullOrWhiteSpace(Color) ? "#243A3346" : $"#33{Color.TrimStart('#')}";

	public ModCategoryDisplayData(string name, string color, string iconId = "")
	{
		Name = name;
		Color = color;
		IconId = iconId ?? String.Empty;
	}
}
