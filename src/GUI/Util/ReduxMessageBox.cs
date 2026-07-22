using DivinityModManager.Views;

using System.Windows;

namespace DivinityModManager.Util;

/// <summary>
/// Drop-in replacement for Xceed.Wpf.Toolkit.MessageBox.Show, backed by the Redux-owned
/// ReduxMessageBoxWindow instead of a third-party control's own template/icons.
/// </summary>
public static class ReduxMessageBox
{
	public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton button,
		MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
	{
		var window = new ReduxMessageBoxWindow(owner, text, caption, button, icon, defaultResult);
		window.ShowDialog();
		return window.Result;
	}

	public static MessageBoxResult Show(string text, string caption, MessageBoxButton button,
		MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
		Show(Application.Current?.MainWindow, text, caption, button, icon, defaultResult);

	/// <summary>
	/// Same as Show, but with extra action buttons (e.g. "Copy to Clipboard") alongside the
	/// standard result buttons. Extra actions run their callback without closing the dialog.
	/// </summary>
	public static MessageBoxResult ShowWithActions(Window owner, string text, string caption, MessageBoxButton button,
		MessageBoxImage icon, MessageBoxResult defaultResult, params (string Label, Action Callback)[] extraActions)
	{
		var window = new ReduxMessageBoxWindow(owner, text, caption, button, icon, defaultResult);
		foreach (var (label, callback) in extraActions)
		{
			window.AddExtraAction(label, callback);
		}
		window.ShowDialog();
		return window.Result;
	}
}
