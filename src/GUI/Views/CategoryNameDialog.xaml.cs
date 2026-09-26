using AdonisUI.Controls;
using DivinityModManager.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using DivinityModManager.Util;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DivinityModManager.Views;

public partial class CategoryNameDialog : AdonisWindow
{
	private bool _updatingColorControls;
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
	public bool HideSeparatorLine => ShowSeparatorLineCheckBox?.IsChecked != true;
	public bool UseSeparatorInEveryLoadOrder => GlobalSeparatorCheckBox?.IsChecked == true;
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

	private sealed record SeparatorParentChoice(string Id, string Label, bool IsGlobal);
	private bool _parentScopeCanChange;
	public string SelectedSeparatorParentId => SeparatorParentComboBox.SelectedValue as string ?? String.Empty;

	public void ConfigureSeparatorParent(IEnumerable<DivinityModManager.Models.ModListVisualDividerData> parents,
		string currentParentId, bool canChange, bool canChoosePersistence, string disabledReason = null)
	{
		_parentScopeCanChange = canChoosePersistence;
		var choices = new List<SeparatorParentChoice> { new(String.Empty, "None — standalone separator", false) };
		choices.AddRange(parents.Select(parent => new SeparatorParentChoice(parent.Id,
			String.IsNullOrWhiteSpace(parent.Title) ? $"Untitled separator (position {parent.Position + 1})" : parent.Title, parent.IsGlobal)));
		SeparatorParentComboBox.ItemsSource = choices;
		SeparatorParentComboBox.SelectedValue = currentParentId ?? String.Empty;
		SeparatorParentComboBox.IsEnabled = canChange;
		if (!canChange) SeparatorParentComboBox.ToolTip = disabledReason;
		if (SeparatorParentPanel.Visibility != Visibility.Visible)
			Height = Math.Min(Height + 36, MaxHeight);
		SeparatorParentPanel.Visibility = Visibility.Visible;
		DescriptionEditorPanel.Margin = new Thickness(0, 8, 0, 8);
		UpdateParentPersistence();
	}

	private void SeparatorParentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateParentPersistence();
	private void UpdateParentPersistence()
	{
		if (GlobalSeparatorCheckBox == null || SeparatorParentComboBox?.SelectedItem is not SeparatorParentChoice choice) return;
		var hasParent = !String.IsNullOrEmpty(choice.Id);
		Title = hasParent ? "Edit Sub-separator" : "Edit Separator";
		GlobalSeparatorCheckBox.IsEnabled = _parentScopeCanChange && !hasParent;
		if (hasParent) GlobalSeparatorCheckBox.IsChecked = choice.IsGlobal;
		GlobalSeparatorCheckBox.ToolTip = hasParent
			? "Sub-separators inherit persistence from their containing separator."
			: _parentScopeCanChange ? "Keep this separator available across saved load orders." : "This pane's organization is shared across load orders.";
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
		NameEditorPanel.Visibility = Visibility.Collapsed;
		IconChooserCard.Visibility = Visibility.Collapsed;
		DescriptionEditorPanel.Visibility = Visibility.Collapsed;
		ConfirmButtonText.Text = "Save";
		ConfirmButtonIcon.StrokeData = FindResource("Redux.Icon.Save") as Geometry;
		DialogHeading.Visibility = Visibility.Visible;
		IconOptionsPanel.Visibility = Visibility.Collapsed;
		DialogHelperText.Visibility = Visibility.Visible;
		MinHeight = Math.Min(380, MaxHeight);
		Height = Math.Min(480, MaxHeight);
	}

