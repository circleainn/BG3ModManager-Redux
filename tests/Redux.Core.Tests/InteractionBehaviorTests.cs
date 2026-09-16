using DivinityModManager;
using DivinityModManager.AppServices;
using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Models.App;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;
using DivinityModManager.Views;

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Redux.Core.Tests;

public sealed class InteractionBehaviorTests
{
	public void ProviderPasswordFieldsFollowLoadedSettingsAndUserEdits()
	{
		var source = new DivinityModManagerSettings
		{
			NexusModsAPIKey = "initial-nexus-key",
			ModioAPIKey = "initial-modio-key"
		};
		var window = new SettingsWindow();
		var grid = (AutoGrid)window.FindName("SettingsAutoGrid");
		typeof(SettingsWindow).GetMethod(
			"CreateSettingsElements",
			BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(window, [source, typeof(DivinityModManagerSettings), grid]);

		var nexusField = grid.Children.OfType<PasswordBox>()
			.Single(field => field.Password == "initial-nexus-key");
		source.NexusModsAPIKey = "restored-nexus-key";
		RegressionAssert.Equal("restored-nexus-key", nexusField.Password);

		nexusField.Password = "replacement-nexus-key";
		RegressionAssert.Equal("replacement-nexus-key", source.NexusModsAPIKey);
		window.Close();
	}

	public void RemoteImageDiagnosticsStripCredentialsAndSignedQueries()
	{
		var converterType = typeof(SettingsWindow).Assembly.GetType(
			"DivinityModManager.Converters.UriToBitmapImageConverter")!;
		var redact = converterType.GetMethod(
			"RedactRemoteImageUri",
			BindingFlags.NonPublic | BindingFlags.Static)!;
		var result = (string)redact.Invoke(null,
			[new Uri("https://user:password@images.example.test/mod.png?token=secret#account")])!;

		RegressionAssert.Equal("https://images.example.test/mod.png", result);
	}

	public void RemoteImageLoaderDecodesWebpReturnedForNexusArtwork()
	{
		using var encoded = new System.IO.MemoryStream();
		using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(2, 2))
		{
			SixLabors.ImageSharp.ImageExtensions.SaveAsWebp(image, encoded);
		}
		encoded.Position = 0;

		var behavior = typeof(SettingsWindow).Assembly.GetType(
			"DivinityModManager.Util.RemoteImageBehavior")!;
		var decode = behavior.GetMethod("DecodeAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
		var task = (System.Threading.Tasks.Task<System.Windows.Media.Imaging.BitmapSource>)decode.Invoke(null, [encoded])!;
		var bitmap = task.GetAwaiter().GetResult();

		RegressionAssert.Equal(2, bitmap.PixelWidth);
		RegressionAssert.Equal(2, bitmap.PixelHeight);
		RegressionAssert.True(bitmap.IsFrozen);
	}

	public void ReduceMotionKeepsPrimaryListStoryboardsFreezeSafeAndInstant()
	{
		ReduxWindowBehavior.ConfigureAccessibility(false, ReduxWindowBehavior.BackgroundEffectsDisabled);
		var animation = new ReduxDoubleAnimation { From = 0, To = 0.62, Duration = TimeSpan.FromMilliseconds(120) };
		var flash = new ReduxSelectionFlashAnimation { Duration = TimeSpan.FromMilliseconds(220) };
		var storyboard = new Storyboard();
		storyboard.Children.Add(animation);
		storyboard.Children.Add(flash);
		storyboard.Freeze();
		RegressionAssert.True(storyboard.IsFrozen);

		ReduxWindowBehavior.ConfigureAccessibility(true, ReduxWindowBehavior.BackgroundEffectsDisabled);
		var opacityCore = typeof(ReduxDoubleAnimation).GetMethod(
			"GetCurrentValueCore",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Reduced-motion opacity animation core was not found.");
		var flashCore = typeof(ReduxSelectionFlashAnimation).GetMethod(
			"GetCurrentValueCore",
			BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Reduced-motion selection animation core was not found.");
		RegressionAssert.Equal(0.62, (double)opacityCore.Invoke(animation, [0d, 0d, animation.CreateClock()])!);
		RegressionAssert.Equal(0d, (double)flashCore.Invoke(flash, [0d, 0d, flash.CreateClock()])!);

		ReduxWindowBehavior.ConfigureAccessibility(false, ReduxWindowBehavior.BackgroundEffectsDisabled);
	}

	public void SaveCampaignAnimationReplacesFrozenTransforms()
	{
		var content = new Border();
		var frozen = new TranslateTransform(0, 0);
		frozen.Freeze();
		content.RenderTransform = frozen;
		var method = typeof(ReduxSaveManagerWindow).GetMethod(
			"EnsureWritableCampaignTransform",
			BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("Save campaign transform guard was not found.");
		var writable = method.Invoke(null, [content]) as TranslateTransform
			?? throw new InvalidOperationException("Save campaign transform guard returned no transform.");

		RegressionAssert.False(writable.IsFrozen);
		RegressionAssert.False(ReferenceEquals(frozen, writable));
		writable.Y = -5;
		RegressionAssert.Equal(-5d, writable.Y);
	}

	public void ModListHeaderSpansTheGutterAndScrollbarStartsBelowIt()
	{
		var resources = new ResourceDictionary
		{
			Source = new Uri(
				"pack://application:,,,/Redux;component/Themes/MainResourceDictionary.xaml",
				UriKind.Absolute)
		};
		var host = new Grid { Width = 12, Height = 260, Resources = resources };
		var scrollBar = new ScrollBar
		{
			Width = 12,
			Height = 260,
			Maximum = 100,
			ViewportSize = 20,
			Template = (ControlTemplate)resources["ReduxModListVerticalScrollBarTemplate"]
		};
		host.Children.Add(scrollBar);
		host.Measure(new Size(12, 260));
		host.Arrange(new Rect(0, 0, 12, 260));
		host.UpdateLayout();

		var headerSurface = (Border?)scrollBar.Template.FindName("ColumnHeaderSurface", scrollBar)
			?? throw new InvalidOperationException("The mod-list scrollbar header surface was not found.");
		var track = (Track?)scrollBar.Template.FindName("PART_Track", scrollBar)
			?? throw new InvalidOperationException("The mod-list scrollbar track was not found.");

		if (Math.Abs(headerSurface.ActualWidth - scrollBar.ActualWidth) >= 0.01)
			throw new InvalidOperationException($"Header width {headerSurface.ActualWidth} did not match scrollbar width {scrollBar.ActualWidth}.");
		var trackTop = track.TransformToAncestor(scrollBar).Transform(new Point()).Y;
		if (Math.Abs(trackTop - headerSurface.ActualHeight) >= 0.01)
			throw new InvalidOperationException($"Track started at {trackTop} instead of below the {headerSurface.ActualHeight}px header surface.");
	}

	public void DrawerRetainsASelectedModDuringCrossListTransferOnly()
	{
		var displayed = new DivinityModData { UUID = "moving-mod", IsSelected = true };
		var retained = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.Equal(displayed, retained);

		displayed.IsSelected = false;
		var cleared = SelectionContinuity.ResolveDisplayedItem<DivinityModData>(
			null,
			displayed,
			mod => mod.IsSelected);
		RegressionAssert.True(cleared == null);

		var replacement = new DivinityModData { UUID = "replacement" };
		var replaced = SelectionContinuity.ResolveDisplayedItem(
			replacement,
			displayed,
			_ => true);
		RegressionAssert.Equal(replacement, replaced);
	}

	public void SavingCurrentOrderCanNeverWriteTheGameExportFile()
	{
		var current = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Profiles\Public\modsettings.lsx",
			IsModSettings = true
		};
		var defensiveLsxCase = new DivinityLoadOrder
		{
			Name = "Unexpected LSX",
			FilePath = @"C:\Profiles\Public\MODSETTINGS.LSX"
		};
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json"
		};

		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(current));
		RegressionAssert.True(LoadOrderPersistencePolicy.RequiresSaveAs(defensiveLsxCase));
		RegressionAssert.False(LoadOrderPersistencePolicy.RequiresSaveAs(saved));
	}

	public void NewBlankOrderContainsNoActivatedMods()
	{
		var order = LoadOrderPersistencePolicy.CreateBlankOrder(
			"New Load Order",
			@"C:\Orders\New Load Order.json");

		RegressionAssert.Equal("New Load Order", order.Name);
		RegressionAssert.Equal(@"C:\Orders\New Load Order.json", order.FilePath);
		RegressionAssert.Equal(0, order.Order.Count);
		RegressionAssert.False(order.IsModSettings);
	}

	public void WorkingChangesStayDetachedUntilExplicitlySaved()
	{
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json",
			Order = [new DivinityLoadOrderEntry { UUID = "saved-mod" }]
		};
		var activeMods = new[]
		{
			new DivinityModData { UUID = "working-mod", Name = "Working Mod" }
		};

		var working = LoadOrderPersistencePolicy.CreateWorkingCopy(saved, activeMods);

		RegressionAssert.Equal(1, saved.Order.Count);
		RegressionAssert.Equal(1, working.Order.Count);
		RegressionAssert.Equal("saved-mod", saved.Order[0].UUID);
		RegressionAssert.Equal("working-mod", working.Order[0].UUID);
		RegressionAssert.Equal(saved.Name, working.Name);
		RegressionAssert.Equal(saved.FilePath, working.FilePath);
		RegressionAssert.False(ReferenceEquals(saved, working));
	}

