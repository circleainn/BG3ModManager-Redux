using AdonisUI.Controls;
using DivinityModManager.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using DivinityModManager.Util;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DivinityModManager.Views;

public partial class CategoryNameDialog : AdonisWindow
{
	private bool _updatingColorControls;
	private bool _draggingSpectrum;
	private double _hue;
	private double _saturation;
	private double _brightness;
	private double _renderedWheelBrightness = Double.NaN;
	private readonly bool _allowEmptyName;
	private readonly List<string> _savedColors;
	private readonly ObservableCollection<IconChooserChoice> _iconChoices;
	public IReadOnlyList<string> SavedColors => _savedColors;
	public bool ResetToDefaultRequested { get; private set; }
	public string CategoryName => CategoryNameTextBox.Text?.Trim();
	public string CategoryColor => CategoryColorPicker.SelectedColor is Color color
		? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : "#8A6AF1";
	public string CategoryIconId
	{
		get
		{
			var iconId = ReduxIconCatalog.Normalize(CategoryIconComboBox?.SelectedValue as string);
			return ReduxCustomIconService.IsCustomReference(iconId)
				? ReduxCustomIconService.WithTint(iconId, TintCustomIconCheckBox?.IsChecked == true)
				: iconId;
		}
	}

	private sealed class IconChooserChoice : INotifyPropertyChanged
	{
		private string _previewIconId;
		public string Id { get; }
		public string DisplayName { get; }
		public string PreviewIconId
		{
			get => _previewIconId;
			set
			{
				if (_previewIconId.Equals(value, StringComparison.OrdinalIgnoreCase)) return;
				_previewIconId = value ?? String.Empty;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewIconId)));
			}
		}
		public bool IsNone => String.IsNullOrWhiteSpace(Id);
		public event PropertyChangedEventHandler PropertyChanged;

		public IconChooserChoice(ReduxIconChoice choice)
		{
			Id = choice.Id;
			DisplayName = choice.DisplayName;
			_previewIconId = choice.Id;
		}

		public IconChooserChoice(string id, string displayName)
		{
			Id = id ?? String.Empty;
			DisplayName = displayName;
			_previewIconId = Id;
		}
	}

	public void ConfigureColorOnlyCopy(string heading, string helperText, string fieldLabel)
	{
		DialogHeading.Text = heading;
		DialogHelperText.Text = helperText;
		ColorFieldLabel.Text = fieldLabel;
		IconChooserPanel.Visibility = Visibility.Collapsed;
	}

	public CategoryNameDialog(string categoryName = "", string color = "#8A6AF1", bool canEditName = true, IEnumerable<string> savedColors = null, bool visualDividerMode = false, string iconId = "", bool canResetToDefault = false)
	{
		InitializeComponent();
		_allowEmptyName = visualDividerMode;
		_savedColors = (savedColors ?? Enumerable.Empty<string>())
			.Where(IsValidHexColor).Select(value => value.ToUpperInvariant())
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		CategoryNameTextBox.Text = categoryName;
		CategoryNameTextBox.IsEnabled = canEditName;
		_iconChoices = new ObservableCollection<IconChooserChoice>(ReduxIconCatalog.Choices
			.Select(choice => new IconChooserChoice(choice)));
		foreach (var storedReference in ReduxCustomIconService.GetStoredReferences())
			_iconChoices.Add(new IconChooserChoice(storedReference, "Custom PNG"));
		var normalizedIconId = ReduxIconCatalog.Normalize(iconId);
		var tintCustomIcon = ReduxCustomIconService.IsTintedReference(normalizedIconId);
		if (ReduxCustomIconService.IsCustomReference(normalizedIconId))
			normalizedIconId = ReduxCustomIconService.WithTint(normalizedIconId, false);
		if (ReduxCustomIconService.IsCustomReference(normalizedIconId) &&
			!_iconChoices.Any(choice => choice.Id.Equals(normalizedIconId, StringComparison.OrdinalIgnoreCase)))
			_iconChoices.Add(new IconChooserChoice(normalizedIconId, "Imported PNG"));
		CategoryIconComboBox.ItemsSource = _iconChoices;
		CategoryIconComboBox.SelectedValue = normalizedIconId;
		TintCustomIconCheckBox.IsChecked = tintCustomIcon;
		UpdateCustomIconControls();
		if (ColorConverter.ConvertFromString(color) is Color selectedColor) CategoryColorPicker.SelectedColor = selectedColor;
		Title = visualDividerMode ? (String.IsNullOrEmpty(categoryName) ? "Add Separator" : "Edit Separator") : canEditName ? "Add Mod Category" : "Edit Category";
		DialogHeading.Text = visualDividerMode ? "Style a separator" : canEditName ? "Create a custom mod category" : $"Edit {categoryName}";
		DialogHelperText.Text = visualDividerMode
			? "Choose a color, marker or icon, and optional label. Leave the name empty for a line-only separator."
			: canEditName
			? "Choose a unique name, color, and marker or icon. Dot is the default."
			: canResetToDefault
			? "Redux category names stay fixed. Customize its color and marker or icon, or restore the Redux defaults."
			: "Choose a color and marker or icon. Dot is the default.";
		ConfirmButton.Content = visualDividerMode ? "Save" : canEditName ? "Add" : "Save";
		ResetToDefaultButton.Visibility = canResetToDefault ? Visibility.Visible : Visibility.Collapsed;
		if (canResetToDefault)
			CategoryNameTextBox.ToolTip = "Redux category names are fixed. Create a custom category for a different name.";
		if (visualDividerMode)
		{
			SeparatorPreviewPanel.Visibility = Visibility.Visible;
			ColorFieldLabel.Text = "Separator color";
			IconFieldLabel.Text = "Icon";
			TintCustomIconText.Text = "Tint with separator color";
			CategoryNameTextBox.ToolTip = "Optional separator label";
		}
		UpdateColorPresentation();
		RefreshSavedColors();
		Loaded += (_, _) => { CategoryNameTextBox.Focus(); UpdateModernColorSurface(); };
		SizeChanged += (_, _) => UpdateModernColorSurface();
	}

	private static bool IsValidHexColor(string value) =>
		!String.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");

	private void RefreshSavedColors()
	{
		if (SavedColorsPanel == null) return;
		SavedColorsPanel.Children.Clear();
		foreach (var value in _savedColors)
		{
			var swatch = new Border
			{
				Tag = value,
				Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)),
				Style = (Style)FindResource("CategoryColorSwatchStyle"),
				ToolTip = $"{value}\nLeft-click to use. Right-click to remove."
			};
			swatch.MouseLeftButtonUp += ColorSwatch_Click;
			swatch.MouseRightButtonUp += SavedColorSwatch_RightClick;
			SavedColorsPanel.Children.Add(swatch);
		}
		NoSavedColorsText.Visibility = _savedColors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
	}

	private void SaveCurrentColor_Click(object sender, RoutedEventArgs e)
	{
		var value = CategoryColor;
		if (!_savedColors.Contains(value, StringComparer.OrdinalIgnoreCase))
		{
			_savedColors.Add(value.ToUpperInvariant());
			RefreshSavedColors();
		}
	}

	private void SavedColorSwatch_RightClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border { Tag: string value })
		{
			_savedColors.RemoveAll(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
			RefreshSavedColors();
			e.Handled = true;
		}
	}

	private void CategoryColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) => UpdateColorPresentation();

	private void UpdateColorPresentation()
	{
		if (CategoryColorPicker?.SelectedColor is not Color color || HexColorTextBox == null || SelectedColorPreview == null) return;
		var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		HexColorTextBox.Text = hex;
		SelectedColorPreview.Background = new SolidColorBrush(color);
		Resources["Redux.CategoryEditor.IconBrush"] = new SolidColorBrush(color);
		RgbToHsv(color, out _hue, out _saturation, out _brightness);
		_updatingColorControls = true;
		SaturationSlider.Value = _saturation * 100;
		BrightnessSlider.Value = _brightness * 100;
		_updatingColorControls = false;
		UpdateModernColorSurface();
	}

	private void UpdateModernColorSurface()
	{
		if (SpectrumSurface == null || ColorWheelImage == null) return;
		RenderColorWheel();
		if (SpectrumSurface.ActualWidth > 0 && SpectrumSurface.ActualHeight > 0)
		{
			var radius = Math.Min(SpectrumSurface.ActualWidth, SpectrumSurface.ActualHeight) / 2;
			var angle = _hue * Math.PI / 180d;
			var distance = _saturation * radius;
			SpectrumMarker.Margin = new Thickness(
				SpectrumSurface.ActualWidth / 2 + Math.Cos(angle) * distance - SpectrumMarker.Width / 2,
				SpectrumSurface.ActualHeight / 2 + Math.Sin(angle) * distance - SpectrumMarker.Height / 2, 0, 0);
		}
	}

	private void RenderColorWheel()
	{
		if (ColorWheelImage == null || Math.Abs(_renderedWheelBrightness - _brightness) < 0.001) return;
		const int size = 198;
		var pixels = new byte[size * size * 4];
		var center = (size - 1) / 2d;
		var radius = center;
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var dx = x - center;
				var dy = y - center;
				var distance = Math.Sqrt(dx * dx + dy * dy);
				if (distance > radius) continue;
				var hue = Math.Atan2(dy, dx) * 180d / Math.PI;
				if (hue < 0) hue += 360;
				var color = HsvToRgb(hue, distance / radius, _brightness);
				var offset = (y * size + x) * 4;
				pixels[offset] = color.B;
				pixels[offset + 1] = color.G;
				pixels[offset + 2] = color.R;
				pixels[offset + 3] = 255;
			}
		}
		var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
		bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
		bitmap.Freeze();
		ColorWheelImage.Source = bitmap;
		_renderedWheelBrightness = _brightness;
	}

	private void SetSpectrumFromPoint(Point point)
	{
		var centerX = SpectrumSurface.ActualWidth / 2;
		var centerY = SpectrumSurface.ActualHeight / 2;
		var dx = point.X - centerX;
		var dy = point.Y - centerY;
		var radius = Math.Max(1, Math.Min(centerX, centerY));
		_saturation = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) / radius, 0, 1);
		_hue = Math.Atan2(dy, dx) * 180d / Math.PI;
		if (_hue < 0) _hue += 360;
		CategoryColorPicker.SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
	}

	private void Spectrum_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _draggingSpectrum = true; SpectrumSurface.CaptureMouse(); SetSpectrumFromPoint(e.GetPosition(SpectrumSurface)); }
	private void Spectrum_MouseMove(object sender, MouseEventArgs e) { if (_draggingSpectrum && e.LeftButton == MouseButtonState.Pressed) SetSpectrumFromPoint(e.GetPosition(SpectrumSurface)); }
	private void ColorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { _draggingSpectrum = false; Mouse.Capture(null); }

	private static Color HsvToRgb(double hue, double saturation, double value)
	{
		var chroma = value * saturation;
		var h = (hue % 360) / 60d;
		var x = chroma * (1 - Math.Abs(h % 2 - 1));
		(double r, double g, double b) = h switch
		{
			< 1 => (chroma, x, 0d), < 2 => (x, chroma, 0d), < 3 => (0d, chroma, x),
			< 4 => (0d, x, chroma), < 5 => (x, 0d, chroma), _ => (chroma, 0d, x)
		};
		var m = value - chroma;
		return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
	}

	private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
	{
		var r = color.R / 255d; var g = color.G / 255d; var b = color.B / 255d;
		var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
		hue = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta) % 6) : max == g ? 60 * (((b - r) / delta) + 2) : 60 * (((r - g) / delta) + 4);
		if (hue < 0) hue += 360;
		saturation = max == 0 ? 0 : delta / max;
		value = max;
	}

	private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || SaturationSlider == null) return;
		_saturation = SaturationSlider.Value / 100d;
		CategoryColorPicker.SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
	}

	private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || BrightnessSlider == null) return;
		_brightness = BrightnessSlider.Value / 100d;
		_renderedWheelBrightness = Double.NaN;
		CategoryColorPicker.SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
	}

	private void ApplyHexColor()
	{
		var value = HexColorTextBox.Text?.Trim();
		if (!String.IsNullOrWhiteSpace(value) && !value.StartsWith('#')) value = $"#{value}";
		if (ColorConverter.ConvertFromString(value) is Color color) CategoryColorPicker.SelectedColor = color;
		else UpdateColorPresentation();
	}

	private void HexColorTextBox_Commit(object sender, RoutedEventArgs e) => ApplyHexColor();
	private void HexColorTextBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter) { ApplyHexColor(); CategoryColorPicker.Focus(); e.Handled = true; }
	}

	private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border { Tag: string value } && ColorConverter.ConvertFromString(value) is Color color)
			CategoryColorPicker.SelectedColor = color;
	}

	private void Add_Click(object sender, RoutedEventArgs e)
	{
		if (_allowEmptyName || !String.IsNullOrWhiteSpace(CategoryName))
		{
			DialogResult = true;
		}
	}

	private void ResetToDefault_Click(object sender, RoutedEventArgs e)
	{
		ResetToDefaultRequested = true;
		DialogResult = true;
	}

	private void ImportCustomIcon_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new OpenFileDialog
		{
			Title = "Import Custom Icon",
			Filter = "PNG images (*.png)|*.png",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true) return;
		if (!ReduxCustomIconService.TryImport(dialog.FileName, out var iconReference, out var error))
		{
			ShowReduxMessage(error, "Import Custom Icon", System.Windows.MessageBoxButton.OK,
				System.Windows.MessageBoxImage.Information);
			return;
		}

		var existing = _iconChoices.FirstOrDefault(choice => choice.Id.Equals(iconReference, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			existing = new IconChooserChoice(iconReference, $"Imported PNG: {Path.GetFileName(dialog.FileName)}");
			_iconChoices.Add(existing);
		}
		CategoryIconComboBox.SelectedValue = existing.Id;
		TintCustomIconCheckBox.IsChecked = false;
		UpdateCustomIconControls();
	}

	private void DeleteCustomIcon_Click(object sender, RoutedEventArgs e)
	{
		if (CategoryIconComboBox.SelectedItem is not IconChooserChoice choice ||
			!ReduxCustomIconService.IsCustomReference(choice.Id)) return;
		var result = ShowReduxMessage(
			"Remove this custom icon from Redux? Categories and separators using it will fall back to the default dot.",
			"Remove Custom Icon", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
		if (result != System.Windows.MessageBoxResult.Yes) return;
		if (!ReduxCustomIconService.TryDelete(choice.Id, out var error))
		{
			ShowReduxMessage(error, "Remove Custom Icon", System.Windows.MessageBoxButton.OK,
				System.Windows.MessageBoxImage.Information);
			return;
		}

		_iconChoices.Remove(choice);
		CategoryIconComboBox.SelectedValue = String.Empty;
		TintCustomIconCheckBox.IsChecked = false;
		UpdateCustomIconControls();
	}

	private void CategoryIconComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCustomIconControls();

	private System.Windows.MessageBoxResult ShowReduxMessage(string message, string caption,
		System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
	{
		var defaultResult = buttons == System.Windows.MessageBoxButton.YesNo
			? System.Windows.MessageBoxResult.No
			: System.Windows.MessageBoxResult.OK;
		return ReduxMessageBox.Show(this, message, caption, buttons, image, defaultResult);
	}

	private void TintCustomIconCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateCustomIconControls();

	private void UpdateCustomIconControls()
	{
		if (CategoryIconComboBox == null || TintCustomIconCheckBox == null) return;
		var selectedId = CategoryIconComboBox.SelectedValue as string;
		var isCustom = ReduxCustomIconService.IsCustomReference(selectedId);
		TintCustomIconCheckBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
		DeleteCustomIconButton.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
		if (CategoryIconComboBox.SelectedItem is IconChooserChoice choice && isCustom)
		{
			choice.PreviewIconId = ReduxCustomIconService.WithTint(selectedId, TintCustomIconCheckBox.IsChecked == true);
		}
	}
}
