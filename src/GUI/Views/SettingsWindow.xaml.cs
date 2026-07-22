using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.Extender;
using DivinityModManager.Models.View;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;

using DynamicData;

using ReactiveMarbles.ObservableEvents;

using Splat;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

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

internal sealed record SettingsGroup(string Title, params string[] PropertyNames);

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : SettingsWindowBase
{
	private bool _updatingCustomThemeSelection;
	private bool _updatingTypographySelection;
	private static readonly SettingsGroup[] GeneralSettingsGroups =
	[
		new("Paths and storage",
			nameof(DivinityModManagerSettings.GameExecutablePath),
			nameof(DivinityModManagerSettings.GameDataPath),
			nameof(DivinityModManagerSettings.DocumentsFolderPathOverride),
			nameof(DivinityModManagerSettings.LoadOrderPath)),
		new("Game launch",
			nameof(DivinityModManagerSettings.LaunchType),
			nameof(DivinityModManagerSettings.CustomLaunchAction),
			nameof(DivinityModManagerSettings.CustomLaunchArgs),
			nameof(DivinityModManagerSettings.LaunchDX11),
			nameof(DivinityModManagerSettings.ActionOnGameLaunch),
			nameof(DivinityModManagerSettings.DisableLauncherTelemetry),
			nameof(DivinityModManagerSettings.DisableLauncherModWarnings),
			nameof(DivinityModManagerSettings.GameStoryLogEnabled)),
		new("Mod-list workflow",
			nameof(DivinityModManagerSettings.AutoAddDependenciesWhenExporting),
			nameof(DivinityModManagerSettings.HideEmptyModCategories),
			nameof(DivinityModManagerSettings.ShiftListFocusOnSwap),
			nameof(DivinityModManagerSettings.SaveWindowLocation),
			nameof(DivinityModManagerSettings.EnableColorblindSupport)),
		new("Metadata services",
			nameof(DivinityModManagerSettings.NexusModsAPIKey),
			nameof(DivinityModManagerSettings.ModioAPIKey),
			nameof(DivinityModManagerSettings.HideModioSourceWarningIcons)),
		new("Warnings and maintenance",
			nameof(DivinityModManagerSettings.CheckForUpdates),
			nameof(DivinityModManagerSettings.DeleteModCrashSanityCheck),
			nameof(DivinityModManagerSettings.DisableMissingModWarnings),
			nameof(DivinityModManagerSettings.ResetModioSupportWarningAcknowledgement),
			nameof(DivinityModManagerSettings.ResetOfflineNexusDatabaseWarningAcknowledgement),
			nameof(DivinityModManagerSettings.ResetReduxPreviewWarningAcknowledgement))
	];

	private static readonly SettingsGroup[] ExtenderSettingsGroups =
	[
		new("Core behavior",
			nameof(ScriptExtenderSettings.CustomProfile),
			nameof(ScriptExtenderSettings.DisableModValidation),
			nameof(ScriptExtenderSettings.InsanityCheck),
			nameof(ScriptExtenderSettings.EnableAchievements),
			nameof(ScriptExtenderSettings.SendCrashReports),
			nameof(ScriptExtenderSettings.ExportDefaultExtenderSettings)),
		new("Logging",
			nameof(ScriptExtenderSettings.LogDirectory),
			nameof(ScriptExtenderSettings.CreateConsole),
			nameof(ScriptExtenderSettings.EnableLogging),
			nameof(ScriptExtenderSettings.LogRuntime),
			nameof(ScriptExtenderSettings.LogCompile),
			nameof(ScriptExtenderSettings.LogFailedCompile)),
		new("Developer and diagnostics",
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

	public SettingsWindow()
	{
		InitializeComponent();
	}

	/*private static readonly MethodInfo m_ItemInfoFromIndex = typeof(ItemsControl).GetMethod("ItemInfoFromIndex", BindingFlags.Instance | BindingFlags.NonPublic);

	private void SetComboBoxToolTips(object sender, EventArgs e)
	{
		if(sender is ComboBox combo)
		{
			combo.DropDownOpened -= SetComboBoxToolTips;
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () =>
			{
				for (var i = 0; i < combo.Items.Count; i++)
				{
					var data = combo.Items.GetItemAt(i) as EnumEntry;
					var item = m_ItemInfoFromIndex.Invoke(combo, [i]);
					if (item != null)
					{
						var itemType = item.GetType();
						var fieldInfo = itemType.GetProperty("Container", BindingFlags.NonPublic | BindingFlags.Instance);
						var itemContainer = fieldInfo.GetMethod?.Invoke(item, []);
						if (itemContainer is ComboBoxItem cbItem && !string.IsNullOrEmpty(data.Description))
						{
							ToolTipService.SetToolTip(cbItem, data.Description);
						}
					}
				}
			});
		}
	}*/

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
			Title = "Import Redux Font",
			Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf|TrueType font (*.ttf)|*.ttf|OpenType font (*.otf)|*.otf",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true) return;
		if (!ReduxCustomFontService.TryImport(dialog.FileName, out var choice, out var error))
		{
			Xceed.Wpf.Toolkit.MessageBox.Show(this, error, "Import Font", MessageBoxButton.OK,
				MessageBoxImage.Error, MessageBoxResult.OK, MainWindow.Self.MessageBoxStyle);
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
		var result = Xceed.Wpf.Toolkit.MessageBox.Show(this,
			$"Remove '{choice.Name}' from Redux?{usageNote}", "Remove Custom Font",
			MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, MainWindow.Self.MessageBoxStyle);
		if (result != MessageBoxResult.Yes) return;
		if (!ReduxCustomFontService.TryDelete(choice.CustomReference, out var error))
		{
			Xceed.Wpf.Toolkit.MessageBox.Show(this, error, "Remove Custom Font", MessageBoxButton.OK,
				MessageBoxImage.Error, MessageBoxResult.OK, MainWindow.Self.MessageBoxStyle);
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
			Xceed.Wpf.Toolkit.MessageBox.Show(this, "Redux could not open the custom fonts folder.", "Custom Fonts",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MainWindow.Self.MessageBoxStyle);
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
		var dialog = new CustomThemeEditorWindow(workingTheme) { Owner = this };
		dialog.PreviewChanged += preview => MainWindow.Self.MainView.PreviewCustomTheme(preview);
		var accepted = dialog.ShowDialog() == true;
		if (!accepted)
		{
			MainWindow.Self.MainView.UpdateColorTheme(previousTheme);
			ReduxTypographyService.Apply(Application.Current.Resources, previousFont, previousCustomFont);
			ReduxTypographyService.ApplyTextSize(Application.Current.Resources, previousTextSize);
		}
		return accepted;
	}

	private void CreateCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var working = ReduxThemeService.CreateFromBase("My Custom Theme", ViewModel.Settings.ColorTheme,
			ViewModel.Settings.TypographyFont, ViewModel.Settings.TextSize, ViewModel.Settings.CustomTypographyFont);
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
		var result = Xceed.Wpf.Toolkit.MessageBox.Show(this,
			$"Delete the custom theme '{selected.Name}'?\n\nExport it first if you may want to use it again.",
			"Delete Custom Theme", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No,
			MainWindow.Self.MessageBoxStyle);
		if (result != MessageBoxResult.Yes) return;
		ViewModel.Settings.CustomThemes.Remove(selected);
		if (selected.Id.Equals(ViewModel.Settings.ActiveCustomThemeId, StringComparison.OrdinalIgnoreCase))
		{
			ViewModel.Settings.ActiveCustomThemeId = String.Empty;
			ViewModel.Settings.TypographyFont = ReduxTypographyFont.Manrope;
			ViewModel.Settings.CustomTypographyFont = String.Empty;
			ViewModel.Settings.TextSize = ReduxTextSize.Default;
			MainWindow.Self.MainView.UpdateColorTheme(ViewModel.Settings.ColorTheme);
		}
		ViewModel.Main.SaveSettings();
		RefreshCustomThemeControls();
	}

	private void ImportCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Import Redux Custom Theme",
			Filter = "Redux theme (*.json)|*.json|All files (*.*)|*.*",
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
			Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Could not import that theme.\n\n{ex.Message}",
				"Import Custom Theme", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
				MainWindow.Self.MessageBoxStyle);
		}
	}

	private void ExportCustomTheme_Click(object sender, RoutedEventArgs e)
	{
		var selected = SelectedCustomTheme;
		if (selected == null) return;
		var safeName = String.Concat(selected.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
		var dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = "Export Redux Custom Theme",
			Filter = "Redux theme (*.json)|*.json",
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
			Xceed.Wpf.Toolkit.MessageBox.Show(this, $"Could not export that theme.\n\n{ex.Message}",
				"Export Custom Theme", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
				MainWindow.Self.MessageBoxStyle);
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
				var heading = new TextBlock
				{
					Text = currentGroupTitle,
					Style = FindResource("SettingsSubsectionTitleStyle") as Style
				};
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
					//cb.HorizontalAlignment = HorizontalAlignment.Right;
					cb.SetBinding(CheckBox.IsCheckedProperty, new Binding(prop.Property.Name)
					{
						Source = source,
						Mode = BindingMode.TwoWay,
						UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
					});
					if (prop.Attribute.IsDebug)
					{
						cb.SetBinding(CheckBox.VisibilityProperty, debugModeBinding);
					}
					targetGrid.Children.Add(cb);
					Grid.SetRow(cb, targetRow);
					Grid.SetColumn(cb, 1);
					createdObject = cb;
					break;

				case TypeCode.String:
					var utb = new UnfocusableTextBox
					{
						ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Center,
						VerticalContentAlignment = VerticalAlignment.Center,
						//utb.HorizontalAlignment = HorizontalAlignment.Stretch;
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
		//main.WhenAnyValue(x => x.Settings).BindTo(ViewModel, vm => vm.Settings);

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

		this.OneWayBind(ViewModel, vm => vm.Main.Keys.All, view => view.KeybindingsListView.ItemsSource);
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
			this.GameLaunchParamsMainButton.ContextMenu.IsOpen = true;
		});

		this.Bind(ViewModel, vm => vm.Settings.GameLaunchParams, view => view.GameLaunchParamsTextBox.Text);

		this.Bind(ViewModel, vm => vm.ExtenderUpdaterSettings.UpdateChannel, view => view.UpdateChannelComboBox.SelectedValue);
		this.OneWayBind(ViewModel, vm => vm.ScriptExtenderUpdates, view => view.UpdaterTargetVersionComboBox.ItemsSource);
		this.OneWayBind(ViewModel, vm => vm.TargetVersion, view => view.UpdaterTargetVersionComboBox.Tag);
		this.Bind(ViewModel, vm => vm.TargetVersion, view => view.UpdaterTargetVersionComboBox.SelectedItem);
		this.Bind(ViewModel, vm => vm.TargetVersionIndex, view => view.UpdaterTargetVersionComboBox.SelectedIndex);

		//this.WhenAnyValue(x => x.UpdaterTargetVersionComboBox.SelectedItem).Subscribe(ViewModel.OnTargetVersionSelected);

		this.Bind(ViewModel, vm => vm.SelectedTabIndex, view => view.PreferencesTabControl.SelectedIndex, TabToIndex, IndexToTab);
		this.OneWayBind(ViewModel, vm => vm.ExtenderUpdaterVisibility, view => view.ScriptExtenderUpdaterTab.Visibility);
		this.OneWayBind(ViewModel, vm => vm.ResetSettingsCommandToolTip, view => view.ResetSettingsButton.ToolTip);

		this.BindCommand(ViewModel, vm => vm.SaveSettingsCommand, view => view.SaveSettingsButton);
		this.BindCommand(ViewModel, vm => vm.OpenSettingsFolderCommand, view => view.OpenSettingsFolderButton);
		this.BindCommand(ViewModel, vm => vm.ResetSettingsCommand, view => view.ResetSettingsButton);
		this.BindCommand(ViewModel, vm => vm.ClearLaunchParamsCommand, view => view.ClearLaunchParamsMenuItem);
		this.BindCommand(ViewModel, vm => vm.ClearCacheCommand, view => view.ClearCacheButton);

		this.Events().IsVisibleChanged.InvokeCommand(ViewModel.OnWindowShownCommand);

		DataContext = ViewModel;
	}

	private bool isSettingKeybinding = false;

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
			//Keyboard.Focus((FrameworkElement)PreferencesTabControl.SelectedContent);
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
