using DivinityModManager.AppServices;
using System.Globalization;
using System.Windows.Data;

namespace DivinityModManager.Localization;

/// <summary>Present immutable snapshot metadata in the current language without altering the archive.</summary>
public sealed class SnapshotSummaryConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length == 0 || values[0] is not ReduxSnapshotStore.Item item) return String.Empty;
		if (!String.IsNullOrEmpty(item.Error)) return item.Error;
		var scope = ReduxLanguageService.Current.Translate(item.IncludeMods ? "Redux + mods and mod lists" : "Redux only");
		return $"{item.Version} · {item.CreatedUtc.ToLocalTime():g} · {item.Bytes / 1048576d:N1} MB · {scope}";
	}
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
