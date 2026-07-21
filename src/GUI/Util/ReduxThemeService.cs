using AdonisUI;

using DivinityModManager.Models;

using Newtonsoft.Json;

using System.Windows;
using System.Windows.Media;

namespace DivinityModManager.Util;

/// <summary>
/// Applies validated color-only theme overlays on top of a built-in Redux theme.
/// Custom theme files are JSON data and never load XAML or executable resources.
/// </summary>
public static class ReduxThemeService
{
	private static readonly string[] OverrideKeys =
	[
		"ReduxWindowColor", "ReduxListInteriorColor", "ReduxSurfaceColor", "ReduxSurfaceElevatedColor",
		"ReduxSurfaceMutedColor", "ReduxBorderColor", "ReduxBorderStrongColor", "ReduxHoverColor",
		"ReduxPressedColor", "ReduxAccentColor", "ReduxAccentHoverColor", "ReduxAccentSoftColor",
		"ReduxSelectionColor", "ReduxSuccessColor", "ReduxSuccessSoftColor", "ReduxWarningColor",
		"ReduxErrorColor", "ReduxInfoColor", "ReduxTextPrimaryColor", "ReduxTextSecondaryColor",
		"ReduxTextMutedColor", "ReduxAccentForegroundColor", "ReduxGithubIconColor"
	];

	private static readonly IReadOnlyDictionary<ReduxThemeType, string[]> BaseColors =
		new Dictionary<ReduxThemeType, string[]>
		{
			[ReduxThemeType.ReduxDark] = ["#0D0B10", "#17121D", "#9676FF", "#F2EDF7", "#49B486", "#E0AA4B", "#E46674", "#74A8E5"],
			[ReduxThemeType.ReduxLight] = ["#EDE9F2", "#F7F4FA", "#7355E7", "#181321", "#27885E", "#AD6D14", "#BD3047", "#356CA8"],
			[ReduxThemeType.Parchment] = ["#E8DDC7", "#EFE3CC", "#8F2E32", "#30251D", "#5E7D4B", "#B8791D", "#A82F37", "#4E7196"]
		};
	private static readonly IReadOnlyDictionary<ReduxThemeType, string[]> BuiltInResourceValues =
		new Dictionary<ReduxThemeType, string[]>
		{
			[ReduxThemeType.ReduxDark] = ["#0D0B10", "#110D15", "#17121D", "#1C1623", "#241C2C", "#33283F", "#4B3A5C", "#2A2034", "#33263F", "#9676FF", "#B49DFF", "#322543", "#3D2C57", "#49B486", "#193F32", "#E0AA4B", "#E46674", "#74A8E5", "#F2EDF7", "#C8BDD4", "#A094AE", "#17131C", "#FFFFFF"],
			[ReduxThemeType.ReduxLight] = ["#EDE9F2", "#F2EEF6", "#F7F4FA", "#F0EBF5", "#E5DFEC", "#D0C6DB", "#AA9ABB", "#E9E2F0", "#DCD2E7", "#7355E7", "#6041D5", "#E5DCFA", "#D8CAFA", "#27885E", "#D7EADF", "#AD6D14", "#BD3047", "#356CA8", "#181321", "#51465F", "#776A85", "#FFFFFF", "#000000"],
			[ReduxThemeType.Parchment] = ["#E8DDC7", "#F4EAD6", "#EFE3CC", "#F7ECD8", "#DDD0B8", "#BCA98B", "#927B5B", "#E7D6B9", "#D8C4A3", "#8F2E32", "#A84649", "#E7C9C2", "#DCB9B2", "#5E7D4B", "#D8E1C3", "#B8791D", "#A82F37", "#4E7196", "#30251D", "#5C4A3A", "#74614D", "#FFF7E8", "#000000"]
		};

	public static ReduxCustomTheme GetActiveTheme(DivinityModManagerSettings settings) =>
		settings?.CustomThemes?.FirstOrDefault(theme =>
			theme.Id.Equals(settings.ActiveCustomThemeId, StringComparison.OrdinalIgnoreCase));

	public static ReduxCustomTheme CreateFromBase(string name, ReduxThemeType baseTheme,
		ReduxTypographyFont typographyFont = 0)
	{
		if (!BaseColors.TryGetValue(baseTheme, out var colors))
		{
			baseTheme = ReduxThemeType.ReduxDark;
			colors = BaseColors[baseTheme];
		}
		return new ReduxCustomTheme
		{
			Name = String.IsNullOrWhiteSpace(name) ? "Custom Theme" : name.Trim(),
			BaseTheme = baseTheme,
			TypographyFont = NormalizeTypography(typographyFont, baseTheme),
			BackgroundColor = colors[0],
			SurfaceColor = colors[1],
			AccentColor = colors[2],
			TextColor = colors[3],
			SuccessColor = colors[4],
			WarningColor = colors[5],
			ErrorColor = colors[6],
			InfoColor = colors[7]
		};
	}

