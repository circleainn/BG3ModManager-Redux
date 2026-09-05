using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Extender;
using DivinityModManager.Models.View;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using DynamicData;

using ReactiveMarbles.ObservableEvents;

using Splat;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

using WpfScreenHelper;

using Xceed.Wpf.Toolkit;

namespace DivinityModManager.Views;

public class SettingsWindowBase : HideWindowBase<SettingsWindowViewModel> { }

internal class SortSettings : IComparer<SettingsAttributeProperty>
{
	private static string[] _priorityList = [
		nameof(DivinityModManagerSettings.GameExecutablePath),
		nameof(DivinityModManagerSettings.GameDataPath),
		nameof(DivinityModManagerSettings.DocumentsFolderPathOverride),
		nameof(DivinityModManagerSettings.LoadOrderPath),
	];

	public int Compare(SettingsAttributeProperty s1, SettingsAttributeProperty s2)
	{
		if (_priorityList.Contains(s1.Property.Name) && _priorityList.Contains(s2.Property.Name))
		{
			return s1.Attribute.DisplayName.CompareTo(s2.Attribute.DisplayName);
		}
		if (_priorityList.Contains(s1.Property.Name))
		{
			return -1;
		}
		if (_priorityList.Contains(s2.Property.Name))
		{
			return 1;
		}
		return s1.Attribute.DisplayName.CompareTo(s2.Attribute.DisplayName);
	}
}

