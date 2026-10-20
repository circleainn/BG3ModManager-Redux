using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;

using WpfScreenHelper;

namespace DivinityModManager.Views;

public partial class ReduxOnboardingWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindow _ownerWindow;
	private readonly DivinityModManagerSettings _settings;
	private readonly ReduxThemeType _initialTheme;
	private readonly ReduxCustomTheme _initialCustomTheme;
	private readonly (ReduxTypographyFont Font, string CustomReference) _initialTypography;
	private readonly bool _initialLocalOnlyMode;
	private readonly bool _initialGuidanceEnabled;
	private readonly bool _initialReduceMotion;
	private readonly bool _initialDisableBackgroundEffects;
	private bool _themePreviewActive;
	private bool _modulePreviewActive;
	private bool _accessibilityPreviewActive;
	private bool _nxmAssociationChoiceAvailable = true;
	private bool _isInitializing = true;
	private bool _appearanceEdited;
	private bool _updatingAppearance;
	private bool _fontChoiceChanged;
	private bool _gradientChoiceChanged;
	private ReduxTextSize _initialTextSize;
	private bool _initialShowIcons;
	private bool _initialIconsOnly;
	private int _step;
	private int _demoStep;
	private readonly Dictionary<Expander, int> _detailsAnimationVersions = new();
	public string SelectedGameExecutablePath { get; private set; }
	public bool AddStarterSeparators => StarterSeparatorsCheckBox.IsChecked == true;
    public IReadOnlyList<string> SelectedStarterSeparators => StarterSeparatorsPreview.Children.OfType<CheckBox>()
        .Where(choice => choice.IsChecked == true).Select(choice => (string)choice.Tag).ToArray();
    public bool OpenDownloadsAfterSetup => OpenDownloadsCheckBox.IsChecked == true;
	public bool WasResolved { get; private set; }
	public bool ApplyChanges { get; private set; }
	public bool ThemeSelectionChanged => _themePreviewActive;
	public ReduxThemeType SelectedTheme => !_themePreviewActive ? _initialTheme : ReduxDarkThemeCard.IsChecked == true
		? ReduxThemeType.ReduxDark
		: ReduxLightThemeCard.IsChecked == true
			? ReduxThemeType.ReduxLight
			: ReduxThemeType.Parchment;
	public bool SelectedLocalOnlyMode => SourceIntegrationsCheckBox.IsChecked != true;
	public bool SelectedGuidanceEnabled => GuidanceCheckBox.IsChecked == true;
	public bool SelectedReduceMotion => ReduceMotionCheckBox.IsChecked == true;
	public bool SelectedDisableBackgroundEffects => DisableBackgroundEffectsCheckBox.IsChecked == true;
	public string SelectedNexusApiKey => NexusApiKeyTextBox.Password?.Trim() ?? String.Empty;
	public string SelectedModioApiKey => ModioApiKeyTextBox.Password?.Trim() ?? String.Empty;
	public bool SelectedNxmAssociationEnabled => NxmLinksCheckBox.IsChecked == true;
	public bool SelectedRetainInstalledPackageArchives => RetainArchivesCheckBox.IsChecked == true;

	public ReduxOnboardingWindow(Window owner, DivinityModManagerSettings settings)
	{
		InitializeComponent();
		if (ReduxRuntimeEnvironment.IsWine)
		{
			NxmLinksLabel.Text = NxmAssociationPresentation.GetToggleLabel(isWine: true);
			NxmLinksCheckBox.ToolTip = NxmAssociationPresentation.WineScopeNote;
		}
		foreach (var title in ReduxOnboardingPolicy.StarterSeparatorTitles)
        {
            var choice = new CheckBox { Tag = title, IsChecked = true, Margin = new Thickness(0, 8, 12, 8),
                Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap } };
            choice.SetResourceReference(StyleProperty, "ReduxCheckBoxStyle");
            StarterSeparatorsPreview.Children.Add(choice);
        }
        ReduxWindowBehavior.AttachDialogTransitions(this, 30);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		_ownerWindow = owner as MainWindow;
		_settings = settings;
		SelectedGameExecutablePath = settings?.GameExecutablePath ?? String.Empty;
		UpdateGameStatus();
		_initialTheme = settings?.ColorTheme ?? ReduxThemeType.ReduxDark;
		_initialCustomTheme = ReduxThemeService.GetActiveTheme(settings);
		_initialTypography = ReduxTypographyService.ResolveSelection(settings);
		_initialLocalOnlyMode = settings?.LocalOnlyMode == true;
		_initialGuidanceEnabled = settings?.EnableLoadOrderAdvisor == true;
		_initialReduceMotion = settings?.ReduceMotion == true;
		_initialDisableBackgroundEffects = settings?.DisableBackgroundEffects == true;
		ApplyAdaptiveDefaultSize(owner);
		Loaded += (_, _) => Dispatcher.BeginInvoke(ClampToCurrentWorkArea, DispatcherPriority.Loaded);

		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
			ReduxDarkThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxDark;
			ReduxLightThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxLight;
			ParchmentThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.Parchment;
			SourceIntegrationsCheckBox.IsChecked = !settings.LocalOnlyMode;
			GuidanceCheckBox.IsChecked = settings.EnableLoadOrderAdvisor;
			NexusApiKeyTextBox.Password = settings.NexusModsAPIKey ?? String.Empty;
			ModioApiKeyTextBox.Password = settings.ModioAPIKey ?? String.Empty;
			ReduceMotionCheckBox.IsChecked = settings.ReduceMotion;
			DisableBackgroundEffectsCheckBox.IsChecked = settings.DisableBackgroundEffects;
			var nxmStatus = _ownerWindow?.ViewModel?.GetNxmAssociationStatus();
			NxmLinksCheckBox.IsChecked = nxmStatus?.Status is NxmAssociationStatus.Owned or NxmAssociationStatus.NeedsRepair;
			RetainArchivesCheckBox.IsChecked = settings.RetainInstalledPackageArchives;
			_nxmAssociationChoiceAvailable = nxmStatus?.Success != false;
			NxmLinksCheckBox.ToolTip = _nxmAssociationChoiceAvailable
				? nxmStatus?.Status == NxmAssociationStatus.OwnedByAnotherHandler
					? "Choose Redux, then confirm replacing the current Nexus link handler."
					: nxmStatus?.Message
				: nxmStatus?.Message;
			if (ReduxRuntimeEnvironment.IsWine)
				NxmLinksCheckBox.ToolTip = nxmStatus == null
					? NxmAssociationPresentation.WineScopeNote
					: NxmAssociationPresentation.GetDetail(nxmStatus, isWine: true);
		}
		else
		{
			ReduxDarkThemeCard.IsChecked = true;
			SourceIntegrationsCheckBox.IsChecked = false;
			GuidanceCheckBox.IsChecked = false;
			RetainArchivesCheckBox.IsChecked = false;
		}

		_initialTextSize = _initialCustomTheme?.TextSize ?? settings?.TextSize ?? ReduxTextSize.Default;
		_initialShowIcons = _initialCustomTheme?.ShowCategoryIconsInPills ?? settings?.ShowCategoryIconsInPills ?? true;
		_initialIconsOnly = _initialCustomTheme?.UseIconsOnly ?? settings?.UseIconsOnly ?? false;
		HideIconsCheckBox.IsChecked = !_initialShowIcons;
		IconsOnlyCheckBox.IsChecked = _initialIconsOnly;
		IconsOnlyCheckBox.IsEnabled = _initialShowIcons;
		CategorySelectionCheckBox.IsChecked = _initialCustomTheme?.UseCategoryColorsForInteractions ?? settings?.UseCategoryColorsForInteractions ?? false;
		CategoryTextCheckBox.IsChecked = _initialCustomTheme?.UseCategoryColorsForSidebarText ?? settings?.UseCategoryColorsForSidebarText ?? false;
		GradientsCheckBox.IsChecked = _initialCustomTheme?.UsesGeneratedGradients ?? settings?.UsesGeneratedGradients ?? true;
		WelcomeFontComboBox.DisplayMemberPath = "Name";
		WelcomeFontComboBox.ItemsSource = ReduxCustomFontService.GetChoices();
		SelectWelcomeFont(_initialTypography.Font, _initialTypography.CustomReference);
		WelcomeTextSizeComboBox.ItemsSource = Enum.GetValues<ReduxTextSize>();
		WelcomeTextSizeComboBox.SelectedItem = _initialTextSize;
		_isInitializing = false;
		UpdateSourceIntegrationState();
		ShowStep(0);
	}

	private void ApplyAdaptiveDefaultSize(Window owner)
	{
		var workArea = owner != null ? Screen.FromWindow(owner).WorkingArea : SystemParameters.WorkArea;
		const double workAreaMargin = 48;
		var availableWidth = Math.Max(320, workArea.Width - workAreaMargin);
		var availableHeight = Math.Max(320, workArea.Height - workAreaMargin);

		// Keep the footer in the arranged window instead of relying on SizeToContent to
		// clip a fixed-size dialog. On constrained/scaled displays the body ScrollViewer
		// absorbs the reduction while the actions remain visible and keyboard reachable.
		MinWidth = Math.Min(MinWidth, availableWidth);
		MinHeight = Math.Min(MinHeight, availableHeight);
		MaxWidth = Math.Max(MinWidth, Math.Min(MaxWidth, availableWidth));
		MaxHeight = Math.Max(MinHeight, Math.Min(MaxHeight, availableHeight));
		Width = Math.Clamp(Math.Clamp(workArea.Width * 0.44, 700, 780), MinWidth, MaxWidth);
		Height = Math.Clamp(Math.Clamp(workArea.Height * 0.82, 640, 740), MinHeight, MaxHeight);
		SizeToContent = SizeToContent.Manual;
	}

	private void ClampToCurrentWorkArea()
	{
		var workArea = Screen.FromWindow(this).WorkingArea;
		var renderedWidth = ActualWidth > 0 ? ActualWidth : Width;
		var renderedHeight = ActualHeight > 0 ? ActualHeight : Height;
		Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - renderedWidth));
		Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - renderedHeight));
	}


	private void SelectWelcomeFont(ReduxTypographyFont font, string reference)
	{
		_updatingAppearance = true;
		WelcomeFontComboBox.SelectedItem = WelcomeFontComboBox.Items.Cast<ReduxFontChoice>().FirstOrDefault(c =>
			String.IsNullOrEmpty(reference) ? !c.IsCustom && c.BuiltInFont == font : c.CustomReference == reference);
		_updatingAppearance = false;
	}

	private ReduxCustomTheme SelectedAppearance()
	{
		var result = (!_themePreviewActive ? _initialCustomTheme?.Clone() : null)
			?? ReduxThemeService.CreateFromBase("Preview", SelectedTheme);
		result.ShowCategoryIconsInPills = HideIconsCheckBox.IsChecked != true;
		result.UseIconsOnly = result.ShowCategoryIconsInPills && IconsOnlyCheckBox.IsChecked == true;
		result.UseCategoryColorsForInteractions = CategorySelectionCheckBox.IsChecked == true;
		result.UseCategoryColorsForSidebarText = CategoryTextCheckBox.IsChecked == true;
		result.UsesGeneratedGradients = GradientsCheckBox.IsChecked == true;
		result.TextSize = WelcomeTextSizeComboBox.SelectedItem is ReduxTextSize size ? size : _initialTextSize;
		if (WelcomeFontComboBox.SelectedItem is ReduxFontChoice font)
		{
			result.TypographyFont = font.BuiltInFont;
			result.CustomTypographyFont = font.IsCustom ? font.CustomReference : String.Empty;
		}
		return result;
	}

	private void AppearanceOption_Click(object sender, RoutedEventArgs e)
	{
		if (_isInitializing || _updatingAppearance) return;
		if (sender == GradientsCheckBox) _gradientChoiceChanged = true;
		if (HideIconsCheckBox.IsChecked == true) IconsOnlyCheckBox.IsChecked = false;
		IconsOnlyCheckBox.IsEnabled = HideIconsCheckBox.IsChecked != true;
		_appearanceEdited = true;
		PreviewAppearance();
	}

	private void AppearanceSelection_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_isInitializing || _updatingAppearance) return;
		if (sender == WelcomeFontComboBox) _fontChoiceChanged = true;
		_appearanceEdited = true;
		PreviewAppearance();
	}

	private void PreviewAppearance()
	{
		var preview = SelectedAppearance();
		ReduxThemeService.Apply(Resources, SelectedTheme, preview);
		_ownerWindow?.PreviewColorTheme(SelectedTheme, preview);
		_ownerWindow?.ViewModel?.PreviewModPresentation(preview);
		DivinityApp.ShowInterfaceIcons = preview.ShowCategoryIconsInPills;
		DivinityApp.UseIconsOnly = preview.UseIconsOnly;
		ReduxTypographyService.Apply(Resources, preview.TypographyFont, preview.CustomTypographyFont);
		ReduxTypographyService.ApplyTextSize(Resources, preview.TextSize);
		if (Application.Current != null) ReduxTypographyService.ApplyTextSize(Application.Current.Resources, preview.TextSize);
	}

	public void ApplyAppearanceSelection(DivinityModManagerSettings settings)
	{
		if (!_appearanceEdited) return;
		var selected = SelectedAppearance();
		settings.ShowCategoryIconsInPills = selected.ShowCategoryIconsInPills;
		settings.UseIconsOnly = selected.UseIconsOnly;
		settings.UseCategoryColorsForInteractions = selected.UseCategoryColorsForInteractions;
		settings.UseCategoryColorsForSidebarText = selected.UseCategoryColorsForSidebarText;
		settings.UsesGeneratedGradients = selected.UsesGeneratedGradients;
		settings.TextSize = selected.TextSize;
		if (_fontChoiceChanged)
		{
			settings.UseThemeDefaultTypography = false;
			settings.TypographyFont = selected.TypographyFont;
			settings.CustomTypographyFont = selected.CustomTypographyFont;
		}
		var custom = ReduxThemeService.GetActiveTheme(settings);
		if (custom != null)
		{
			custom.ShowCategoryIconsInPills = selected.ShowCategoryIconsInPills;
			custom.UseIconsOnly = selected.UseIconsOnly;
			custom.UseCategoryColorsForInteractions = selected.UseCategoryColorsForInteractions;
			custom.UseCategoryColorsForSidebarText = selected.UseCategoryColorsForSidebarText;
			custom.UsesGeneratedGradients = selected.UsesGeneratedGradients;
			custom.TypographyFont = selected.TypographyFont;
			custom.CustomTypographyFont = selected.CustomTypographyFont;
			custom.TextSize = selected.TextSize;
		}
	}

	private void ThemeCard_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: ReduxThemeType theme })
		{
			ReduxThemeService.Apply(Resources, theme);
			_ownerWindow?.PreviewColorTheme(theme);
			var typography = _settings?.UseThemeDefaultTypography != false
				? (ReduxTypographyService.GetThemeDefault(theme), String.Empty)
				: (_settings.TypographyFont, _settings.CustomTypographyFont);
			ReduxTypographyService.Apply(Resources, typography.Item1, typography.Item2);
			_themePreviewActive = true;
			_appearanceEdited = true;
			if (!_gradientChoiceChanged) GradientsCheckBox.IsChecked = theme != ReduxThemeType.Parchment;
			if (!_fontChoiceChanged && _settings?.UseThemeDefaultTypography != false)
				SelectWelcomeFont(ReduxTypographyService.GetThemeDefault(theme), String.Empty);
			PreviewAppearance();
		}
	}

	private void SourceIntegrationsCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isInitializing)
		{
			UpdateSourceIntegrationState();
			ApplyModulePreview();
		}
	}

	private void UpdateSourceIntegrationState()
	{
		if (SourceCredentialsPanel == null)
		{
			return;
		}

		var showCredentials = SourceIntegrationsCheckBox.IsChecked == true;
		NxmLinksCheckBox.IsEnabled = showCredentials && _nxmAssociationChoiceAvailable;
		if (!IsLoaded || ReduxWindowBehavior.ReduceMotion)
		{
			SourceCredentialsPanel.BeginAnimation(FrameworkElement.HeightProperty, null);
			SourceCredentialsPanel.BeginAnimation(UIElement.OpacityProperty, null);
			SourceCredentialsPanel.ClearValue(FrameworkElement.HeightProperty);
			SourceCredentialsPanel.Opacity = 1;
			SourceCredentialsPanel.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
			return;
		}

		AnimateSourceCredentials(showCredentials);
	}

	private void AnimateSourceCredentials(bool show)
	{
		var panel = SourceCredentialsPanel;
		panel.BeginAnimation(FrameworkElement.HeightProperty, null);
		panel.BeginAnimation(UIElement.OpacityProperty, null);

		if (show)
		{
			panel.Visibility = Visibility.Visible;
			panel.ClearValue(FrameworkElement.HeightProperty);
			var measureWidth = panel.ActualWidth;
			if (measureWidth <= 1 && panel.Parent is FrameworkElement parent)
			{
				measureWidth = parent.ActualWidth;
			}
			if (measureWidth <= 1)
			{
				measureWidth = Math.Max(1, ActualWidth - 72);
			}
			panel.Measure(new Size(measureWidth, Double.PositiveInfinity));
			var targetHeight = Math.Max(1, panel.DesiredSize.Height);
			panel.Height = 0;
			panel.Opacity = 0;
			AnimateSourceCredentialsTo(panel, targetHeight, 1, collapseWhenComplete: false);
			return;
		}

		var currentHeight = Math.Max(1, panel.ActualHeight);
		panel.Height = currentHeight;
		panel.Opacity = 1;
		AnimateSourceCredentialsTo(panel, 0, 0, collapseWhenComplete: true);
	}

	private static void AnimateSourceCredentialsTo(
		FrameworkElement panel,
		double targetHeight,
		double targetOpacity,
		bool collapseWhenComplete)
	{
		var duration = TimeSpan.FromMilliseconds(180);
		var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
		var heightAnimation = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
		var opacityAnimation = new DoubleAnimation(targetOpacity, duration) { EasingFunction = easing };

		heightAnimation.Completed += (_, _) =>
		{
			panel.BeginAnimation(FrameworkElement.HeightProperty, null);
			panel.BeginAnimation(UIElement.OpacityProperty, null);
			panel.ClearValue(FrameworkElement.HeightProperty);
			panel.Opacity = 1;
			if (collapseWhenComplete)
			{
				panel.Visibility = Visibility.Collapsed;
			}
		};

		panel.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
		panel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
	}

	private void GuidanceCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isInitializing)
		{
			ApplyModulePreview();
		}
	}

	private void ApplyModulePreview()
	{
		if (_settings == null || _isInitializing)
		{
			return;
		}

		_settings.LocalOnlyMode = SelectedLocalOnlyMode;
		_settings.EnableLoadOrderAdvisor = SelectedGuidanceEnabled;
		_modulePreviewActive = true;
	}

	private void AccessibilityCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isInitializing)
		{
			return;
		}

		ReduxWindowBehavior.ConfigureAccessibility(
			SelectedReduceMotion,
			SelectedDisableBackgroundEffects);
		_accessibilityPreviewActive = true;
		if (SelectedReduceMotion)
		{
			BeforePlayExpansion_Changed(BeforePlayExpander, new RoutedEventArgs());
			BeforePlayExpansion_Changed(AppearanceOptionsExpander, new RoutedEventArgs());
			foreach (var element in new FrameworkElement[] { AppearancePage, ConnectionsPage, ManagersPage, OptionsPage, OrganizePage, DemoActiveText, DemoInstructionText, TourResult, TourExplanation })
			{
				element.BeginAnimation(OpacityProperty, null);
				element.Opacity = 1;
				element.RenderTransform = Transform.Identity;
			}
		}

	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		WasResolved = true;
		ApplyChanges = false;
		Close();
	}

	private void UpdateGameStatus()
	{
		var exists = System.IO.File.Exists(SelectedGameExecutablePath);
		GameStatusText.Text = exists ? "Game found" : "Choose your game location";
		GameStatusText.SetResourceReference(TextBlock.ForegroundProperty, exists ? "ReduxSuccessBrush" : "ReduxWarningBrush");
		GamePathText.Text = exists ? SelectedGameExecutablePath : "Select bg3.exe or bg3_dx11.exe in the game’s bin folder.";
		GamePathText.ToolTip = GamePathText.Text;
	}

	private void ChooseGame_Click(object sender, RoutedEventArgs e)
	{
		var picker = new Microsoft.Win32.OpenFileDialog { Title = "Locate Baldur’s Gate 3", Filter = "Baldur’s Gate 3|bg3.exe;bg3_dx11.exe", CheckFileExists = true };
		if (picker.ShowDialog(this) != true) return;
		var name = System.IO.Path.GetFileName(picker.FileName);
		if (!name.Equals("bg3.exe", StringComparison.OrdinalIgnoreCase) && !name.Equals("bg3_dx11.exe", StringComparison.OrdinalIgnoreCase)) return;
		SelectedGameExecutablePath = picker.FileName;
		UpdateGameStatus();
	}


	private bool _nativeTour;
	private int _managerTourStep;

	private void ManagerTourSelection_Click(object sender, RoutedEventArgs e)
	{
		_nativeTour = (sender as Button)?.Tag?.ToString() == "native";
		_managerTourStep = 0;
		UpdateManagerTour();
	}

	private void ManagerTourAction_Click(object sender, RoutedEventArgs e)
	{
		_managerTourStep = (_managerTourStep + 1) % 3;
		UpdateManagerTour();
	}

	private void UpdateManagerTour()
	{
		SaveTourButton.SetResourceReference(StyleProperty, _nativeTour ? "WelcomeStepStyle" : "WelcomeCurrentStepStyle");
		NativeTourButton.SetResourceReference(StyleProperty, _nativeTour ? "WelcomeCurrentStepStyle" : "WelcomeStepStyle");
		System.Windows.Automation.AutomationProperties.SetItemStatus(SaveTourButton, _nativeTour ? "" : "Selected");
		System.Windows.Automation.AutomationProperties.SetItemStatus(NativeTourButton, _nativeTour ? "Selected" : "");
		TourTitle.Text = _nativeTour ? "Native installation · Preview" : "Import a save · Preview";
		TourDestinationIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty, _managerTourStep == 2 ? "Redux.Icon.Check" : "Redux.Icon.FolderOpen");
		TourFileName.Text = _nativeTour ? "Example native mod.zip" : "Example save.zip";
		TourFileDescription.Text = _nativeTour ? "A package containing native DLLs" : "A save file, folder, or ZIP";
		TourFileIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty, _nativeTour ? "Redux.Icon.Shield" : "Redux.Icon.Save");
		TourDestination.Text = _nativeTour ? "Game-Directory Mod Manager" : "Save Game Manager";
		TourResult.Text = (_nativeTour ? new[] { "Ready for a native package", "Review files and replacements", "Installed in the game folder" } : new[] { "Ready for your save", "Save found · selected profile", "Save added to the profile" })[_managerTourStep];
		TourResult.SetResourceReference(TextBlock.ForegroundProperty, _managerTourStep == 2 ? "ReduxSuccessBrush" : "ReduxTextSecondaryBrush");
		TourExplanation.Text = (_nativeTour ? new[] {
			"Drop a native DLL package into Redux. These mods live in the game folder.",
			"Check the files being replaced. Redux verifies protected backups before continuing.",
			"Installed. Manage tracked files here, outside the load order."
		} : new[] {
			"Drop a save file, folder, or ZIP into Redux.",
			"Choose the profile. Review any existing files before replacing them.",
			"Your save is ready. Install its required mods before playing."
		})[_managerTourStep];
		TourActionLabel.Text = (_nativeTour ? new[] { "Preview install", "Install example", "Replay" } : new[] { "Preview import", "Import example", "Replay" })[_managerTourStep];
		TourLocation.Text = _nativeTour ? "Tools → Game-Directory Mod Manager" : "Saves → Manage";
		TourTip.Text = _nativeTour ? "Native mods run code inside the game. Only install from sources you trust." : "Browse, import, and open the selected profile’s saves.";
		AnimateEntrance(TourResult, 1);
		AnimateEntrance(TourExplanation, 1);
	}

	private void DemoAction_Click(object sender, RoutedEventArgs e)
	{
		_demoStep = (_demoStep + 1) % 4;
		DemoInactiveText.Text = _demoStep == 0 ? "Example mod" : "No inactive mods";
		DemoActiveText.Text = _demoStep == 0 ? "No active mods" : "1  Example mod";
		DemoInstructionText.Text = new[] {
			"Installed mods start here. Move one to Active to use it.",
			"Save keeps this arrangement in Redux.",
			"Sync applies your saved order to Baldur’s Gate 3.",
			"Ready to play. That’s the full workflow."
		}[_demoStep];
		DemoActionIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty, new[] { "Redux.Icon.ChevronRightStroke", "Redux.Icon.Save", "Redux.Icon.Check", "Redux.Icon.ReorderStroke" }[_demoStep]);
		DemoActionLabel.Text = new[] { "Activate mod", "Save order", "Sync to game", "Replay" }[_demoStep];
		DemoProgressText.Text = new[] { "01 / Activate", "02 / Save", "03 / Sync", "Order synced" }[_demoStep];
		DemoProgressText.SetResourceReference(TextBlock.ForegroundProperty, _demoStep == 3 ? "ReduxSuccessBrush" : "ReduxAccentHoverBrush");
		AnimateEntrance(DemoActiveText, 1);
		AnimateEntrance(DemoInstructionText, 1);

	}

	private void BeforePlayExpansion_Changed(object sender, RoutedEventArgs e)
	{
		if (sender is not Expander expander) return;
		expander.ApplyTemplate();
		if (expander.Template.FindName("Details", expander) is not Border panel) return;
		var version = _detailsAnimationVersions.GetValueOrDefault(expander) + 1;
		_detailsAnimationVersions[expander] = version;
		var currentHeight = panel.Visibility == Visibility.Visible ? panel.ActualHeight : 0;
		panel.BeginAnimation(HeightProperty, null);
		panel.BeginAnimation(OpacityProperty, null);
		var expanded = expander.IsExpanded;
		if (!IsLoaded || SelectedReduceMotion || ReduxWindowBehavior.ReduceMotion)
		{
			panel.Height = expanded ? Double.NaN : 0;
			panel.Opacity = 1;
			panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
			return;
		}
		panel.Visibility = Visibility.Visible;
		panel.Child.Measure(new Size(Math.Max(1, expander.ActualWidth), Double.PositiveInfinity));
		var targetHeight = expanded ? panel.Child.DesiredSize.Height : 0;
		panel.Height = currentHeight;
		var duration = TimeSpan.FromMilliseconds(180);
		var animation = new DoubleAnimation(currentHeight, targetHeight, duration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
		};
		animation.Completed += (_, _) =>
		{
			if (version != _detailsAnimationVersions.GetValueOrDefault(expander)) return;
			panel.BeginAnimation(HeightProperty, null);
			panel.BeginAnimation(OpacityProperty, null);
			panel.Height = expander.IsExpanded ? Double.NaN : 0;
			panel.Opacity = 1;
			panel.Visibility = expander.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
		};
		panel.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
		panel.BeginAnimation(OpacityProperty, new DoubleAnimation(expanded ? 1 : 0, duration), HandoffBehavior.SnapshotAndReplace);
	}

	private void StepButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button button && Int32.TryParse(button.Tag?.ToString(), out var step)) ShowStep(step);
	}

	private void AnimateEntrance(FrameworkElement element, int direction)
	{
		element.BeginAnimation(OpacityProperty, null);
		element.Opacity = 1;
		element.RenderTransform = Transform.Identity;
		if (!IsLoaded || SelectedReduceMotion || ReduxWindowBehavior.ReduceMotion) return;
		var offset = new TranslateTransform();
		element.RenderTransform = offset;
		var duration = TimeSpan.FromMilliseconds(180);
		var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1, duration) { FillBehavior = FillBehavior.Stop });
		offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(12 * direction, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
	}

	private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(_step - 1);

	private void ShowStep(int step)
	{
		var direction = step >= _step ? 1 : -1;
		_step = Math.Clamp(step, 0, 4);
		AppearancePage.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
		ConnectionsPage.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
		OrganizePage.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
		ManagersPage.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
		OptionsPage.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
		StepProgressText.Text = $"Step {_step + 1} of 5 · {new[] { "Your setup", "Add mods", "Load order", "Organize", "Saves & DLLs" }[_step]}";
		StepTitleText.Text = new[] { "Welcome to Redux", "Bring in your mods", "Make it part of your game", "A place for every mod", "Beyond the load order" }[_step];
		StepDescriptionText.Text = new[] { "Set up Redux for Baldur’s Gate 3.", "Local files and Nexus downloads meet in Download Manager.", "Activate. Save. Sync. Try it below without changing your files.", "Make your library easier to browse with categories and separators.", "Saves and native mods have their own place in Redux." }[_step];
		BackButton.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;
		ContinueLabel.Text = _step == 4 ? "Start using Redux" : "Continue";
		ContinueIcon.SetResourceReference(Controls.ReduxIcon.StrokeDataProperty, _step == 4 ? "Redux.Icon.Check" : "Redux.Icon.ChevronRightStroke");
		var stepButtons = new[] { SetupStepButton, SourcesStepButton, OrderStepButton, OrganizeStepButton, ManagersStepButton };
		for (var i = 0; i < stepButtons.Length; i++)
		{
			stepButtons[i].SetResourceReference(StyleProperty, i == _step ? "WelcomeCurrentStepStyle" : "WelcomeStepStyle");
			System.Windows.Automation.AutomationProperties.SetItemStatus(stepButtons[i], i == _step ? "Current step" : "Go to step");
		}
		AnimateEntrance(_step == 0 ? AppearancePage : _step == 1 ? ConnectionsPage : _step == 2 ? OptionsPage : _step == 3 ? OrganizePage : ManagersPage, direction);
		OnboardingContentScrollViewer.ScrollToTop();
		System.Windows.Input.FocusManager.SetFocusedElement(this, _step == 0 ? ReduxDarkThemeCard : _step == 1 ? SourceIntegrationsCheckBox : _step == 2 ? GuidanceCheckBox : _step == 3 ? StarterSeparatorsCheckBox : SaveTourButton);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		if (_step < 4)
		{
			ShowStep(_step + 1);
			return;
		}
		WasResolved = true;
		ApplyChanges = true;
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!WasResolved)
		{
			WasResolved = true;
			ApplyChanges = false;
		}

		if (!ApplyChanges && (_themePreviewActive || _appearanceEdited))
		{
			_ownerWindow?.PreviewColorTheme(_initialTheme, _initialCustomTheme);
			ReduxTypographyService.Apply(Resources, _initialTypography.Font, _initialTypography.CustomReference);
			ReduxTypographyService.ApplyTextSize(Resources, _initialTextSize);
			if (Application.Current != null) ReduxTypographyService.ApplyTextSize(Application.Current.Resources, _initialTextSize);
			_ownerWindow?.ViewModel?.PreviewModPresentation(_initialCustomTheme);
			DivinityApp.ShowInterfaceIcons = _initialShowIcons;
			DivinityApp.UseIconsOnly = _initialIconsOnly;
		}
		if (!ApplyChanges && _modulePreviewActive && _settings != null)
		{
			_settings.LocalOnlyMode = _initialLocalOnlyMode;
			_settings.EnableLoadOrderAdvisor = _initialGuidanceEnabled;
		}
		if (!ApplyChanges && _accessibilityPreviewActive)
		{
			ReduxWindowBehavior.ConfigureAccessibility(
				_initialReduceMotion,
				_initialDisableBackgroundEffects);
		}

		base.OnClosing(e);
	}
}