	public static void ResetToBase(ReduxCustomTheme theme, ReduxThemeType baseTheme)
	{
		var defaults = CreateFromBase(theme.Name, baseTheme);
		theme.BaseTheme = baseTheme;
		theme.BackgroundColor = defaults.BackgroundColor;
		theme.SurfaceColor = defaults.SurfaceColor;
		theme.AccentColor = defaults.AccentColor;
		theme.TextColor = defaults.TextColor;
		theme.SuccessColor = defaults.SuccessColor;
		theme.WarningColor = defaults.WarningColor;
		theme.ErrorColor = defaults.ErrorColor;
		theme.InfoColor = defaults.InfoColor;
	}

	public static bool TryValidate(ReduxCustomTheme theme, out string error)
	{
		if (theme == null)
		{
			error = "The custom theme file is empty.";
			return false;
		}
		if (String.IsNullOrWhiteSpace(theme.Name))
		{
			error = "Enter a name for the custom theme.";
			return false;
		}
		if (!BaseColors.ContainsKey(theme.BaseTheme))
		{
			error = "The custom theme uses an unsupported base theme.";
			return false;
		}
		if (!Enum.IsDefined(theme.TypographyFont) || theme.TypographyFont == 0)
		{
			error = "The custom theme uses an unsupported typeface.";
			return false;
		}
		foreach (var (label, value) in GetEditableColors(theme))
		{
			if (!TryParseColor(value, out _))
			{
				error = $"{label} must be a valid #RRGGBB color.";
				return false;
			}
		}
		error = null;
		return true;
	}

	public static void Apply(ResourceDictionary resources, ReduxThemeType builtInTheme, ReduxCustomTheme customTheme = null)
	{
		if (resources == null) return;
		foreach (var key in OverrideKeys) resources.Remove(key);
		var baseTheme = customTheme != null && TryValidate(customTheme, out _) ? customTheme.BaseTheme : builtInTheme;
		ResourceLocator.SetColorScheme(resources, DivinityApp.GetThemeUri(baseTheme));
		var palette = customTheme != null && TryValidate(customTheme, out _)
			? CreateResourceColors(customTheme)
			: CreateBuiltInResourceColors(baseTheme);
		foreach (var entry in palette)
		{
			var owner = FindResourceOwner(resources, entry.Key) ?? resources;
			owner[entry.Key] = entry.Value;
		}
	}

	private static Dictionary<string, Color> CreateBuiltInResourceColors(ReduxThemeType theme)
	{
		if (!BuiltInResourceValues.TryGetValue(theme, out var values)) values = BuiltInResourceValues[ReduxThemeType.ReduxDark];
		var palette = new Dictionary<string, Color>(OverrideKeys.Length);
		for (var index = 0; index < OverrideKeys.Length; index++) palette[OverrideKeys[index]] = Parse(values[index]);
		return palette;
	}