internal sealed record SettingsGroup(string Title, string Description, params string[] PropertyNames);

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : SettingsWindowBase
{
	private ICollectionView _keybindingsView;

	private bool _updatingCustomThemeSelection;
	private bool _updatingTypographySelection;

	private void InterfaceIconsCheckBox_Unchecked(object sender, RoutedEventArgs e)
	{
		if (ViewModel?.Settings != null)
		{
			ViewModel.Settings.UseIconsOnly = false;
		}
	}
	private static readonly SettingsGroup[] GeneralSettingsGroups =
	[
		new("Paths and storage",
			"Choose where Redux finds the game, profiles, and saved load orders.",
			nameof(DivinityModManagerSettings.GameExecutablePath),
			nameof(DivinityModManagerSettings.GameDataPath),
			nameof(DivinityModManagerSettings.DocumentsFolderPathOverride),
			nameof(DivinityModManagerSettings.LoadOrderPath)),
		new("Game launch",
			"Control how Redux starts Baldur's Gate 3 and what happens after launch.",
			nameof(DivinityModManagerSettings.LaunchType),
			nameof(DivinityModManagerSettings.CustomLaunchAction),
			nameof(DivinityModManagerSettings.CustomLaunchArgs),
			nameof(DivinityModManagerSettings.LaunchDX11),
			nameof(DivinityModManagerSettings.ActionOnGameLaunch),
			nameof(DivinityModManagerSettings.DisableLauncherTelemetry),
			nameof(DivinityModManagerSettings.DisableLauncherModWarnings),
			nameof(DivinityModManagerSettings.GameStoryLogEnabled)),
		new("Mod-list workflow",
			"Adjust load-order editing, dependency handling, categories, and workspace behavior.",
			nameof(DivinityModManagerSettings.AutoAddDependenciesWhenExporting),
			nameof(DivinityModManagerSettings.HideEmptyModCategories),
			nameof(DivinityModManagerSettings.ShiftListFocusOnSwap),
			nameof(DivinityModManagerSettings.SaveWindowLocation),
			nameof(DivinityModManagerSettings.EnableColorblindSupport),
			nameof(DivinityModManagerSettings.HideToolbar)),
		new("Visual comfort",
			"Reduce motion or background effects for a quieter interface.",
			nameof(DivinityModManagerSettings.ReduceMotion),
			nameof(DivinityModManagerSettings.DisableBackgroundEffects)),
		new("Optional features",
			"Control source linking, read-only diagnostics, and experimental load-order guidance.",
			nameof(DivinityModManagerSettings.LocalOnlyMode),
			nameof(DivinityModManagerSettings.EnableModHealth),
			nameof(DivinityModManagerSettings.DisableModioWarnings),
			nameof(DivinityModManagerSettings.EnableLoadOrderAdvisor)),
		new("Metadata services",
			"Add optional provider keys for source details and update information.",
			nameof(DivinityModManagerSettings.NexusModsAPIKey),
			nameof(DivinityModManagerSettings.ModioAPIKey)),
		new("Warnings and maintenance",
			"Choose which update and safety notices Redux keeps active.",
			nameof(DivinityModManagerSettings.CheckForUpdates),
			nameof(DivinityModManagerSettings.DeleteModCrashSanityCheck),
			nameof(DivinityModManagerSettings.DisableMissingModWarnings))
	];

	private static readonly SettingsGroup[] ExtenderSettingsGroups =
	[
		new("Core behavior",
			"Configure Script Extender validation, achievements, crash reporting, and saved settings.",
			nameof(ScriptExtenderSettings.CustomProfile),
			nameof(ScriptExtenderSettings.DisableModValidation),
			nameof(ScriptExtenderSettings.InsanityCheck),
			nameof(ScriptExtenderSettings.EnableAchievements),
			nameof(ScriptExtenderSettings.SendCrashReports),
			nameof(ScriptExtenderSettings.ExportDefaultExtenderSettings)),
		new("Logging",
			"Choose what Script Extender records and where its logs are stored.",
			nameof(ScriptExtenderSettings.LogDirectory),
			nameof(ScriptExtenderSettings.CreateConsole),
			nameof(ScriptExtenderSettings.EnableLogging),
			nameof(ScriptExtenderSettings.LogRuntime),
			nameof(ScriptExtenderSettings.LogCompile),
			nameof(ScriptExtenderSettings.LogFailedCompile)),
		new("Developer and diagnostics",
			"Advanced Script Extender debugging, patching, console, and performance options.",
			nameof(ScriptExtenderSettings.DeveloperMode),
			nameof(ScriptExtenderSettings.DebuggerFlags),
			nameof(ScriptExtenderSettings.DisableLauncher),
			nameof(ScriptExtenderSettings.DisableStoryMerge),
			nameof(ScriptExtenderSettings.DisableStoryPatching),
			nameof(ScriptExtenderSettings.EnableExtensions),
			nameof(ScriptExtenderSettings.EnableDebugger),
			nameof(ScriptExtenderSettings.DebuggerPort),
			nameof(ScriptExtenderSettings.DumpNetworkStrings),
			nameof(ScriptExtenderSettings.EnableLuaDebugger),
			nameof(ScriptExtenderSettings.LuaBuiltinResourceDirectory),
			nameof(ScriptExtenderSettings.ClearOnReset),
			nameof(ScriptExtenderSettings.DefaultToClientConsole),
			nameof(ScriptExtenderSettings.ShowPerfWarnings))
	];

	private static bool IsSourceIntegrationSetting(string propertyName)
	{
		return propertyName == nameof(DivinityModManagerSettings.NexusModsAPIKey)
			|| propertyName == nameof(DivinityModManagerSettings.ModioAPIKey);
	}

	private void ApplyModuleAvailability(string propertyName, TextBlock label, FrameworkElement control)
	{
		if (!IsSourceIntegrationSetting(propertyName) || ViewModel?.Main?.Modules == null)
		{
			return;
		}

		var sourceIntegrationsEnabled = new Binding(nameof(ReduxModuleState.SourceIntegrationsEnabled))
		{
			Source = ViewModel.Main.Modules,
			Mode = BindingMode.OneWay
		};
		control.SetBinding(IsEnabledProperty, sourceIntegrationsEnabled);
		label.SetBinding(IsEnabledProperty, new Binding(nameof(ReduxModuleState.SourceIntegrationsEnabled))
		{
			Source = ViewModel.Main.Modules,
			Mode = BindingMode.OneWay
		});
	}

	public SettingsWindow()
	{
		InitializeComponent();
	}

	public void ApplyAdaptiveDefaultSize(Window owner)
	{
		var workArea = owner != null ? Screen.FromWindow(owner).WorkingArea : SystemParameters.WorkArea;
		var targetWidth = Math.Clamp(workArea.Width * 0.34, 820, 900);
		var targetHeight = Math.Clamp(workArea.Height * 0.78, 720, 960);
		Width = Math.Max(MinWidth, Math.Min(targetWidth, workArea.Width - 64));
		Height = Math.Max(MinHeight, Math.Min(targetHeight, workArea.Height - 64));
	}

	private void SetComboBoxMainToolTip(object sender, SelectionChangedEventArgs e)
	{
		if(sender is ComboBox combo && combo.SelectedItem is EnumEntry enumEntry && !string.IsNullOrWhiteSpace(enumEntry.Description))
		{
			ToolTipService.SetToolTip(combo, enumEntry.Description);
		}
	}

	private void ThemeCard_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: ReduxThemeType theme })
		{
			ViewModel.Settings.ActiveCustomThemeId = String.Empty;
			ViewModel.Settings.TypographyFont = ReduxTypographyFont.Manrope;
			ViewModel.Settings.CustomTypographyFont = String.Empty;
			ViewModel.Settings.TextSize = ReduxTextSize.Default;
			ReduxThemeService.ApplyBuiltInCategoryPresentation(ViewModel.Settings, theme);
			ThemeComboBox.SelectedValue = theme;
			RefreshTypographyChoices();
			RefreshCustomThemeControls();
		}
	}

	private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ThemeComboBox.SelectedValue is not ReduxThemeType theme) return;

		var customThemeActive = !String.IsNullOrWhiteSpace(ViewModel?.Settings?.ActiveCustomThemeId);
		ReduxDarkThemeCard.IsChecked = !customThemeActive && theme == ReduxThemeType.ReduxDark;
		ReduxLightThemeCard.IsChecked = !customThemeActive && theme == ReduxThemeType.ReduxLight;
		ParchmentThemeCard.IsChecked = !customThemeActive && theme == ReduxThemeType.Parchment;
	}

	private void RefreshTypographyChoices(string preferredCustomReference = null)
	{
		if (TypographyComboBox == null) return;
		_updatingTypographySelection = true;
		var choices = ReduxCustomFontService.GetChoices();
		TypographyComboBox.ItemsSource = choices;
		var customReference = preferredCustomReference ?? ViewModel?.Settings?.CustomTypographyFont ?? String.Empty;
		var selected = !String.IsNullOrWhiteSpace(customReference)
			? choices.FirstOrDefault(choice => choice.CustomReference.Equals(customReference, StringComparison.OrdinalIgnoreCase))
			: null;
		selected ??= choices.FirstOrDefault(choice => !choice.IsCustom && choice.BuiltInFont == (ViewModel?.Settings?.TypographyFont ?? ReduxTypographyFont.Manrope));
		selected ??= choices.First(choice => choice.BuiltInFont == ReduxTypographyFont.Manrope && !choice.IsCustom);
		TypographyComboBox.SelectedItem = selected;
		DeleteCustomFontButton.IsEnabled = selected.IsCustom;
		_updatingTypographySelection = false;
	}

	private void TypographyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_updatingTypographySelection || ViewModel?.Settings == null || TypographyComboBox.SelectedItem is not ReduxFontChoice choice) return;
		ViewModel.Settings.CustomTypographyFont = choice.IsCustom ? choice.CustomReference : String.Empty;
		ViewModel.Settings.TypographyFont = choice.IsCustom ? ReduxTypographyFont.Manrope : choice.BuiltInFont;
		DeleteCustomFontButton.IsEnabled = choice.IsCustom;
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
			ReduxMessageBox.Show(this, error, "Import Font", MessageBoxButton.OK,
				MessageBoxImage.Error, MessageBoxResult.OK);
			return;
		}
		RefreshTypographyChoices(choice.CustomReference);
		TypographyComboBox_SelectionChanged(TypographyComboBox, null);
	}

	private void DeleteCustomFont_Click(object sender, RoutedEventArgs e)
	{
		if (TypographyComboBox.SelectedItem is not ReduxFontChoice { IsCustom: true } choice) return;
		if (!choice.CustomReference.StartsWith(ReduxCustomFontService.ReferencePrefix, StringComparison.OrdinalIgnoreCase)) return;
		var affectedThemes = ViewModel.Settings.CustomThemes.Count(theme =>
			theme.CustomTypographyFont.Equals(choice.CustomReference, StringComparison.OrdinalIgnoreCase));
		var usageNote = affectedThemes == 0
			? ""
			: $"\n\n{affectedThemes} custom theme{(affectedThemes == 1 ? "" : "s")} will fall back to Manrope.";
		var result = ReduxMessageBox.Show(this,
			$"Remove '{choice.Name}'?{usageNote}", "Remove Custom Font",
			MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (result != MessageBoxResult.Yes) return;
		if (!ReduxCustomFontService.TryDelete(choice.CustomReference, out var error))
		{
			ReduxMessageBox.Show(this, error, "Remove Custom Font", MessageBoxButton.OK,
				MessageBoxImage.Error, MessageBoxResult.OK);
			return;
		}

		if (ViewModel.Settings.CustomTypographyFont.Equals(choice.CustomReference, StringComparison.OrdinalIgnoreCase))
		{
			ViewModel.Settings.CustomTypographyFont = String.Empty;
			ViewModel.Settings.TypographyFont = ReduxTypographyFont.Manrope;
		}
		foreach (var theme in ViewModel.Settings.CustomThemes.Where(theme =>
			theme.CustomTypographyFont.Equals(choice.CustomReference, StringComparison.OrdinalIgnoreCase)))
		{
			theme.CustomTypographyFont = String.Empty;
			theme.TypographyFont = ReduxTypographyFont.Manrope;
		}
		ViewModel.Main.SaveSettings();
		RefreshTypographyChoices();
		RefreshCustomThemeControls();
	}

	private void OpenCustomFontsFolder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = ReduxCustomFontService.GetLibraryDirectory(),
				UseShellExecute = true
			});
		}
		catch (Exception exception)
		{
			DivinityApp.Log($"Could not open the Redux custom fonts folder: {exception.Message}");
			ReduxMessageBox.Show(this, "The custom fonts folder could not be opened.", "Custom Fonts",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private ReduxCustomTheme SelectedCustomTheme => CustomThemeComboBox.SelectedItem as ReduxCustomTheme;

	private void RefreshCustomThemeControls()
	{
		if (ViewModel?.Settings == null) return;
		_updatingCustomThemeSelection = true;
		CustomThemeComboBox.ItemsSource = ViewModel.Settings.CustomThemes;
		var activeTheme = ReduxThemeService.GetActiveTheme(ViewModel.Settings);
		CustomThemeComboBox.SelectedItem = activeTheme;
		var hasSelection = CustomThemeComboBox.SelectedItem is ReduxCustomTheme;
		EditCustomThemeButton.IsEnabled = hasSelection;
		DeleteCustomThemeButton.IsEnabled = hasSelection;
		DuplicateCustomThemeButton.IsEnabled = hasSelection;
		ExportCustomThemeButton.IsEnabled = hasSelection;
		CustomThemeStatusText.Text = activeTheme != null
			? $"Active custom theme · {activeTheme.BaseTheme.GetDescription()} · {ReduxCustomFontService.GetDisplayName(activeTheme.TypographyFont, activeTheme.CustomTypographyFont)} · {activeTheme.TextSize.GetDescription()} text"
			: ViewModel.Settings.CustomThemes.Count == 0
				? "No custom themes yet. Create one from the current built-in palette."
				: "Choose a custom theme above, or keep using a built-in theme.";
		_updatingCustomThemeSelection = false;
		ThemeComboBox_SelectionChanged(ThemeComboBox, null);
	}

	private void CustomThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_updatingCustomThemeSelection || SelectedCustomTheme == null || ViewModel?.Settings == null) return;
		ActivateCustomTheme(SelectedCustomTheme);
	}

	private void ActivateCustomTheme(ReduxCustomTheme theme)
	{
		ViewModel.Settings.ActiveCustomThemeId = theme.Id;
		ViewModel.Settings.ColorTheme = theme.BaseTheme;
		ViewModel.Settings.TypographyFont = theme.TypographyFont;
		ViewModel.Settings.CustomTypographyFont = theme.CustomTypographyFont;
		ViewModel.Settings.TextSize = theme.TextSize;
		ReduxThemeService.ApplyCustomCategoryPresentation(ViewModel.Settings, theme);
		MainWindow.Self.MainView.UpdateColorTheme(theme.BaseTheme);
		ViewModel.Main.SaveSettings();
		RefreshTypographyChoices();
		RefreshCustomThemeControls();
	}

	private bool EditCustomTheme(ReduxCustomTheme workingTheme)
	{
		var previousTheme = ViewModel.Settings.ColorTheme;
		var previousFont = ViewModel.Settings.TypographyFont;
		var previousCustomFont = ViewModel.Settings.CustomTypographyFont;
		var previousTextSize = ViewModel.Settings.TextSize;
		var mainWindow = MainWindow.Self;
		var dialog = new CustomThemeEditorWindow(workingTheme)
		{
			Owner = mainWindow
		};
		dialog.PreviewChanged += preview => MainWindow.Self.MainView.PreviewCustomTheme(preview);
		dialog.ColorPreviewChanged += preview => MainWindow.Self.MainView.PreviewCustomThemeColors(preview);

		// Editing a theme from a modal Preferences window buried the live Redux
		// preview beneath two dimmed surfaces. Temporarily remove Preferences
		// from the stack and place the editor directly over the main application.
		var restorePreferences = IsVisible;
		if (restorePreferences)
		{
			ReduxWindowBehavior.RemoveOwnerBackdrop(this);
			Hide();
		}

		var accepted = false;
		try
		{
			accepted = dialog.ShowDialog() == true;
		}
		finally
		{
			if (!accepted)
			{
				MainWindow.Self.MainView.UpdateColorTheme(previousTheme);
				ReduxTypographyService.Apply(Application.Current.Resources, previousFont, previousCustomFont);
				ReduxTypographyService.ApplyTextSize(Application.Current.Resources, previousTextSize);
			}
			if (restorePreferences)
			{
				ShowWithTransition();
			}
		}

		return accepted;
	}

	private void CreateCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var working = ReduxThemeService.CreateFromBase("My Custom Theme", ViewModel.Settings.ColorTheme,
			ViewModel.Settings.TypographyFont, ViewModel.Settings.TextSize, ViewModel.Settings.CustomTypographyFont,
			ViewModel.Settings.UseCategoryColorsForInteractions, ViewModel.Settings.ShowCategoryIconsInPills,
			ViewModel.Settings.UseCategoryColorsForSidebarText,
			ViewModel.Settings.UseIconsOnly);
		if (!EditCustomTheme(working)) return;
		ViewModel.Settings.CustomThemes.Add(working);
		ActivateCustomTheme(working);
	}

	private void EditCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedCustomTheme;
		if (selected == null) return;
		var working = selected.Clone();
		if (!EditCustomTheme(working)) return;
		var index = ViewModel.Settings.CustomThemes.IndexOf(selected);
		if (index >= 0) ViewModel.Settings.CustomThemes[index] = working;
		ActivateCustomTheme(working);
	}

	private void DuplicateCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedCustomTheme;
		if (selected == null) return;
		var working = selected.Clone(createNewIdentity: true);
		working.Name = $"{selected.Name} Copy";
		if (!EditCustomTheme(working)) return;
		ViewModel.Settings.CustomThemes.Add(working);
		ActivateCustomTheme(working);
	}

	private void DeleteCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedCustomTheme;
		if (selected == null) return;
		var result = ReduxMessageBox.Show(this,
			$"Delete the custom theme '{selected.Name}'?\n\nExport it first if you may want to use it again.",
			"Delete Custom Theme", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		if (result != MessageBoxResult.Yes) return;
		ViewModel.Settings.CustomThemes.Remove(selected);
		if (selected.Id.Equals(ViewModel.Settings.ActiveCustomThemeId, StringComparison.OrdinalIgnoreCase))
		{
			ViewModel.Settings.ActiveCustomThemeId = String.Empty;
			ViewModel.Settings.TypographyFont = ReduxTypographyFont.Manrope;
			ViewModel.Settings.CustomTypographyFont = String.Empty;
			ViewModel.Settings.TextSize = ReduxTextSize.Default;
			ReduxThemeService.ApplyBuiltInCategoryPresentation(ViewModel.Settings, ViewModel.Settings.ColorTheme);
			MainWindow.Self.MainView.UpdateColorTheme(ViewModel.Settings.ColorTheme);
		}
		ViewModel.Main.SaveSettings();
		RefreshCustomThemeControls();
	}

	private void ImportCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Import Custom Theme",
			Filter = "Theme file (*.json)|*.json|All files (*.*)|*.*",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true) return;
		try
		{
			var imported = ReduxThemeService.Import(dialog.FileName);
			ViewModel.Settings.CustomThemes.Add(imported);
			ActivateCustomTheme(imported);
		}
		catch (Exception ex)
		{
			ReduxMessageBox.Show(this, $"Could not import that theme.\n\n{ex.Message}",
				"Import Custom Theme", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void ExportCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedCustomTheme;
		if (selected == null) return;
		var safeName = String.Concat(selected.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
		var dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = "Export Custom Theme",
			Filter = "Theme file (*.json)|*.json",
			FileName = $"{safeName}.json",
			AddExtension = true,
			DefaultExt = ".json"
		};
		if (dialog.ShowDialog(this) != true) return;
		try
		{
			ReduxThemeService.Export(dialog.FileName, selected);
			ViewModel.ShowAlert($"Exported '{selected.Name}'.", AlertType.Success);
		}
		catch (Exception ex)
		{
			ReduxMessageBox.Show(this, $"Could not export that theme.\n\n{ex.Message}",
				"Export Custom Theme", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
		}
	}

	private void CreateSettingsElements(ReactiveObject source, Type settingsModelType, AutoGrid targetGrid)
	{
		var sorter = new SortSettings();
		var props = settingsModelType.GetProperties()
			.Select(SettingsAttributeProperty.FromProperty)
			.Where(x => x.Attribute != null && !x.Attribute.HideFromUI)
			.OrderBy(x => x, sorter).ToList();
		var settingsGroups = settingsModelType == typeof(DivinityModManagerSettings)
			? GeneralSettingsGroups
			: settingsModelType == typeof(ScriptExtenderSettings)
				? ExtenderSettingsGroups
				: null;
		if (settingsGroups != null)
		{
			var propertyOrder = settingsGroups
				.SelectMany(group => group.PropertyNames)
				.Select((name, index) => (name, index))
				.ToDictionary(entry => entry.name, entry => entry.index);
			props = props
				.OrderBy(prop => propertyOrder.TryGetValue(prop.Property.Name, out var index) ? index : Int32.MaxValue)
				.ThenBy(prop => prop.Attribute.DisplayName)
				.ToList();
		}

		int count = props.Count + (settingsGroups?.Length ?? 0) + targetGrid.Children.Count + 1;
		int row = targetGrid.Children.Count;

		var enumDataTemplate = FindResource("EnumEntryTemplate") as DataTemplate;

		targetGrid.RowCount = count;
		targetGrid.Rows = String.Join(",", Enumerable.Repeat("auto", count));

		var debugModeBinding = new Binding(nameof(SettingsWindowViewModel.DeveloperModeVisibility))
		{
			Source = ViewModel,
			FallbackValue = Visibility.Collapsed
		};

		string currentGroupTitle = null;
		foreach (var prop in props)
		{
			var group = settingsGroups?.FirstOrDefault(candidate => candidate.PropertyNames.Contains(prop.Property.Name));
			if (group != null && group.Title != currentGroupTitle)
			{
				currentGroupTitle = group.Title;
				var heading = new StackPanel
				{
					Orientation = Orientation.Vertical,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Margin = new Thickness(0)
				};
				heading.Children.Add(new TextBlock
				{
					Text = currentGroupTitle,
					Style = FindResource("SettingsSubsectionTitleStyle") as Style
				});
				heading.Children.Add(new TextBlock
				{
					Text = group.Description,
					Margin = new Thickness(0, 2, 0, 4),
					Foreground = FindResource("ReduxTextMutedBrush") as System.Windows.Media.Brush,
					FontSize = (double)FindResource("Redux.FontSize.10"),
					TextWrapping = TextWrapping.Wrap,
					HorizontalAlignment = HorizontalAlignment.Stretch
				});
				targetGrid.Children.Add(heading);
				Grid.SetRow(heading, row++);
				Grid.SetColumnSpan(heading, 2);
			}
			var isBlankTooltip = String.IsNullOrEmpty(prop.Attribute.Tooltip);
			var targetRow = row;
			row++;
			var tb = new TextBlock
			{
				Text = prop.Attribute.DisplayName,
				ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
				TextWrapping = TextWrapping.Wrap,
				VerticalAlignment = VerticalAlignment.Center,
			};
			targetGrid.Children.Add(tb);
			Grid.SetRow(tb, targetRow);

			var tooltip = prop.Property.GetCustomAttributes(false).OfType<DisplayAttribute>().FirstOrDefault()?.Description ?? prop.Attribute.Tooltip;

			FrameworkElement createdObject = null;

			if (prop.Attribute.IsDebug)
			{
				tb.SetBinding(TextBlock.VisibilityProperty, debugModeBinding);
			}

			if (prop.Property.PropertyType.IsEnum)
			{
				var combo = new ComboBox()
				{
					ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalContentAlignment = VerticalAlignment.Center,
					SelectedValuePath = "Value",
					ItemsSource = prop.Property.PropertyType.GetEnumValues().Cast<Enum>().Select(x => new EnumEntry(x))
				};
				combo.SetBinding(ComboBox.SelectedValueProperty, new Binding(prop.Property.Name)
				{
					Source = source,
					Mode = BindingMode.TwoWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
				});
				targetGrid.Children.Add(combo);
				Grid.SetRow(combo, targetRow);
				Grid.SetColumn(combo, 1);
				createdObject = combo;

				if (enumDataTemplate != null) combo.ItemTemplate = enumDataTemplate;

				if (!string.IsNullOrWhiteSpace(tooltip))
				{
					combo.SelectionChanged += SetComboBoxMainToolTip;
					combo.Loaded += (o,e) =>
					{
						SetComboBoxMainToolTip(o, null);
					};
				}
				goto SetTooltip;
			}

			var propType = Type.GetTypeCode(prop.Property.PropertyType);

			switch (propType)
			{
				case TypeCode.Boolean:
					var cb = new CheckBox
					{
						ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
						VerticalAlignment = VerticalAlignment.Center
					};
					cb.SetBinding(CheckBox.IsCheckedProperty, new Binding(prop.Property.Name)
					{
						Source = source,
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
					});
					if (source is DivinityModManagerSettings accessibilitySettings
						&& prop.Property.Name is nameof(DivinityModManagerSettings.ReduceMotion)
							or nameof(DivinityModManagerSettings.DisableBackgroundEffects))
					{
						void ApplyAccessibilityPreference(object _, RoutedEventArgs __)
						{
							var reduceMotion = prop.Property.Name == nameof(DivinityModManagerSettings.ReduceMotion)
								? cb.IsChecked == true
								: accessibilitySettings.ReduceMotion;
							var disableBackgroundEffects = prop.Property.Name == nameof(DivinityModManagerSettings.DisableBackgroundEffects)
								? cb.IsChecked == true
								: accessibilitySettings.DisableBackgroundEffects;
							ReduxWindowBehavior.ConfigureAccessibility(reduceMotion, disableBackgroundEffects);
						}

						cb.Checked += ApplyAccessibilityPreference;
						cb.Unchecked += ApplyAccessibilityPreference;
					}
					if (prop.Attribute.IsDebug)
					{
						cb.SetBinding(CheckBox.VisibilityProperty, debugModeBinding);
					}
					// These optional diagnostic details only apply while Mod Diagnostics is running.
					if (prop.Property.Name == nameof(DivinityModManagerSettings.EnableLoadOrderAdvisor)
						|| prop.Property.Name == nameof(DivinityModManagerSettings.DisableModioWarnings))
					{
						var modHealthEnabledBinding = new Binding(nameof(DivinityModManagerSettings.EnableModHealth))
						{
							Source = source,
							Mode = BindingMode.OneWay
						};
						cb.SetBinding(CheckBox.IsEnabledProperty, modHealthEnabledBinding);
						tb.SetBinding(TextBlock.IsEnabledProperty, new Binding(nameof(DivinityModManagerSettings.EnableModHealth))
						{
							Source = source,
							Mode = BindingMode.OneWay
						});
					}
					targetGrid.Children.Add(cb);
					Grid.SetRow(cb, targetRow);
					Grid.SetColumn(cb, 1);
					createdObject = cb;
					break;

				case TypeCode.String:
					if (IsSourceIntegrationSetting(prop.Property.Name))
					{
						var passwordBox = new PasswordBox
						{
							ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
							HorizontalAlignment = HorizontalAlignment.Stretch,
							VerticalAlignment = VerticalAlignment.Center,
							VerticalContentAlignment = VerticalAlignment.Center,
							Password = prop.Property.GetValue(source) as string ?? String.Empty
						};
						passwordBox.PasswordChanged += (_, _) =>
							prop.Property.SetValue(source, passwordBox.Password?.Trim() ?? String.Empty);
						targetGrid.Children.Add(passwordBox);
						Grid.SetRow(passwordBox, targetRow);
						Grid.SetColumn(passwordBox, 1);
						createdObject = passwordBox;
						break;
					}

					var utb = new UnfocusableTextBox
					{
						ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Center,
						VerticalContentAlignment = VerticalAlignment.Center,
						TextAlignment = TextAlignment.Left
					};
					utb.SetBinding(UnfocusableTextBox.TextProperty, new Binding(prop.Property.Name)
					{
						Source = source,
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
					});
					if (prop.Attribute.IsDebug)
					{
						utb.SetBinding(UnfocusableTextBox.VisibilityProperty, debugModeBinding);
					}
					else
					{
						if (prop.Property.Name == nameof(DivinityModManagerSettings.CustomLaunchAction) || prop.Property.Name == nameof(DivinityModManagerSettings.CustomLaunchArgs))
						{
							utb.SetBinding(UnfocusableTextBox.VisibilityProperty, new Binding(nameof(DivinityModManagerSettings.CustomLaunchVisibility))
							{
								Source = source,
								Mode = BindingMode.OneWay,
								UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
							});
							tb.SetBinding(TextBlock.VisibilityProperty, new Binding(nameof(DivinityModManagerSettings.CustomLaunchVisibility))
							{
								Source = source,
								Mode = BindingMode.OneWay,
								UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
							});
						}
					}
					targetGrid.Children.Add(utb);
					Grid.SetRow(utb, targetRow);
					Grid.SetColumn(utb, 1);
					createdObject = utb;
					break;
				case TypeCode.Int32:
				case TypeCode.Int64:
					var ud = new Xceed.Wpf.Toolkit.IntegerUpDown
					{
						ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
						VerticalAlignment = VerticalAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Left,
						Padding = new Thickness(4, 2, 4, 2),
						AllowTextInput = true
					};
					ud.SetBinding(IntegerUpDown.ValueProperty, new Binding(prop.Property.Name)
					{
						Source = ViewModel.ExtenderSettings,
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
					});
					if (prop.Attribute.IsDebug)
					{
						ud.SetBinding(VisibilityProperty, debugModeBinding);
					}
					targetGrid.Children.Add(ud);
					Grid.SetRow(ud, targetRow);
					Grid.SetColumn(ud, 1);
					createdObject = ud;
					break;
			}

			SetTooltip:
			if (createdObject != null)
			{
				ApplyModuleAvailability(prop.Property.Name, tb, createdObject);
			}
			if (createdObject != null && !string.IsNullOrWhiteSpace(tooltip))
			{
				ToolTipService.SetToolTip(tb, tooltip);
				ToolTipService.SetToolTip(createdObject, tooltip);
			}
		}
	}

	private SettingsWindowTab IndexToTab(int index)
	{
		return (SettingsWindowTab)index;
	}

	private int TabToIndex(SettingsWindowTab tab)
	{
		return (int)tab;
	}

	public void Init(MainWindowViewModel main)
	{
		ViewModel = new SettingsWindowViewModel(this, main);
		Services.RegisterSingleton(ViewModel);

		var settingsFilePath = DivinityApp.GetAppDirectory("Data", "settings.json");
		var keybindingsFilePath = DivinityApp.GetAppDirectory("Data", "keybindings.json");

		GeneralSettingsTabHeader.Tag = settingsFilePath;
		AdvancedSettingsTabHeader.Tag = settingsFilePath;
		KeybindingsTabHeader.Tag = keybindingsFilePath;

		Observable.FromEventPattern<DependencyPropertyChangedEventHandler, DependencyPropertyChangedEventArgs>(
		  handler => AlertBar.grdWrapper.IsVisibleChanged += handler,
		  handler => AlertBar.grdWrapper.IsVisibleChanged -= handler)
		.Select(x => (bool)x.EventArgs.NewValue)
		.ObserveOn(RxApp.MainThreadScheduler)
		.BindTo(ViewModel, x => x.IsAlertActive);

		this.OneWayBind(ViewModel, vm => vm.ExtenderSettingsFilePath, view => view.ScriptExtenderTabHeader.Tag);
		this.OneWayBind(ViewModel, vm => vm.ExtenderUpdaterSettingsFilePath, view => view.UpdaterTabHeader.Tag);

		this.KeyDown += SettingsWindow_KeyDown;
		KeybindingsListView.Loaded += (o, e) =>
		{
			if (KeybindingsListView.SelectedIndex < 0)
			{
				KeybindingsListView.SelectedIndex = 0;
			}
			ListViewItem row = (ListViewItem)KeybindingsListView.ItemContainerGenerator.ContainerFromIndex(KeybindingsListView.SelectedIndex);
			if (row != null && !FocusHelper.HasKeyboardFocus(row))
			{
				Keyboard.Focus(row);
			}
		};
		KeybindingsListView.KeyUp += KeybindingsListView_KeyUp;

		CreateSettingsElements(ViewModel.Settings, typeof(DivinityModManagerSettings), SettingsAutoGrid);
		CreateSettingsElements(ViewModel.ExtenderSettings, typeof(ScriptExtenderSettings), ExtenderSettingsAutoGrid);
		CreateSettingsElements(ViewModel.ExtenderUpdaterSettings, typeof(ScriptExtenderUpdateConfig), ExtenderUpdaterSettingsAutoGrid);

		_keybindingsView = CollectionViewSource.GetDefaultView(ViewModel.Main.Keys.All);
		_keybindingsView.GroupDescriptions.Clear();
		_keybindingsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Hotkey.Category)));
		_keybindingsView.Filter = FilterHotkey;
		KeybindingsListView.ItemsSource = _keybindingsView;
		this.Bind(ViewModel, vm => vm.SelectedHotkey, view => view.KeybindingsListView.SelectedItem);

		this.Bind(ViewModel, vm => vm.Settings.DebugModeEnabled, view => view.DebugModeCheckBox.IsChecked);
		this.Bind(ViewModel, vm => vm.Settings.LogEnabled, view => view.LogEnabledCheckBox.IsChecked);
		this.Bind(ViewModel, vm => vm.Settings.ColorTheme, view => view.ThemeComboBox.SelectedValue);
		this.Bind(ViewModel, vm => vm.Settings.TextSize, view => view.TextSizeComboBox.SelectedValue);
		RefreshTypographyChoices();
		ViewModel.Settings.WhenAnyValue(settings => settings.ActiveCustomThemeId)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => RefreshCustomThemeControls());
		RefreshCustomThemeControls();

		this.OneWayBind(ViewModel, vm => vm.LaunchParams, view => view.GameLaunchParamsMainMenu.ItemsSource);
		GameLaunchParamsMainButton.Events().Click.Subscribe(e =>
		{
			var menu = GameLaunchParamsMainButton.ContextMenu;
			menu.PlacementTarget = GameLaunchParamsMainButton;
			menu.Placement = PlacementMode.Bottom;
			menu.IsOpen = true;
		});

		this.Bind(ViewModel, vm => vm.Settings.GameLaunchParams, view => view.GameLaunchParamsTextBox.Text);

		this.Bind(ViewModel, vm => vm.ExtenderUpdaterSettings.UpdateChannel, view => view.UpdateChannelComboBox.SelectedValue);
		this.OneWayBind(ViewModel, vm => vm.ScriptExtenderUpdates, view => view.UpdaterTargetVersionComboBox.ItemsSource);
		this.OneWayBind(ViewModel, vm => vm.TargetVersion, view => view.UpdaterTargetVersionComboBox.Tag);
		this.Bind(ViewModel, vm => vm.TargetVersion, view => view.UpdaterTargetVersionComboBox.SelectedItem);
		this.Bind(ViewModel, vm => vm.TargetVersionIndex, view => view.UpdaterTargetVersionComboBox.SelectedIndex);

		this.Bind(ViewModel, vm => vm.SelectedTabIndex, view => view.PreferencesTabControl.SelectedIndex, TabToIndex, IndexToTab);
		this.OneWayBind(ViewModel, vm => vm.ExtenderUpdaterVisibility, view => view.ScriptExtenderUpdaterTab.Visibility);
		this.OneWayBind(ViewModel, vm => vm.ResetSettingsCommandToolTip, view => view.ResetSettingsButton.ToolTip);

		this.BindCommand(ViewModel, vm => vm.SaveSettingsCommand, view => view.SaveSettingsButton);
		this.BindCommand(ViewModel, vm => vm.OpenSettingsFolderCommand, view => view.OpenSettingsFolderButton);
		this.BindCommand(ViewModel, vm => vm.ResetSettingsCommand, view => view.ResetSettingsButton);
		this.BindCommand(ViewModel, vm => vm.ClearLaunchParamsCommand, view => view.ClearLaunchParamsMenuItem);
		this.BindCommand(ViewModel, vm => vm.ClearCacheCommand, view => view.ClearCacheButton);
		this.BindCommand(ViewModel, vm => vm.ResetSourceCacheCommand, view => view.ResetSourceCacheButton);
		this.BindCommand(ViewModel, vm => vm.RestoreAutomaticCategoriesCommand, view => view.RestoreAutomaticCategoriesButton);
		this.BindCommand(ViewModel, vm => vm.ClearSourceHistoryCommand, view => view.ClearSourceHistoryButton);

		this.Events().IsVisibleChanged.InvokeCommand(ViewModel.OnWindowShownCommand);

		DataContext = ViewModel;
	}

	private bool isSettingKeybinding = false;

	private bool FilterHotkey(object item)
	{
		if (item is not Hotkey hotkey)
			return false;

		var query = KeybindingsSearchTextBox?.Text?.Trim();
		if (String.IsNullOrWhiteSpace(query))
			return true;

		return hotkey.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
			hotkey.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
			hotkey.DisplayBindingText.Contains(query, StringComparison.OrdinalIgnoreCase);
	}

	private void KeybindingsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		_keybindingsView?.Refresh();
		if (KeybindingsListView.Items.Count > 0 && KeybindingsListView.SelectedIndex < 0)
			KeybindingsListView.SelectedIndex = 0;
	}

	private void ClearFocus()
	{
		foreach (var item in KeybindingsListView.Items)
		{
			if (item is HotkeyEditorControl hotkey && hotkey.IsEditing)
			{
				hotkey.SetEditing(false);
			}
		}
	}

	private void FocusSelectedHotkey()
	{
		ListViewItem row = (ListViewItem)KeybindingsListView.ItemContainerGenerator.ContainerFromIndex(KeybindingsListView.SelectedIndex);
		var hotkeyControls = row.FindVisualChildren<HotkeyEditorControl>();
		foreach (var c in hotkeyControls)
		{
			c.SetEditing(true);
			isSettingKeybinding = true;
		}
	}

	private void KeybindingsListView_KeyUp(object sender, KeyEventArgs e)
	{
		if (KeybindingsListView.SelectedIndex >= 0 && e.Key == Key.Enter)
		{
			FocusSelectedHotkey();
		}
	}

	private void SettingsWindow_KeyDown(object sender, KeyEventArgs e)
	{
		if (isSettingKeybinding)
		{
			return;
		}
		else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
		{
			ViewModel.SaveSettingsCommand.Execute(null);
			e.Handled = true;
		}
		else if (e.Key == Key.Left && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
		{
			int current = PreferencesTabControl.SelectedIndex;
			int nextIndex = current - 1;
			if (nextIndex < 0)
			{
				nextIndex = PreferencesTabControl.Items.Count - 1;
			}
			PreferencesTabControl.SelectedIndex = nextIndex;
			Keyboard.Focus((FrameworkElement)PreferencesTabControl.SelectedContent);
			MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}
		else if (e.Key == Key.Right && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
		{
			int current = PreferencesTabControl.SelectedIndex;
			int nextIndex = current + 1;
			if (nextIndex >= PreferencesTabControl.Items.Count)
			{
				nextIndex = 0;
			}
			PreferencesTabControl.SelectedIndex = nextIndex;
			//MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}
	}

	private void HotkeyEditorControl_GotFocus(object sender, RoutedEventArgs e)
	{
		isSettingKeybinding = true;
	}

	private void HotkeyEditorControl_LostFocus(object sender, RoutedEventArgs e)
	{
		isSettingKeybinding = false;
	}

	private void HotkeyListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		FocusSelectedHotkey();
	}
}
