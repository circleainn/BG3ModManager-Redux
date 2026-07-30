using DivinityModManager.Controls;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxOnboardingWindow : AdonisUI.Controls.AdonisWindow
{
	private static readonly string[] PageTitles =
	[
		"Welcome to Redux",
		"How Redux fits your workflow",
		"Choose how Redux starts"
	];

	private static readonly string[] PageSubtitles =
	[
		"Preview, source, and mod.io guidance are collected here instead of appearing as separate startup warnings.",
		"Redux adds context and safeguards around the familiar BG3 Mod Manager workflow.",
		"These safe defaults can be changed at any time in Preferences."
	];

	private readonly StackPanel[] _pages;
	private readonly Border[] _progressSegments;
	private int _pageIndex;

	public bool WasResolved { get; private set; }
	public bool TourFinished { get; private set; }
	public bool SelectedLocalOnlyMode => LocalOnlyRadio.IsChecked == true;
	public bool SelectedDiagnosticsEnabled => DiagnosticsCheckBox.IsChecked == true;
	public bool SelectedGuidanceEnabled =>
		SelectedDiagnosticsEnabled && GuidanceCheckBox.IsChecked == true;

	public ReduxOnboardingWindow(
		Window owner,
		DivinityModManagerSettings settings)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);

		if (owner?.IsLoaded == true)
		{
			Owner = owner;
		}

		if (settings != null)
		{
			ReduxThemeService.Apply(
				Resources,
				settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings));
			SourceIntegrationsRadio.IsChecked = !settings.LocalOnlyMode;
			LocalOnlyRadio.IsChecked = settings.LocalOnlyMode;
			DiagnosticsCheckBox.IsChecked = settings.EnableModHealth;
			GuidanceCheckBox.IsChecked =
				settings.EnableModHealth && settings.EnableLoadOrderAdvisor;
		}
		else
		{
			SourceIntegrationsRadio.IsChecked = true;
			DiagnosticsCheckBox.IsChecked = true;
		}

		SkipButtonText.Text = "Close";

		_pages = [WelcomePage, WorkflowPage, ChoicesPage];
		_progressSegments = [Progress0, Progress1, Progress2];
		UpdateDiagnosticsState();
		UpdatePage();
	}

	private void UpdatePage()
	{
		for (var index = 0; index < _pages.Length; index++)
		{
			_pages[index].Visibility =
				index == _pageIndex ? Visibility.Visible : Visibility.Collapsed;
			_progressSegments[index].SetResourceReference(
				Border.BackgroundProperty,
				index <= _pageIndex ? "ReduxAccentBrush" : "ReduxBorderBrush");
		}

		PageTitleText.Text = PageTitles[_pageIndex];
		PageSubtitleText.Text = PageSubtitles[_pageIndex];
		StepText.Text = $"{_pageIndex + 1} of {_pages.Length}";
		BackButton.Visibility = _pageIndex == 0 ? Visibility.Collapsed : Visibility.Visible;

		var isLastPage = _pageIndex == _pages.Length - 1;
		NextButtonText.Text = isLastPage ? "Finish" : "Continue";
		NextButtonIcon.SetResourceReference(
			ReduxIcon.StrokeDataProperty,
			isLastPage ? "Redux.Icon.Check" : "Redux.Icon.ArrowForwardStroke");

		NextButton.IsEnabled = true;
	}

	private void UpdateDiagnosticsState()
	{
		if (GuidanceCheckBox == null || DiagnosticsCheckBox == null)
		{
			return;
		}

		var diagnosticsEnabled = DiagnosticsCheckBox.IsChecked == true;
		GuidanceCheckBox.IsEnabled = diagnosticsEnabled;
		if (!diagnosticsEnabled)
		{
			GuidanceCheckBox.IsChecked = false;
		}
	}

	private void DiagnosticsCheckBox_Changed(object sender, RoutedEventArgs e) =>
		UpdateDiagnosticsState();

	private void SkipButton_Click(object sender, RoutedEventArgs e)
	{
		if (!SkipButton.IsEnabled)
		{
			return;
		}

		WasResolved = true;
		TourFinished = false;
		Close();
	}

	private void BackButton_Click(object sender, RoutedEventArgs e)
	{
		if (_pageIndex <= 0)
		{
			return;
		}

		_pageIndex--;
		UpdatePage();
	}

	private void NextButton_Click(object sender, RoutedEventArgs e)
	{
		if (!NextButton.IsEnabled)
		{
			return;
		}

		if (_pageIndex < _pages.Length - 1)
		{
			_pageIndex++;
			UpdatePage();
			return;
		}

		WasResolved = true;
		TourFinished = true;
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!WasResolved)
		{
			WasResolved = true;
			TourFinished = false;
		}

		base.OnClosing(e);
	}
}
