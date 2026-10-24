using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DivinityModManager.Localization;

/// <summary>Translate a UI sentence before inserting a bound count, name, or path.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class FormatTextExtension : MarkupExtension
{
	public string Key { get; set; }
	public string Path { get; set; } = ".";
	public string ElementName { get; set; }

	private sealed class Converter(string key) : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length == 0 || values[0] == DependencyProperty.UnsetValue) return String.Empty;
			var service = ReduxLanguageService.Current;
			return service.FormatEnglish(service.GetTranslation(key, "en"), values[0]);
		}
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		var value = new Binding(Path) { Mode = BindingMode.OneWay };
		if (!String.IsNullOrWhiteSpace(ElementName)) value.ElementName = ElementName;
		var binding = new MultiBinding { Converter = new Converter(Key), Mode = BindingMode.OneWay };
		binding.Bindings.Add(value);
		binding.Bindings.Add(new Binding(nameof(ReduxLanguageService.Language)) { Source = ReduxLanguageService.Current });
		return binding.ProvideValue(serviceProvider);
	}
}
