using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public enum ReduxInstallReviewTone
{
	Info,
	Success,
	Warning,
	Error
}

public enum ReduxInstallReviewKind
{
	Mods,
	Saves,
	GameDirectory,
	Batch
}

public sealed record ReduxInstallReviewItem(
	string Name,
	string Detail,
	string Status,
	ReduxInstallReviewTone Tone);

public partial class ReduxInstallReviewWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }
	public bool SkipFutureCleanReviews => SkipCleanReviewCheckBox.IsChecked == true;

	public ReduxInstallReviewWindow(
		Window owner,
		IReadOnlyList<ReduxInstallReviewItem> items,
		bool isSaveInstall,
		string destination,
		string summaryDetail,
		bool canSkipCleanReview = false)
		: this(owner, items, isSaveInstall ? ReduxInstallReviewKind.Saves : ReduxInstallReviewKind.Mods,
			destination, summaryDetail, canSkipCleanReview)
	{
	}

	public ReduxInstallReviewWindow(
		Window owner,
		IReadOnlyList<ReduxInstallReviewItem> items,
		ReduxInstallReviewKind kind,
		string destination,
		string summaryDetail,
		bool canSkipCleanReview = false,
		bool resultsOnly = false)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme,
				ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		var isSaveInstall = kind == ReduxInstallReviewKind.Saves;
		var isGameDirectoryInstall = kind == ReduxInstallReviewKind.GameDirectory;
		var isBatchInstall = kind == ReduxInstallReviewKind.Batch;
		var noun = isSaveInstall ? "save" : isGameDirectoryInstall ? "change" : isBatchInstall ? "package" : "mod";
		var count = items?.Count ?? 0;
		Title = resultsOnly ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install All Results")
			: isSaveInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install Saves") : isGameDirectoryInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review Game-Directory Changes")
			: isBatchInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install All Packages") : DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install Mods");
		HeadingText.Text = resultsOnly ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install All complete")
			: isSaveInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install saves?") : isGameDirectoryInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Apply these game-directory changes?")
			: isBatchInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install all ready packages?") : DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install mods?");
		DescriptionText.Text = resultsOnly
			? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review what Redux installed, skipped, or could not finish. Failed and skipped packages remain in the inbox.")
			: isSaveInstall
			? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review what Save Game Manager found before anything is copied into the selected profile.")
			: isGameDirectoryInstall
				? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Review every destination carefully. Native DLLs run inside Baldur's Gate 3 and should only be installed from sources you trust.")
				: isBatchInstall
					? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Redux will route each package to its reviewed destination without activating, reordering, or syncing mods.")
					: $"Review what Redux found before anything is installed into {destination}.";
		SummaryIcon.IconKey = resultsOnly ? "circle-check" : isSaveInstall ? "book-open" : isGameDirectoryInstall ? "blocks" : "package";
		SummaryText.Text = resultsOnly ? $"{count} package result{(count == 1 ? String.Empty : "s")}"
			: $"{count} {noun}{(count == 1 ? String.Empty : "s")} ready for review";
		SummaryDetailText.Text = summaryDetail;
		ReviewList.ItemsSource = items ?? [];
		FootnoteText.Text = resultsOnly
			? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("New load-order mods start inactive; updates preserve their state and position. Review your order before syncing.")
			: isSaveInstall
			? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Existing saves require a separate replacement confirmation.")
			: isGameDirectoryInstall
				? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Redux rechecks the archive, prerequisites, destinations, and ownership record immediately before applying anything.")
				: isBatchInstall
					? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Packages install one at a time. One failure will not stop independent packages.")
					: DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Packages are validated again during installation.");
		InstallButtonText.Text = resultsOnly ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Close") : isSaveInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install Saves")
			: isGameDirectoryInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Apply Changes") : isBatchInstall ? DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install All") : DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Install Mods");
		SkipCleanReviewCheckBox.Visibility = canSkipCleanReview && kind == ReduxInstallReviewKind.Mods
			? Visibility.Visible : Visibility.Collapsed;
		CancelButton.Visibility = resultsOnly ? Visibility.Collapsed : Visibility.Visible;
		if (resultsOnly)
		{
			InstallButton.SetResourceReference(StyleProperty, "ReduxSecondaryActionButtonStyle");
			InstallButtonIcon.SetResourceReference(DivinityModManager.Controls.ReduxIcon.StrokeDataProperty, "Redux.Icon.Close");
		}
	}

	private void InstallButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		DialogResult = true;
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
