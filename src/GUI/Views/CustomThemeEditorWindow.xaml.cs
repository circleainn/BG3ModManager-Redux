using AdonisUI.Controls;

using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class CustomThemeEditorWindow : AdonisWindow
{
	private bool _initializing = true;
	public ReduxCustomTheme Theme { get; }
	public event Action<ReduxCustomTheme> PreviewChanged;

	public CustomThemeEditorWindow(ReduxCustomTheme theme)
	{
		Theme = theme ?? throw new ArgumentNullException(nameof(theme));
		InitializeComponent();
		DataContext = Theme;
		BaseThemeComboBox.SelectedValue = Theme.BaseTheme;
		TypographyFontComboBox.SelectedValue = Theme.TypographyFont;
		ReduxThemeService.Apply(Resources, Theme.BaseTheme, Theme);
		Loaded += (_, _) =>
		{
			_initializing = false;
			ThemeNameTextBox.Focus();
			ThemeNameTextBox.SelectAll();
		};
	}

	private void BaseThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_initializing || BaseThemeComboBox.SelectedValue is not ReduxThemeType baseTheme || baseTheme == Theme.BaseTheme) return;
		ReduxThemeService.ResetToBase(Theme, baseTheme);
		PreviewTheme();
	}

	private void TypographyFontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_initializing || TypographyFontComboBox.SelectedValue is not ReduxTypographyFont typographyFont) return;
		Theme.TypographyFont = typographyFont;
		ReduxTypographyService.Apply(Application.Current.Resources, typographyFont);
		PreviewChanged?.Invoke(Theme);
	}

	private void ColorButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not Button { Tag: string propertyName }) return;
		var property = typeof(ReduxCustomTheme).GetProperty(propertyName);
		if (property?.GetValue(Theme) is not string currentColor) return;
		var label = propertyName.Replace("Color", String.Empty);
		var dialog = new CategoryNameDialog(label, currentColor, false)
		{
			Owner = this,
			Title = $"Choose {label} Color"
		};
		dialog.ConfigureColorOnlyCopy(
			$"Change {label} color",
			"Choose a color for this theme token. The custom theme preview updates after you save it.",
			"Theme color");
		ReduxThemeService.Apply(dialog.Resources, Theme.BaseTheme, Theme);
		if (dialog.ShowDialog() != true) return;
		property.SetValue(Theme, dialog.CategoryColor);
		PreviewTheme();
	}

	private void PreviewTheme()
	{
		ReduxThemeService.Apply(Resources, Theme.BaseTheme, Theme);
		ReduxTypographyService.Apply(Application.Current.Resources, Theme.TypographyFont);
		PreviewChanged?.Invoke(Theme);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (!ReduxThemeService.TryValidate(Theme, out var error))
		{
			Xceed.Wpf.Toolkit.MessageBox.Show(this, error, "Custom Theme",
				System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information,
				System.Windows.MessageBoxResult.OK, MainWindow.Self.MessageBoxStyle);
			return;
		}
		ReduxThemeService.NormalizeColors(Theme);
		Theme.Name = Theme.Name.Trim();
		DialogResult = true;
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
