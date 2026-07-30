using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxModNoteWindow : AdonisUI.Controls.AdonisWindow
{
	private bool _requiresSharedNoteText;
	public bool Accepted { get; private set; }
	public string Note => NoteTextBox.Text?.Trim() ?? String.Empty;

	public ReduxModNoteWindow(Window owner, DivinityModData mod)
		: this(owner, mod == null ? [] : [mod])
	{
	}

	public ReduxModNoteWindow(Window owner, IReadOnlyList<DivinityModData> mods)
	{
		InitializeComponent();
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));

		var targets = (mods ?? [])
			.Where(mod => mod != null)
			.ToArray();
		if (targets.Length <= 1)
		{
			ModNameText.Text = targets.FirstOrDefault()?.DisplayName ?? "Selected mod";
			NoteTextBox.Text = targets.FirstOrDefault()?.PrivateNote ?? String.Empty;
		}
		else
		{
			var notes = targets
				.Select(mod => mod.PrivateNote?.Trim() ?? String.Empty)
				.Distinct(StringComparer.Ordinal)
				.ToArray();
			_requiresSharedNoteText = notes.Length > 1;
			ModNameText.Text = _requiresSharedNoteText
				? $"{targets.Length} selected mods \u00B7 existing notes differ, so enter the shared note to apply."
				: $"{targets.Length} selected mods \u00B7 saving applies the same note to every selected mod.";
			NoteTextBox.Text = notes.Length == 1 ? notes[0] : String.Empty;
		}
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
		if (SaveButton != null)
			SaveButton.IsEnabled = !_requiresSharedNoteText || !String.IsNullOrWhiteSpace(NoteTextBox.Text);
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
