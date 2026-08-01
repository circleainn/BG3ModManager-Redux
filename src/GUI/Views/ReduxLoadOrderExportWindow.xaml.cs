using DivinityModManager.Util;

using System.Windows;

namespace DivinityModManager.Views;

public partial class ReduxLoadOrderExportWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }
	public bool OpenContainingFolder => OpenFolderCheckBox.IsChecked == true;
	public bool IncludePrivateNotes => IncludePrivateNotesCheckBox.IsChecked == true;

	public ReduxLoadOrderExportWindow(
		Window owner,
		string orderName,
		int modCount,
		int categoryCount,
		int separatorCount,
		int iconCount,
		int privateNoteCount,
		int unavailableIconCount)
	{
		InitializeComponent();
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		if (owner?.IsLoaded == true) Owner = owner;

		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));

		OrderNameText.Text = String.IsNullOrWhiteSpace(orderName) ? "Redux Modlist" : orderName;
		ModCountText.Text = FormatCount(modCount, "mod");
		CategoryCountText.Text = FormatCount(categoryCount, "category", "categories");
		SeparatorCountText.Text = FormatCount(separatorCount, "separator");
		IconCountText.Text = FormatCount(iconCount, "icon");
		PrivateNotesTitleText.Text =
			$"Include notes ({Math.Max(0, privateNoteCount)})";
		IncludePrivateNotesCheckBox.IsEnabled = privateNoteCount > 0;
		IncludePrivateNotesCheckBox.IsChecked = false;
		if (unavailableIconCount > 0)
		{
			BundleSafetyText.Text +=
				$" {FormatCount(unavailableIconCount, "unavailable custom icon")} will use the default marker.";
		}
	}

	private static string FormatCount(int count, string singular, string plural = null) =>
		$"{Math.Max(0, count)} {(count == 1 ? singular : plural ?? $"{singular}s")}";

	private void ContinueButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
