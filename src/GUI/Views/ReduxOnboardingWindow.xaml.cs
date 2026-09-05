using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

using WpfScreenHelper;

namespace DivinityModManager.Views;

public partial class ReduxOnboardingWindow : AdonisUI.Controls.AdonisWindow
{
	private readonly MainWindow _ownerWindow;
	private readonly DivinityModManagerSettings _settings;
	private readonly ReduxThemeType _initialTheme;
	private readonly ReduxCustomTheme _initialCustomTheme;
	private readonly bool _initialLocalOnlyMode;
	private readonly bool _initialDiagnosticsEnabled;
	private readonly bool _initialGuidanceEnabled;
	private readonly bool _initialReduceMotion;
	private readonly bool _initialDisableBackgroundEffects;
	private bool _themePreviewActive;
	private bool _modulePreviewActive;
	private bool _accessibilityPreviewActive;
	private bool _isInitializing = true;
	private double _availableHeight = 780;

	public bool WasResolved { get; private set; }
	public bool ApplyChanges { get; private set; }
	public ReduxThemeType SelectedTheme => ReduxDarkThemeCard.IsChecked == true
		? ReduxThemeType.ReduxDark
		: ReduxLightThemeCard.IsChecked == true
			? ReduxThemeType.ReduxLight
			: ReduxThemeType.Parchment;
	public bool SelectedLocalOnlyMode => SourceIntegrationsCheckBox.IsChecked != true;
	public bool SelectedDiagnosticsEnabled => DiagnosticsCheckBox.IsChecked == true;
	public bool SelectedGuidanceEnabled => SelectedDiagnosticsEnabled && GuidanceCheckBox.IsChecked == true;
	public bool SelectedReduceMotion => ReduceMotionCheckBox.IsChecked == true;
	public bool SelectedDisableBackgroundEffects => DisableBackgroundEffectsCheckBox.IsChecked == true;
	public string SelectedNexusApiKey => NexusApiKeyTextBox.Password?.Trim() ?? String.Empty;
	public string SelectedModioApiKey => ModioApiKeyTextBox.Password?.Trim() ?? String.Empty;

	public ReduxOnboardingWindow(Window owner, DivinityModManagerSettings settings)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 30);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		_ownerWindow = owner as MainWindow;
		_settings = settings;
		_initialTheme = settings?.ColorTheme ?? ReduxThemeType.ReduxDark;
		_initialCustomTheme = ReduxThemeService.GetActiveTheme(settings);
		_initialLocalOnlyMode = settings?.LocalOnlyMode == true;
		_initialDiagnosticsEnabled = settings?.EnableModHealth == true;
		_initialGuidanceEnabled = settings?.EnableLoadOrderAdvisor == true;
		_initialReduceMotion = settings?.ReduceMotion == true;
		_initialDisableBackgroundEffects = settings?.DisableBackgroundEffects == true;
		ApplyAdaptiveDefaultSize(owner);

		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
			ReduxDarkThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxDark;
			ReduxLightThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.ReduxLight;
			ParchmentThemeCard.IsChecked = settings.ColorTheme == ReduxThemeType.Parchment;
			SourceIntegrationsCheckBox.IsChecked = !settings.LocalOnlyMode;
			DiagnosticsCheckBox.IsChecked = settings.EnableModHealth;
			GuidanceCheckBox.IsChecked = settings.EnableModHealth && settings.EnableLoadOrderAdvisor;
			NexusApiKeyTextBox.Password = settings.NexusModsAPIKey ?? String.Empty;
			ModioApiKeyTextBox.Password = settings.ModioAPIKey ?? String.Empty;
			ReduceMotionCheckBox.IsChecked = settings.ReduceMotion;
			DisableBackgroundEffectsCheckBox.IsChecked = settings.DisableBackgroundEffects;
		}
		else
		{
			ReduxDarkThemeCard.IsChecked = true;
			SourceIntegrationsCheckBox.IsChecked = true;
			DiagnosticsCheckBox.IsChecked = true;
		}

		_isInitializing = false;
		UpdateDiagnosticsState();
		UpdateSourceIntegrationState();
	}

	private void ApplyAdaptiveDefaultSize(Window owner)
	{
		var workArea = owner != null ? Screen.FromWindow(owner).WorkingArea : SystemParameters.WorkArea;
		var targetWidth = Math.Clamp(workArea.Width * 0.44, 720, 780);
		_availableHeight = Math.Max(MinHeight, workArea.Height - 48);
		Width = Math.Max(MinWidth, Math.Min(targetWidth, workArea.Width - 48));
		MaxHeight = _availableHeight;
		SizeToContent = SizeToContent.Height;
	}

	private void ThemeCard_Click(object sender, RoutedEventArgs e)
	{
		if (sender is RadioButton { Tag: ReduxThemeType theme })
		{
			ReduxThemeService.Apply(Resources, theme);
			_ownerWindow?.PreviewColorTheme(theme);
			_themePreviewActive = true;
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

	private void DiagnosticsCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isInitializing)
		{
			UpdateDiagnosticsState();
			ApplyModulePreview();
		}
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
		_settings.EnableModHealth = SelectedDiagnosticsEnabled;
		_settings.EnableLoadOrderAdvisor = SelectedGuidanceEnabled;
		_modulePreviewActive = true;
	}

	private void UpdateDiagnosticsState()
	{
		if (GuidanceCheckBox == null || DiagnosticsCheckBox == null)
		{
			return;
		}

		GuidanceCheckBox.IsEnabled = DiagnosticsCheckBox.IsChecked == true;
		if (!GuidanceCheckBox.IsEnabled)
		{
			GuidanceCheckBox.IsChecked = false;
		}
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
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		WasResolved = true;
		ApplyChanges = false;
		Close();
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
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

		if (!ApplyChanges && _themePreviewActive)
		{
			_ownerWindow?.PreviewColorTheme(_initialTheme, _initialCustomTheme);
		}
		if (!ApplyChanges && _modulePreviewActive && _settings != null)
		{
			_settings.LocalOnlyMode = _initialLocalOnlyMode;
			_settings.EnableModHealth = _initialDiagnosticsEnabled;
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
