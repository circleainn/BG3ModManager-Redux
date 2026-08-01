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
	private bool _draggingHue;
	private bool _draggingColorPlane;
	private bool _preserveHsvOnColorChange;
	private double _hue;
	private double _saturation;
	private double _brightness;
	private string _lastPreviewColor = String.Empty;
	private readonly bool _allowEmptyName;
	private readonly List<string> _savedColors;
	private readonly ObservableCollection<IconChooserChoice> _iconChoices;
	public IReadOnlyList<string> SavedColors => _savedColors;
	public event Action<string> ColorPreviewChanged;
	public bool ResetToDefaultRequested { get; private set; }
	public string CategoryName => CategoryNameTextBox.Text?.Trim();
	public string CategoryDescription => CategoryDescriptionTextBox.Text?.Trim() ?? String.Empty;
	public bool HideSeparatorLine => HideSeparatorLineCheckBox?.IsChecked == true;
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
		CategoryNameTextBox.Visibility = Visibility.Collapsed;
		IconChooserCard.Visibility = Visibility.Collapsed;
		DescriptionEditorPanel.Visibility = Visibility.Collapsed;
		CategoryPreviewPanel.Visibility = Visibility.Collapsed;
		MinHeight = Math.Min(560, MaxHeight);
		Height = Math.Min(620, MaxHeight);
	}

	public CategoryNameDialog(string categoryName = "", string color = "#8A6AF1", bool canEditName = true,
		IEnumerable<string> savedColors = null, bool visualDividerMode = false, string iconId = "",
		bool canResetToDefault = false, bool useCategoryColorsForHover = false, string description = "",
		bool useCategoryColorsForSidebarSelection = false, bool useCategoryColorsForSidebarText = false,
		bool showInterfaceIcons = true, bool hideSeparatorLine = false)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
		Height = Math.Min(860, MaxHeight);
		_allowEmptyName = visualDividerMode;
		_savedColors = (savedColors ?? Enumerable.Empty<string>())
			.Where(IsValidHexColor).Select(value => value.ToUpperInvariant())
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		CategoryNameTextBox.Text = categoryName;
		CategoryDescriptionTextBox.Text = description ?? String.Empty;
		CategoryPreviewPanel.Tag = useCategoryColorsForSidebarSelection;
		CategoryPreviewName.Tag = useCategoryColorsForSidebarText;
		CategoryPreviewIconHost.Visibility = showInterfaceIcons ? Visibility.Visible : Visibility.Collapsed;
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
		DialogHeading.Text = visualDividerMode ? "Style a separator" : canEditName ? "Create a category" : $"Edit {categoryName}";
		DialogHelperText.Text = visualDividerMode
			? "Choose a name, color, and icon. Leave the name empty for a line-only separator."
			: canEditName
			? "Choose a unique name, optional description, color, and marker or icon. Dot is the default."
			: canResetToDefault
			? "Built-in category names cannot be changed. Change its color and icon, or reset it to the default."
			: "Choose a color and marker or icon. Dot is the default.";
		ConfirmButton.Content = visualDividerMode ? "Save" : canEditName ? "Add" : "Save";
		ResetToDefaultButton.Visibility = canResetToDefault ? Visibility.Visible : Visibility.Collapsed;
		if (canResetToDefault)
			CategoryNameTextBox.ToolTip = "Create a custom category to use a different name.";
		if (visualDividerMode)
		{
			DescriptionEditorPanel.Visibility = Visibility.Visible;
			CategoryPreviewPanel.Visibility = Visibility.Collapsed;
			SeparatorPreviewPanel.Visibility = Visibility.Visible;
			SeparatorPreviewPanel.Tag = useCategoryColorsForHover;
			HideSeparatorLineCheckBox.IsChecked = hideSeparatorLine;
			HideSeparatorLineCheckBox.Visibility = Visibility.Visible;
			ColorFieldLabel.Text = "Separator color";
			IconFieldLabel.Text = "Icon";
			TintCustomIconText.Text = "Tint with separator color";
			CategoryNameTextBox.ToolTip = "Optional separator label";
			CategoryDescriptionTextBox.ToolTip = "Shown when the separator is hovered in the mod list";
		}
		UpdateCategoryPreviewToolTip();
		UpdateColorPresentation();
		RefreshSavedColors();
		Loaded += (_, _) => { CategoryNameTextBox.Focus(); UpdateModernColorSurface(); };
		SizeChanged += (_, _) => UpdateModernColorSurface();
	}

	private static bool IsValidHexColor(string value) =>
		!String.IsNullOrWhiteSpace(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");

	private void CategoryDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
		UpdateCategoryPreviewToolTip();

	private void UpdateCategoryPreviewToolTip()
	{
		if (CategoryPreviewRow == null || CategoryDescriptionTextBox == null) return;
		var description = CategoryDescriptionTextBox.Text?.Trim();
		CategoryPreviewRow.ToolTip = String.IsNullOrWhiteSpace(description) ? null : description;
		if (SeparatorPreviewPanel != null)
			SeparatorPreviewPanel.ToolTip = String.IsNullOrWhiteSpace(description) ? null : description;
	}

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
		// Same horizontal sheen and alpha pair as the sidebar's category hover (see
		// CategoryHoverSurface in HorizontalModLayout.xaml), so the preview matches what
		// hovering the real category row will actually look like.
		var hoverGradient = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0),
			EndPoint = new Point(1, 0)
		};
		hoverGradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x28, color.R, color.G, color.B), 0));
		hoverGradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x10, color.R, color.G, color.B), 1));
		Resources["Redux.CategoryEditor.HoverBrush"] = hoverGradient;
		Resources["Redux.CategoryEditor.CountHoverBrush"] =
			new SolidColorBrush(Color.FromArgb(0x24, color.R, color.G, color.B));
		if (!_preserveHsvOnColorChange)
		{
			RgbToHsv(color, out var calculatedHue, out _saturation, out _brightness);
			// Hue is undefined for grayscale and black. Preserve the last meaningful hue
			// so moving away from the left/bottom edge does not unexpectedly jump to red.
			if (_saturation > 0.0001 && _brightness > 0.0001)
				_hue = calculatedHue;
		}
		var pureHue = HsvToRgb(_hue, 1, 1);
		Resources["Redux.CategoryEditor.HueBrush"] = new SolidColorBrush(pureHue);
		Resources["Redux.CategoryEditor.SaturationTrackBrush"] = CreateHorizontalGradient(Colors.White, HsvToRgb(_hue, 1, _brightness));
		Resources["Redux.CategoryEditor.BrightnessTrackBrush"] = CreateHorizontalGradient(Colors.Black, HsvToRgb(_hue, _saturation, 1));
		Resources["Redux.CategoryEditor.RedTrackBrush"] = CreateHorizontalGradient(Color.FromRgb(0, color.G, color.B), Color.FromRgb(255, color.G, color.B));
		Resources["Redux.CategoryEditor.GreenTrackBrush"] = CreateHorizontalGradient(Color.FromRgb(color.R, 0, color.B), Color.FromRgb(color.R, 255, color.B));
		Resources["Redux.CategoryEditor.BlueTrackBrush"] = CreateHorizontalGradient(Color.FromRgb(color.R, color.G, 0), Color.FromRgb(color.R, color.G, 255));
		_updatingColorControls = true;
		SaturationSlider.Value = _saturation * 100;
		BrightnessSlider.Value = _brightness * 100;
		RedSlider.Value = color.R;
		GreenSlider.Value = color.G;
		BlueSlider.Value = color.B;
		_updatingColorControls = false;
		UpdateModernColorSurface();
		if (!_lastPreviewColor.Equals(hex, StringComparison.OrdinalIgnoreCase))
		{
			_lastPreviewColor = hex;
			ColorPreviewChanged?.Invoke(hex);
		}
	}

	private static LinearGradientBrush CreateHorizontalGradient(Color start, Color end)
	{
		var brush = new LinearGradientBrush(start, end, new Point(0, 0.5), new Point(1, 0.5));
		brush.Freeze();
		return brush;
	}

	private void UpdateModernColorSurface()
	{
		if (SpectrumSurface == null || ColorWheelImage == null) return;
		RenderColorWheel();
		Resources["Redux.CategoryEditor.HueBrush"] = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
		if (SpectrumSurface.ActualWidth > 0 && SpectrumSurface.ActualHeight > 0)
		{
			var centerX = SpectrumSurface.ActualWidth / 2;
			var centerY = SpectrumSurface.ActualHeight / 2;
			var ringRadius = Math.Min(SpectrumSurface.ActualWidth, SpectrumSurface.ActualHeight) * 0.42;
			var angle = _hue * Math.PI / 180d;
			SpectrumMarker.Margin = new Thickness(
				centerX + Math.Cos(angle) * ringRadius - SpectrumMarker.Width / 2,
				centerY + Math.Sin(angle) * ringRadius - SpectrumMarker.Height / 2, 0, 0);
		}
		if (ColorPlane?.ActualWidth > 0 && ColorPlane.ActualHeight > 0)
		{
			var markerPoint = ColorValuesToDiscPoint(_saturation, _brightness);
			ColorPlaneMarker.Margin = new Thickness(
				markerPoint.X - ColorPlaneMarker.Width / 2,
				markerPoint.Y - ColorPlaneMarker.Height / 2,
				0, 0);
		}
	}

	private Point ColorValuesToDiscPoint(double saturation, double brightness)
	{
		var centerX = ColorPlane.ActualWidth / 2;
		var centerY = ColorPlane.ActualHeight / 2;
		var radius = Math.Max(0, Math.Min(centerX, centerY) - ColorPlaneMarker.Width / 2);
		var squareX = saturation * 2 - 1;
		var squareY = (1 - brightness) * 2 - 1;
		if (Math.Abs(squareX) <= Double.Epsilon && Math.Abs(squareY) <= Double.Epsilon)
			return new Point(centerX, centerY);

		double discRadius;
		double angle;
		if (Math.Abs(squareX) > Math.Abs(squareY))
		{
			discRadius = squareX;
			angle = Math.PI / 4 * (squareY / squareX);
		}
		else
		{
			discRadius = squareY;
			angle = Math.PI / 2 - Math.PI / 4 * (squareX / squareY);
		}

		return new Point(
			centerX + radius * discRadius * Math.Cos(angle),
			centerY + radius * discRadius * Math.Sin(angle));
	}

	private void DiscPointToColorValues(Point point, out double saturation, out double brightness)
	{
		var centerX = ColorPlane.ActualWidth / 2;
		var centerY = ColorPlane.ActualHeight / 2;
		// Input uses the complete visible disc. The marker itself is inset separately
		// when rendered, so the outer pixels do not become a hidden all-black clamp zone.
		var radius = Math.Max(1, Math.Min(centerX, centerY));
		var discX = (point.X - centerX) / radius;
		var discY = (point.Y - centerY) / radius;
		var discRadius = Math.Sqrt(discX * discX + discY * discY);
		if (discRadius > 1)
		{
			discX /= discRadius;
			discY /= discRadius;
			discRadius = 1;
		}

		if (discRadius <= Double.Epsilon)
		{
			saturation = 0.5;
			brightness = 0.5;
			return;
		}

		var angle = Math.Atan2(discY, discX);
		double squareX;
		double squareY;
		if (angle < -3 * Math.PI / 4)
		{
			squareX = -discRadius;
			squareY = -discRadius * (angle + Math.PI) / (Math.PI / 4);
		}
		else if (angle < -Math.PI / 4)
		{
			squareY = -discRadius;
			squareX = discRadius * (angle + Math.PI / 2) / (Math.PI / 4);
		}
		else if (angle < Math.PI / 4)
		{
			squareX = discRadius;
			squareY = discRadius * angle / (Math.PI / 4);
		}
		else if (angle < 3 * Math.PI / 4)
		{
			squareY = discRadius;
			squareX = -discRadius * (angle - Math.PI / 2) / (Math.PI / 4);
		}
		else
		{
			squareX = -discRadius;
			squareY = -discRadius * (angle - Math.PI) / (Math.PI / 4);
		}

		saturation = Math.Clamp((squareX + 1) / 2, 0, 1);
		brightness = 1 - Math.Clamp((squareY + 1) / 2, 0, 1);
	}

	private void RenderColorWheel()
	{
		if (ColorWheelImage == null || ColorWheelImage.Source != null) return;
		// Render at 2x and let WPF downsample it for a smoother ring on scaled displays.
		const int size = 396;
		var pixels = new byte[size * size * 4];
		var center = (size - 1) / 2d;
		var outerRadius = center;
		var innerRadius = center * 0.62;
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var dx = x - center;
				var dy = y - center;
				var distance = Math.Sqrt(dx * dx + dy * dy);
				if (distance < innerRadius || distance > outerRadius) continue;
				var hue = Math.Atan2(dy, dx) * 180d / Math.PI;
				if (hue < 0) hue += 360;
				var color = HsvToRgb(hue, 1, 1);
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
	}

	private bool SetSpectrumFromPoint(Point point, bool requireRingHit)
	{
		var centerX = SpectrumSurface.ActualWidth / 2;
		var centerY = SpectrumSurface.ActualHeight / 2;
		var dx = point.X - centerX;
		var dy = point.Y - centerY;
		var radius = Math.Min(centerX, centerY);
		var distance = Math.Sqrt(dx * dx + dy * dy);

		// The center is a read-only preview. Only the visible hue ring changes hue.
		if (requireRingHit && (distance < radius * 0.69 || distance > radius))
			return false;
		if (distance <= Double.Epsilon)
			return false;

		_hue = Math.Atan2(dy, dx) * 180d / Math.PI;
		if (_hue < 0) _hue += 360;
		SetSelectedHsvColor();
		return true;
	}

	private void Spectrum_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (ColorPlane.IsMouseOver) return;
		if (!SetSpectrumFromPoint(e.GetPosition(SpectrumSurface), requireRingHit: true)) return;
		_draggingHue = true;
		SpectrumSurface.CaptureMouse();
		e.Handled = true;
	}

	private void Spectrum_MouseMove(object sender, MouseEventArgs e)
	{
		if (_draggingHue && e.LeftButton == MouseButtonState.Pressed)
			SetSpectrumFromPoint(e.GetPosition(SpectrumSurface), requireRingHit: false);
	}

	private void ColorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_draggingHue = false;
		_draggingColorPlane = false;
		Mouse.Capture(null);
	}

	private void SetColorPlaneFromPoint(Point point)
	{
		if (ColorPlane.ActualWidth <= 0 || ColorPlane.ActualHeight <= 0) return;
		DiscPointToColorValues(point, out _saturation, out _brightness);
		SetSelectedHsvColor();
	}

	private void SetSelectedHsvColor()
	{
		var selectedColor = HsvToRgb(_hue, _saturation, _brightness);
		if (CategoryColorPicker.SelectedColor == selectedColor)
		{
			// Several nearby HSV positions can quantize to the same 8-bit RGB color,
			// especially near black. Keep the pointers moving continuously even when
			// the externally visible color has not changed by a full RGB step.
			UpdateModernColorSurface();
			return;
		}

		_preserveHsvOnColorChange = true;
		try
		{
			CategoryColorPicker.SelectedColor = selectedColor;
		}
		finally
		{
			_preserveHsvOnColorChange = false;
		}
	}

	private void ColorPlane_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_draggingColorPlane = true;
		ColorPlane.CaptureMouse();
		SetColorPlaneFromPoint(e.GetPosition(ColorPlane));
		e.Handled = true;
	}

	private void ColorPlane_MouseMove(object sender, MouseEventArgs e)
	{
		if (_draggingColorPlane && e.LeftButton == MouseButtonState.Pressed)
			SetColorPlaneFromPoint(e.GetPosition(ColorPlane));
	}

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
		SetSelectedHsvColor();
	}

	private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || BrightnessSlider == null) return;
		_brightness = BrightnessSlider.Value / 100d;
		SetSelectedHsvColor();
	}

	private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || RedSlider == null || GreenSlider == null || BlueSlider == null) return;
		CategoryColorPicker.SelectedColor = Color.FromRgb(
			(byte)Math.Round(RedSlider.Value),
			(byte)Math.Round(GreenSlider.Value),
			(byte)Math.Round(BlueSlider.Value));
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
			"Remove this imported icon? Categories and separators using it will use the default dot.",
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
