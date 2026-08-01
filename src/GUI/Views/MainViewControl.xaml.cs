using AdonisUI;



using DivinityModManager.AppServices;
using DivinityModManager.Controls;
using DivinityModManager.Converters;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Health;
using DivinityModManager.Models.View;
using DivinityModManager.Util;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.ViewModels;

using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DivinityModManager.Views;

public class MainViewControlViewBase : ReactiveUserControl<MainWindowViewModel> { }

public partial class MainViewControl : MainViewControlViewBase
{
	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out NativePoint point);

	private readonly MainWindow main;
	private IDisposable _toolbarVisibilitySubscription;
	private IDisposable _modDiagnosticsStatusSubscription;
	private double _toolbarExpandedHeight;
	private int _toolbarAnimationVersion;
	private int _diagnosticStatusHoverVersion;
	private readonly HashSet<ContextMenu> _closingToolbarStatusMenus = new();

	private readonly Dictionary<string, MenuItem> menuItems = new();
	public Dictionary<string, MenuItem> MenuItems => menuItems;
	private static readonly IReadOnlyDictionary<string, (string Resource, bool UseStroke, string Foreground)> MenuIconMap =
		new Dictionary<string, (string, bool, string)>
		{
			[nameof(AppKeys.ImportMod)] = ("Redux.Icon.AddCircle", true, null),
			[nameof(AppKeys.NewOrder)] = ("Redux.Icon.DocumentText", true, null),
			[nameof(AppKeys.Save)] = ("Redux.Icon.Save", true, null),
			[nameof(AppKeys.SaveAs)] = ("Redux.Icon.Duplicate", true, null),
			[nameof(AppKeys.CompareLoadOrders)] = ("Redux.Icon.SwapHorizontalStroke", true, null),
			[nameof(AppKeys.RestorePoints)] = ("Redux.Icon.ScrollText", true, null),
			[nameof(AppKeys.ImportOrderFromSave)] = ("Redux.Icon.FolderOpen", true, null),
			[nameof(AppKeys.ImportOrderFromSaveAsNew)] = ("Redux.Icon.AddCircle", true, null),
			[nameof(AppKeys.ImportOrderFromFile)] = ("Redux.Icon.FolderOpen", true, null),
			[nameof(AppKeys.ImportReduxLoadOrder)] = ("Redux.Icon.Download", true, null),
			[nameof(AppKeys.ImportOrderFromZipFile)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ExportOrderToGame)] = ("Redux.Icon.GameController", true, null),
			[nameof(AppKeys.ExportOrderToList)] = ("Redux.Icon.DocumentText", true, null),
			[nameof(AppKeys.ExportReduxLoadOrder)] = ("Redux.Icon.CloudUpload", true, null),
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
			[nameof(AppKeys.ToggleToolbar)] = ("Redux.Icon.Desktop", true, null),
			[nameof(AppKeys.ExtractSelectedMods)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ExtractSelectedAdventure)] = ("Redux.Icon.Archive", true, null),
			[nameof(AppKeys.ToggleVersionGeneratorWindow)] = ("Redux.Icon.Build", true, null),
			[nameof(AppKeys.InspectFileOverlaps)] = ("Redux.Icon.Blocks", true, null),
			[nameof(AppKeys.DownloadScriptExtender)] = ("Redux.Icon.Download", true, null),
			[nameof(AppKeys.SpeakActiveModOrder)] = ("Redux.Icon.VolumeHigh", true, null),
			[nameof(AppKeys.StopSpeaking)] = ("Redux.Icon.StopCircle", true, "ReduxErrorBrush"),
			[nameof(AppKeys.CheckForUpdates)] = ("Redux.Icon.Download", true, null),
			[nameof(AppKeys.OpenAboutWindow)] = ("Redux.Icon.Information", true, null)
		};

	private void OpenBg3Nexus_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_BG3_NEXUS);
	private void OpenScriptExtenderRepo_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_EXTENDER_REPO);
	private void OpenReduxNexus_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_NEXUS);
	private void OpenReduxRepo_Click(object sender, RoutedEventArgs e) => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_REPO);

	private static void UpdateCheckedMenuItems(MenuItem parent, Func<object, bool> isSelected)
	{
		foreach (var item in parent.Items)
		{
			if (parent.ItemContainerGenerator.ContainerFromItem(item) is MenuItem menuItem)
			{
				menuItem.IsCheckable = true;
				menuItem.IsChecked = isSelected(item);
			}
		}
	}

	private void ToolbarProfileMenu_SubmenuOpened(object sender, RoutedEventArgs e)
		=> UpdateCheckedMenuItems(ToolbarProfileMenu, item => Equals(item, ViewModel.SelectedProfile));

	private void ToolbarProfileMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: DivinityProfileData profile })
		{
			var index = ViewModel.Profiles.IndexOf(profile);
			if (index >= 0) ViewModel.SelectedProfileIndex = index;
		}
	}

	private void ToolbarCampaignMenu_SubmenuOpened(object sender, RoutedEventArgs e)
		=> UpdateCheckedMenuItems(ToolbarCampaignMenu, item => Equals(item, ViewModel.SelectedAdventureMod));

	private void ToolbarCampaignMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: DivinityModData campaign })
		{
			var index = ViewModel.AdventureMods.IndexOf(campaign);
			if (index >= 0) ViewModel.SelectedAdventureModIndex = index;
		}
	}

	private void ToolbarLoadOrderMenu_SubmenuOpened(object sender, RoutedEventArgs e)
		=> UpdateCheckedMenuItems(ToolbarLoadOrderMenu, item => Equals(item, ViewModel.SelectedModOrder));

	private void ToolbarLoadOrderMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: DivinityLoadOrder order })
		{
			var index = ViewModel.ModOrderList.IndexOf(order);
			if (index < 0) return;

			ViewModel.SelectedModOrderIndex = index;
			if (ViewModel.Settings != null && ViewModel.Settings.LastOrder != order.Name)
			{
				ViewModel.Settings.LastOrder = order.Name;
				ViewModel.SaveSettings();
			}
		}
	}

	private void ToolbarAfterLaunchMenu_SubmenuOpened(object sender, RoutedEventArgs e)
		=> UpdateCheckedMenuItems(
			ToolbarAfterLaunchMenu,
			item => item is EnumEntry entry && Equals(entry.Value, ViewModel.Settings.ActionOnGameLaunch));

	private void ToolbarAfterLaunchMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: EnumEntry { Value: DivinityGameLaunchWindowAction action } })
		{
			ViewModel.Settings.ActionOnGameLaunch = action;
		}
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
				prop.Name == nameof(AppKeys.OpenCommandPalette) ||
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
				Command = key.Command
			};
			BindingOperations.SetBinding(
				newEntry,
				MenuItem.InputGestureTextProperty,
				new Binding { Path = new PropertyPath(nameof(Hotkey.DisplayBindingText)), Source = key });
			if (MenuIconMap.TryGetValue(prop.Name, out var iconSpec))
			{
				newEntry.Icon = ReduxIcon.FromResource(iconSpec.Resource, iconSpec.UseStroke, iconSpec.Foreground);
			}
			if(key == ViewModel.Keys.DownloadScriptExtender && TryFindResource("MenuItemHighlightBlink") is Style blinkStyle)
			{
				newEntry.Style = blinkStyle;
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
			var reduceMotionItem = new MenuItem
			{
				Header = "Reduce Motion",
				IsCheckable = true,
				StaysOpenOnClick = true,
				ToolTip = "Use immediate transitions while retaining simple visual feedback.",
				Icon = ReduxIcon.FromResource("Redux.Icon.CircleMinus", true)
			};
			BindingOperations.SetBinding(
				reduceMotionItem,
				MenuItem.IsCheckedProperty,
				new Binding("Settings.ReduceMotion")
				{
					Source = ViewModel,
					Mode = BindingMode.TwoWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
				});

			var disableBackgroundEffectsItem = new MenuItem
			{
				Header = "Disable Blur and Dimming",
				IsCheckable = true,
				StaysOpenOnClick = true,
				ToolTip = "Keep the main window clear behind dialogs and secondary windows.",
				Icon = ReduxIcon.FromResource("Redux.Icon.Eye", true)
			};
			BindingOperations.SetBinding(
				disableBackgroundEffectsItem,
				MenuItem.IsCheckedProperty,
				new Binding("Settings.DisableBackgroundEffects")
				{
					Source = ViewModel,
					Mode = BindingMode.TwoWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
				});

			accessibilityMenuItem.Items.Add(new Separator());
			accessibilityMenuItem.Items.Add(reduceMotionItem);
			accessibilityMenuItem.Items.Add(disableBackgroundEffectsItem);

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

		if (menuItems.TryGetValue("Tools", out var toolsMenuItem))
		{
			if (toolsMenuItem.Items.Count > 0) toolsMenuItem.Items.Add(new Separator());
			var contributionItem = new MenuItem
			{
				Header = "Generate Redux Database Contribution...",
				ToolTip = "Scan every installed user mod and create one privacy-limited, shareable report.",
				Icon = ReduxIcon.FromResource("Redux.Icon.Database", true)
			};
			contributionItem.Click += GenerateReduxDatabaseContribution_Click;
			toolsMenuItem.Items.Add(contributionItem);
		}

		// Keep attribution available without dedicating a second top-level menu to it.
		if (menuItems.TryGetValue("Help", out var helpMenuItem))
		{
			helpMenuItem.Items.Add(new Separator());
			var reduxWelcomeMenuItem = new MenuItem
			{
				Header = "Welcome Setup...",
				Icon = ReduxIcon.FromResource("Redux.Icon.Sparkles", true)
			};
			reduxWelcomeMenuItem.Click += (_, _) => ViewModel.ShowReduxWelcome();
			helpMenuItem.Items.Add(reduxWelcomeMenuItem);

			var reportBugMenuItem = new MenuItem
			{
				Header = "Report a Bug...",
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
				Icon = new ContentControl
				{
					ContentTemplate = FindResource("GithubPlatformIconTemplate") as DataTemplate
				}
			});
			creditsMenu.Items.Add(new MenuItem
			{
				Header = "Support LaughingLeader on Ko-fi",
				Command = ViewModel.Keys.OpenDonationLink.Command,
				Icon = ReduxIcon.FromResource("Redux.Icon.Heart", true)
			});
			helpMenuItem.Items.Add(creditsMenu);
		}

		// Generated top-menu commands do not pass through a XAML declaration where semantic
		// hover brushes can be attached individually. Resolve them once after the menu is
		// complete so destructive, source, and other explicitly coloured actions receive the
		// same rail-and-wash treatment as context-menu and category entries.
		foreach (var topLevelMenu in TopMenuBar.Items.OfType<MenuItem>())
		{
			ReduxMenuItemExtension.ApplySemanticHoverToMenu(topLevelMenu);
		}
	}

	private async void GenerateReduxDatabaseContribution_Click(object sender, RoutedEventArgs e)
	{
		var installedMods = ViewModel.UserMods?.Where(mod => mod != null && !mod.IsVisualDivider).ToList()
			?? new List<DivinityModData>();
		if (installedMods.Count == 0)
		{
			ReduxMessageBox.Show(main,
				"No installed user mods were found.",
				"Redux Database Contribution",
				MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
			return;
		}

		var consent = ReduxMessageBox.Show(main,
			$"Create one contribution report for all {installedMods.Count} installed mod package(s)?\n\n" +
			"The report includes mod names, authors, versions, module UUIDs, PAK filenames, exact file sizes and fingerprints, " +
				"and known Nexus Mods or mod.io IDs.\n\n" +
			"It does not include absolute paths, load order, profile names, settings, API keys, or other credentials. " +
			"Generating exact fingerprints may take a while for a large mod library.",
			"Generate Redux Database Contribution?",
			MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
		if (consent != MessageBoxResult.Yes) return;

		var dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = "Save Redux Database Contribution",
			Filter = "Redux database contribution (*.bg3redux-report)|*.bg3redux-report|JSON files (*.json)|*.json",
			DefaultExt = ".bg3redux-report",
			AddExtension = true,
			OverwritePrompt = true,
			FileName = $"Redux-Mod-Database-Contribution-{DateTime.Now:yyyy-MM-dd}"
		};
		if (dialog.ShowDialog(main) != true) return;

		try
		{
			ViewModel.ShowAlert($"Generating fingerprints for {installedMods.Count} installed mod package(s)...", AlertType.Info);
			var result = await ReduxDatabaseContributionService.CreateAsync(installedMods);
			ReduxDatabaseContributionService.Save(dialog.FileName, result.Report);

			var unavailableText = result.UnavailableFingerprintCount > 0
				? $"\n\n{result.UnavailableFingerprintCount} package(s) had identity metadata but no readable PAK fingerprint."
				: String.Empty;
			var outputDirectory = Path.GetDirectoryName(dialog.FileName);
			ReduxMessageBox.ShowWithActions(main,
				$"Saved a contribution report containing {result.Report.Mods.Count} mod package(s) and " +
				$"{result.FingerprintedCount} exact PAK fingerprint(s).{unavailableText}\n\n" +
				"You can review this text-based report before sharing it.",
				"Redux Database Contribution Saved",
				MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK,
				("Open Folder", "Redux.Icon.FolderOpen", () =>
				{
					if (!String.IsNullOrWhiteSpace(outputDirectory))
						ProcessHelper.TryOpenPath(outputDirectory, Directory.Exists);
				}));
			ViewModel.ShowAlert("Saved Redux database contribution report.", AlertType.Success, 20);
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Failed to generate a Redux database contribution report:\n{ex}");
			ReduxMessageBox.Show(main,
				"The contribution report could not be created. Check the log for details.",
				"Redux Database Contribution",
				MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
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

	public void PreviewCustomThemeColors(ReduxCustomTheme theme)
	{
		ReduxThemeService.PreviewColors(this.Resources, theme);
		main.PreviewCustomThemeColors(theme);
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
								("Copy to Clipboard", "Redux.Icon.Copy", () => ((System.Windows.Input.ICommand)DivinityApp.Commands.CopyToClipboardCommand).Execute(message)));
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

	private void SetInitialToolbarVisibility()
	{
		_toolbarAnimationVersion++;
		ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, null);
		ToolbarBand.BeginAnimation(UIElement.OpacityProperty, null);
		ToolbarBand.ClearValue(FrameworkElement.HeightProperty);
		ToolbarBand.Opacity = 1;
		ToolbarBand.Visibility = ViewModel.Settings.HideToolbar ? Visibility.Collapsed : Visibility.Visible;
	}

	private void AnimateToolbarVisibility(bool hide)
	{
		if (!IsLoaded || ReduxWindowBehavior.ReduceMotion)
		{
			SetInitialToolbarVisibility();
			return;
		}

		var animationVersion = ++_toolbarAnimationVersion;
		ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, null);
		ToolbarBand.BeginAnimation(UIElement.OpacityProperty, null);

		var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
		if (hide)
		{
			var startHeight = Math.Max(ToolbarBand.ActualHeight, ToolbarBand.DesiredSize.Height);
			if (startHeight <= 0)
			{
				ToolbarBand.Visibility = Visibility.Collapsed;
				return;
			}

			_toolbarExpandedHeight = startHeight;
			ToolbarBand.Visibility = Visibility.Visible;
			ToolbarBand.Height = startHeight;

        var heightAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        var opacityAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        heightAnimation.Completed += (_, _) =>
        {
            if (animationVersion != _toolbarAnimationVersion) return;
            ToolbarBand.Visibility = Visibility.Collapsed;
            ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, null);
            ToolbarBand.BeginAnimation(UIElement.OpacityProperty, null);
            ToolbarBand.ClearValue(FrameworkElement.HeightProperty);
            ToolbarBand.Opacity = 1;
        };

			ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
			ToolbarBand.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
			return;
		}

		var targetHeight = _toolbarExpandedHeight;
		if (targetHeight <= 0)
		{
			ToolbarBand.Visibility = Visibility.Hidden;
			ToolbarBand.ClearValue(FrameworkElement.HeightProperty);
			ToolbarBand.Measure(new Size(Math.Max(1, ModOrderPanel.ActualWidth), Double.PositiveInfinity));
			targetHeight = Math.Max(1, ToolbarBand.DesiredSize.Height);
		}

		ToolbarBand.Visibility = Visibility.Visible;
		ToolbarBand.Height = 0;
		ToolbarBand.Opacity = 0;

    var expandAnimation = new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(240))
    {
        EasingFunction = easing,
        FillBehavior = FillBehavior.HoldEnd
    };
    var fadeAnimation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(190))
    {
        BeginTime = TimeSpan.FromMilliseconds(25),
        EasingFunction = easing,
        FillBehavior = FillBehavior.HoldEnd
    };
    expandAnimation.Completed += (_, _) =>
    {
        if (animationVersion != _toolbarAnimationVersion) return;
        ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, null);
        ToolbarBand.BeginAnimation(UIElement.OpacityProperty, null);
        ToolbarBand.ClearValue(FrameworkElement.HeightProperty);
        ToolbarBand.ClearValue(UIElement.OpacityProperty);
    };

		ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, expandAnimation);
		ToolbarBand.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
	}

	private void ToolbarModDiagnosticsStatusButton_Click(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.Modules.ModDiagnosticsEnabled || !ViewModel.HasActiveDiagnosticAttention)
			return;

		if (sender is Button button)
		{
			AnimateToolbarModDiagnosticsStatus(button, true);
			OpenToolbarModDiagnosticsMenu(button);
		}

		e.Handled = true;
	}

	private async void ToolbarModDiagnosticsStatusButton_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is not Button button)
			return;

		if (!ViewModel.Modules.ModDiagnosticsEnabled)
			return;

		AnimateToolbarModDiagnosticsStatus(button, true);
		if (!ViewModel.HasActiveDiagnosticAttention)
			return;

		var hoverVersion = ++_diagnosticStatusHoverVersion;
		RestoreToolbarStatusMenuOpacity(button.ContextMenu);
		await Task.Delay(160);
		if (hoverVersion == _diagnosticStatusHoverVersion
			&& button.IsMouseOver
			&& ViewModel.Modules.ModDiagnosticsEnabled
			&& ViewModel.HasActiveDiagnosticAttention)
		{
			OpenToolbarModDiagnosticsMenu(button);
		}
	}

	private async void ToolbarModDiagnosticsStatusButton_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is Button button)
			await ScheduleToolbarStatusCloseAsync(button);
	}

	private void ToolbarModDiagnosticsStatusButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (!ViewModel.Modules.ModDiagnosticsEnabled || !ViewModel.HasActiveDiagnosticAttention)
			e.Handled = true;
	}

	private void CloseToolbarModDiagnosticsMenu()
	{
		_diagnosticStatusHoverVersion++;
		if (ToolbarModDiagnosticsStatusButton.ContextMenu is { } menu)
			menu.IsOpen = false;

		AnimateToolbarModDiagnosticsStatus(ToolbarModDiagnosticsStatusButton, false);
	}

	private static void OpenToolbarModDiagnosticsMenu(Button button)
	{
		if (button.ContextMenu is not { } menu || menu.IsOpen)
			return;

		menu.PlacementTarget = button;
		menu.Placement = PlacementMode.Bottom;
		// Small gap so the popup reads as its own surface instead of welding onto the
		// status chrome. Crossing it cannot collapse the button: MouseLeave defers to
		// ContextMenu.IsOpen.
		menu.VerticalOffset = 4;
		menu.IsOpen = true;
	}

	private void ToolbarModDiagnosticsMenu_Opened(object sender, RoutedEventArgs e)
	{
		if (sender is ContextMenu { PlacementTarget: Button button } menu)
		{
			_diagnosticStatusHoverVersion++;
			AnimateToolbarStatusMenu(menu, true);
			AnimateToolbarModDiagnosticsStatus(button, true);
			_ = MonitorToolbarStatusPointerAsync(button);
		}
	}

	private void ToolbarModDiagnosticsMenu_Closed(object sender, RoutedEventArgs e)
	{
		if (sender is ContextMenu { PlacementTarget: Button button } menu)
		{
			RestoreToolbarStatusMenuOpacity(menu);
			if (!button.IsMouseOver)
				AnimateToolbarModDiagnosticsStatus(button, false);
		}
	}

	private void ToolbarModDiagnosticsMenu_MouseEnter(object sender, MouseEventArgs e)
	{
		_diagnosticStatusHoverVersion++;
		if (sender is ContextMenu menu)
			RestoreToolbarStatusMenuOpacity(menu, true);
	}

	private async void ToolbarModDiagnosticsMenu_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is ContextMenu { PlacementTarget: Button button })
			await ScheduleToolbarStatusCloseAsync(button);
	}

	private async Task ScheduleToolbarStatusCloseAsync(Button button)
	{
		var hoverVersion = ++_diagnosticStatusHoverVersion;
		await Task.Delay(150);

		var menu = button.ContextMenu;
		if (hoverVersion != _diagnosticStatusHoverVersion
			|| IsPointerWithin(button)
			|| menu?.IsOpen == true)
			return;

		AnimateToolbarModDiagnosticsStatus(button, false);
	}

	private async Task MonitorToolbarStatusPointerAsync(Button button)
	{
		if (button.ContextMenu is not { } menu)
			return;

		// ContextMenu is hosted in a separate popup window and may retain mouse capture,
		// which makes MouseLeave/IsMouseOver unreliable. Polling each surface's actual
		// pointer coordinates gives the compact status controls deterministic dismissal.
		await Task.Delay(100);
		DateTime? pointerLeftAt = null;

		while (menu.IsOpen)
		{
			if (IsPointerWithin(button) || IsPointerWithin(menu))
			{
				pointerLeftAt = null;
				RestoreToolbarStatusMenuOpacity(menu, true);
			}
			else
			{
				pointerLeftAt ??= DateTime.UtcNow;
				if (DateTime.UtcNow - pointerLeftAt.Value >= TimeSpan.FromMilliseconds(170))
				{
					AnimateToolbarStatusMenu(menu, false);
					await Task.Delay(130);

					if (!IsPointerWithin(button) && !IsPointerWithin(menu))
					{
						_diagnosticStatusHoverVersion++;

						menu.IsOpen = false;
						AnimateToolbarModDiagnosticsStatus(button, false);
						return;
					}

					pointerLeftAt = null;
					RestoreToolbarStatusMenuOpacity(menu);
				}
			}

			await Task.Delay(40);
		}
	}

	private static bool IsPointerWithin(FrameworkElement element)
	{
		if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
			return false;

		try
		{
			if (!GetCursorPos(out var cursor))
				return false;

			var topLeft = element.PointToScreen(new Point(0, 0));
			var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
			return cursor.X >= Math.Min(topLeft.X, bottomRight.X)
				&& cursor.Y >= Math.Min(topLeft.Y, bottomRight.Y)
				&& cursor.X <= Math.Max(topLeft.X, bottomRight.X)
				&& cursor.Y <= Math.Max(topLeft.Y, bottomRight.Y);
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void AnimateToolbarStatusMenu(ContextMenu menu, bool show)
	{
		var duration = show
			? new Duration(TimeSpan.FromMilliseconds(165))
			: new Duration(TimeSpan.FromMilliseconds(130));
		var easing = menu.TryFindResource("Redux.Motion.EaseOut") as IEasingFunction
			?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

		var translate = menu.RenderTransform as TranslateTransform;
		if (translate == null || translate.IsFrozen)
		{
			// Context menus are popup windows. Give each opened instance a mutable
			// transform instead of animating a shared template resource.
			translate = new TranslateTransform();
			menu.RenderTransform = translate;
		}

		if (show)
		{
			_closingToolbarStatusMenus.Remove(menu);
			menu.Opacity = 0;
			translate.Y = -3;
		}
		else
		{
			_closingToolbarStatusMenus.Add(menu);
		}

		if (ReduxWindowBehavior.ReduceMotion)
		{
			menu.BeginAnimation(UIElement.OpacityProperty, null);
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			menu.Opacity = show ? 1 : 0;
			translate.Y = 0;
			return;
		}

		menu.BeginAnimation(
			UIElement.OpacityProperty,
			new DoubleAnimation(show ? 1 : 0, duration) { EasingFunction = easing },
			HandoffBehavior.SnapshotAndReplace);
		translate.BeginAnimation(
			TranslateTransform.YProperty,
			new DoubleAnimation(show ? 0 : -2, duration) { EasingFunction = easing },
			HandoffBehavior.SnapshotAndReplace);
	}

	private void RestoreToolbarStatusMenuOpacity(ContextMenu menu, bool onlyIfClosing = false)
	{
		if (menu == null)
			return;
		if (onlyIfClosing && !_closingToolbarStatusMenus.Contains(menu))
			return;

		_closingToolbarStatusMenus.Remove(menu);
		menu.BeginAnimation(UIElement.OpacityProperty, null);
		menu.Opacity = 1;
		if (menu.RenderTransform is TranslateTransform translate)
		{
			// A menu that was never run through AnimateToolbarStatusMenu still carries the
			// shared transform from its template, which WPF freezes. Both BeginAnimation and
			// the Y assignment below throw on a frozen Freezable, so swap in a mutable clone
			// first — the same guard AnimateToolbarModDiagnosticsStatus already uses.
			if (translate.IsFrozen)
			{
				translate = translate.CloneCurrentValue();
				menu.RenderTransform = translate;
			}

			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
		}
	}

	// Compact width is the icon+chevron cluster with balanced 7px insets.
	private const double ToolbarDiagnosticCompactWidth = 44;
	private const double ToolbarDiagnosticFallbackExpandedWidth = 126;
	private const double ToolbarDiagnosticMaxExpandedWidth = 240;

	private static void AnimateToolbarHoverSurface(FrameworkElement element, bool isHovered)
	{
		if (element == null)
			return;

		var translate = element.RenderTransform as TranslateTransform;
		if (translate == null)
		{
			translate = new TranslateTransform();
			element.RenderTransform = translate;
		}
		else if (translate.IsFrozen)
		{
			translate = translate.CloneCurrentValue();
			element.RenderTransform = translate;
		}

		if (ReduxWindowBehavior.ReduceMotion)
		{
			translate.BeginAnimation(TranslateTransform.YProperty, null);
			translate.Y = 0;
			return;
		}

		var duration = isHovered
			? new Duration(TimeSpan.FromMilliseconds(120))
			: new Duration(TimeSpan.FromMilliseconds(150));
		var easing = element.TryFindResource("Redux.Motion.EaseOut") as IEasingFunction
			?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

		translate.BeginAnimation(
			TranslateTransform.YProperty,
			new DoubleAnimation(isHovered ? -1 : 0, duration) { EasingFunction = easing },
			HandoffBehavior.SnapshotAndReplace);
	}

	private void ToolbarComboBox_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is ComboBox comboBox)
			AnimateToolbarHoverSurface(comboBox, true);
	}

	private void ToolbarComboBox_MouseLeave(object sender, MouseEventArgs e)
	{
		if (sender is ComboBox comboBox)
			AnimateToolbarHoverSurface(comboBox, false);
	}

	/// <summary>
	/// Width the expanded status control needs for its current summary text. The label is
	/// laid out even while transparent, so the content row's desired size is authoritative.
	/// Measuring keeps the reveal correct under Large text, custom fonts and longer
	/// summaries, where a hardcoded width clipped.
	/// </summary>
	private static double MeasureToolbarDiagnosticExpandedWidth(Button button)
	{
		if (button.Template?.FindName("StatusContent", button) is not FrameworkElement content)
			return ToolbarDiagnosticFallbackExpandedWidth;

		content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		var desired = content.DesiredSize.Width;
		if (double.IsNaN(desired) || desired <= 0)
			return ToolbarDiagnosticFallbackExpandedWidth;

		var chrome = button.BorderThickness.Left + button.BorderThickness.Right;
		// A few device-independent pixels beyond DesiredSize protect the final glyph
		// from ClearType overhang and layout rounding at non-100% DPI.
		var width = Math.Ceiling(desired + chrome + 7);
		return Math.Min(ToolbarDiagnosticMaxExpandedWidth, Math.Max(ToolbarDiagnosticCompactWidth, width));
	}

	private static void AnimateToolbarModDiagnosticsStatus(Button button, bool expand)
	{
		// The menu-row variants intentionally remain icon-only. They reuse the same
		// status popup lifecycle without changing the width of the application menu.
		if (button.Name.StartsWith("CompactToolbar", StringComparison.Ordinal))
			return;

		const double compactLabelOpacity = 0;
		const double expandedLabelOpacity = 1;

		button.ApplyTemplate();

		// Width is the only layout-affecting part of this interaction. The status chrome
		// remains stationary so its popup anchor and neighboring controls stay visually stable.
		var duration = new Duration(TimeSpan.FromMilliseconds(expand ? 175 : 145));
		var easing = button.TryFindResource("Redux.Motion.EaseOut") as IEasingFunction
			?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

		var targetWidth = expand
			? MeasureToolbarDiagnosticExpandedWidth(button)
			: ToolbarDiagnosticCompactWidth;

		if (ReduxWindowBehavior.ReduceMotion)
		{
			button.BeginAnimation(FrameworkElement.WidthProperty, null);
			button.Width = targetWidth;
			if (button.Template.FindName("StatusLabel", button) is TextBlock immediateLabel)
			{
				immediateLabel.BeginAnimation(UIElement.OpacityProperty, null);
				immediateLabel.Opacity = expand ? expandedLabelOpacity : compactLabelOpacity;
			}
			if (button.Template.FindName("StatusChevron", button) is FrameworkElement immediateChevron)
			{
				var immediateRotation = immediateChevron.RenderTransform as RotateTransform;
				if (immediateRotation == null || immediateRotation.IsFrozen)
				{
					immediateRotation = immediateRotation?.CloneCurrentValue() ?? new RotateTransform();
					immediateChevron.RenderTransform = immediateRotation;
				}
				immediateRotation.BeginAnimation(RotateTransform.AngleProperty, null);
				immediateRotation.Angle = expand ? 90 : 0;
			}
			return;
		}

		button.BeginAnimation(
			FrameworkElement.WidthProperty,
			new DoubleAnimation(targetWidth, duration) { EasingFunction = easing },
			HandoffBehavior.SnapshotAndReplace);

		if (button.Template.FindName("StatusLabel", button) is TextBlock label)
		{
			label.BeginAnimation(
				UIElement.OpacityProperty,
				new DoubleAnimation(expand ? expandedLabelOpacity : compactLabelOpacity, duration) { EasingFunction = easing },
				HandoffBehavior.SnapshotAndReplace);
		}

		if (button.Template.FindName("StatusChevron", button) is FrameworkElement chevron)
		{
			var chevronRotation = chevron.RenderTransform as RotateTransform;
			if (chevronRotation == null)
			{
				chevronRotation = new RotateTransform();
				chevron.RenderTransform = chevronRotation;
			}
			else if (chevronRotation.IsFrozen)
			{
				// Freezables declared inside a shared ControlTemplate may be frozen by WPF.
				// Give this button its own mutable transform before starting the hover animation.
				chevronRotation = chevronRotation.CloneCurrentValue();
				chevron.RenderTransform = chevronRotation;
			}

			chevronRotation.BeginAnimation(
				RotateTransform.AngleProperty,
				new DoubleAnimation(expand ? 90 : 0, duration) { EasingFunction = easing },
				HandoffBehavior.SnapshotAndReplace);
		}

	}

	private void ToolbarModDiagnosticsMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: ModHealthSnapshot snapshot })
			ModLayout.FocusDiagnosticSnapshot(snapshot);
		else if (sender is MenuItem { DataContext: ModDiagnosticFindingGroupViewModel group })
			ModLayout.FocusDiagnosticSnapshot(group.PrimarySnapshot);
	}

	private void ToolbarDiagnosticAffectedMod_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: ModHealthSnapshot snapshot } button)
		{
			button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
			ModLayout.FocusDiagnosticSnapshot(snapshot);
		}

		e.Handled = true;
	}

	private void ToolbarDiagnosticRelatedMod_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: DivinityModData mod } button)
		{
			button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
			ModLayout.FocusModEntry(mod);
		}

		e.Handled = true;
	}

	private void ToolbarDiagnosticActivateDependency_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not Button { CommandParameter: ModDiagnosticFindingGroupViewModel group } button
			|| group.PrimaryRelatedMod == null
			|| group.PrimaryRelatedMod.IsActive)
		{
			e.Handled = true;
			return;
		}

		button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
		var dependency = group.PrimaryRelatedMod;
		var requiredBy = String.Join(
			", ",
			group.AffectedMods
				.Select(item => item.Mod.DisplayName)
				.Distinct(StringComparer.CurrentCultureIgnoreCase));
		var result = ReduxMessageBox.Show(
			Window.GetWindow(this),
			$"Activate {dependency.DisplayName}?\n\nRequired by: {requiredBy}\n\n"
			+ "Redux will add the installed dependency to the end of the current working order. "
			+ "Review its placement before exporting. No game files are changed until you export.",
			"Activate Dependency",
			MessageBoxButton.YesNo,
			MessageBoxImage.Question,
			MessageBoxResult.No);
		if (result == MessageBoxResult.Yes)
		{
			ViewModel.AddActiveMod(dependency);
			ViewModel.ShowAlert(
				$"Activated {dependency.DisplayName}. Review its load-order position before exporting.",
				AlertType.Success,
				20);
			ModLayout.FocusModEntry(dependency);
		}

		e.Handled = true;
	}

	private void ToolbarDiagnosticRelatedSource_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: DivinityModData mod } button)
		{
			button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
			DivinityApp.Commands.OpenModSourcePage(mod);
		}

		e.Handled = true;
	}

	private void ToolbarDiagnosticCopyDependencyUuid_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: string uuid } button
			&& !String.IsNullOrWhiteSpace(uuid))
		{
			button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
			DivinityApp.Commands.CopyToClipboard(uuid);
		}

		e.Handled = true;
	}

	private void ToolbarDiagnosticAffectedSource_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { CommandParameter: DivinityModData mod } button)
		{
			button.FindVisualParent<ContextMenu>()?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
			DivinityApp.Commands.OpenModSourcePage(mod);
		}

		e.Handled = true;
	}

	public void FocusModEntry(DivinityModData mod) => ModLayout.FocusModEntry(mod);

	public void OnActivated()
	{
		// Toolbar buttons that open a modal dialog (file pickers, message boxes) can be left
		// visually stuck in their hover state: while the dialog owns input the main window stops
		// receiving mouse messages, so IsMouseOver is never re-evaluated and the hover trigger's
		// ExitActions never run. Re-syncing when the window regains activation makes WPF
		// recompute the element under the cursor and releases the stale state.
		if (Window.GetWindow(this) is { } ownerWindow)
		{
			ownerWindow.Activated += (_, _) => Mouse.Synchronize();
		}

		// Same stale-hover problem, different cause: the busy overlay used during Refresh/Export/etc.
		// sits over the toolbar and intercepts its mouse messages without ever moving the owner
		// window out of the activated state, so the Activated-based sync above never fires for it.
		// Catch the other trigger directly: re-sync whenever the overlay goes from busy back to idle.
		this.WhenAnyValue(x => x.ViewModel.MainProgressIsActive)
			.DistinctUntilChanged()
			.Skip(1)
			.Where(isActive => !isActive)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => Mouse.Synchronize());

		this.OneWayBind(ViewModel, vm => vm.IsDeletingFiles, view => view.ModListRectangle.Visibility, BoolToVisibilityConverter.FromBool);
		this.OneWayBind(ViewModel, vm => vm.MainProgressIsActive, view => view.MainBusyIndicator.IsBusy);

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
		SetInitialToolbarVisibility();
		_toolbarVisibilitySubscription?.Dispose();
		_toolbarVisibilitySubscription = ViewModel.WhenAnyValue(vm => vm.Settings.HideToolbar)
			.Skip(1)
			.DistinctUntilChanged()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(AnimateToolbarVisibility);
		_modDiagnosticsStatusSubscription?.Dispose();
		_modDiagnosticsStatusSubscription = ViewModel
			.WhenAnyValue(
				vm => vm.Modules.ModDiagnosticsEnabled,
				vm => vm.HasActiveDiagnosticAttention,
				(enabled, hasAttention) => enabled && hasAttention)
			.DistinctUntilChanged()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(canShowAttention => !canShowAttention)
			.Subscribe(_ => CloseToolbarModDiagnosticsMenu());

		this.OneWayBind(ViewModel, vm => vm.UpdatesViewVisibility, view => view.ModUpdaterPanel.Visibility);
		var whenUpdatesViewData = ViewModel.WhenAnyValue(x => x.ModUpdatesViewData);
		whenUpdatesViewData.BindTo(this, x => x.ModUpdaterPanel.ViewModel);
		whenUpdatesViewData.BindTo(this, x => x.ModUpdaterPanel.DataContext);

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
