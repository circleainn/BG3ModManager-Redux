using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Views;

/// <summary>
/// Redux-owned replacement for Xceed's MessageBox. A plain AdonisWindow dialog matching the
/// same visual language as the other Redux warning windows (ReduxPreviewWarningWindow, etc.),
/// so confirmations no longer depend on a third-party control's own template/icons.
/// </summary>
public partial class ReduxMessageBoxWindow : AdonisUI.Controls.AdonisWindow
{
	public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

	private readonly Button _fallbackEscapeButton;

	public ReduxMessageBoxWindow(Window owner, string text, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
	{
		InitializeComponent();

		if (owner != null && owner.IsLoaded)
		{
			Owner = owner;
		}

		// Dynamically-created windows only see the app-level default theme unless they're
		// explicitly themed, same as every other on-demand dialog in this app (CategoryNameDialog,
		// CustomThemeEditorWindow, etc.) - otherwise this always renders in Redux Dark regardless
		// of the user's active theme.
		var settings = MainWindow.Self?.ViewModel?.Settings;
		if (settings != null)
		{
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings));
		}

		Title = caption;
		CaptionText.Text = caption;
		MessageTextBox.Text = text;

		ApplySeverityIcon(icon);

		switch (button)
		{
			case MessageBoxButton.OK:
				OkButton.Visibility = Visibility.Visible;
				_fallbackEscapeButton = OkButton;
				break;
			case MessageBoxButton.OKCancel:
				OkButton.Visibility = Visibility.Visible;
				CancelButton.Visibility = Visibility.Visible;
				_fallbackEscapeButton = CancelButton;
				break;
			case MessageBoxButton.YesNoCancel:
				YesButton.Visibility = Visibility.Visible;
				NoButton.Visibility = Visibility.Visible;
				CancelButton.Visibility = Visibility.Visible;
				_fallbackEscapeButton = CancelButton;
				break;
			case MessageBoxButton.YesNo:
				YesButton.Visibility = Visibility.Visible;
				NoButton.Visibility = Visibility.Visible;
				_fallbackEscapeButton = NoButton;
				break;
		}

		var defaultButton = defaultResult switch
		{
			MessageBoxResult.OK => OkButton,
			MessageBoxResult.Yes => YesButton,
			MessageBoxResult.No => NoButton,
			MessageBoxResult.Cancel => CancelButton,
			_ => null
		};
		if (defaultButton is { Visibility: Visibility.Visible })
		{
			defaultButton.IsDefault = true;
		}
		else if (OkButton.Visibility == Visibility.Visible)
		{
			OkButton.IsDefault = true;
		}
		else if (YesButton.Visibility == Visibility.Visible)
		{
			YesButton.IsDefault = true;
		}
	}

	private void ApplySeverityIcon(MessageBoxImage icon)
	{
		var (iconKey, brushKey) = icon switch
		{
			MessageBoxImage.Error => ("error", "ReduxErrorBrush"),
			MessageBoxImage.Warning => ("warning", "ReduxWarningBrush"),
			MessageBoxImage.Question => ("question", "ReduxAccentBrush"),
			MessageBoxImage.Information => ("info", "ReduxInfoBrush"),
			_ => (null, null)
		};

		if (iconKey == null)
		{
			IconBadge.Visibility = Visibility.Collapsed;
			return;
		}

		var brush = TryFindResource(brushKey) as Brush;
		SeverityIcon.IconKey = iconKey;
		SeverityIcon.Foreground = brush;
		IconBadge.BorderBrush = brush;
	}

	/// <summary>
	/// Adds an extra action button (e.g. "Copy to Clipboard") alongside the standard result
	/// buttons. Extra actions do not close the dialog by default, matching the behavior of the
	/// legacy shared MessageBoxSelectableText template this replaces.
	/// </summary>
	public void AddExtraAction(string label, Action callback, bool closesDialog = false)
	{
		var button = new Button
		{
			Content = label,
			MinWidth = 80,
			Margin = new Thickness(0, 0, 8, 0),
			Style = (Style)FindResource("ReduxRoundedSecondaryButtonStyle")
		};
		button.Click += (_, _) =>
		{
			callback();
			if (closesDialog) Close();
		};
		ExtraActionsPanel.Children.Add(button);
	}

	private void YesButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
	private void NoButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
	private void OkButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }
	private void CancelButton_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape && CancelButton.Visibility != Visibility.Visible && _fallbackEscapeButton != null)
		{
			_fallbackEscapeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			e.Handled = true;
		}
	}
}
