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
			[ReduxThemeType.ReduxDark] = ["#0D0B10", "#17121D", "#9676FF", "#F2EDF7", "#3FC58B", "#F0B43C", "#F05D70", "#5B99FA"],
			[ReduxThemeType.ReduxLight] = ["#E4DFE9", "#F1EDF4", "#694AD6", "#201927", "#147A53", "#A45B08", "#B92340", "#326A9F"],
			[ReduxThemeType.Parchment] = ["#DCCFB7", "#EBDEC5", "#8B3034", "#2D231B", "#3F6B35", "#8F4C08", "#AC2E3E", "#466989"]
		};
	private static readonly IReadOnlyDictionary<ReduxThemeType, string[]> BuiltInResourceValues =
		new Dictionary<ReduxThemeType, string[]>
		{
			[ReduxThemeType.ReduxDark] = ["#0D0B10", "#110D15", "#17121D", "#1C1623", "#241C2C", "#33283F", "#4B3A5C", "#2A2034", "#33263F", "#9676FF", "#B49DFF", "#322543", "#3D2C57", "#49B486", "#193F32", "#E0AA4B", "#E46674", "#5B99FA", "#F2EDF7", "#C8BDD4", "#A094AE", "#17131C", "#FFFFFF"],
			[ReduxThemeType.ReduxLight] = ["#E4DFE9", "#EAE6EF", "#F1EDF4", "#ECE7F1", "#DDD6E5", "#C5B9D1", "#9584A8", "#DED6E7", "#CEC2DA", "#694AD6", "#5738C4", "#DBD1F1", "#CBB9F1", "#147A53", "#CEE4D8", "#A45B08", "#B92340", "#326A9F", "#201927", "#554A61", "#756A80", "#FFFFFF", "#000000"],
			[ReduxThemeType.Parchment] = ["#DCCFB7", "#E7DAC2", "#EBDEC5", "#F0E4CE", "#D1C0A2", "#AD9876", "#806A49", "#DBC7A6", "#CAB187", "#8B3034", "#A13E42", "#DFC0B6", "#D2A9A1", "#3F6B35", "#C8D7B4", "#8F4C08", "#AC2E3E", "#466989", "#2D231B", "#584737", "#6F5D4B", "#FFF8EB", "#000000"]
		};

	public static ReduxCustomTheme GetActiveTheme(DivinityModManagerSettings settings) =>
		settings?.CustomThemes?.FirstOrDefault(theme =>
			theme.Id.Equals(settings.ActiveCustomThemeId, StringComparison.OrdinalIgnoreCase));

	// Must mirror the manager's own settings serializer. IgnoreAndPopulate in particular is
	// load-bearing: the settings file omits any value equal to its DefaultValue, so a user on
	// the default theme has no ColorTheme key at all. Without IgnoreAndPopulate, ColorTheme
	// stays 0 and DarkThemeEnabled stays false, and the settings model's OnDeserialized hook
	// resolves that pair to ReduxLight -- the opposite of what was actually saved.
	private static readonly JsonSerializerSettings PresentationReadSettings = new()
	{
		DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
		MissingMemberHandling = MissingMemberHandling.Ignore,
		Error = static (_, args) => args.ErrorContext.Handled = true,
		Converters = [new Newtonsoft.Json.Converters.StringEnumConverter()]
	};

	/// <summary>
	/// Applies the colour theme persisted by the previous session, read straight from disk.
	/// </summary>
	/// <remarks>
	/// The startup splash is created and shown before <c>MainWindowViewModel.LoadSettings</c>
	/// runs, so at that point <c>Settings.ColorTheme</c> is still its default and the splash
	/// renders Redux Dark no matter what the user last selected. This reads presentation
	/// preferences only; LoadSettings remains the sole owner of applying settings to the
	/// application.
	/// </remarks>
	public static void ApplyPersistedTheme(ResourceDictionary resources)
	{
		if (resources == null) return;

		try
		{
			var settingsFile = DivinityApp.GetAppDirectory("Data", "settings.json");
			if (!File.Exists(settingsFile)) return;

			var settings = DivinityJsonUtils.SafeDeserialize<DivinityModManagerSettings>(
				File.ReadAllText(settingsFile),
				PresentationReadSettings);
			if (settings == null) return;

			Apply(resources, settings.ColorTheme, GetActiveTheme(settings));
			// Apply accessibility after the theme so freshly loaded template resources
			// cannot restore popup motion that Reduce Motion has disabled.
			ReduxWindowBehavior.ConfigureAccessibility(
				settings.ReduceMotion,
				settings.DisableBackgroundEffects);
		}
		catch (Exception ex)
		{
			// A first run, a locked file or a corrupt settings file all just mean the
			// built-in palette stays in place.
			DivinityApp.Log($"Could not read the persisted theme for the startup window: {ex.Message}");
		}
	}

	public static ReduxCustomTheme CreateFromBase(string name, ReduxThemeType baseTheme,
		ReduxTypographyFont typographyFont = 0, ReduxTextSize textSize = 0, string customTypographyFont = "",
		bool? useCategoryColorsForInteractions = null, bool? showCategoryIconsInPills = null,
		bool? useCategoryColorsForSidebarText = null,
		bool? useIconsOnly = null)
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
			CustomTypographyFont = customTypographyFont ?? String.Empty,
			TextSize = NormalizeTextSize(textSize),
			UseCategoryColorsForInteractions = useCategoryColorsForInteractions ?? baseTheme == ReduxThemeType.ReduxDark,
			ShowCategoryIconsInPills = showCategoryIconsInPills ?? true,
			UseCategoryColorsForSidebarText = useCategoryColorsForSidebarText ?? baseTheme == ReduxThemeType.ReduxDark,
			UseIconsOnly = (showCategoryIconsInPills ?? true) && (useIconsOnly ?? false),
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
		theme.UseCategoryColorsForInteractions = defaults.UseCategoryColorsForInteractions;
		theme.ShowCategoryIconsInPills = defaults.ShowCategoryIconsInPills;
		theme.UseCategoryColorsForSidebarText = defaults.UseCategoryColorsForSidebarText;
		theme.UseIconsOnly = defaults.UseIconsOnly;
	}

	public static void ApplyBuiltInCategoryPresentation(DivinityModManagerSettings settings, ReduxThemeType theme)
	{
		if (settings == null) return;
		settings.UseCategoryColorsForInteractions = theme == ReduxThemeType.ReduxDark;
		settings.ShowCategoryIconsInPills = true;
		settings.UseCategoryColorsForSidebarText = theme == ReduxThemeType.ReduxDark;
		settings.UseIconsOnly = false;
	}

	public static void ApplyCustomCategoryPresentation(DivinityModManagerSettings settings, ReduxCustomTheme theme)
	{
		if (settings == null || theme == null) return;
		settings.UseCategoryColorsForInteractions = theme.UseCategoryColorsForInteractions;
		settings.ShowCategoryIconsInPills = theme.ShowCategoryIconsInPills;
		settings.UseCategoryColorsForSidebarText = theme.UseCategoryColorsForSidebarText;
		settings.UseIconsOnly = theme.ShowCategoryIconsInPills && theme.UseIconsOnly;
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
		if (!Enum.IsDefined(theme.TextSize) || theme.TextSize == 0)
		{
			error = "The custom theme uses an unsupported text size.";
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
		var hasValidCustomTheme = customTheme != null && TryValidate(customTheme, out _);
		var baseTheme = hasValidCustomTheme ? customTheme.BaseTheme : builtInTheme;
		ResourceLocator.SetColorScheme(resources, DivinityApp.GetThemeUri(baseTheme));
		var palette = hasValidCustomTheme
			? CreateResourceColors(customTheme)
			: CreateBuiltInResourceColors(baseTheme);
		ApplyPalette(resources, palette, baseTheme, hasValidCustomTheme);
	}

	public static void PreviewColors(ResourceDictionary resources, ReduxCustomTheme customTheme)
	{
		if (resources == null || customTheme == null || !TryValidate(customTheme, out _)) return;
		ApplyPalette(resources, CreateResourceColors(customTheme), customTheme.BaseTheme, isCustomTheme: true);
	}

	private static void ApplyPalette(ResourceDictionary resources, IReadOnlyDictionary<string, Color> palette,
		ReduxThemeType baseTheme, bool isCustomTheme)
	{
		foreach (var entry in palette)
		{
			var owner = FindResourceOwner(resources, entry.Key) ?? resources;
			owner[entry.Key] = entry.Value;
		}
		// Pill gradients contain alpha-bearing color stops, so WPF cannot express them as
		// simple DynamicResource color references. Regenerate every semantic variant when
		// the palette changes; otherwise custom themes inherit the base theme's pill colors.
		SetBrushResource(resources, "ReduxAccentPillBackground", CreatePillGradient(palette["ReduxAccentColor"]));
		SetBrushResource(resources, "ReduxSelectionPillBackground", CreateSelectionPillGradient(palette["ReduxSelectionColor"]));
		SetBrushResource(resources, "ReduxSuccessPillBackground", CreatePillGradient(palette["ReduxSuccessColor"]));
		SetBrushResource(resources, "ReduxWarningPillBackground", CreatePillGradient(palette["ReduxWarningColor"]));
		SetBrushResource(resources, "ReduxErrorPillBackground", CreatePillGradient(palette["ReduxErrorColor"]));
		SetBrushResource(resources, "ReduxInfoPillBackground", CreatePillGradient(palette["ReduxInfoColor"]));
		// Reapply the built-in art direction explicitly. This also prevents a generated
		// custom-theme brush from surviving when the user switches back to the same base theme.
		var primaryActionOwner = FindResourceOwner(resources, "ReduxPrimaryActionBackgroundBrush") ?? resources;
		primaryActionOwner["ReduxPrimaryActionBackgroundBrush"] = isCustomTheme
			? CreatePrimaryActionGradient(palette["ReduxAccentColor"], restrainedHueShift: false)
			: baseTheme == ReduxThemeType.Parchment
				? CreateGradient("#681D2B", "#962735", "#B7383D")
				: CreatePrimaryActionGradient(palette["ReduxAccentColor"], restrainedHueShift: false);

		var destructiveActionOwner = FindResourceOwner(resources, "ReduxDestructiveActionBackgroundBrush") ?? resources;
		destructiveActionOwner["ReduxDestructiveActionBackgroundBrush"] = isCustomTheme
			? CreateDestructiveActionGradient(palette["ReduxErrorColor"])
			: CreateBuiltInDestructiveActionGradient(baseTheme);
		var destructiveForegroundOwner = FindResourceOwner(resources, "ReduxDestructiveActionForegroundBrush") ?? resources;
		destructiveForegroundOwner["ReduxDestructiveActionForegroundBrush"] = new SolidColorBrush(isCustomTheme
			? BestForeground(ScaleBrightness(palette["ReduxErrorColor"], 0.72))
			: System.Windows.Media.Colors.White);
	}

	private static void SetBrushResource(ResourceDictionary resources, string key, Brush brush)
	{
		var owner = FindResourceOwner(resources, key) ?? resources;
		owner[key] = brush;
	}

	private static LinearGradientBrush CreatePillGradient(Color color)
	{
		var leading = color;
		leading.A = 0x3E;
		var trailing = color;
		trailing.A = 0x1E;
		var brush = new LinearGradientBrush(
			leading,
			trailing,
			new Point(0, 0),
			new Point(1, 0));
		if (brush.CanFreeze) brush.Freeze();
		return brush;
	}

	private static LinearGradientBrush CreateSelectionPillGradient(Color color)
	{
		var leading = color;
		leading.A = 0xFF;
		var trailing = color;
		trailing.A = 0xD9;
		var brush = new LinearGradientBrush(
			leading,
			trailing,
			new Point(0, 0),
			new Point(1, 0));
		if (brush.CanFreeze) brush.Freeze();
		return brush;
	}

	private static LinearGradientBrush CreatePrimaryActionGradient(Color accent, bool restrainedHueShift)
	{
		// Warm/light surfaces need stronger value separation while keeping every stop
		// derived from the selected accent. Scaling RGB preserves saturation better than
		// mixing toward the paper surface or white.
		var leading = restrainedHueShift
			? ScaleBrightness(accent, 0.82)
			: Mix(accent, ShiftHue(accent, -18), 0.48);
		var trailing = restrainedHueShift
			? ShiftHue(ScaleBrightness(accent, 1.25), 10)
			: Mix(accent, ShiftHue(accent, 18), 0.48);
		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0.5),
			EndPoint = new Point(1, 0.5),
			GradientStops =
			[
				new GradientStop(leading, 0),
				new GradientStop(accent, 0.52),
				new GradientStop(trailing, 1)
			]
		};
		if (brush.CanFreeze) brush.Freeze();
		return brush;
	}

	private static LinearGradientBrush CreateDestructiveActionGradient(Color error)
	{
		// Destructive actions need to remain unmistakably red. The general primary-action
		// gradient spreads hue and value for a colorful accent, which makes pale error
		// colors drift toward coral. A small redward hue correction plus a restrained
		// value ramp keeps the user's semantic error color recognizable without washing out.
		var red = ShiftHue(error, 6);
		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0.5),
			EndPoint = new Point(1, 0.5),
			GradientStops =
			[
				new GradientStop(ScaleBrightness(red, 0.68), 0),
				new GradientStop(ScaleBrightness(red, 0.82), 0.52),
				new GradientStop(ScaleBrightness(red, 0.96), 1)
			]
		};
		if (brush.CanFreeze) brush.Freeze();
		return brush;
	}

	private static LinearGradientBrush CreateBuiltInDestructiveActionGradient(ReduxThemeType theme) => theme switch
	{
		ReduxThemeType.ReduxLight => CreateGradient("#7E1231", "#BA1E3D", "#DC3046"),
		ReduxThemeType.Parchment => CreateGradient("#74122E", "#A91D39", "#CC3340"),
		_ => CreateGradient("#8D1739", "#CB2948", "#EF3C50")
	};

	private static LinearGradientBrush CreateGradient(string leading, string middle, string trailing)
	{
		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0.5),
			EndPoint = new Point(1, 0.5),
			GradientStops =
			[
				new GradientStop(Parse(leading), 0),
				new GradientStop(Parse(middle), 0.48),
				new GradientStop(Parse(trailing), 1)
			]
		};
		if (brush.CanFreeze) brush.Freeze();
		return brush;
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
		if (theme != null)
		{
			theme.TypographyFont = NormalizeTypography(theme.TypographyFont, theme.BaseTheme);
			theme.TextSize = NormalizeTextSize(theme.TextSize);
		}
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
		// Begin with the hand-tuned base palette and regenerate only the roles affected by
		// an edited semantic token. Previously, changing one token switched every derived
		// color to the generic formula, so a tiny background hue change also altered headers,
		// borders and typography that the user had not touched.
		var palette = CreateBuiltInResourceColors(theme.BaseTheme);
		var baseColors = BaseColors[theme.BaseTheme];
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
		var backgroundChanged = !ColorsMatch(theme.BackgroundColor, baseColors[0]);
		var surfaceChanged = !ColorsMatch(theme.SurfaceColor, baseColors[1]);
		var accentChanged = !ColorsMatch(theme.AccentColor, baseColors[2]);
		var textChanged = !ColorsMatch(theme.TextColor, baseColors[3]);
		var successChanged = !ColorsMatch(theme.SuccessColor, baseColors[4]);

		if (backgroundChanged)
		{
			palette["ReduxWindowColor"] = background;
			palette["ReduxListInteriorColor"] = Mix(background, surface, 0.42);
		}
		if (surfaceChanged)
		{
			palette["ReduxSurfaceColor"] = surface;
			palette["ReduxListInteriorColor"] = Mix(background, surface, 0.42);
			palette["ReduxSurfaceElevatedColor"] = Mix(surface, contrastTarget, 0.055);
			palette["ReduxSurfaceMutedColor"] = Mix(surface, text, isDark ? 0.075 : 0.09);
			palette["ReduxBorderColor"] = Mix(surface, text, isDark ? 0.16 : 0.18);
			palette["ReduxBorderStrongColor"] = Mix(surface, text, isDark ? 0.28 : 0.32);
			palette["ReduxHoverColor"] = Mix(surface, accent, 0.12);
			palette["ReduxPressedColor"] = Mix(surface, accent, 0.20);
			palette["ReduxAccentSoftColor"] = Mix(surface, accent, 0.22);
			palette["ReduxSelectionColor"] = Mix(surface, accent, 0.34);
			palette["ReduxSuccessSoftColor"] = Mix(surface, success, 0.18);
			palette["ReduxTextSecondaryColor"] = Mix(surface, text, 0.78);
			palette["ReduxTextMutedColor"] = Mix(surface, text, 0.58);
		}
		if (accentChanged)
		{
			palette["ReduxAccentColor"] = accent;
			palette["ReduxAccentHoverColor"] = Mix(accent, contrastTarget, 0.18);
			palette["ReduxAccentSoftColor"] = Mix(surface, accent, 0.22);
			palette["ReduxSelectionColor"] = Mix(surface, accent, 0.34);
			palette["ReduxHoverColor"] = Mix(surface, accent, 0.12);
			palette["ReduxPressedColor"] = Mix(surface, accent, 0.20);
			palette["ReduxAccentForegroundColor"] = BestForeground(accent);
		}
		if (textChanged)
		{
			palette["ReduxTextPrimaryColor"] = text;
			palette["ReduxTextSecondaryColor"] = Mix(surface, text, 0.78);
			palette["ReduxTextMutedColor"] = Mix(surface, text, 0.58);
			palette["ReduxSurfaceMutedColor"] = Mix(surface, text, isDark ? 0.075 : 0.09);
			palette["ReduxBorderColor"] = Mix(surface, text, isDark ? 0.16 : 0.18);
			palette["ReduxBorderStrongColor"] = Mix(surface, text, isDark ? 0.28 : 0.32);
		}
		if (successChanged)
		{
			palette["ReduxSuccessColor"] = success;
			palette["ReduxSuccessSoftColor"] = Mix(surface, success, 0.18);
		}
		if (!ColorsMatch(theme.WarningColor, baseColors[5])) palette["ReduxWarningColor"] = warning;
		if (!ColorsMatch(theme.ErrorColor, baseColors[6])) palette["ReduxErrorColor"] = error;
		if (!ColorsMatch(theme.InfoColor, baseColors[7])) palette["ReduxInfoColor"] = info;
		return palette;
	}

	private static bool ColorsMatch(string left, string right) =>
		Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);

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
	private static ReduxTextSize NormalizeTextSize(ReduxTextSize value) =>
		Enum.IsDefined(value) && value != 0 ? value : ReduxTextSize.Default;
	private static Color Mix(Color left, Color right, double amount) => Color.FromRgb(
		(byte)Math.Round(left.R + ((right.R - left.R) * amount)),
		(byte)Math.Round(left.G + ((right.G - left.G) * amount)),
		(byte)Math.Round(left.B + ((right.B - left.B) * amount)));
	private static Color ScaleBrightness(Color color, double scale) => Color.FromRgb(
		(byte)Math.Clamp(Math.Round(color.R * scale), 0, 255),
		(byte)Math.Clamp(Math.Round(color.G * scale), 0, 255),
		(byte)Math.Clamp(Math.Round(color.B * scale), 0, 255));
	private static Color ShiftHue(Color color, double degrees)
	{
		var red = color.R / 255d;
		var green = color.G / 255d;
		var blue = color.B / 255d;
		var maximum = Math.Max(red, Math.Max(green, blue));
		var minimum = Math.Min(red, Math.Min(green, blue));
		var chroma = maximum - minimum;
		var lightness = (maximum + minimum) / 2d;
		var saturation = chroma == 0
			? 0
			: chroma / (1d - Math.Abs((2d * lightness) - 1d));
		var hue = chroma == 0
			? 0
			: maximum == red
				? 60d * (((green - blue) / chroma) % 6d)
				: maximum == green
					? 60d * (((blue - red) / chroma) + 2d)
					: 60d * (((red - green) / chroma) + 4d);
		hue = (hue + degrees + 360d) % 360d;

		var h = hue / 360d;
		var q = lightness < 0.5
			? lightness * (1d + saturation)
			: lightness + saturation - (lightness * saturation);
		var p = (2d * lightness) - q;
		static double HueChannel(double p, double q, double channel)
		{
			if (channel < 0) channel += 1;
			if (channel > 1) channel -= 1;
			if (channel < 1d / 6d) return p + ((q - p) * 6d * channel);
			if (channel < 1d / 2d) return q;
			if (channel < 2d / 3d) return p + ((q - p) * (2d / 3d - channel) * 6d);
			return p;
		}

		if (saturation == 0) return Color.FromRgb(color.R, color.G, color.B);
		return Color.FromRgb(
			(byte)Math.Round(HueChannel(p, q, h + (1d / 3d)) * 255d),
			(byte)Math.Round(HueChannel(p, q, h) * 255d),
			(byte)Math.Round(HueChannel(p, q, h - (1d / 3d)) * 255d));
	}
	private static double RelativeLuminance(Color color) => ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255d;
	private static Color BestForeground(Color background) => RelativeLuminance(background) > 0.56 ? Color.FromRgb(24, 19, 33) : System.Windows.Media.Colors.White;
}
