using AdonisUI.Controls;

using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DivinityModManager.Views;

public partial class CustomThemeEditorWindow : AdonisWindow
{
	private bool _initializing = true;
	public ReduxCustomTheme Theme { get; }
	public event Action<ReduxCustomTheme> PreviewChanged;
	public event Action<ReduxCustomTheme> ColorPreviewChanged;

	public CustomThemeEditorWindow(ReduxCustomTheme theme)
	{
		Theme = theme ?? throw new ArgumentNullException(nameof(theme));
		InitializeComponent();
		// The editor previews directly against the main Redux window. Keep that
		// surface visible instead of dimming it behind another settings layer.
		ReduxWindowBehavior.AttachDialogTransitions(this, 40, dimOwner: false);
		DataContext = Theme;
		BaseThemeComboBox.SelectedValue = Theme.BaseTheme;
		RefreshTypographyChoices();
		TextSizeComboBox.SelectedValue = Theme.TextSize;
		ReduxThemeService.Apply(Resources, Theme.BaseTheme, Theme);
		ReduxTypographyService.Apply(Application.Current.Resources, Theme.TypographyFont, Theme.CustomTypographyFont);
		Loaded += (_, _) =>
		{
			_initializing = false;
			ThemeNameTextBox.Focus();
			ThemeNameTextBox.SelectAll();
		};
	}

	private void TextSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_initializing || TextSizeComboBox.SelectedValue is not ReduxTextSize textSize) return;
		Theme.TextSize = textSize;
		ReduxTypographyService.ApplyTextSize(Application.Current.Resources, textSize);
		PreviewChanged?.Invoke(Theme);
	}

	private void BaseThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_initializing || BaseThemeComboBox.SelectedValue is not ReduxThemeType baseTheme || baseTheme == Theme.BaseTheme) return;
		ReduxThemeService.ResetToBase(Theme, baseTheme);
		PreviewTheme();
	}

	private void TypographyFontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_initializing || TypographyFontComboBox.SelectedItem is not ReduxFontChoice choice) return;
		Theme.CustomTypographyFont = choice.IsCustom ? choice.CustomReference : String.Empty;
		Theme.TypographyFont = choice.IsCustom ? ReduxTypographyFont.Manrope : choice.BuiltInFont;
		ReduxTypographyService.Apply(Application.Current.Resources, Theme.TypographyFont, Theme.CustomTypographyFont);
		PreviewChanged?.Invoke(Theme);
	}

	private void RefreshTypographyChoices(string preferredCustomReference = null)
	{
		var choices = ReduxCustomFontService.GetChoices();
		TypographyFontComboBox.ItemsSource = choices;
		var customReference = preferredCustomReference ?? Theme.CustomTypographyFont;
		var selected = !String.IsNullOrWhiteSpace(customReference)
			? choices.FirstOrDefault(choice => choice.CustomReference.Equals(customReference, StringComparison.OrdinalIgnoreCase))
			: null;
		selected ??= choices.FirstOrDefault(choice => !choice.IsCustom && choice.BuiltInFont == Theme.TypographyFont);
		TypographyFontComboBox.SelectedItem = selected ?? choices.First(choice => !choice.IsCustom && choice.BuiltInFont == ReduxTypographyFont.Manrope);
	}

	private void ImportCustomFont_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Import Font",
			Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|TrueType font (*.ttf)|*.ttf|OpenType font (*.otf)|*.otf",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true) return;
		if (!ReduxCustomFontService.TryImport(dialog.FileName, out var choice, out var error))
		{
			ReduxMessageBox.Show(this, error, "Import Font", System.Windows.MessageBoxButton.OK,
				System.Windows.MessageBoxImage.Error, System.Windows.MessageBoxResult.OK);
			return;
		}
		RefreshTypographyChoices(choice.CustomReference);
		TypographyFontComboBox_SelectionChanged(TypographyFontComboBox, null);
	}

	private void InterfaceIconsCheckBox_Unchecked(object sender, RoutedEventArgs e)
	{
		Theme.UseIconsOnly = false;
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
			"Changes preview live. Cancel restores the previous color.",
			"Theme color");
		ReduxThemeService.Apply(dialog.Resources, Theme.BaseTheme, Theme);
		// Keep whole-app preview work below input/render priority so dragging the
		// picker remains fluid even while the main Redux surface updates behind it.
		var previewTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
		{
			Interval = TimeSpan.FromMilliseconds(50)
		};
		previewTimer.Tick += (_, _) =>
		{
			previewTimer.Stop();
			ReduxThemeService.PreviewColors(dialog.Resources, Theme);
			ReduxThemeService.PreviewColors(Resources, Theme);
			ColorPreviewChanged?.Invoke(Theme);
		};
		dialog.ColorPreviewChanged += color =>
		{
			property.SetValue(Theme, color);
			if (!previewTimer.IsEnabled)
				previewTimer.Start();
		};

		var accepted = dialog.ShowDialog() == true;
		previewTimer.Stop();
		property.SetValue(Theme, accepted ? dialog.CategoryColor : currentColor);
		PreviewTheme();
	}

	private void PreviewTheme()
	{
		ReduxThemeService.Apply(Resources, Theme.BaseTheme, Theme);
		ReduxTypographyService.Apply(Application.Current.Resources, Theme.TypographyFont, Theme.CustomTypographyFont);
		ReduxTypographyService.ApplyTextSize(Application.Current.Resources, Theme.TextSize);
		PreviewChanged?.Invoke(Theme);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (!ReduxThemeService.TryValidate(Theme, out var error))
		{
			ReduxMessageBox.Show(this, error, "Custom Theme",
				System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information,
				System.Windows.MessageBoxResult.OK);
			return;
		}
		ReduxThemeService.NormalizeColors(Theme);
		Theme.Name = Theme.Name.Trim();
		DialogResult = true;
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
