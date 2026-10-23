using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DivinityModManager.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
	public string Key { get; set; }
	public TextExtension() { }
	public TextExtension(string key) => Key = key;
	public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{Key}]")
	{
		Source = ReduxLanguageService.Current, Mode = BindingMode.OneWay
	}.ProvideValue(serviceProvider);
}

/// <summary>Translate a UI-owned bound label while retaining its original identifier.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class BoundTextExtension : MarkupExtension
{
	public string Path { get; set; } = ".";
	private sealed class Converter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
			values.Length > 0 && values[0] is string text ? ReduxLanguageService.Current.Translate(text) : String.Empty;
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
			throw new NotSupportedException();
	}
	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		var binding = new MultiBinding { Converter = new Converter(), Mode = BindingMode.OneWay };
		binding.Bindings.Add(new Binding(Path));
		binding.Bindings.Add(new Binding(nameof(ReduxLanguageService.Language)) { Source = ReduxLanguageService.Current });
		return binding.ProvideValue(serviceProvider);
	}
}

/// <summary>Opt-in bindings for UI-owned labels; never walk or translate user content.</summary>
public static class LocalizationBindings
{
	// Existing command styling uses English action names; translated captions are display only.
	public static string SourceText(DependencyObject target, DependencyProperty property, string fallback)
	{
		var binding = BindingOperations.GetBinding(target, property);
		if (binding?.Source is not ReduxLanguageService service) return fallback;
		if (binding?.ConverterParameter is string english) return english;
		if (binding.Path?.Path is string path
			&& path.StartsWith("[") && path.EndsWith("]"))
			return service.GetTranslation(path[1..^1], "en");
		return fallback;
	}
	private sealed class TranslationConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			ReduxLanguageService.Current.Translate(parameter as string);
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
	}
	private static readonly IValueConverter Converter = new TranslationConverter();
	public static void Set(DependencyObject target, DependencyProperty property, string english) =>
		BindingOperations.SetBinding(target, property, new Binding(nameof(ReduxLanguageService.Language))
		{
			Source = ReduxLanguageService.Current, Mode = BindingMode.OneWay,
			Converter = Converter, ConverterParameter = english
		});
}
