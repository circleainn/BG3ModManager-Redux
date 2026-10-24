using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

#if REDUX_UPDATER
namespace ReduxUpdater;
#else
namespace DivinityModManager.AppServices;
#endif

// A data-only palette travels with the external runner; it never loads the installation's
// assemblies, theme XAML, or font files while those files may be replaced by a restore.
public sealed class ReduxSnapshotAppearance
{
	public static readonly IReadOnlyDictionary<string, string> DefaultColors = new Dictionary<string, string>
	{
		["ReduxWindowBrush"] = "#FF0D0B10", ["ReduxSurfaceBrush"] = "#FF17121D",
		["ReduxSurfaceElevatedBrush"] = "#FF1C1623", ["ReduxSurfaceMutedBrush"] = "#FF241C2C",
		["ReduxBorderBrush"] = "#FF33283F", ["ReduxBorderStrongBrush"] = "#FF4B3A5C",
		["ReduxAccentBrush"] = "#FF9676FF", ["ReduxAccentHoverBrush"] = "#FFB49DFF",
		["ReduxAccentSoftBrush"] = "#FF322543", ["ReduxPressedBrush"] = "#FF33263F",
		["ReduxTextPrimaryBrush"] = "#FFF2EDF7", ["ReduxTextSecondaryBrush"] = "#FFC8BDD4"
	};
	public Dictionary<string, string> Colors { get; set; } = new();
	public double TextScale { get; set; } = 1;
	public string FontFamily { get; set; } = "Manrope";
	public string Language { get; set; } = "en";
	public bool ShowIcons { get; set; } = true;
	public ReduxSnapshotAppearance Normalize()
	{
		var result = new ReduxSnapshotAppearance
		{
			TextScale = Double.IsFinite(TextScale) ? Math.Clamp(TextScale, 0.8, 1.5) : 1,
			FontFamily = FontFamily is "Manrope" or "Segoe UI" or "Archivo Black" or "IBM Plex Mono" or "Atkinson Hyperlegible" ? FontFamily : "Manrope",
			ShowIcons = ShowIcons,
			Language = Localization.ReduxLanguageService.NormalizeLanguage(Language)
		};
		foreach (var entry in DefaultColors)
			result.Colors[entry.Key] = Colors != null && Colors.TryGetValue(entry.Key, out var color)
				&& color is { Length: 9 } && color[0] == '#' && color[1..].All(Uri.IsHexDigit) ? color : entry.Value;
		return result;
	}
	public void Write(string path) => File.WriteAllText(path, JsonSerializer.Serialize(Normalize()));
	public static ReduxSnapshotAppearance Read(string path)
	{
		try
		{
			using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			if (file.Length > 16384) return new ReduxSnapshotAppearance().Normalize();
			var bytes = new byte[(int)file.Length];
			file.ReadExactly(bytes);
			return (JsonSerializer.Deserialize<ReduxSnapshotAppearance>(bytes) ?? new()).Normalize();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
		{
			return new ReduxSnapshotAppearance().Normalize();
		}
	}
}