	public void ConfigureNameOnly(string title, string heading, string helperText, string confirmText)
	{
		IconChooserCard.Visibility = Visibility.Collapsed;
		IconOptionsPanel.Visibility = Visibility.Collapsed;
		DialogHelperText.Visibility = Visibility.Visible;
		Title = title;
		DialogHeading.Text = heading;
		DialogHelperText.Text = helperText;
		DescriptionEditorPanel.Visibility = Visibility.Collapsed;
		ColorFieldLabel.Visibility = Visibility.Collapsed;
		ColorEditorCard.Visibility = Visibility.Collapsed;
		ResetToDefaultButton.Visibility = Visibility.Collapsed;
		ConfirmButtonText.Text = confirmText;
		ConfirmButtonIcon.StrokeData = FindResource("Redux.Icon.Save") as Geometry;
		DialogTitleBar.IconData = FindResource("Redux.Icon.Duplicate") as Geometry;
		Width = 520;
		MinWidth = 440;
		MinHeight = 220;
		MaxHeight = 320;
		Height = Double.NaN;
		SizeToContent = SizeToContent.Height;
		ResizeMode = ResizeMode.NoResize;
	}

	public CategoryNameDialog(string categoryName = "", string color = "#8A6AF1", bool canEditName = true,
		IEnumerable<string> savedColors = null, bool visualDividerMode = false, string iconId = "",
		bool canResetToDefault = false, bool useCategoryColorsForHover = false, string description = "",
		bool useCategoryColorsForSidebarSelection = false, bool useCategoryColorsForSidebarText = false,
		bool showInterfaceIcons = true, bool hideSeparatorLine = true,
		bool allowGlobalSeparator = false, bool isGlobalSeparator = false,
		bool? lockedGlobalSeparator = null, bool isChildSeparator = false)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 32);
		MinHeight = Math.Min(MinHeight, MaxHeight);
		Height = Math.Min(visualDividerMode ? 600 : 550, MaxHeight);
		_allowEmptyName = visualDividerMode;
		// Categories retain their compact naming constraint. Separator labels can be
		// descriptive and are safely trimmed in the list, so do not truncate them at
		// the editor boundary.
		CategoryNameTextBox.MaxLength = visualDividerMode ? 0 : 40;
		_savedColors = (savedColors ?? Enumerable.Empty<string>())
			.Where(IsValidHexColor).Select(value => value.ToUpperInvariant())
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		CategoryNameTextBox.Text = categoryName;
		CategoryDescriptionTextBox.Text = description ?? String.Empty;
		DescriptionEditorPanel.Header = String.IsNullOrWhiteSpace(description) ? "Add a description" : "Description";
		DescriptionEditorPanel.IsExpanded = !String.IsNullOrWhiteSpace(description);
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
			? "Organize your mods with a label, icon, and color."
			: canEditName
			? "Give your category a name, then choose an icon and color."
			: canResetToDefault
			? "Built-in category names cannot be changed. Change its color and icon, or reset it to the default."
			: "Choose a color and marker or icon. Dot is the default.";
		var isNewCategory = !visualDividerMode && canEditName;
		ConfirmButtonText.Text = isNewCategory ? "Add" : "Save";
		ConfirmButtonIcon.StrokeData = FindResource(isNewCategory ? "Redux.Icon.AddCircle" : "Redux.Icon.Save") as Geometry;
		ResetToDefaultButton.Visibility = canResetToDefault ? Visibility.Visible : Visibility.Collapsed;
		if (canResetToDefault)
			CategoryNameTextBox.ToolTip = "Create a custom category to use a different name.";
		if (visualDividerMode)
		{
			CategoryNameFieldLabel.Text = "Label (optional)";
			DescriptionEditorPanel.Visibility = Visibility.Visible;
			ShowSeparatorLineCheckBox.IsChecked = !hideSeparatorLine;
			ShowSeparatorLineCheckBox.Visibility = Visibility.Visible;
			GlobalSeparatorCheckBox.Visibility = Visibility.Visible;
			GlobalSeparatorCheckBox.IsChecked = lockedGlobalSeparator.HasValue
				? lockedGlobalSeparator.Value
				: allowGlobalSeparator ? isGlobalSeparator : true;
			GlobalSeparatorCheckBox.IsEnabled = allowGlobalSeparator && !lockedGlobalSeparator.HasValue;
			if (lockedGlobalSeparator.HasValue)
			{
				GlobalSeparatorCheckBoxText.Text = "Use in every load order";
				GlobalSeparatorCheckBox.ToolTip = lockedGlobalSeparator.Value
					? "This sub-separator inherits persistence from its containing separator."
					: "This sub-separator is local because its containing separator is local.";
			}
			else if (!allowGlobalSeparator)
			{
				GlobalSeparatorCheckBoxText.Text = "Used in every load order";
				GlobalSeparatorCheckBox.ToolTip = "Inactive Mods organization is shared across every load order automatically.";
			}
			ColorFieldLabel.Text = "Separator color";
			IconFieldLabel.Text = "Icon";
			TintCustomIconText.Text = "Tint with separator color";
			CategoryNameTextBox.ToolTip = "Optional separator label";
			CategoryDescriptionTextBox.ToolTip = "Shown when the separator is hovered in the mod list";
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
			var swatch = new Button
			{
				Tag = value,
				Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)),
				Style = (Style)FindResource("CategoryColorSwatchStyle"),
				ToolTip = $"{value}\nLeft-click to use. Right-click to remove."
			};
			swatch.Click += ColorSwatch_Click;
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
		if (sender is FrameworkElement { Tag: string value })
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
		HueSlider.Value = _hue;
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

	private void ColorPlane_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		ColorPlane.Clip = new RectangleGeometry(new Rect(e.NewSize), 6, 6);
		UpdateModernColorSurface();
	}

	private void UpdateModernColorSurface()
	{
		if (ColorPlane == null || ColorPlane.ActualWidth <= 0 || ColorPlane.ActualHeight <= 0 || ColorPlaneMarker == null) return;
		Resources["Redux.CategoryEditor.HueBrush"] = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
		// Keep the complete pointer visible at the edges without changing the selected color.
		ColorPlaneMarker.Margin = new Thickness(
			Math.Clamp(_saturation * ColorPlane.ActualWidth - 6, 0, Math.Max(0, ColorPlane.ActualWidth - 12)),
			Math.Clamp((1 - _brightness) * ColorPlane.ActualHeight - 6, 0, Math.Max(0, ColorPlane.ActualHeight - 12)), 0, 0);
	}

	private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_updatingColorControls || CategoryColorPicker == null) return;
		_hue = e.NewValue;
		SetSelectedHsvColor();
	}

	private void ColorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_draggingColorPlane = false;
		Mouse.Capture(null);
	}

	private void SetColorPlaneFromPoint(Point point)
	{
		if (ColorPlane.ActualWidth <= 0 || ColorPlane.ActualHeight <= 0) return;
		_saturation = Math.Clamp(point.X / ColorPlane.ActualWidth, 0, 1);
		_brightness = 1 - Math.Clamp(point.Y / ColorPlane.ActualHeight, 0, 1);
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
		if (IsValidHexColor(value) && ColorConverter.ConvertFromString(value) is Color color) CategoryColorPicker.SelectedColor = color;
		else UpdateColorPresentation();
	}

	private void HexColorTextBox_Commit(object sender, RoutedEventArgs e) => ApplyHexColor();
	private void HexColorTextBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter) { ApplyHexColor(); HexColorTextBox.SelectAll(); e.Handled = true; }
	}

	private void ColorSwatch_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string value } && ColorConverter.ConvertFromString(value) is Color color)
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
		if (IconOptionsPanel != null) IconOptionsPanel.Visibility = _allowEmptyName || isCustom ? Visibility.Visible : Visibility.Collapsed;
		TintCustomIconCheckBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
		DeleteCustomIconButton.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
		if (CategoryIconComboBox.SelectedItem is IconChooserChoice choice && isCustom)
		{
			choice.PreviewIconId = ReduxCustomIconService.WithTint(selectedId, TintCustomIconCheckBox.IsChecked == true);
		}
	}

}
