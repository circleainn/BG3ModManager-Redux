using DivinityModManager.Models;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Util;

/// <summary>
/// Applies the user-selected Redux UI family through the application-level
/// dynamic typography token. Color themes intentionally do not own typography.
/// </summary>
public static class ReduxTypographyService
{
	private static readonly double[] FontSizeBaselines =
	[
		10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 32, 34
	];

	public static void Apply(ResourceDictionary resources, ReduxTypographyFont selection, string customReference = "")
	{
		if (resources == null) return;
		ReduxCustomFontService.ProcessPendingDeletions();
		if (!Enum.IsDefined(selection)) selection = ReduxTypographyFont.Manrope;

		FontFamily family;
		if (!String.IsNullOrWhiteSpace(customReference))
		{
			// Custom-theme files intentionally carry only Redux's path-free reference.
			// A font that is not installed locally must never prevent the theme loading.
			family = ReduxCustomFontService.TryCreateFontFamily(customReference, out var customFamily)
				? customFamily
				: CreateBundledFont("Manrope");
		}
		else family = selection switch
		{
			ReduxTypographyFont.SegoeUI => new FontFamily("Segoe UI"),
			ReduxTypographyFont.AtkinsonHyperlegible => CreateBundledFont("Atkinson Hyperlegible"),
			ReduxTypographyFont.MonaspaceNeon => CreateBundledFont("Monaspace Neon"),
			ReduxTypographyFont.Minipax => CreateBundledFont("Minipax"),
			ReduxTypographyFont.Chivo => CreateBundledFont("Chivo"),
			_ => CreateBundledFont("Manrope")
		};

		// Replace the value in the dictionary that owns each token. Adding a new
		// shadowing key at the application root does not reliably invalidate controls
		// already subscribed to a resource in a merged dictionary.
		SetResource(resources, "Redux.FontFamily.UI", family);
		SetResource(resources, "ReduxFontFamily", family);

		// FontFamily is inherited from each top-level window. Applying the selected
		// family there makes the switch immediate and deterministic even when a
		// control was created from a separately merged resource dictionary.
		if (Application.Current != null)
		{
			foreach (Window window in Application.Current.Windows)
			{
				window.FontFamily = family;
			}
		}
	}

	public static void ApplyTextSize(ResourceDictionary resources, ReduxTextSize selection)
	{
		if (resources == null) return;
		if (!Enum.IsDefined(selection)) selection = ReduxTextSize.Default;

		var scale = selection switch
		{
			ReduxTextSize.Compact => 0.92,
			ReduxTextSize.Large => 1.15,
			_ => 1.0
		};

		foreach (var baseline in FontSizeBaselines)
		{
			var scaled = Math.Round(baseline * scale * 2, MidpointRounding.AwayFromZero) / 2;
			SetResource(resources, $"Redux.FontSize.{baseline:0}", scaled);
		}

		if (Application.Current != null)
		{
			var bodySize = Math.Round(12 * scale * 2, MidpointRounding.AwayFromZero) / 2;
			foreach (Window window in Application.Current.Windows)
			{
				window.FontSize = bodySize;
			}
		}
	}

	private static FontFamily CreateBundledFont(string familyName)
	{
		var fontDirectory = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts")
			+ Path.DirectorySeparatorChar;
		var family = new FontFamily(new Uri(fontDirectory, UriKind.Absolute), $"./#{familyName}");

		// Invalid WPF font references silently fall back. Verify that the selected
		// family resolves to a real glyph face before returning it.
		foreach (var typeface in family.GetTypefaces())
		{
			if (typeface.TryGetGlyphTypeface(out _)) return family;
		}

		DivinityApp.Log($"Could not load bundled typography family '{familyName}' from '{fontDirectory}'. Falling back to Segoe UI.");
		return new FontFamily("Segoe UI");
	}

	private static void SetResource(ResourceDictionary resources, string key, object value)
	{
		var owner = FindResourceOwner(resources, key) ?? resources;
		owner[key] = value;
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
}
