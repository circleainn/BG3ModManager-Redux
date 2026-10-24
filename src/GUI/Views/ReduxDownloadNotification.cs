using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using DivinityModManager.Controls;
using DivinityModManager.Util;
using DivinityModManager.Models;

namespace DivinityModManager.Views;

/// <summary>A single passive surface; only an explicit action opens the manager.</summary>
public sealed class ReduxDownloadNotification : Window
{
	private readonly TextBlock _message;
	private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };
	private int _animationVersion;

	public ReduxDownloadNotification(Window main, Func<Task> openDownloads)
	{
		Title = DivinityModManager.Localization.ReduxLanguageService.Current.Translate("Redux downloads");
		ShowActivated = false;
		ShowInTaskbar = false;
		Topmost = true;
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		ResizeMode = ResizeMode.NoResize;
		SizeToContent = SizeToContent.Height;
		Width = 300;
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/MainResourceDictionary.xaml", UriKind.Relative) });
		var settings = (main as MainWindow)?.ViewModel?.Settings;
		if (settings != null)
			ReduxThemeService.Apply(Resources, settings.ColorTheme, ReduxThemeService.GetActiveTheme(settings), settings.UsesGeneratedGradients);
		else ReduxThemeService.Apply(Resources, ReduxThemeType.ReduxDark);
		Background = Brushes.Transparent;
		SetResourceReference(ForegroundProperty, "ReduxTextPrimaryBrush");
		SetResourceReference(FontFamilyProperty, "Redux.FontFamily.UI");
		SetResourceReference(FontSizeProperty, "Redux.FontSize.14");
		_message = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 64 };
		AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
		var row = new Grid();
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		row.ColumnDefinitions.Add(new ColumnDefinition());
		var icon = new ReduxIcon { Width = 18, Height = 18, Margin = new Thickness(0, 0, 10, 0) };
		icon.SetResourceReference(ReduxIcon.StrokeDataProperty, "Redux.Icon.Download");
		icon.SetResourceReference(ForegroundProperty, "ReduxAccentBrush");
		row.Children.Add(icon);
		Grid.SetColumn(_message, 1);
		row.Children.Add(_message);
		var open = new Button { Content = row, Padding = new Thickness(12, 10, 12, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch };
		open.SetResourceReference(StyleProperty, "ReduxSecondaryActionButtonStyle");
		AutomationProperties.SetName(open, "Open Download Manager");
		open.Click += async (_, _) => { Close(); await openDownloads(); };
		PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
		open.BorderThickness = new Thickness(0);
		open.Background = Brushes.Transparent;
		var border = new Border { Child = open, Padding = new Thickness(3), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) };
		border.SetResourceReference(Border.BackgroundProperty, "ReduxSurfaceBrush");
		border.SetResourceReference(Border.BorderBrushProperty, "ReduxBorderBrush");
		Content = border;
		SizeChanged += (_, _) => Position();
		_timer.Tick += (_, _) => { if (!IsMouseOver && !IsKeyboardFocusWithin) FadeAway(); };
		Closed += (_, _) => _timer.Stop();
		main.Closed += MainClosed;
		Closed += (_, _) => main.Closed -= MainClosed;
	}

	private void MainClosed(object sender, EventArgs e) => Close();

	public void Notify(string id, string message)
	{
		_animationVersion++;
		BeginAnimation(OpacityProperty, null);
		Opacity = 1;
		_message.Text = message;
		_message.ToolTip = _message.Text;
		AutomationProperties.SetName(this, _message.Text);
		Position();
		if (!IsVisible)
		{
			Show();
			if (!ReduxWindowBehavior.ReduceMotion && SystemParameters.ClientAreaAnimation)
				BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
		}
		UIElementAutomationPeer.CreatePeerForElement(_message)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
		_timer.Stop();
		_timer.Start();
	}

	private void FadeAway()
	{
		_timer.Stop();
		if (ReduxWindowBehavior.ReduceMotion || !SystemParameters.ClientAreaAnimation) { Close(); return; }
		var version = ++_animationVersion;
		var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(240));
		fade.Completed += (_, _) => { if (version == _animationVersion) Close(); };
		BeginAnimation(OpacityProperty, fade);
	}

	private void Position()
	{
		var area = SystemParameters.WorkArea;
		Width = Math.Min(300, area.Width - 24);
		Left = area.Right - Width - 12;
		Top = Math.Max(area.Top + 12, area.Bottom - ActualHeight - 12);
	}
}
