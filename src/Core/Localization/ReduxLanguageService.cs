using System.ComponentModel;
using System.Text.Json;

namespace DivinityModManager.Localization;

public sealed record InterfaceLanguageChoice(string Code, string NativeName);

/// <summary>Bundled interface translations. Never changes data, file names, or formatting culture.</summary>
public sealed class ReduxLanguageService : INotifyPropertyChanged
{
	public static ReduxLanguageService Current { get; } = new();
	public static IReadOnlyList<InterfaceLanguageChoice> Languages { get; } = Array.AsReadOnly(new[]
	{
		new InterfaceLanguageChoice("en", "English"), new("fr", "Français"), new("es", "Español"),
		new("ru", "Русский"), new("zh-Hans", "简体中文")
	});
	private readonly Dictionary<string, Dictionary<string, string>> _strings;
	private readonly Dictionary<string, string> _englishKeys;
	public string Language { get; private set; } = "en";
	public event PropertyChangedEventHandler PropertyChanged;
	public IReadOnlyCollection<string> Keys => _strings.Keys;

	public ReduxLanguageService()
	{
		using var stream = typeof(ReduxLanguageService).Assembly.GetManifestResourceStream("Redux.InterfaceStrings.json")
			?? throw new InvalidOperationException("The bundled interface translations are missing.");
		_strings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
			?? throw new InvalidOperationException("The bundled interface translations are empty.");
		_englishKeys = _strings.ToDictionary(pair => pair.Value["en"], pair => pair.Key, StringComparer.Ordinal);
	}

	public static string NormalizeLanguage(string language)
	{
		var code = (language ?? "").Trim().Replace('_', '-').ToLowerInvariant();
		if (code is "zh" or "zh-cn" or "zh-sg" or "zh-hans" || code.StartsWith("zh-hans-")) return "zh-Hans";
		var primary = code.Split('-')[0];
		return primary is "en" or "fr" or "es" or "ru" ? primary : "en";
	}

	// Call on the UI thread when bound controls are present.
	public void SetLanguage(string language)
	{
		var normalized = NormalizeLanguage(language);
		if (Language == normalized) return;
		Language = normalized;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
	}

	public string this[string key] => GetTranslation(key, Language);
	public string GetTranslation(string key, string language)
	{
		if (key == null) return String.Empty;
		if (!_strings.TryGetValue(key, out var translations)) return key;
		return translations.TryGetValue(NormalizeLanguage(language), out var value) && !String.IsNullOrWhiteSpace(value)
			? value : translations["en"];
	}
	public string Translate(string english) => english != null && _englishKeys.TryGetValue(english, out var key)
		? this[key] : english;
}
