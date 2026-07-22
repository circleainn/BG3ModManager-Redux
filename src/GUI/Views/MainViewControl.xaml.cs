using AdonisUI;



using DivinityModManager.Controls;
using DivinityModManager.Converters;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Util;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.ViewModels;

using System.Data;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DivinityModManager.Views;

public class MainViewControlViewBase : ReactiveUserControl<MainWindowViewModel> { }

public partial class MainViewControl : MainViewControlViewBase
{
	private readonly MainWindow main;

	private readonly Dictionary<string, MenuItem> menuItems = new();
	public Dictionary<string, MenuItem> MenuItems => menuItems;
	private static readonly IReadOnlyDictionary<string, (string Resource, bool UseStroke, string Foreground)> MenuIconMap =
		new Dictionary<string, (string, bool, string)>
		{
			[nameof(AppKeys.ImportMod)] = ("Redux.Icon.AddCircle", true, null),
			[nameof(AppKeys.NewOrder)] = ("Redux.Icon.DocumentText", true, null),
			[nameof(AppKeys.Save)] = ("Redux.Icon.Save", true, null),
			[nameof(AppKeys.SaveAs)] = ("Redux.Icon.Duplicate", true, null),
			[nameof(AppKeys.ImportOrderFromSave)] = ("Redux.Icon.FolderOpen", true, null),
			[nameof(AppKeys.ImportOrderFromSaveAsNew)] = ("Redux.Icon.AddCircle", true, null),
			[nameof(AppKeys.ImportOrderFromFile)] = ("Redux.Icon.FolderOpen", true, null),
			[nameof(AppKeys.ImportOrderFromZipFile)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ExportOrderToGame)] = ("Redux.Icon.GameController", true, null),
			[nameof(AppKeys.ExportOrderToList)] = ("Redux.Icon.DocumentText", true, null),
			[nameof(AppKeys.ExportOrderToZip)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ExportOrderToArchiveAs)] = ("Redux.Icon.Duplicate", true, null),
			[nameof(AppKeys.Refresh)] = ("Redux.Icon.RefreshStroke", true, null),
			[nameof(AppKeys.Confirm)] = ("Redux.Icon.SwapHorizontalStroke", true, null),
			[nameof(AppKeys.MoveFocusLeft)] = ("Redux.Icon.ArrowBackStroke", true, null),
			[nameof(AppKeys.MoveFocusRight)] = ("Redux.Icon.ArrowForwardStroke", true, null),
			[nameof(AppKeys.SwapListFocus)] = ("Redux.Icon.SwapHorizontalStroke", true, null),
			[nameof(AppKeys.MoveToTop)] = ("Redux.Icon.ChevronUpStroke", true, null),
			[nameof(AppKeys.MoveToBottom)] = ("Redux.Icon.ChevronDownStroke", true, null),
			[nameof(AppKeys.ToggleFilterFocus)] = ("Redux.Icon.Funnel", true, null),
			[nameof(AppKeys.DeleteSelectedMods)] = ("Redux.Icon.Trash", true, "ReduxErrorBrush"),
			[nameof(AppKeys.OpenPreferences)] = ("Redux.Icon.Settings", true, null),
			[nameof(AppKeys.OpenKeybindings)] = ("Redux.Icon.Key", true, null),
			[nameof(AppKeys.ToggleViewTheme)] = ("Redux.Icon.ColorPalette", true, null),
			[nameof(AppKeys.ExtractSelectedMods)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ExtractSelectedAdventure)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ToggleVersionGeneratorWindow)] = ("Redux.Icon.Build", true, null),
			[nameof(AppKeys.DownloadScriptExtender)] = ("Redux.Icon.Download", true, null),
			[nameof(AppKeys.SpeakActiveModOrder)] = ("Redux.Icon.VolumeHigh", true, null),
			[nameof(AppKeys.StopSpeaking)] = ("Redux.Icon.StopCircle", true, "ReduxErrorBrush"),
			[nameof(AppKeys.CheckForUpdates)] = ("Redux.Icon.Download", true, null),
			[nameof(AppKeys.OpenAboutWindow)] = ("Redux.Icon.Information", true, null)
		};

	private void QuickLinksButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button button && button.ContextMenu != null)
		{
			button.ContextMenu.PlacementTarget = button;
			button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			button.ContextMenu.IsOpen = true;
		}
	}

	private void OpenBg3Nexus_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_BG3_NEXUS);
	private void OpenScriptExtenderRepo_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_EXTENDER_REPO);
	private void OpenReduxRepo_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_REPO);

	private void OpenModsFolderQuickLink_Click(object sender, RoutedEventArgs e)
	{
		ProcessHelper.TryOpenPath(ViewModel.PathwayData.AppDataModsPath, Directory.Exists);
	}

	private void OpenSaveGamesFolder_Click(object sender, RoutedEventArgs e)
	{
		var saveGamesPath = ViewModel.SelectedProfile?.Folder == null
			? null
			: Path.Combine(ViewModel.SelectedProfile.Folder, "Savegames", "Story");
		if (!String.IsNullOrWhiteSpace(saveGamesPath) && Directory.Exists(saveGamesPath))
		{
			ProcessHelper.TryOpenPath(saveGamesPath, Directory.Exists);
		}
		else
		{
			ViewModel.ShowAlert("The selected profile's save games folder could not be found.", AlertType.Warning);
		}
	}

	private void RegisterKeyBindings()
	{
		foreach (var key in ViewModel.Keys.All)
		{
			var keyBinding = new KeyBinding(key.Command, key.Key, key.Modifiers);
			BindingOperations.SetBinding(keyBinding, InputBinding.CommandProperty, new Binding { Path = new PropertyPath("Command"), Source = key });
			BindingOperations.SetBinding(keyBinding, KeyBinding.KeyProperty, new Binding { Path = new PropertyPath("Key"), Source = key });
			BindingOperations.SetBinding(keyBinding, KeyBinding.ModifiersProperty, new Binding { Path = new PropertyPath("Modifiers"), Source = key });
			main.InputBindings.Add(keyBinding);
		}

		//Initial keyboard focus by hitting up or down
		var setInitialFocusCommand = ReactiveCommand.Create(() =>
		{
			if (!DivinityApp.IsKeyboardNavigating && this.ViewModel.ActiveSelected == 0 && this.ViewModel.InactiveSelected == 0)
			{
				ModLayout.FocusInitialActiveSelected();
			}
		});
		main.InputBindings.Add(new KeyBinding(setInitialFocusCommand, Key.Up, ModifierKeys.None));
		main.InputBindings.Add(new KeyBinding(setInitialFocusCommand, Key.Down, ModifierKeys.None));

		foreach (var item in TopMenuBar.Items)
		{
			if (item is MenuItem entry)
			{
				if (entry.Header is string label)
				{
					menuItems.Add(label, entry);
				}
				else if (!String.IsNullOrWhiteSpace(entry.Name))
				{
					menuItems.Add(entry.Name, entry);
				}
			}
		}

		//Generating menu items
		var menuKeyProperties = typeof(AppKeys)
		.GetRuntimeProperties()
		.Where(prop => Attribute.IsDefined(prop, typeof(MenuSettingsAttribute)))
		.Select(prop => typeof(AppKeys).GetProperty(prop.Name));
		foreach (var prop in menuKeyProperties)
		{
			Hotkey key = (Hotkey)prop.GetValue(ViewModel.Keys);
			MenuSettingsAttribute menuSettings = prop.GetCustomAttribute<MenuSettingsAttribute>();
			if (String.IsNullOrEmpty(key.DisplayName))
				key.DisplayName = menuSettings.DisplayName;

			// Redux consolidates folder navigation into Quick Links. Donation/project
			// destinations live under Credits and Quick Links, while their hotkeys remain active.
			if (menuSettings.Parent.Equals("Go", StringComparison.OrdinalIgnoreCase) ||
				prop.Name == nameof(AppKeys.OpenDonationLink) ||
				prop.Name == nameof(AppKeys.OpenRepositoryPage))
			{
				continue;
			}

			if (!menuItems.TryGetValue(menuSettings.Parent, out MenuItem parentMenuItem))
			{
				parentMenuItem = new MenuItem
				{
					Header = menuSettings.Parent
				};
				TopMenuBar.Items.Add(parentMenuItem);
				menuItems.Add(menuSettings.Parent, parentMenuItem);
			}

			MenuItem newEntry = new MenuItem
			{
				Header = menuSettings.DisplayName,
				InputGestureText = key.ToString(),
				Command = key.Command
			};
			if (MenuIconMap.TryGetValue(prop.Name, out var iconSpec))
			{
				newEntry.Icon = ReduxIcon.FromResource(iconSpec.Resource, iconSpec.UseStroke, iconSpec.Foreground);
			}
			if(key == ViewModel.Keys.DownloadScriptExtender && TryFindResource("MenuItemHightlightBlink") is Style blinKStyle)
			{
				newEntry.Style = blinKStyle;
			}
			BindingOperations.SetBinding(newEntry, MenuItem.CommandProperty, new Binding { Path = new PropertyPath("Command"), Source = key });
			parentMenuItem.Items.Add(newEntry);
			if (!String.IsNullOrWhiteSpace(menuSettings.Tooltip))
			{
				newEntry.ToolTip = menuSettings.Tooltip;
			}
			if (!String.IsNullOrWhiteSpace(menuSettings.Style))
			{
				Style style = (Style)TryFindResource(menuSettings.Style);
				if (style != null)
				{
					newEntry.Style = style;
				}
			}

			if (menuSettings.AddSeparator)
			{
				parentMenuItem.Items.Add(new Separator());
			}

			menuItems.Add(prop.Name, newEntry);
		}

		if (menuItems.TryGetValue("Accessibility", out var accessibilityMenuItem))
		{
			var keyboardShortcutsItem = new MenuItem
			{
				Header = "Keyboard Shortcuts...",
				Command = ViewModel.Keys.OpenKeybindings.Command,
				ToolTip = "Open Preferences to customize keyboard shortcuts.",
				Icon = ReduxIcon.FromResource("Redux.Icon.Key", true)
			};

			accessibilityMenuItem.Items.Add(new Separator());
			accessibilityMenuItem.Items.Add(keyboardShortcutsItem);
		}

		// Keep attribution available without dedicating a second top-level menu to it.
		if (menuItems.TryGetValue("Help", out var helpMenuItem))
		{
			helpMenuItem.Items.Add(new Separator());
			var reportBugMenuItem = new MenuItem
			{
				Header = "Report a Bug...",
				ToolTip = "Open the BG3 Mod Manager Redux bug report form on GitHub",
				Icon = ReduxIcon.FromResource("Redux.Icon.Bug", true)
			};
			reportBugMenuItem.Click += (_, _) => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_BUG_REPORT);
			helpMenuItem.Items.Add(reportBugMenuItem);
			helpMenuItem.Items.Add(new Separator());
			var creditsMenu = new MenuItem
			{
				Header = "Credits & Attribution",
				Icon = ReduxIcon.FromResource("Redux.Icon.Information", true)
			};
			creditsMenu.Items.Add(new MenuItem
			{
				Header = "Original BG3 Mod Manager on GitHub",
				Command = ViewModel.Keys.OpenRepositoryPage.Command,
				Icon = ReduxIcon.FromResource("Redux.Icon.Github", foregroundResourceKey: "ReduxGithubIconBrush")
			});
			creditsMenu.Items.Add(new MenuItem
			{
				Header = "Support LaughingLeader on Ko-fi",
				Command = ViewModel.Keys.OpenDonationLink.Command,
				Icon = ReduxIcon.FromResource("Redux.Icon.Heart", true)
			});
			helpMenuItem.Items.Add(creditsMenu);
		}
	}

	protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
	{
		return new CachedAutomationPeer(this);
	}

	public void UpdateColorTheme(ReduxThemeType theme)
	{
		var customTheme = ReduxThemeService.GetActiveTheme(ViewModel.Settings);
		ReduxThemeService.Apply(this.Resources, theme, customTheme);
		main.UpdateColorTheme(theme, customTheme);
	}

	public void PreviewCustomTheme(ReduxCustomTheme theme)
	{
		var baseTheme = theme?.BaseTheme ?? ViewModel.Settings.ColorTheme;
		ReduxThemeService.Apply(this.Resources, baseTheme, theme);
		main.UpdateColorTheme(baseTheme, theme);
	}

	private void ComboBox_KeyDown_LoseFocus(object sender, KeyEventArgs e)
	{
		bool loseFocus = false;
		if ((e.Key == Key.Enter || e.Key == Key.Return))
		{
			UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
			elementWithFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			ViewModel.StopRenaming(false);
			loseFocus = true;
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			ViewModel.StopRenaming(true);
			loseFocus = true;
		}

		if (loseFocus && sender is ComboBox comboBox)
		{
			var tb = comboBox.FindVisualChildren<TextBox>().FirstOrDefault();
			tb?.Select(0, 0);
		}
	}

	private void OrdersComboBox_LostFocus(object sender, RoutedEventArgs e)
	{
		if (sender is ComboBox comboBox && ViewModel.IsRenamingOrder)
		{
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(250), _ =>
			{
				var tb = comboBox.FindVisualChildren<TextBox>().FirstOrDefault();
				if (tb != null && !tb.IsFocused)
				{
					var cancel = string.IsNullOrEmpty(tb.Text);
					ViewModel.StopRenaming(cancel);
					if (!cancel)
					{
						var nextName = tb.Text;
						var order = ViewModel.SelectedModOrder;
						var lastFilePath = order.FilePath;
						var directory = Path.GetDirectoryName(lastFilePath);
						var ext = Path.GetExtension(lastFilePath);
						var nextFilePath = Path.Combine(directory, DivinityModDataLoader.MakeSafeFilename(Path.Combine(nextName + ext), '_'));
						try
						{
							if (File.Exists(nextFilePath))
							{
								var result = ReduxMessageBox.Show(main,
									$"Overwrite '{nextFilePath}'?",
									"Confirm Order Renaming (Overwriting File)",
									MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.OK);
								if (result == MessageBoxResult.No)
								{
									AlertBar.SetInformationAlert($"Cancelled order renaming", 10);
									return;
								}
							}
							File.Move(lastFilePath, nextFilePath, true);
							var existingOrder = ViewModel.ModOrderList.FirstOrDefault(x => x.FilePath == nextFilePath);
							if (existingOrder != null)
							{
								ViewModel.ModOrderList.Remove(existingOrder);
							}
							order.Name = nextName;
							order.FilePath = nextFilePath;
							AlertBar.SetSuccessAlert($"Renamed load order name/path to '{nextFilePath}'", 20);
						}
						catch (Exception ex)
						{
							AlertBar.SetDangerAlert($"Failed to rename file '{lastFilePath}' to '{nextFilePath}'", 20);
							var message = $"Failed to rename file '{lastFilePath}' to '{nextFilePath}':\n{ex}";
							ReduxMessageBox.ShowWithActions(main, message, "Failed to Rename Order",
								MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
								("Copy to Clipboard", () => ((System.Windows.Input.ICommand)DivinityApp.Commands.CopyToClipboardCommand).Execute(message)));
						}
					}
				}
			});
		}
	}

	private void OrderComboBox_OnUserClick(object sender, MouseButtonEventArgs e)
	{
		RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(200), () =>
		{
			if (ViewModel.Settings != null && ViewModel.Settings.LastOrder != ViewModel.SelectedModOrder.Name)
			{
				ViewModel.Settings.LastOrder = ViewModel.SelectedModOrder.Name;
				ViewModel.SaveSettings();
			}
		});
	}

	private void OrdersComboBox_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is ComboBox ordersComboBox)
		{
			var tb = ordersComboBox.FindVisualChildren<TextBox>().FirstOrDefault();
			if (tb != null)
			{
				tb.ContextMenu = ordersComboBox.ContextMenu;
				tb.ContextMenu.DataContext = ViewModel;
			}
		}
	}

	private readonly Dictionary<string, string> _shortcutButtonBindings = new()
	{
		["OpenModsFolderButton"] = "Keys.OpenModsFolder.Command",
		["OpenExtenderLogsFolderButton"] = "Keys.OpenLogsFolder.Command",
		["OpenGameButton"] = "Keys.LaunchGame.Command"
	};

	private void ModOrderPanel_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is Grid orderPanel)
		{
			var buttons = orderPanel.FindVisualChildren<Button>();
			foreach (var button in buttons)
			{
				if (_shortcutButtonBindings.TryGetValue(button.Name, out string path))
				{
					if (button.Command == null)
					{
						BindingHelper.CreateCommandBinding(button, path, ViewModel);
					}
				}
			}
		};
	}

	public void OnActivated()
	{
		this.WhenAnyValue(x => x.ViewModel.MainProgressIsActive).Take(1).Delay(TimeSpan.FromMilliseconds(25)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(b =>
		{
			this.MainBusyIndicator.Visibility = Visibility.Visible;
		});
		this.OneWayBind(ViewModel, vm => vm.HideModList, view => view.ModListRectangle.Visibility, BoolToVisibilityConverter.FromBool);
		this.OneWayBind(ViewModel, vm => vm.MainProgressIsActive, view => view.MainBusyIndicator.IsBusy);

		//this.OneWayBind(ViewModel, vm => vm, view => view.ModLayout.ViewModel);
		this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.ModLayout.ViewModel);

		this.OneWayBind(ViewModel, vm => vm.StatusBarRightText, view => view.StatusBarLoadingOperationTextBlock.Text);

		this.OneWayBind(ViewModel, vm => vm.ModUpdatesAvailable, view => view.UpdatesButtonPanel.IsEnabled);

		this.OneWayBind(ViewModel, vm => vm.UpdatingBusyIndicatorVisibility, view => view.UpdatesToggleButtonBusyIndicator.Visibility);
		this.OneWayBind(ViewModel, vm => vm.UpdatesViewVisibility, view => view.UpdatesToggleButtonExpandImage.Visibility);
		this.OneWayBind(ViewModel, vm => vm.UpdateCountVisibility, view => view.UpdateCountTextBlock.Visibility);
		this.OneWayBind(ViewModel, vm => vm.ModUpdatesViewData.TotalUpdates, view => view.UpdateCountTextBlock.Text);

		this.OneWayBind(ViewModel, vm => vm.ModOrderList, view => view.OrdersComboBox.ItemsSource);
		this.Bind(ViewModel, vm => vm.SelectedModOrderIndex, view => view.OrdersComboBox.SelectedIndex);
		this.OneWayBind(ViewModel, vm => vm.IsRenamingOrder, view => view.OrdersComboBox.IsEditable);
		this.OneWayBind(ViewModel, vm => vm.SelectedModOrderName, view => view.OrdersComboBox.Text);
		this.OneWayBind(ViewModel, vm => vm, view => view.OrdersComboBox.Tag);

		this.OneWayBind(ViewModel, vm => vm.Profiles, view => view.ProfilesComboBox.ItemsSource);
		this.Bind(ViewModel, vm => vm.SelectedProfileIndex, view => view.ProfilesComboBox.SelectedIndex);
		this.OneWayBind(ViewModel, vm => vm, view => view.ProfilesComboBox.Tag);

		this.OneWayBind(ViewModel, vm => vm.AdventureMods, view => view.AdventureModComboBox.ItemsSource);
		this.Bind(ViewModel, vm => vm.SelectedAdventureModIndex, view => view.AdventureModComboBox.SelectedIndex);
		this.OneWayBind(ViewModel, vm => vm.SelectedAdventureMod, view => view.AdventureModComboBox.Tag);

		this.BindCommand(ViewModel, vm => vm.ToggleUpdatesViewCommand, view => view.UpdateViewToggleButton);

		this.BindCommand(ViewModel, vm => vm.Keys.ImportMod.Command, view => view.ImportModButton);
		this.BindCommand(ViewModel, vm => vm.Keys.Save.Command, view => view.SaveButton);
		this.BindCommand(ViewModel, vm => vm.Keys.SaveAs.Command, view => view.SaveAsButton);
		this.BindCommand(ViewModel, vm => vm.Keys.NewOrder.Command, view => view.AddNewOrderButton);
		this.BindCommand(ViewModel, vm => vm.Keys.ExportOrderToGame.Command, view => view.ExportToModSettingsButton);
		this.BindCommand(ViewModel, vm => vm.Keys.ExportOrderToZip.Command, view => view.ExportOrderToArchiveButton);
		this.BindCommand(ViewModel, vm => vm.Keys.ExportOrderToArchiveAs.Command, view => view.ExportOrderToArchiveAsButton);
		this.BindCommand(ViewModel, vm => vm.Keys.Refresh.Command, view => view.RefreshButton);
		this.BindCommand(ViewModel, vm => vm.Keys.OpenModsFolder.Command, view => view.OpenModsFolderButton);
		this.BindCommand(ViewModel, vm => vm.Keys.OpenLogsFolder.Command, view => view.OpenExtenderLogsFolderButton);
		this.BindCommand(ViewModel, vm => vm.Keys.LaunchGame.Command, view => view.OpenGameButton);
		this.BindCommand(ViewModel, vm => vm.Keys.OpenDonationLink.Command, view => view.OpenDonationPageButton);
		this.BindCommand(ViewModel, vm => vm.Keys.OpenRepositoryPage.Command, view => view.OpenRepoPageButton);
		this.OneWayBind(ViewModel, vm => vm.LogFolderShortcutButtonVisibility, view => view.OpenExtenderLogsFolderButton.Visibility);

		this.Bind(ViewModel, vm => vm.Settings.ActionOnGameLaunch, view => view.GameLaunchActionComboBox.SelectedValue);

		this.OneWayBind(ViewModel, vm => vm.UpdatesViewVisibility, view => view.ModUpdaterPanel.Visibility);
		var whenUpdatesViewData = ViewModel.WhenAnyValue(x => x.ModUpdatesViewData);
		whenUpdatesViewData.BindTo(this, x => x.ModUpdaterPanel.ViewModel);
		whenUpdatesViewData.BindTo(this, x => x.ModUpdaterPanel.DataContext);
		//this.OneWayBind(ViewModel, vm => vm.ModUpdatesViewData, view => view.ModUpdaterPanel.ViewModel);

		RegisterKeyBindings();

		this.DeleteFilesView.ViewModel.FileDeletionComplete += (o, e) =>
		{
			DivinityApp.Log($"Deleted {e.TotalFilesDeleted} file(s).");
			if (e.TotalFilesDeleted > 0)
			{
				if (!e.IsDeletingDuplicates)
				{
					var deletedUUIDs = e.DeletedFiles.Select(x => x.UUID).ToHashSet();
					ViewModel.RemoveDeletedMods(deletedUUIDs, e.RemoveFromLoadOrder);
				}
				main.Activate();
			}
			if (e.FailureMessages.Count > 0)
			{
				var firstFailure = e.FailureMessages[0];
				var additional = e.FailureMessages.Count > 1 ? $" (+{e.FailureMessages.Count - 1} more; see the log)" : String.Empty;
				ViewModel.ShowAlert($"Could not delete {e.FailureMessages.Count} mod file(s). {firstFailure}{additional}", AlertType.Danger, 60);
				main.Activate();
			}
		};

		FocusManager.SetFocusedElement(this, ModOrderPanel);
	}

	public MainViewControl(MainWindow window, MainWindowViewModel vm)
	{
		InitializeComponent();

		main = window;
		ViewModel = vm;
	}
}
