using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class DialogLayoutTests
{
	public void DownloadIntakeProgressBindsToReadOnlyViewModelProperties()
	{
		// Avoid starting profile discovery or accessing user settings while exercising
		// the production window bindings against the real private-set properties.
		var viewModel = (DivinityModManager.ViewModels.MainWindowViewModel)
			System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(DivinityModManager.ViewModels.MainWindowViewModel));
		var window = new ReduxNexusDownloadsWindow();
		try
		{
			window.DataContext = viewModel;
			var progress = (ProgressBar)window.FindName("LocalPackageIntakeProgress");
			void SetProgress(string property, object value) => viewModel.GetType().GetProperty(property)!.SetValue(viewModel, value);
			SetProgress(nameof(viewModel.LocalPackageIntakeTotal), 4);
			SetProgress(nameof(viewModel.LocalPackageIntakeCompleted), 1);
			SetProgress(nameof(viewModel.LocalPackageIntakeIsActive), true);
			window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
			RegressionAssert.Equal(4d, progress.Maximum);
			RegressionAssert.Equal(1d, progress.Value);
			RegressionAssert.Equal(Visibility.Visible, progress.Visibility);
			SetProgress(nameof(viewModel.LocalPackageIntakeCompleted), 4);
			window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
			RegressionAssert.Equal(4d, progress.Value);
			SetProgress(nameof(viewModel.LocalPackageIntakeIsActive), false);
			window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
			RegressionAssert.Equal(Visibility.Collapsed, progress.Visibility);
		}
		finally { window.Close(); }
	}

	public void ReviewDialogsKeepActionsReachableWithLargeText()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var review = new ReduxSaveModReviewWindow(null!, "A long campaign name — latest save", [
				new("11111111-1111-1111-1111-111111111111", "Installed required mod", DivinityModManager.AppServices.SaveModStatus.Inactive),
				new("22222222-2222-2222-2222-222222222222", "A missing required mod", DivinityModManager.AppServices.SaveModStatus.Missing)], new DivinityModManagerSettings());
			VerifyLayout(review, theme, 780, 560, "save-mod-review-normal", "ApplyButton");
			VerifyLayout(review, theme, 640, 480, "save-mod-review", "ApplyButton");
			review.Close();
            var collectionOrder = new ReduxSaveModReviewWindow(null!, "Example collection", [
                new("11111111-1111-1111-1111-111111111111", "Installed inactive mod", DivinityModManager.AppServices.SaveModStatus.Inactive),
                new("22222222-2222-2222-2222-222222222222", "Missing collection mod", DivinityModManager.AppServices.SaveModStatus.Missing)], new DivinityModManagerSettings(), collectionOrder: true);
            VerifyLayout(collectionOrder, theme, 780, 560, "collection-order-review", "ApplyButton");
            collectionOrder.Close();
			var manager = new ReduxGameDirectoryModManagerWindow();
			((ListBox)manager.FindName("InstalledList")).ItemsSource = new[] {
				new { Name = "Native Mod Loader", StatusText = "Can't manage · this installation has no protected original backup",
				ManagementNote = "Use the Redux installation holding the original backups.", Summary = "Native loader",
				DetailsText = "Native plugin", FileSummary = "bink2w64.dll", ThumbnailUrl = "", HasSource = true, CanAdopt = false, CanRestore = false,
				Status = DivinityModManager.AppServices.ReduxGameDirectoryModStatus.External, IsExternalReplacement = true }
			};
			((FrameworkElement)manager.FindName("EmptyText")).Visibility = Visibility.Collapsed;
			VerifyLayout(manager, theme, 760, 540, "native-status", "GameDirectoryCloseButton");
			var comparison = new ReduxLoadOrderComparisonWindow(null!, [], 0, 0);
			VerifyLayout(comparison, theme, 680, 540, "comparison", "SwapOrdersButton");
			RegressionAssert.True(ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(comparison), comparison.FindName("BaselineComboBox")));
			var overlap = new ReduxFileOverlapWindow(null!, []);
			VerifyLayout(overlap, theme, 680, 540, "overlap", "ScanActionButton");
			RegressionAssert.True(ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(overlap), overlap.FindName("ScanActionButton")));
			var preflight = new ReduxPackagePreflightWindow(null!, "example.pak", []);
			VerifyLayout(preflight, theme, 660, 540, "preflight", "ScanActionButton");
			RegressionAssert.True(ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(preflight), preflight.FindName("ScanActionButton")));
		}
	}

	public void DownloadToolbarActionsRemainVisibleWithLargeText()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var collection = new ReduxCollectionWindow(null!, null!);
			collection.PresentFiles(new[] {
				new CollectionFileChoice(new(5, 101, "Example collection mod", "Core file", "1.0", 1234, false, true), false),
				new CollectionFileChoice(new(5, 102, "Example optional textures", "Textures", "1.0", 1234, true, true), false),
				new CollectionFileChoice(new(0, 103, "", "", "", 0, false, false), false)
			});
			VerifyLayout(collection, theme, 760, 560, "collection-preview", "DownloadButton", "PreviewButton");
            ((FrameworkElement)collection.FindName("DownloadGuidePanel")).Visibility = Visibility.Visible;
            ((TextBlock)collection.FindName("DownloadGuideText")).Text = "2 downloaded · 0 installed · 3 need a Nexus link\nNext: Example collection mod. Choose Mod Manager Download on Nexus.";
            VerifyLayout(collection, theme, 900, 660, "collection-download-guide", "DownloadButton", "NextFileButton");
            ((FrameworkElement)collection.FindName("RetryFailedButton")).Visibility = Visibility.Visible;
            VerifyLayout(collection, theme, 900, 660, "collection-download-retry", "DownloadButton", "NextFileButton", "RetryFailedButton");
			collection.Close();
			var saves = new ReduxSaveManagerWindow(null, null);
            ((ListBox)saves.FindName("SaveList")).ItemsSource = new[] {
                new ReduxSaveGameItem(new DivinityModManager.AppServices.Bg3SaveGameEntry(
                    "", "Tav__AutoSave_12", "AutoSave 12", "Tav", "AutoSave_12.lsv", null,
                    DateTime.UtcNow, 12345678, DivinityModManager.AppServices.Bg3SaveDifficulty.Tactician) {
                        Location = "WLD_Main_A", GameVersion = "4.1.1.123",
                        Party = new[] { new DivinityModManager.AppServices.Bg3SavePartyMember("Gale", 5, "Human", "Wizard (Evocation)", "Grove") }
                    })
            };
            var saveList = (ListBox)saves.FindName("SaveList");
            var grouped = new System.Windows.Data.ListCollectionView(saveList.Items.Cast<ReduxSaveGameItem>().ToArray());
            grouped.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ReduxSaveGameItem.CampaignGroupName)));
            saveList.ItemsSource = grouped;
            ((FrameworkElement)saves.FindName("EmptyState")).Visibility = Visibility.Collapsed;
            VerifyLayout(saves, theme, 760, 540, "save-rows");
            ((ListBox)saves.FindName("SaveList")).SelectedIndex = 0;
            VerifyLayout(saves, theme, 760, 540, "save-details-panel");
            saves.Close();
            var downloads = new ReduxNexusDownloadsWindow();
			var selectionHandler = (SelectionChangedEventHandler)Delegate.CreateDelegate(typeof(SelectionChangedEventHandler), downloads,
				typeof(ReduxNexusDownloadsWindow).GetMethod("DownloadsTabs_SelectionChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!);
			((TabControl)downloads.FindName("DownloadsTabs")).SelectionChanged -= selectionHandler;
			((Button)downloads.FindName("ClearArchivesButton")).Visibility = Visibility.Visible;
			((Button)downloads.FindName("ClearInstalledButton")).Visibility = Visibility.Visible;
			VerifyLayout(downloads, theme, 760, 540, "downloads", "PreviousVersionsButton", "DownloadsCloseButton");
			var root = (Grid)downloads.Content;
			var actions = (WrapPanel)downloads.FindName("DownloadActions");
			foreach (Button button in actions.Children)
			{
				var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
				RegressionAssert.True(bounds.Width > 0 && bounds.Height > 0);
				RegressionAssert.True(new Rect(0, 0, 760, 540).Contains(bounds));
			}
			((TabControl)downloads.FindName("DownloadsTabs")).SelectedIndex = 1;
			VerifyLayout(downloads, theme, 760, 540, "installed-actions", "ClearInstalledButton");
			((TabControl)downloads.FindName("DownloadsTabs")).SelectedIndex = 2;
			VerifyLayout(downloads, theme, 760, 540, "archive-actions", "ClearArchivesButton");
		}
	}

	public void PreferencesAndReleaseNotesUseReadableCompactLayouts()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var welcome = new ReduxOnboardingWindow(null!, new DivinityModManagerSettings());
			for (var step = 0; step < 5; step++)
			{
				if (step == 1) ((CheckBox)welcome.FindName("SourceIntegrationsCheckBox")).IsChecked = true;
				VerifyLayout(welcome, theme, 780, 740, $"welcome-{step}", "NotNowButton", "SaveContinueButton");
				VerifyLayout(welcome, theme, 780, 740, $"welcome-{step}-normal", "NotNowButton", "SaveContinueButton");
				RegressionAssert.True(((ScrollViewer)welcome.FindName("OnboardingContentScrollViewer")).ScrollableHeight < 1);

				if (step == 0)
				{
					var appearance = (Expander)welcome.FindName("AppearanceOptionsExpander");
					appearance.IsExpanded = true;
					((ScrollViewer)welcome.FindName("OnboardingContentScrollViewer")).ScrollToBottom();
					VerifyLayout(welcome, theme, 780, 740, "welcome-appearance-normal", "NotNowButton", "SaveContinueButton");
					appearance.IsExpanded = false;
				}
				if (step == 4)
				{
					((Button)welcome.FindName("NativeTourButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					for (var state = 0; state < 3; state++)
					{
						VerifyLayout(welcome, theme, 780, 740, $"welcome-native-{state}-normal", "TourActionButton", "SaveContinueButton");
						RegressionAssert.True(((ScrollViewer)welcome.FindName("OnboardingContentScrollViewer")).ScrollableHeight < 1);
						((Button)welcome.FindName("TourActionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					}
				}
				if (step < 4) ((Button)welcome.FindName("SaveContinueButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			}
			welcome.Close();
			var settings = new SettingsWindow();
			var grid = (DivinityModManager.Controls.AutoGrid)settings.FindName("SettingsAutoGrid");
			typeof(SettingsWindow).GetMethod("CreateSettingsElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(settings, [new DivinityModManagerSettings(), typeof(DivinityModManagerSettings), grid]);
			VerifyLayout(settings, theme, 820, 600, "preferences", "SaveSettingsButton", "ResetSettingsButton");
			((TabControl)settings.FindName("PreferencesTabControl")).SelectedIndex = 1;
			VerifyLayout(settings, theme, 780, 600, "preferences-appearance", "SaveSettingsButton", "ResetSettingsButton");
			foreach (var controlName in new[] { "TypographyComboBox", "TextSizeComboBox", "CustomThemeComboBox", "ReduxDarkThemeCard", "ReduxLightThemeCard", "ParchmentThemeCard", "DeleteCustomFontButton", "EditCustomThemeButton" })
			{
				var control = (Control)settings.FindName(controlName);
				var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(control);
				RegressionAssert.True(!String.IsNullOrWhiteSpace(peer?.GetName()));
				RegressionAssert.True(control.IsTabStop);
			}
			var appearanceTab = (TabItem)((TabControl)settings.FindName("PreferencesTabControl")).Items[1];
			var appearanceScroll = (ScrollViewer)appearanceTab.Content;
			appearanceScroll.ScrollToEnd();
			VerifyLayout(settings, theme, 780, 600, "preferences-typography", "SaveSettingsButton", "ResetSettingsButton");
			var notes = new ReduxWhatsNewWindow(null!, "test");
			ReduxThemeService.Apply(notes.Resources, theme);
			notes.SetNotes("<!-- redux:no-announce -->\n# Improvements\n\nClear text, **strong emphasis**, and `code`.\n\n## Fixes\n\n- Preserved load order\n- Improved downloads\n\n[Read more](https://github.com/circleainn/BG3ModManager-Redux)");
			VerifyLayout(notes, theme, 560, 440, "whats-new");
		}
	}

	public void ModlistActionsRemainReachableWithLargeTextAndWarnings()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var import = new ReduxLoadOrderImportWindow(null!, null!, ["Missing mod"], ["Existing category"]);
			((Border)import.FindName("SourceLinksOptionBorder")).Visibility = Visibility.Visible;
			((Border)import.FindName("PrivateNotesOptionBorder")).Visibility = Visibility.Visible;
			((CheckBox)import.FindName("ImportSourceLinksCheckBox")).IsChecked = true;
			RegressionAssert.Contains(((TextBlock)import.FindName("ImportImpactText")).Text, "Replaces source links");
			VerifyLayout(import, theme, 420, 440, "modlist-import", "CancelButton", "ImportButton");
			var export = new ReduxLoadOrderExportWindow(null!, "A long saved load order name for sharing with other players", 100, 12, 5, 4, 80, 20, 2);
			RegressionAssert.False(export.IncludePrivateNotes);
			VerifyLayout(export, theme, 420, 440, "modlist-export", "CancelButton", "ChooseLocationButton");
		}
	}

	public void UpdateAndMessageActionsRemainReachableWithLongText()
	{
		foreach (var theme in new[] { ReduxThemeType.ReduxDark, ReduxThemeType.ReduxLight, ReduxThemeType.Parchment })
		{
			var update = new AppUpdateWindow();
			((TextBlock)update.FindName("UpdateDescription")).Text = "Redux could not safely prepare this update. The current installation was not changed.";
			((FlowDocumentScrollViewer)update.FindName("UpdateChangelogView")).Document = new FlowDocument(
				new Paragraph(new Run(String.Join(" ", Enumerable.Repeat("Keep using Redux and try again later.", 20))))) { FontSize = 18 };
			VerifyLayout(update, theme, 560, 440, "update", "SkipButton", "ConfirmButton");
			var message = new ReduxMessageBoxWindow(null!, String.Join(" ", Enumerable.Repeat("Review these changes before continuing.", 30)),
				"Review changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
			message.SetButtonLabel(MessageBoxResult.Yes, "Apply reviewed changes");
			message.SetButtonLabel(MessageBoxResult.No, "Keep existing files");
			VerifyLayout(message, theme, 420, 440, "message", "YesButton", "NoButton", "CancelButton");
		}
	}

	private static void VerifyLayout(Window window, ReduxThemeType theme, int width, int height, string name, params string[] buttons)
	{
		ReduxThemeService.Apply(window.Resources, theme);
		window.Resources["Redux.FontSize.12"] = name.EndsWith("-normal", StringComparison.Ordinal) ? 12d : 18d;
		var root = (Grid)window.Content;
		root.Opacity = 1;
		root.RenderTransform = Transform.Identity;
		root.Background = window.Background;
		root.Measure(new Size(width, height));
		root.Arrange(new Rect(0, 0, width, height));
		root.UpdateLayout();
		foreach (var key in buttons)
		{
			var button = (Button)window.FindName(key);
			var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
			RegressionAssert.True(bounds.Width > 0 && bounds.Height > 0);
			RegressionAssert.True(new Rect(0, 0, width, height).Contains(bounds));
		}
		var output = Environment.GetEnvironmentVariable("REDUX_LAYOUT_PREVIEW");
		if (!String.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
			bitmap.Render(root);
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bitmap));
			using var file = File.Create(Path.Combine(output, $"{name}-{theme}.png"));
			encoder.Save(file);
		}
	}
}
