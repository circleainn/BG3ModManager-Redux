using AdonisUI.Controls;

using System.ComponentModel;
using System.Windows;

namespace DivinityModManager.Views;

public partial class OfflineNexusDatabaseWarningWindow : AdonisWindow
{
	private bool _acknowledged;

	public OfflineNexusDatabaseWarningWindow() => InitializeComponent();

	private void ContinueButton_Click(object sender, RoutedEventArgs e)
	{
		_acknowledged = true;
		DialogResult = true;
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!_acknowledged)
		{
			e.Cancel = true;
			return;
		}

		base.OnClosing(e);
	}
}
