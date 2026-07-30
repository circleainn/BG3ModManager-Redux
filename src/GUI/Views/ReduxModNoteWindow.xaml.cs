using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxModNoteWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }
	public string Note => NoteTextBox.Text?.Trim() ?? String.Empty;

	public ReduxModNoteWindow(Window owner, DivinityModData mod)
	{
		InitializeComponent();
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));

		ModNameText.Text = mod?.DisplayName ?? "Selected mod";
		NoteTextBox.Text = mod?.PrivateNote ?? String.Empty;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		Loaded += (_, _) =>
		{
			NoteTextBox.Focus();
			NoteTextBox.CaretIndex = NoteTextBox.Text.Length;
		};
		UpdateCharacterCount();
	}

	private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCharacterCount();

	private void UpdateCharacterCount()
	{
		if (CharacterCountText == null || NoteTextBox == null) return;
		CharacterCountText.Text =
			$"{NoteTextBox.Text.Length:N0} / {ReduxModAnnotationService.MaximumNoteLength:N0}";
		ClearButton.IsEnabled = !String.IsNullOrWhiteSpace(NoteTextBox.Text);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void ClearButton_Click(object sender, RoutedEventArgs e)
	{
		NoteTextBox.Clear();
		Accepted = true;
		Close();
	}
}