	public void SavedOrdersKeepIndependentActiveSeparators()
	{
		var saved = new DivinityLoadOrder
		{
			Name = "My Order",
			FilePath = @"C:\Orders\My Order.json",
			VisualDividers =
			[
				new ModListVisualDividerData
				{
					Id = "saved-section", Title = "Saved section", IsActiveList = true,
					Position = 2, MemberModUuids = ["saved-mod"]
				}
			]
		};
		var workingDividers = new[]
		{
			new ModListVisualDividerData
			{
				Id = "working-section", Title = "Working section", IsActiveList = true,
				Position = 0, MemberModUuids = ["working-mod"]
			},
			new ModListVisualDividerData { Id = "inactive", IsActiveList = false }
		};

		var working = LoadOrderPersistencePolicy.CreateWorkingCopy(
			saved,
			Array.Empty<DivinityModData>(),
			workingDividers);

		RegressionAssert.Equal("saved-section", saved.VisualDividers.Single().Id);
		RegressionAssert.Equal("working-section", working.VisualDividers.Single().Id);
		RegressionAssert.True(working.VisualDividers.Single().IsActiveList);
		RegressionAssert.False(ReferenceEquals(saved.VisualDividers, working.VisualDividers));
	}

	public void GlobalSeparatorsKeepIndependentPerOrderPlacements()
	{
		var dividers = new[]
		{
			new ModListVisualDividerData
			{
				Id = "global", Title = "Everywhere", IsActiveList = true, IsGlobal = true
			},
			new ModListVisualDividerData
			{
				Id = "local", Title = "This order", IsActiveList = true
			}
		};

		var snapshot = LoadOrderPersistencePolicy.CloneActiveVisualDividers(dividers);
		var savedPlacement = new ModListVisualDividerData
		{
			Id = "global", Title = "Old copied title", IsActiveList = true, IsGlobal = true,
			Position = 7, IsCollapsed = true, MemberModUuids = ["order-specific-mod"]
		};
		var restored = LoadOrderPersistencePolicy.MergeGlobalDividerPlacement(dividers[0], savedPlacement);

		RegressionAssert.Equal(2, snapshot.Count);
		RegressionAssert.True(snapshot.Single(divider => divider.Id == "global").IsGlobal);
		RegressionAssert.Equal("Everywhere", restored.Title);
		RegressionAssert.Equal(7, restored.Position);
		RegressionAssert.True(restored.IsCollapsed);
		RegressionAssert.SequenceEqual(new[] { "order-specific-mod" }, restored.MemberModUuids);
	}

