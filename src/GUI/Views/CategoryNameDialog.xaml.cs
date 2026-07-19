using AdonisUI.Controls;
using DivinityModManager.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace DivinityModManager.Views;

public partial class CategoryNameDialog : AdonisWindow
{
	private bool _updatingColorControls;
	private bool _draggingSpectrum;
	private bool _draggingHue;
	private double _hue;
	private double _saturation;
	private double _brightness;
	private readonly bool _allowEmptyName;
	private readonly List<string> _savedColors;
	public IReadOnlyList<string> SavedColors => _savedColors;
	public bool ResetToDefaultRequested { get; private set; }
	public string CategoryName => CategoryNameTextBox.Text?.Trim();
	public string CategoryColor => CategoryColorPicker.SelectedColor is Color color
		? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : "#8A6AF1";
	public string CategoryIconId => ReduxIconCatalog.Normalize(CategoryIconComboBox?.SelectedValue as string);

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
		CategoryIconComboBox.SelectedValue = ReduxIconCatalog.Normalize(iconId);
		if (ColorConverter.ConvertFromString(color) is Color selectedColor) CategoryColorPicker.SelectedColor = selectedColor;
		Title = visualDividerMode ? (String.IsNullOrEmpty(categoryName) ? "Add Separator" : "Edit Separator") : canEditName ? "Add Mod Category" : "Edit Category";
		DialogHeading.Text = visualDividerMode ? "Style a separator" : canEditName ? "Create a custom mod category" : $"Edit {categoryName}";
		DialogHelperText.Text = visualDividerMode
			? "Choose a color, optional icon, and optional label. Leave the name empty for a line-only separator."
			: canEditName
			? "Choose a unique name, color, and optional icon. No icon uses the original colored dot."
			: canResetToDefault
			? "Redux category names stay fixed. Customize its color and icon, or restore the Redux defaults."
			: "Choose a color and optional icon. No icon uses the original colored dot.";
		ConfirmButton.Content = visualDividerMode ? "Save" : canEditName ? "Add" : "Save";
		ResetToDefaultButton.Visibility = canResetToDefault ? Visibility.Visible : Visibility.Collapsed;
		if (canResetToDefault)
			CategoryNameTextBox.ToolTip = "Redux category names are fixed. Create a custom category for a different name.";
		if (visualDividerMode)
		{
			ColorFieldLabel.Text = "Separator color";
			IconFieldLabel.Text = "ICON";
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
		_updatingColorControls = true;
		RedSlider.Value = color.R;
		GreenSlider.Value = color.G;
		BlueSlider.Value = color.B;
		_updatingColorControls = false;
		RgbToHsv(color, out _hue, out _saturation, out _brightness);
		UpdateModernColorSurface();
	}

	private void UpdateModernColorSurface()
	{
		if (SpectrumSurface == null || HueSurface == null) return;
		var hueColor = HsvToRgb(_hue, 1, 1);
		SpectrumSurface.Background = new LinearGradientBrush(Colors.White, hueColor, new Point(0, 0.5), new Point(1, 0.5));
		if (SpectrumSurface.ActualWidth > 0 && SpectrumSurface.ActualHeight > 0)
		{
			SpectrumMarker.Margin = new Thickness(
				Math.Clamp(_saturation * SpectrumSurface.ActualWidth - SpectrumMarker.Width / 2, -SpectrumMarker.Width / 2, SpectrumSurface.ActualWidth - SpectrumMarker.Width / 2),
				Math.Clamp((1 - _brightness) * SpectrumSurface.ActualHeight - SpectrumMarker.Height / 2, -SpectrumMarker.Height / 2, SpectrumSurface.ActualHeight - SpectrumMarker.Height / 2), 0, 0);
		}
		if (HueSurface.ActualHeight > 0)
			HueMarker.Margin = new Thickness(-2, Math.Clamp((_hue / 360d) * HueSurface.ActualHeight - HueMarker.Height / 2, 0, HueSurface.ActualHeight - HueMarker.Height), -2, 0);
	}

	private void SetSpectrumFromPoint(Point point)
	{
		_saturation = Math.Clamp(point.X / Math.Max(1, SpectrumSurface.ActualWidth), 0, 1);
		_brightness = 1 - Math.Clamp(point.Y / Math.Max(1, SpectrumSurface.ActualHeight), 0, 1);
		CategoryColorPicker.SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
	}

	private void SetHueFromPoint(Point point)
	{
		_hue = Math.Clamp(point.Y / Math.Max(1, HueSurface.ActualHeight), 0, 1) * 360d;
		CategoryColorPicker.SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
	}

	private void Spectrum_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _draggingSpectrum = true; SpectrumSurface.CaptureMouse(); SetSpectrumFromPoint(e.GetPosition(SpectrumSurface)); }
	private void Spectrum_MouseMove(object sender, MouseEventArgs e) { if (_draggingSpectrum && e.LeftButton == MouseButtonState.Pressed) SetSpectrumFromPoint(e.GetPosition(SpectrumSurface)); }
	private void Hue_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _draggingHue = true; HueSurface.CaptureMouse(); SetHueFromPoint(e.GetPosition(HueSurface)); }
	private void Hue_MouseMove(object sender, MouseEventArgs e) { if (_draggingHue && e.LeftButton == MouseButtonState.Pressed) SetHueFromPoint(e.GetPosition(HueSurface)); }
	private void ColorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { _draggingSpectrum = false; _draggingHue = false; Mouse.Capture(null); }

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

	private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || RedSlider == null || GreenSlider == null || BlueSlider == null) return;
		CategoryColorPicker.SelectedColor = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
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
}
