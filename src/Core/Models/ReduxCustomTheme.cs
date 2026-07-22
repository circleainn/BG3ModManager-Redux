using System.Runtime.Serialization;
using System.ComponentModel;

namespace DivinityModManager.Models;

/// <summary>
/// A safe Redux theme containing semantic colors and curated typography choices.
/// Layout, templates, icons, and behavior remain owned by the application and
/// cannot be supplied by imported themes.
/// </summary>
[DataContract]
public class ReduxCustomTheme : ReactiveObject
{
	[DataMember, Reactive] public string Id { get; set; } = Guid.NewGuid().ToString("N");
	[DataMember, Reactive] public string Name { get; set; } = "Custom Theme";
	[DataMember, Reactive] public ReduxThemeType BaseTheme { get; set; } = ReduxThemeType.ReduxDark;
	[DefaultValue(ReduxTypographyFont.Manrope)]
	[DataMember, Reactive] public ReduxTypographyFont TypographyFont { get; set; } = ReduxTypographyFont.Manrope;
	[DefaultValue(ReduxTextSize.Default)]
	[DataMember, Reactive] public ReduxTextSize TextSize { get; set; } = ReduxTextSize.Default;
	[DataMember, Reactive] public string BackgroundColor { get; set; } = "#0D0B10";
	[DataMember, Reactive] public string SurfaceColor { get; set; } = "#17121D";
	[DataMember, Reactive] public string AccentColor { get; set; } = "#9676FF";
	[DataMember, Reactive] public string TextColor { get; set; } = "#F2EDF7";
	[DataMember, Reactive] public string SuccessColor { get; set; } = "#49B486";
	[DataMember, Reactive] public string WarningColor { get; set; } = "#E0AA4B";
	[DataMember, Reactive] public string ErrorColor { get; set; } = "#E46674";
	[DataMember, Reactive] public string InfoColor { get; set; } = "#74A8E5";

	public ReduxCustomTheme Clone(bool createNewIdentity = false) => new()
	{
		Id = createNewIdentity ? Guid.NewGuid().ToString("N") : Id,
		Name = Name,
		BaseTheme = BaseTheme,
		TypographyFont = TypographyFont,
		TextSize = TextSize,
		BackgroundColor = BackgroundColor,
		SurfaceColor = SurfaceColor,
		AccentColor = AccentColor,
		TextColor = TextColor,
		SuccessColor = SuccessColor,
		WarningColor = WarningColor,
		ErrorColor = ErrorColor,
		InfoColor = InfoColor
	};
}
