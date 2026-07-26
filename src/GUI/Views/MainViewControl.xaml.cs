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
	private readonly MainWindow main;
	private IDisposable _toolbarVisibilitySubscription;
	private double _toolbarExpandedHeight;
	private int _toolbarAnimationVersion;
	private int _healthStatusHoverVersion;

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
				Header = "Export Redux Database Contribution...",
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
	}

	private async void GenerateReduxDatabaseContribution_Click(object sender, RoutedEventArgs e)
	{
		var installedMods = ViewModel.UserMods?.Where(mod => mod != null && !mod.IsVisualDivider).ToList()
			?? new List<DivinityModData>();
		if (installedMods.Count == 0)
		{
			ReduxMessageBox.Show(main,
				"Redux has not detected any installed user mods to include.",
				"Redux Database Contribution",
				MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
			return;
		}

		var consent = ReduxMessageBox.Show(main,
			$"Create one contribution report for all {installedMods.Count} installed mod package(s)?\n\n" +
			"The report includes mod names, authors, versions, module UUIDs, PAK filenames, exact file sizes and fingerprints, " +
			"and source IDs already known to Redux.\n\n" +
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
				("Open Folder", () =>
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
				$"Redux could not create the contribution report.\n\n{ex.Message}",
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
		if (!IsLoaded)
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
				FillBehavior = FillBehavior.Stop
			};
			var opacityAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(155))
			{
				EasingFunction = easing,
				FillBehavior = FillBehavior.Stop
			};
			heightAnimation.Completed += (_, _) =>
			{
				if (animationVersion != _toolbarAnimationVersion) return;
				ToolbarBand.Visibility = Visibility.Collapsed;
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
			FillBehavior = FillBehavior.Stop
		};
		var fadeAnimation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(190))
		{
			BeginTime = TimeSpan.FromMilliseconds(25),
			EasingFunction = easing,
			FillBehavior = FillBehavior.Stop
		};
		expandAnimation.Completed += (_, _) =>
		{
			if (animationVersion != _toolbarAnimationVersion) return;
			ToolbarBand.ClearValue(FrameworkElement.HeightProperty);
			ToolbarBand.ClearValue(UIElement.OpacityProperty);
		};

		ToolbarBand.BeginAnimation(FrameworkElement.HeightProperty, expandAnimation);
		ToolbarBand.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
	}

	private void ToolbarModHealthStatusButton_Click(object sender, RoutedEventArgs e)
	{
		if (!ViewModel.HasActiveModHealthAttention)
			return;

		if (sender is Button button)
		{
			AnimateToolbarModHealthStatus(button, true);
			OpenToolbarModHealthMenu(button);
		}

		e.Handled = true;
	}

	private async void ToolbarModHealthStatusButton_MouseEnter(object sender, MouseEventArgs e)
	{
		if (sender is not Button button)
			return;

		AnimateToolbarModHealthStatus(button, true);
		if (!ViewModel.HasActiveModHealthAttention)
			return;

		var hoverVersion = ++_healthStatusHoverVersion;
		await Task.Delay(180);
		if (hoverVersion == _healthStatusHoverVersion
			&& button.IsMouseOver
			&& ViewModel.HasActiveModHealthAttention)
		{
			OpenToolbarModHealthMenu(button);
		}
	}

	private void ToolbarModHealthStatusButton_MouseLeave(object sender, MouseEventArgs e)
	{
		_healthStatusHoverVersion++;
		if (sender is Button button && button.ContextMenu?.IsOpen != true)
			AnimateToolbarModHealthStatus(button, false);
	}

	private void ToolbarModHealthStatusButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (!ViewModel.HasActiveModHealthAttention)
			e.Handled = true;
	}

	private static void OpenToolbarModHealthMenu(Button button)
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

	private void ToolbarModHealthMenu_Opened(object sender, RoutedEventArgs e)
	{
		if (sender is ContextMenu { PlacementTarget: Button button })
			AnimateToolbarModHealthStatus(button, true);
	}

	private void ToolbarModHealthMenu_Closed(object sender, RoutedEventArgs e)
	{
		if (sender is ContextMenu { PlacementTarget: Button button } && !button.IsMouseOver)
			AnimateToolbarModHealthStatus(button, false);
	}

	// Compact width is the icon+chevron cluster with even 8px insets:
	// 1 border + 8 + 15 icon + 4 + 11 chevron + 8 + 1 border.
	private const double ToolbarHealthCompactWidth = 48;
	private const double ToolbarHealthFallbackExpandedWidth = 126;
	private const double ToolbarHealthMaxExpandedWidth = 240;

	/// <summary>
	/// Width the expanded status control needs for its current summary text. The label is
	/// laid out even while transparent, so the content row's desired size is authoritative.
	/// Measuring keeps the reveal correct under Large text, custom fonts and longer
	/// summaries, where a hardcoded width clipped.
	/// </summary>
	private static double MeasureToolbarHealthExpandedWidth(Button button)
	{
		if (button.Template?.FindName("StatusContent", button) is not FrameworkElement content)
			return ToolbarHealthFallbackExpandedWidth;

		content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

		var desired = content.DesiredSize.Width;
		if (double.IsNaN(desired) || desired <= 0)
			return ToolbarHealthFallbackExpandedWidth;

		var chrome = button.BorderThickness.Left + button.BorderThickness.Right;
		var width = Math.Ceiling(desired + chrome);
		return Math.Min(ToolbarHealthMaxExpandedWidth, Math.Max(ToolbarHealthCompactWidth, width));
	}

	private static void AnimateToolbarModHealthStatus(Button button, bool expand)
	{
		const double compactLabelOpacity = 0;
		const double expandedLabelOpacity = 1;

		button.ApplyTemplate();

		// Share the toolbar's motion language rather than restating it numerically.
		var duration = button.TryFindResource("Redux.Motion.Standard") is Duration themedDuration
			? themedDuration
			: new Duration(TimeSpan.FromMilliseconds(160));
		var easing = button.TryFindResource("Redux.Motion.EaseOut") as IEasingFunction
			?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

		var targetWidth = expand
			? MeasureToolbarHealthExpandedWidth(button)
			: ToolbarHealthCompactWidth;

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

	private void ToolbarModHealthMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem { DataContext: ModHealthSnapshot snapshot })
			ModLayout.FocusModHealthSnapshot(snapshot);
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
		SetInitialToolbarVisibility();
		_toolbarVisibilitySubscription?.Dispose();
		_toolbarVisibilitySubscription = ViewModel.WhenAnyValue(vm => vm.Settings.HideToolbar)
			.Skip(1)
			.DistinctUntilChanged()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(AnimateToolbarVisibility);

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
