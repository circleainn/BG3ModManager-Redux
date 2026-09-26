using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shell;
using DivinityModManager.Controls;
using DivinityModManager.Util;
using DivinityModManager.Models;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DivinityModManager.Views;

public sealed class ReduxWhatsNewWindow : Window
{
	private readonly FlowDocumentScrollViewer _notes = new() { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
	private readonly CancellationTokenSource _closed = new();
	public ReduxWhatsNewWindow(Window owner, string version, DivinityModManagerSettings preferences = null, Action savePreferences = null)
	{
		Owner = owner;
		Title = "What's New";
		Width = 720; Height = 560; MinWidth = 420; MinHeight = 320;
		ShowInTaskbar = false;
		ShowActivated = owner?.WindowState != WindowState.Minimized;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/MainResourceDictionary.xaml", UriKind.Relative) });
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Redux;component/Themes/Typography.xaml", UriKind.Relative) });
		var settings = preferences ?? MainWindow.Self?.ViewModel?.Settings;
		var typography = ReduxTypographyService.ResolveSelection(settings);
		Resources["Redux.FontFamily.UI"] = ReduxTypographyService.ResolveFontFamily(typography.Font, typography.CustomReference);
		foreach (var size in new[] { 11, 12, 13, 14, 16, 20 })
			if (Application.Current?.TryFindResource($"Redux.FontSize.{size}") is double scaled)
				Resources[$"Redux.FontSize.{size}"] = scaled;
		savePreferences ??= () => MainWindow.Self?.ViewModel?.SaveSettings();
		ReduxThemeService.Apply(Resources, settings?.ColorTheme ?? ReduxThemeType.ReduxDark, settings == null ? null : ReduxThemeService.GetActiveTheme(settings));
		SetResourceReference(TemplateProperty, "ReduxWindowTemplate");
		SetResourceReference(BackgroundProperty, "ReduxSurfaceBrush");
		SetResourceReference(ForegroundProperty, "ReduxTextPrimaryBrush");
		SetResourceReference(FontFamilyProperty, "Redux.FontFamily.UI");
		var root = new Grid();
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition());
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		var titleBar = new ReduxWindowTitleBar();
		titleBar.SetResourceReference(ReduxWindowTitleBar.IconDataProperty, "Redux.Icon.DocumentText");
		root.Children.Add(titleBar);
		var heading = new TextBlock { Text = "Update changelog", Margin = new Thickness(18, 14, 18, 10), TextWrapping = TextWrapping.Wrap };
		heading.SetResourceReference(TextBlock.FontSizeProperty, "Redux.FontSize.20");
		Grid.SetRow(heading, 1); root.Children.Add(heading);
		_notes.Margin = new Thickness(18, 0, 18, 8);
		Grid.SetRow(_notes, 2); root.Children.Add(_notes);
		var tag = "v" + version.TrimStart('v');
		var url = "https://github.com/circleainn/BG3ModManager-Redux/releases/tag/" + Uri.EscapeDataString(tag);
		var footer = new StackPanel { Margin = new Thickness(18, 8, 18, 14) };
		var suppress = new CheckBox { Content = "Do not show for future updates", IsChecked = settings?.ShowWhatsNewAfterUpdates == false, Margin = new Thickness(0, 0, 0, 10) };
		suppress.SetResourceReference(StyleProperty, "ReduxCheckBoxStyle");
		System.Windows.Automation.AutomationProperties.SetName(suppress, "Do not show What's New for future updates");
		void RememberPreference(object sender, RoutedEventArgs args)
		{
			if (settings == null) return;
			settings.ShowWhatsNewAfterUpdates = suppress.IsChecked != true;
			savePreferences();
		}
		suppress.Checked += RememberPreference;
		suppress.Unchecked += RememberPreference;
		footer.Children.Add(suppress);
		var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
		StackPanel ActionContent(UIElement icon, string label)
		{
			var panel = new StackPanel { Orientation = Orientation.Horizontal };
			panel.Children.Add(icon);
			panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
			return panel;
		}
		var githubIcon = new ContentControl { Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
		githubIcon.SetResourceReference(ContentControl.ContentTemplateProperty, "GithubPlatformIconTemplate");
		var supportIcon = ReduxIcon.FromResource("Redux.Icon.Heart", true);
		supportIcon.Width = 14; supportIcon.Height = 14;
		supportIcon.Margin = new Thickness(0, 0, 6, 0);
		supportIcon.VerticalAlignment = VerticalAlignment.Center;
		var closeIcon = ReduxIcon.FromResource("Redux.Icon.Close", true);
		closeIcon.Width = 14; closeIcon.Height = 14;
		closeIcon.Margin = new Thickness(0, 0, 6, 0);
		closeIcon.VerticalAlignment = VerticalAlignment.Center;
		var github = new Button { Content = ActionContent(githubIcon, "View on GitHub"), Margin = new Thickness(0, 0, 8, 0) };
		System.Windows.Automation.AutomationProperties.SetName(github, "View release notes on GitHub");
		github.SetResourceReference(StyleProperty, "ReduxSecondaryActionButtonStyle");
		github.Click += (_, _) => ProcessHelper.TryOpenUrl(url);
		var support = new Button { Content = ActionContent(supportIcon, "Support me"), Margin = new Thickness(0, 0, 8, 0) };
		System.Windows.Automation.AutomationProperties.SetName(support, "Support circleain on Ko-fi");
		support.ToolTip = "Support circleain on Ko-fi";
		support.SetResourceReference(StyleProperty, "ReduxSecondaryActionButtonStyle");
		support.Click += (_, _) => ProcessHelper.TryOpenUrl(DivinityApp.URL_REDUX_DONATION);
		var close = new Button { Content = ActionContent(closeIcon, "Close"), IsCancel = true, IsDefault = true };
		System.Windows.Automation.AutomationProperties.SetName(close, "Close");
		close.SetResourceReference(StyleProperty, "ReduxPrimaryActionButtonStyle");
		close.Click += (_, _) => Close();
		actions.Children.Add(support); actions.Children.Add(github); actions.Children.Add(close);
		footer.Children.Add(actions);
		Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
		ReduxWindowBehavior.AttachDialogTransitions(this, 40);
		ReduxWindowBehavior.AttachRoundedCorners(this);
		Closed += (_, _) => _closed.Cancel();
		Loaded += async (_, _) =>
		{
			SetNotes("Loading release notes from GitHub…");
			try
			{
				using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024 };
				client.DefaultRequestHeaders.UserAgent.ParseAdd("BG3ModManagerRedux");
				var json = await client.GetStringAsync("https://api.github.com/repos/circleainn/BG3ModManager-Redux/releases/tags/" + Uri.EscapeDataString(tag), _closed.Token);
				var body = JObject.Parse(json).Value<string>("body");
				if (!_closed.IsCancellationRequested) SetNotes(String.IsNullOrWhiteSpace(body) ? "Release notes are available on GitHub." : body);
			}
			catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Newtonsoft.Json.JsonException)
			{
				if (!_closed.IsCancellationRequested) SetNotes("Release notes couldn't be loaded. You can keep using Redux or view this release on GitHub.");
			}
		};
	}
	public static string CleanReleaseNotes(string text)
	{
		var notes = Regex.Replace(text ?? String.Empty, @"<!--[\s\S]*?(?:-->|$)",
			String.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
		return Regex.Replace(notes, @"\bsilent[ -]+(?=maintenance\b|hotfix\b|update\b)",
			String.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Trim();
	}

	public void SetNotes(string text)
	{
		Style Heading(string size)
		{
			var style = new Style(typeof(Paragraph));
			style.Setters.Add(new Setter(TextElement.FontSizeProperty, FindResource(size)));
			style.Setters.Add(new Setter(TextElement.FontWeightProperty, FontWeights.SemiBold));
			style.Setters.Add(new Setter(Block.MarginProperty, new Thickness(0, 10, 0, 6)));
			return style;
		}
		var markdown = new Markdown {
			Heading1Style = Heading("Redux.FontSize.20"), Heading2Style = Heading("Redux.FontSize.16"),
			Heading3Style = Heading("Redux.FontSize.14"), Heading4Style = Heading("Redux.FontSize.13")
		};
		_notes.Document = markdown.Transform(CleanReleaseNotes(text));
		_notes.Document.Resources.MergedDictionaries.Add(Resources);
		_notes.Document.FontFamily = (FontFamily)FindResource("Redux.FontFamily.UI");
		_notes.Document.FontSize = (double)FindResource("Redux.FontSize.13");
		_notes.Document.PagePadding = new Thickness(8);
		ApplyReadingColors();
	}

	private void ApplyReadingColors()
	{
		if (_notes.Document == null) return;
		_notes.SetResourceReference(Control.BackgroundProperty, "ReduxAppBackgroundBrush");
		_notes.Document.SetResourceReference(TextElement.BackgroundProperty, "ReduxAppBackgroundBrush");
		_notes.Document.SetResourceReference(TextElement.ForegroundProperty, "ReduxTextPrimaryBrush");
		void Apply(DependencyObject parent)
		{
			foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
			{
				if (child is TextElement element)
				{
					element.SetResourceReference(TextElement.ForegroundProperty, "ReduxTextPrimaryBrush");
					element.Background = Brushes.Transparent;
					if (element is Hyperlink link) link.TextDecorations = TextDecorations.Underline;
				}
				Apply(child);
			}
		}
		Apply(_notes.Document);
	}

	public static Color ReadingForeground(Color background)
	{
		static double Linear(byte value) { var channel = value / 255d; return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4); }
		var luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
		return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) ? Colors.Black : Colors.White;
	}
}