	public void SavedCurrentStateRestoresIntoTheSingleCurrentEntry()
	{
		var current = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Profiles\Public\modsettings.lsx",
			IsModSettings = true,
			Order = [new DivinityLoadOrderEntry { UUID = "game-order" }]
		};
		var savedState = new DivinityLoadOrder
		{
			Name = "Current",
			FilePath = @"C:\Redux\Data\CurrentOrders\profile.json",
			Order = [new DivinityLoadOrderEntry { UUID = "saved-working-order" }]
		};

		RegressionAssert.True(LoadOrderPersistencePolicy.RestoreSavedCurrentState(current, savedState));
		RegressionAssert.Equal("Current", current.Name);
		RegressionAssert.Equal(@"C:\Profiles\Public\modsettings.lsx", current.FilePath);
		RegressionAssert.True(current.IsModSettings);
		RegressionAssert.Equal(1, current.Order.Count);
		RegressionAssert.Equal("saved-working-order", current.Order[0].UUID);
	}

	public void DuplicateWandChoiceNormalizesToTheSingleVisibleIcon()
	{
		RegressionAssert.Equal("wand", ReduxIconCatalog.Normalize("wand-sparkles"));
		RegressionAssert.Equal(1, ReduxIconCatalog.Choices.Where(choice =>
			choice.Id.Contains("wand", StringComparison.OrdinalIgnoreCase)).Count());
	}

	public void CustomThemeEditorShellsPreviewTheBackgroundRoleLive()
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var theme = ReduxThemeService.CreateFromBase("Live shell preview", ReduxThemeType.ReduxDark);
		var editor = new CustomThemeEditorWindow(theme);
		var picker = new CategoryNameDialog("Background", theme.BackgroundColor, false);
		try
		{
			ReduxThemeService.Apply(picker.Resources, theme.BaseTheme, theme);
			theme.BackgroundColor = "#E409FF";
			ReduxThemeService.PreviewColors(theme, editor.Resources, picker.Resources);
			editor.Measure(new Size(editor.Width, editor.Height));
			editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
			picker.Measure(new Size(picker.Width, picker.Height));
			picker.Arrange(new Rect(0, 0, picker.Width, picker.Height));
			editor.UpdateLayout();
			picker.UpdateLayout();

			var editorShell = (Border?)editor.FindName("EditorWindowShell")
				?? throw new InvalidOperationException("The custom-theme editor window shell was not found.");
			var pickerShell = (Border?)picker.FindName("DialogWindowShell")
				?? throw new InvalidOperationException("The color-picker window shell was not found.");
			var expected = Color.FromRgb(0xE4, 0x09, 0xFF);
			RegressionAssert.Equal(expected, RequireSolidColor(editor.Background, "editor window"));
			RegressionAssert.Equal(expected, RequireSolidColor(editorShell.Background, "editor shell"));
			RegressionAssert.Equal(expected, RequireSolidColor(pickerShell.Background, "picker shell"));
		}
		finally
		{
			picker.Close();
			editor.Close();
		}

		static Color RequireSolidColor(Brush brush, string surface) =>
			brush is SolidColorBrush solid
				? solid.Color
				: throw new InvalidOperationException($"The {surface} did not resolve a solid semantic background brush.");
	}

	public void PreferencesAndEditorActionsUseModernChromeAndLabeledIcons()
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var settings = new SettingsWindow();
		var newCategory = new CategoryNameDialog("", "#8A6AF1", true);
		var existingCategory = new CategoryNameDialog("Gameplay", "#D7A24B", false);
		try
		{
			ReduxThemeService.Apply(settings.Resources, ReduxThemeType.ReduxDark);
			settings.Measure(new Size(settings.Width, settings.Height));
			settings.Arrange(new Rect(0, 0, settings.Width, settings.Height));
			settings.UpdateLayout();

			var modernTemplate = (ControlTemplate)settings.FindResource("ReduxActionButtonTemplate");
			var duplicate = (Button)settings.FindName("DuplicateCustomThemeButton");
			var delete = (Button)settings.FindName("DeleteCustomThemeButton");
			RegressionAssert.True(ReferenceEquals(modernTemplate, duplicate.Template));
			RegressionAssert.True(ReferenceEquals(settings.FindResource("ReduxAccentPillButtonTemplate"), delete.Template));
			AssertLabeledIcon(duplicate, "Duplicate");
			AssertLabeledIcon(delete, "Delete");
			RegressionAssert.Equal(
				((SolidColorBrush)settings.FindResource("ReduxErrorBrush")).Color,
				((SolidColorBrush)delete.Foreground).Color);

			var addButton = (Button)newCategory.FindName("ConfirmButton");
			var saveButton = (Button)existingCategory.FindName("ConfirmButton");
			AssertLabeledIcon(addButton, "Add");
			AssertLabeledIcon(saveButton, "Save");
			RegressionAssert.True(ReferenceEquals(
				newCategory.FindResource("Redux.Icon.AddCircle"),
				((ReduxIcon)((StackPanel)addButton.Content).Children[0]).StrokeData));
			RegressionAssert.True(ReferenceEquals(
				existingCategory.FindResource("Redux.Icon.Save"),
				((ReduxIcon)((StackPanel)saveButton.Content).Children[0]).StrokeData));
		}
		finally
		{
			existingCategory.Close();
			newCategory.Close();
			settings.Close();
		}

		static void AssertLabeledIcon(Button button, string expectedLabel)
		{
			if (button.Content is not StackPanel content)
				throw new InvalidOperationException($"The {expectedLabel} action does not retain structured icon-and-label content.");
			RegressionAssert.True(content.Children.OfType<ReduxIcon>().Any());
			RegressionAssert.True(content.Children.OfType<TextBlock>().Any(text => text.Text == expectedLabel));
		}
	}

	public void OnboardingAppearancePreviewsAndRestoresWithoutSaving()
	{
		var settings = new DivinityModManagerSettings { ShowCategoryIconsInPills = true, TextSize = ReduxTextSize.Default };
		var window = new ReduxOnboardingWindow(null!, settings);
		try
		{
			var hide = (CheckBox)window.FindName("HideIconsCheckBox");
			hide.IsChecked = true;
			hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.False(DivinityApp.ShowInterfaceIcons);
			var previewIcon = (ReduxIcon)window.FindName("TourFileIcon");
			System.Windows.Data.BindingOperations.GetBindingExpression(previewIcon, UIElement.VisibilityProperty)!.UpdateTarget();
			RegressionAssert.Equal(Visibility.Collapsed, previewIcon.Visibility);
			RegressionAssert.True(settings.ShowCategoryIconsInPills);
			RegressionAssert.False(((CheckBox)window.FindName("IconsOnlyCheckBox")).IsEnabled);
			((ComboBox)window.FindName("WelcomeTextSizeComboBox")).SelectedItem = ReduxTextSize.Large;
			RegressionAssert.True((double)window.FindResource("Redux.FontSize.12") > 12);
			var saved = new DivinityModManagerSettings();
			window.ApplyAppearanceSelection(saved);
			RegressionAssert.False(saved.ShowCategoryIconsInPills);
			RegressionAssert.Equal(ReduxTextSize.Large, saved.TextSize);
			RegressionAssert.Equal(ReduxTextSize.Default, settings.TextSize);
		}
		finally { window.Close(); }
		RegressionAssert.True(DivinityApp.ShowInterfaceIcons);
		RegressionAssert.Equal(12d, (double)window.FindResource("Redux.FontSize.12"));
	}

	public void OnboardingKeepsActionsVisibleAtItsMinimumSupportedSize()
	{
		Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		var window = new ReduxOnboardingWindow(null!, new DivinityModManagerSettings());
		try
		{
			RegressionAssert.Equal(ResizeMode.CanResize, window.ResizeMode);
			var parchment = (RadioButton)window.FindName("ParchmentThemeCard");
			parchment.IsChecked = true;
			parchment.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal("Segoe UI", window.FontFamily.Source);
			var dark = (RadioButton)window.FindName("ReduxDarkThemeCard");
			dark.IsChecked = true;
			dark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(window.FontFamily.Source, "Manrope");

			RegressionAssert.Equal(SizeToContent.Manual, window.SizeToContent);

			window.Width = window.MinWidth;
			window.Height = window.MinHeight;
			var contentRoot = (FrameworkElement)window.Content;
			contentRoot.Measure(new Size(window.MinWidth, window.MinHeight));
			contentRoot.Arrange(new Rect(0, 0, window.MinWidth, window.MinHeight));
			contentRoot.UpdateLayout();

			var contentScrollViewer = (ScrollViewer)window.FindName("OnboardingContentScrollViewer");
			var notNow = (Button)window.FindName("NotNowButton");
			var saveContinue = (Button)window.FindName("SaveContinueButton");
			if (contentScrollViewer.ActualHeight <= 0)
				throw new InvalidOperationException("The onboarding content did not receive a scrollable viewport.");
			AssertInsideWindow(notNow, contentRoot, "Not now");
			AssertInsideWindow(saveContinue, contentRoot, "Save & Continue");
			var source = (CheckBox)window.FindName("SourceIntegrationsCheckBox");
			saveContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.False(window.WasResolved);
			RegressionAssert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ConnectionsPage")).Visibility);
			source.IsChecked = true;
			((PasswordBox)window.FindName("NexusApiKeyTextBox")).Password = "test-key";
			saveContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.False(window.ApplyChanges);
			RegressionAssert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("OptionsPage")).Visibility);
			var detailsExpander = (Expander)window.FindName("BeforePlayExpander");
			detailsExpander.ApplyTemplate();
			detailsExpander.IsExpanded = true;
			var detailsPanel = (Border)detailsExpander.Template.FindName("Details", detailsExpander);
			RegressionAssert.Equal(Visibility.Visible, detailsPanel.Visibility);
			RegressionAssert.True(Double.IsNaN(detailsPanel.Height));
			detailsExpander.IsExpanded = false;
			RegressionAssert.Equal(Visibility.Collapsed, detailsPanel.Visibility);
			RegressionAssert.Equal(0d, detailsPanel.Height);

			var demo = (Button)window.FindName("DemoActionButton");
			demo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal("1  Example mod", ((TextBlock)window.FindName("DemoActiveText")).Text);
			demo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("DemoInstructionText")).Text, "Sync applies your saved order");
			demo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("DemoInstructionText")).Text, "Ready to play");
			RegressionAssert.False(window.ApplyChanges);

			saveContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RegressionAssert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("OrganizePage")).Visibility);
            RegressionAssert.False(window.AddStarterSeparators);
            RegressionAssert.Equal(9, window.SelectedStarterSeparators.Count);
            ((CheckBox)window.FindName("StarterSeparatorsCheckBox")).IsChecked = true;
            var starter = (System.Windows.Controls.Primitives.UniformGrid)window.FindName("StarterSeparatorsPreview");
            ((CheckBox)starter.Children[0]).IsChecked = false;
            RegressionAssert.Equal(8, window.SelectedStarterSeparators.Count);
            RegressionAssert.True(window.AddStarterSeparators);
            saveContinue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ManagersPage")).Visibility);
			var tour = (Button)window.FindName("TourActionButton");
			tour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			tour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("TourResult")).Text, "Save added");
			((Button)window.FindName("NativeTourButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("TourResult")).Text, "Ready");
			tour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("TourResult")).Text, "Review");
			tour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Contains(((TextBlock)window.FindName("TourResult")).Text, "game folder");
			RegressionAssert.False(window.ApplyChanges);
			((Button)window.FindName("BackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			RegressionAssert.Equal("test-key", window.SelectedNexusApiKey);
			RegressionAssert.False(window.SelectedLocalOnlyMode);
			contentRoot.UpdateLayout();
			AssertInsideWindow(saveContinue, contentRoot, "Next");
			AssertInsideWindow((Button)window.FindName("BackButton"), contentRoot, "Back");

		}
		finally
		{
			window.Close();
		}

		static void AssertInsideWindow(FrameworkElement element, FrameworkElement windowContent, string name)
		{
			RegressionAssert.Equal(Visibility.Visible, element.Visibility);
			if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
				throw new InvalidOperationException($"The onboarding '{name}' action was not arranged.");
			var topLeft = element.TranslatePoint(new Point(0, 0), windowContent);
			var bottomRight = element.TranslatePoint(new Point(element.ActualWidth, element.ActualHeight), windowContent);
			if (topLeft.X < 0 || topLeft.Y < 0 || bottomRight.X > windowContent.ActualWidth || bottomRight.Y > windowContent.ActualHeight)
			{
				throw new InvalidOperationException(
					$"The onboarding '{name}' action was outside the {windowContent.ActualWidth}x{windowContent.ActualHeight} content: {topLeft} to {bottomRight}.");
			}
		}
	}

	public void PopupPlacementPrefersRightwardGrowthWithScreenEdgeFallbacks()
	{
		var below = ReduxWindowBehavior.GetBelowRightwardPlacements(
			new Size(240, 180),
			new Size(80, 32),
			new Point(0, 4));
		RegressionAssert.Equal(new Point(0, 36), below[0].Point);
		RegressionAssert.Equal(new Point(-160, 36), below[1].Point);
		RegressionAssert.Equal(new Point(0, -184), below[2].Point);
		RegressionAssert.Equal(new Point(-160, -184), below[3].Point);

		var beside = ReduxWindowBehavior.GetBesideRightwardPlacements(
			new Size(240, 180),
			new Size(80, 32),
			new Point(0, 0));
		RegressionAssert.Equal(new Point(80, 0), beside[0].Point);
		RegressionAssert.Equal(new Point(-240, 0), beside[1].Point);

		var popup = new Popup();
		ReduxWindowBehavior.SetPreferredPopupPlacement(
			popup,
			ReduxPreferredPopupPlacement.BelowRightward);
		RegressionAssert.Equal(PlacementMode.Custom, popup.Placement);
		RegressionAssert.True(popup.CustomPopupPlacementCallback != null);
	}

	public void MessageBoxSupportsExplicitElevationWarningActions()
	{
		var window = new ReduxMessageBoxWindow(null!, "Elevation warning", "Redux Is Running as Administrator",
			MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
		try
		{
			window.SetButtonLabel(MessageBoxResult.Yes, "Don't show again");
			window.SetButtonLabel(MessageBoxResult.No, "Close");

			RegressionAssert.Equal("Don't show again", ((TextBlock)window.FindName("YesButtonLabel")).Text);
			RegressionAssert.Equal("Close", ((TextBlock)window.FindName("NoButtonLabel")).Text);
			RegressionAssert.True(((Button)window.FindName("NoButton")).IsDefault);
			RegressionAssert.False(((Button)window.FindName("YesButton")).IsDefault);
		}
		finally
		{
			window.Close();
		}
	}

	public void BuiltInIconPickerHasAUniqueExpandedCatalog()
	{
		var choices = ReduxIconCatalog.Choices.Where(choice => !choice.IsNone).ToList();

		RegressionAssert.Equal(choices.Count, choices.Select(choice => choice.Id)
			.Distinct(StringComparer.OrdinalIgnoreCase).Count());
		RegressionAssert.Equal(choices.Count, choices.Select(choice => choice.ResourceKey)
			.Distinct(StringComparer.OrdinalIgnoreCase).Count());
		RegressionAssert.True(choices.Count >= 110);
		RegressionAssert.True(ReduxIconCatalog.TryGet("backpack", out _));
		RegressionAssert.True(ReduxIconCatalog.TryGet("languages", out _));
		RegressionAssert.True(ReduxIconCatalog.TryGet("workflow", out _));
		RegressionAssert.True(ReduxIconCatalog.TryGet("redux-star", out var reduxStar));
		RegressionAssert.Equal("Redux.Icon.ReduxStar", reduxStar.ResourceKey);
	}

	public void AsyncProviderMetadataSignalsAutomaticCategoryRefresh()
	{
		var mod = new DivinityModData { UUID = "metadata-refresh" };
		var initialRevision = mod.CategoryMetadataRevision;

		mod.ModioData.Update(new ModioModData
		{
			UUID = mod.UUID,
			ModId = 42,
			Name = "Interface Improvements"
		});

		RegressionAssert.True(mod.CategoryMetadataRevision > initialRevision);
	}

	public void MenuSemanticColorDistinguishesSavingFromSaveNavigation()
	{
		RegressionAssert.True(ReduxMenuItemExtension.IsPositiveCommitAction("Save Current Order"));
		RegressionAssert.True(ReduxMenuItemExtension.IsPositiveCommitAction("Export Redux Modlist..."));
		RegressionAssert.False(ReduxMenuItemExtension.IsPositiveCommitAction("Save Game Manager..."));
		RegressionAssert.False(ReduxMenuItemExtension.IsPositiveCommitAction("Save Games Folder"));
	}

	public void CommandTooltipUsesLiveShortcutAndSharedDescription()
	{
		var hotkey = new Hotkey(System.Windows.Input.Key.S, System.Windows.Input.ModifierKeys.Control)
		{
			DisplayName = "Save Current Order",
			Description = "Save changes to the selected load order."
		};

		RegressionAssert.Equal(
			"Save Current Order (Ctrl + S)\nSave changes to the selected load order.",
			hotkey.CommandToolTip);

		hotkey.Clear();
		RegressionAssert.Equal(
			"Save Current Order\nSave changes to the selected load order.",
			hotkey.CommandToolTip);
	}

	public void CommandPaletteItemTemplateResolvesCoreBindingsAtRuntime()
	{
		var originalInteractionColors = DivinityApp.UseCategoryColorsForInteractions;
		DivinityApp.UseCategoryColorsForInteractions = true;
		var window = new ReduxCommandPaletteWindow(null!, null!, null!);
		try
		{
			var errorInteractionBrush = new SolidColorBrush(Colors.Red);
			var primaryTextBrush = new SolidColorBrush(Colors.White);
			window.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
			window.Resources["ReduxErrorBrush"] = errorInteractionBrush;
			window.Resources["ReduxTextPrimaryBrush"] = primaryTextBrush;
			var list = (ListBox)window.FindName("CommandList");
			list.ItemsSource = new[]
			{
				new ReduxCommandPaletteItem(
					"Delete Selected Mods...",
					"Mod lists",
					"Delete selected mods.",
					"Delete",
					"trash",
					() => { },
					tone: ReduxCommandPaletteTone.Error),
				new ReduxCommandPaletteItem(
					"Filter category: Gameplay",
					"Category filters",
					"Show mods assigned to this category.",
					String.Empty,
					"gameplay",
					() => { },
					accentColor: "#D7A24B")
			};
			window.Measure(new Size(620, 530));
			window.Arrange(new Rect(0, 0, 620, 530));
			window.UpdateLayout();
			RegressionAssert.Equal(2, list.Items.Count);

			(ListBoxItem Item, Border Surface) CreateSelectedSurface(ReduxCommandPaletteItem data)
			{
				var item = new ListBoxItem
				{
					DataContext = data,
					IsSelected = true,
					Style = (Style)window.FindResource("CommandPaletteItemStyle")
				};
				item.Resources["ReduxErrorPillBackground"] = errorInteractionBrush;
				item.Resources["ReduxErrorBrush"] = errorInteractionBrush;
				item.Resources["ReduxTextPrimaryBrush"] = primaryTextBrush;
				item.Resources["Redux.Rail.Thickness"] = new Thickness(3, 0, 0, 0);
				item.ApplyTemplate();
				item.Measure(new Size(560, 60));
				item.Arrange(new Rect(0, 0, 560, 60));
				item.UpdateLayout();
				return (item, (Border)item.Template.FindName("ContextualSelectionSurface", item));
			}

			var errorSelection = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[0]);
			var errorSurface = errorSelection.Surface;
			if (errorSurface.Background is not SolidColorBrush errorBrush
				|| errorBrush.Color != Colors.Red
				|| errorSurface.Opacity != 1)
				throw new InvalidOperationException($"Semantic selection brush did not resolve; received {errorSurface.Background?.GetType().Name ?? "null"} " +
					$"{(errorSurface.Background as SolidColorBrush)?.Color} at opacity {errorSurface.Opacity}; " +
					$"tone {(errorSurface.DataContext as ReduxCommandPaletteItem)?.Tone}, interactions {DivinityApp.UseCategoryColorsForInteractions}.");

			var selectionRail = (Border)errorSelection.Item.Template.FindName("SelectionRail", errorSelection.Item);
			var hoverRail = (Border)errorSelection.Item.Template.FindName("HoverRail", errorSelection.Item);
			var gesturePill = (Border)errorSelection.Item.Template.FindName("GesturePill", errorSelection.Item);
			var gestureText = (TextBlock)errorSelection.Item.Template.FindName("GestureText", errorSelection.Item);
			RegressionAssert.Equal(1d, selectionRail.Opacity);
			RegressionAssert.Equal(new Thickness(3, 0, 0, 0), selectionRail.BorderThickness);
			RegressionAssert.Equal(Colors.Red, ((SolidColorBrush)selectionRail.BorderBrush).Color);
			RegressionAssert.Equal(Colors.Transparent, ((SolidColorBrush)hoverRail.BorderBrush).Color);
			RegressionAssert.Equal(Colors.Transparent, ((SolidColorBrush)gesturePill.BorderBrush).Color);
			RegressionAssert.Equal(Colors.White, ((SolidColorBrush)gestureText.Foreground).Color);

			var categorySurface = CreateSelectedSurface((ReduxCommandPaletteItem)list.Items[1]).Surface;
			if (categorySurface.Background is not LinearGradientBrush categoryBrush
				|| categoryBrush.GradientStops.Count != 2
				|| !categoryBrush.GradientStops.All(stop => stop.Color.R == 0xD7
					&& stop.Color.G == 0xA2
					&& stop.Color.B == 0x4B)
				|| categorySurface.Opacity != 1)
				throw new InvalidOperationException($"Category selection brush did not resolve; received {categorySurface.Background?.GetType().Name ?? "null"}.");
		}
		finally
		{
			window.Close();
			DivinityApp.UseCategoryColorsForInteractions = originalInteractionColors;
		}
	}

	public void ReduxDialogTemplatesResolveCoreBindingsAtRuntime()
	{
		var nexusDownloads = new ReduxNexusDownloadsWindow();
		ReduxThemeService.Apply(nexusDownloads.Resources, ReduxThemeType.ReduxDark);
		var installReview = new ReduxInstallReviewWindow(null!,
			[new ReduxInstallReviewItem("Clean package", "Version 1.0", "New mod", ReduxInstallReviewTone.Success)],
			false, "Inactive Mods", "Destination: Inactive Mods · 1 new", true);
		RegressionAssert.Equal(Visibility.Visible,
			((CheckBox)installReview.FindName("SkipCleanReviewCheckBox")).Visibility);
		var downloadsList = (ListBox)nexusDownloads.FindName("DownloadsList");
		downloadsList.ItemsSource = new[]
		{
			new NxmDownloadItem
			{
				ProjectName = "Runtime template check",
				FileName = "runtime-template-check.zip",
				State = NxmDownloadState.Downloaded,
				ThumbnailUrl = ""
			}
		};
		var deleteFiles = new DeleteFilesConfirmationView(null);
		ReduxThemeService.Apply(deleteFiles.Resources, ReduxThemeType.ReduxDark);
		deleteFiles.ViewModel.Files.Add(new ModFileDeletionData
		{
			DisplayName = "Runtime template check",
			FilePath = @"C:\Mods\runtime-template-check.pak",
			UUID = "runtime-template-check",
			IsSelected = true
		});

		var windows = new Window[]
		{
			new ReduxSaveManagerWindow(null!, null!),
			new ReduxFileOverlapWindow(null!, Array.Empty<DivinityModData>()),
			new ReduxExportReviewWindow(null!, new ReduxExportReviewData(
				"Current",
				"Public",
				null!,
				0,
				0,
				0,
				0)),
			nexusDownloads,
			installReview,
			deleteFiles
		};

		try
		{
			foreach (var window in windows)
			{
				window.Measure(new Size(860, 700));
				window.Arrange(new Rect(0, 0, 860, 700));
				window.UpdateLayout();
			}

			var sharedWindowTemplate = (ControlTemplate)nexusDownloads.FindResource("ReduxWindowTemplate");
			var sharedWindowTemplateRoot = (FrameworkElement)sharedWindowTemplate.LoadContent();
			var resizeGlow = sharedWindowTemplateRoot.FindName("SharedResizeGlow") as Border;
			if (resizeGlow == null) throw new InvalidOperationException("The shared Redux window template did not create its resize feedback surface.");
			if (!ReduxWindowBehavior.SupportsResizeFeedback(nexusDownloads)) throw new InvalidOperationException("A resizable Redux window was not eligible for shared resize feedback.");
			if (ReduxWindowBehavior.SupportsResizeFeedback(new Window { ResizeMode = ResizeMode.NoResize })) throw new InvalidOperationException("A non-resizable window was eligible for shared resize feedback.");
			if (!ReduxWindowBehavior.SupportsMoveFeedback(new Window { ResizeMode = ResizeMode.NoResize })) throw new InvalidOperationException("A non-resizable modal was not eligible for shared move feedback.");
			RegressionAssert.Equal(0d, resizeGlow.Opacity);
			RegressionAssert.Equal(
				new Thickness(3),
				(Thickness)nexusDownloads.FindResource("Redux.WindowInteraction.BorderThickness"));

			var deleteButton = (Button)deleteFiles.FindName("DeleteActionButton");
			var deleteIcon = (ReduxIcon)deleteFiles.FindName("DeleteActionIcon");
			RegressionAssert.True(deleteButton.IsEnabled);
			RegressionAssert.Equal(
				((SolidColorBrush)deleteFiles.FindResource("ReduxErrorBrush")).Color,
				((SolidColorBrush)deleteIcon.Foreground).Color);

			var installAllButton = (Button)nexusDownloads.FindName("InstallAllButton");
			var installAllIcon = (ReduxIcon)nexusDownloads.FindName("InstallAllIcon");
			var clearArchivesButton = (Button)nexusDownloads.FindName("ClearArchivesButton");
			var clearArchivesIcon = (ReduxIcon)nexusDownloads.FindName("ClearArchivesIcon");
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxSuccessBrush")).Color,
				((SolidColorBrush)installAllButton.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)installAllButton.Foreground).Color,
				((SolidColorBrush)installAllIcon.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxErrorBrush")).Color,
				((SolidColorBrush)clearArchivesButton.Foreground).Color);
			RegressionAssert.Equal(
				((SolidColorBrush)clearArchivesButton.Foreground).Color,
				((SolidColorBrush)clearArchivesIcon.Foreground).Color);
			installAllButton.IsEnabled = false;
			RegressionAssert.Equal(
				((SolidColorBrush)nexusDownloads.FindResource("ReduxSuccessBrush")).Color,
				((SolidColorBrush)installAllIcon.Foreground).Color);
		}
		finally
		{
			foreach (var window in windows)
			{
				window.Close();
			}
		}
	}

	public void KeyboardShortcutGroupsAvoidVirtualizedContainerRecycling()
	{
		var window = new SettingsWindow();
		try
		{
			var list = (ListView)window.FindName("KeybindingsListView");
			RegressionAssert.False(VirtualizingPanel.GetIsVirtualizing(list));
			RegressionAssert.False(VirtualizingPanel.GetIsVirtualizingWhenGrouping(list));
		}
		finally
		{
			window.Close();
		}
	}

	public void ThemeCyclingIncludesValidCustomThemesInSavedOrder()
	{
		var settings = new DivinityModManagerSettings
		{
			ColorTheme = ReduxThemeType.Parchment
		};
		var first = ReduxThemeService.CreateFromBase("First", ReduxThemeType.ReduxDark);
		var invalid = ReduxThemeService.CreateFromBase("Invalid", ReduxThemeType.ReduxLight);
		invalid.Name = String.Empty;
		var second = ReduxThemeService.CreateFromBase("Second", ReduxThemeType.ReduxLight);
		settings.CustomThemes.Add(first);
		settings.CustomThemes.Add(invalid);
		settings.CustomThemes.Add(second);

		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(first.Id, settings.ActiveCustomThemeId);
		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(second.Id, settings.ActiveCustomThemeId);
		ReduxThemeService.CycleTheme(settings);
		RegressionAssert.Equal(String.Empty, settings.ActiveCustomThemeId);
		RegressionAssert.Equal(ReduxThemeType.ReduxDark, settings.ColorTheme);
	}
}
