using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;

namespace DivinityModManager.Views;

public partial class ReduxModAliasWindow : AdonisUI.Controls.AdonisWindow
{
	public bool Accepted { get; private set; }
	public string Alias => AliasTextBox.Text?.Trim() ?? String.Empty;

	public ReduxModAliasWindow(Window owner, DivinityModData mod)
	{
		InitializeComponent();
		if (owner?.IsLoaded == true) Owner = owner;
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);

		OriginalNameText.Text = $"Original name: {mod?.Metadata?.PackageTitle ?? mod?.Name ?? "Selected mod"}";
		FileNameText.Text = $"Package file: {mod?.FileName ?? "Unavailable"}";
		AliasTextBox.Text = mod?.CustomAlias ?? String.Empty;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		Loaded += (_, _) =>
		{
			AliasTextBox.Focus();
			AliasTextBox.SelectAll();
		};
		UpdateState();
	}

	private void AliasTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateState();

	private void UpdateState()
	{
		if (AliasTextBox == null || CharacterCountText == null || ClearButton == null) return;
		CharacterCountText.Text = $"{AliasTextBox.Text.Length:N0} / {ReduxModAnnotationService.MaximumAliasLength:N0}";
		ClearButton.IsEnabled = !String.IsNullOrWhiteSpace(AliasTextBox.Text);
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		Accepted = true;
		Close();
	}

	private void ClearButton_Click(object sender, RoutedEventArgs e)
	{
		AliasTextBox.Clear();
		Accepted = true;
		Close();
	}
}