	private static ResourceDictionary FindResourceOwner(ResourceDictionary resources, string key)
	{
		if (resources.Contains(key)) return resources;
		for (var index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
		{
			var owner = FindResourceOwner(resources.MergedDictionaries[index], key);
			if (owner != null) return owner;
		}
		return null;
	}

	public static void Export(string path, ReduxCustomTheme theme)
	{
		if (!TryValidate(theme, out var error)) throw new InvalidDataException(error);
		var contents = JsonConvert.SerializeObject(theme, Formatting.Indented);
		AtomicFileWriter.WriteAllText(path, contents, validateTemporaryFile: temporaryPath =>
		{
			var imported = JsonConvert.DeserializeObject<ReduxCustomTheme>(File.ReadAllText(temporaryPath));
			return TryValidate(imported, out _);
		});
	}

	public static ReduxCustomTheme Import(string path)
	{
		var theme = JsonConvert.DeserializeObject<ReduxCustomTheme>(File.ReadAllText(path));
		if (theme != null) theme.TypographyFont = NormalizeTypography(theme.TypographyFont, theme.BaseTheme);
		if (!TryValidate(theme, out var error)) throw new InvalidDataException(error);
		theme.Id = Guid.NewGuid().ToString("N");
		theme.Name = theme.Name.Trim();
		NormalizeColors(theme);
		return theme;
	}

	public static void NormalizeColors(ReduxCustomTheme theme)
	{
		theme.BackgroundColor = Normalize(theme.BackgroundColor);
		theme.SurfaceColor = Normalize(theme.SurfaceColor);
		theme.AccentColor = Normalize(theme.AccentColor);
		theme.TextColor = Normalize(theme.TextColor);
		theme.SuccessColor = Normalize(theme.SuccessColor);
		theme.WarningColor = Normalize(theme.WarningColor);
		theme.ErrorColor = Normalize(theme.ErrorColor);
		theme.InfoColor = Normalize(theme.InfoColor);
	}

	private static Dictionary<string, Color> CreateResourceColors(ReduxCustomTheme theme)
	{
		var background = Parse(theme.BackgroundColor);
		var surface = Parse(theme.SurfaceColor);
		var accent = Parse(theme.AccentColor);
		var text = Parse(theme.TextColor);
		var success = Parse(theme.SuccessColor);
		var warning = Parse(theme.WarningColor);
		var error = Parse(theme.ErrorColor);
		var info = Parse(theme.InfoColor);
		var isDark = RelativeLuminance(background) < 0.42;
		var contrastTarget = isDark ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black;

		return new Dictionary<string, Color>
		{
			["ReduxWindowColor"] = background,
			["ReduxListInteriorColor"] = Mix(background, surface, 0.42),
			["ReduxSurfaceColor"] = surface,
			["ReduxSurfaceElevatedColor"] = Mix(surface, contrastTarget, 0.055),
			["ReduxSurfaceMutedColor"] = Mix(surface, text, isDark ? 0.075 : 0.09),
			["ReduxBorderColor"] = Mix(surface, text, isDark ? 0.16 : 0.18),
			["ReduxBorderStrongColor"] = Mix(surface, text, isDark ? 0.28 : 0.32),
			["ReduxHoverColor"] = Mix(surface, accent, 0.12),
			["ReduxPressedColor"] = Mix(surface, accent, 0.20),
			["ReduxAccentColor"] = accent,
			["ReduxAccentHoverColor"] = Mix(accent, contrastTarget, 0.18),
			["ReduxAccentSoftColor"] = Mix(surface, accent, 0.22),
			["ReduxSelectionColor"] = Mix(surface, accent, 0.34),
			["ReduxSuccessColor"] = success,
			["ReduxSuccessSoftColor"] = Mix(surface, success, 0.18),
			["ReduxWarningColor"] = warning,
			["ReduxErrorColor"] = error,
			["ReduxInfoColor"] = info,
			["ReduxTextPrimaryColor"] = text,
			["ReduxTextSecondaryColor"] = Mix(surface, text, 0.78),
			["ReduxTextMutedColor"] = Mix(surface, text, 0.58),
			["ReduxAccentForegroundColor"] = BestForeground(accent),
			["ReduxGithubIconColor"] = theme.BaseTheme == ReduxThemeType.ReduxDark
				? System.Windows.Media.Colors.White
				: System.Windows.Media.Colors.Black
		};
	}

	private static IEnumerable<(string Label, string Value)> GetEditableColors(ReduxCustomTheme theme)
	{
		yield return ("Background", theme.BackgroundColor);
		yield return ("Surface", theme.SurfaceColor);
		yield return ("Accent", theme.AccentColor);
		yield return ("Text", theme.TextColor);
		yield return ("Success", theme.SuccessColor);
		yield return ("Warning", theme.WarningColor);
		yield return ("Error", theme.ErrorColor);
		yield return ("Information", theme.InfoColor);
	}

	private static bool TryParseColor(string value, out Color color)
	{
		color = default;
		if (String.IsNullOrWhiteSpace(value)) return false;
		try
		{
			if (ColorConverter.ConvertFromString(value) is not Color parsed) return false;
			color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static Color Parse(string value) => TryParseColor(value, out var color) ? color : System.Windows.Media.Colors.Magenta;
	private static string Normalize(string value) => $"#{Parse(value).R:X2}{Parse(value).G:X2}{Parse(value).B:X2}";
	private static ReduxTypographyFont NormalizeTypography(ReduxTypographyFont value, ReduxThemeType baseTheme) =>
		Enum.IsDefined(value) && value != 0
			? value
			: ReduxTypographyFont.Manrope;
	private static Color Mix(Color left, Color right, double amount) => Color.FromRgb(
		(byte)Math.Round(left.R + ((right.R - left.R) * amount)),
		(byte)Math.Round(left.G + ((right.G - left.G) * amount)),
		(byte)Math.Round(left.B + ((right.B - left.B) * amount)));
	private static double RelativeLuminance(Color color) => ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255d;
	private static Color BestForeground(Color background) => RelativeLuminance(background) > 0.56 ? Color.FromRgb(24, 19, 33) : System.Windows.Media.Colors.White;
}
