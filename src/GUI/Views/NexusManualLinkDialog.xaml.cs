using AdonisUI.Controls;

using System.Windows;

namespace DivinityModManager.Views;

public partial class NexusManualLinkDialog : AdonisWindow
{
	public string NexusLink => NexusLinkTextBox.Text?.Trim();

	public NexusManualLinkDialog(string currentLink = null)
	{
		InitializeComponent();
		NexusLinkTextBox.Text = currentLink ?? String.Empty;
		Loaded += (_, _) =>
		{
			NexusLinkTextBox.Focus();
			NexusLinkTextBox.SelectAll();
		};
	}

	private void Link_Click(object sender, RoutedEventArgs e)
	{
		if (!String.IsNullOrWhiteSpace(NexusLink)) DialogResult = true;
	}
}
