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

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : SettingsWindowBase
{
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
			ThemeComboBox.SelectedValue = theme;
		}
	}

	private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ThemeComboBox.SelectedValue is not ReduxThemeType theme) return;

		ReduxDarkThemeCard.IsChecked = theme == ReduxThemeType.ReduxDark;
		ReduxLightThemeCard.IsChecked = theme == ReduxThemeType.ReduxLight;
		ParchmentThemeCard.IsChecked = theme == ReduxThemeType.Parchment;
	}

	private void CreateSettingsElements(ReactiveObject source, Type settingsModelType, AutoGrid targetGrid)
	{
		var sorter = new SortSettings();
		var props = settingsModelType.GetProperties()
			.Select(SettingsAttributeProperty.FromProperty)
			.Where(x => x.Attribute != null && !x.Attribute.HideFromUI)
			.OrderBy(x => x, sorter).ToList();

		int count = props.Count + targetGrid.Children.Count + 1;
		int row = targetGrid.Children.Count;

		var enumDataTemplate = FindResource("EnumEntryTemplate") as DataTemplate;

		targetGrid.RowCount = count;
		targetGrid.Rows = String.Join(",", Enumerable.Repeat("auto", count));

		var debugModeBinding = new Binding(nameof(SettingsWindowViewModel.DeveloperModeVisibility))
		{
			Source = ViewModel,
			FallbackValue = Visibility.Collapsed
		};

		foreach (var prop in props)
		{
			var isBlankTooltip = String.IsNullOrEmpty(prop.Attribute.Tooltip);
			var targetRow = row;
			row++;
			var tb = new TextBlock
			{
				Text = prop.Attribute.DisplayName,
				ToolTip = !isBlankTooltip ? prop.Attribute.Tooltip : null,
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
