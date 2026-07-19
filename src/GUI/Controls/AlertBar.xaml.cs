using DivinityModManager.Util.ScreenReader;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Controls;

/// <summary>
/// Source: https://github.com/chadkuehn/AlertBarWpf
/// MIT License 2014
/// </summary>
public partial class AlertBar : UserControl
{
	private readonly SynchronizationContext _syncContext;

	private string barText = "";
	public string GetText()
	{
		return barText;
	}

	public AlertBar()
	{
		InitializeComponent();
		// grdWrapper.DataContext = this;

		_syncContext = SynchronizationContext.Current;
	}

	public static readonly RoutedEvent ShowEvent = EventManager.RegisterRoutedEvent("Show", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AlertBar));

	public event RoutedEventHandler Show
	{
		add { AddHandler(ShowEvent, value); }
		remove { RemoveHandler(ShowEvent, value); }
	}

	private void RaiseShowEvent()
	{
		RoutedEventArgs newEventArgs = new RoutedEventArgs(AlertBar.ShowEvent);
		RaiseEvent(newEventArgs);
	}

	private Brush GetSemanticBrush(string resourceKey, string fallbackColor)
	{
		return TryFindResource(resourceKey) as Brush
			?? (Brush)new BrushConverter().ConvertFrom(fallbackColor);
	}

	private static Brush CreateSoftBrush(Brush accentBrush, byte alpha)
	{
		var color = accentBrush is SolidColorBrush solidBrush ? solidBrush.Color : Colors.Transparent;
		return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
	}

	private static Brush CreateSoftGradientBrush(Brush accentBrush)
	{
		var color = accentBrush is SolidColorBrush solidBrush ? solidBrush.Color : Colors.Transparent;
		return new LinearGradientBrush(
			Color.FromArgb(0x34, color.R, color.G, color.B),
			Color.FromArgb(0x1A, color.R, color.G, color.B),
			0);
	}

	private void TransformStage(string msg, int secs, Brush accentBrush, Geometry iconData)
	{

		Grid grdParent;
		switch (_Theme)
		{
			case ThemeType.Standard:
				spStandard.Visibility = Visibility.Visible;
				spOutline.Visibility = Visibility.Collapsed;

				grdParent = FindVisualChildren<Grid>(spStandard).FirstOrDefault();
				bdrStandard.BorderBrush = accentBrush;
				bdrStandard.Background = CreateSoftGradientBrush(accentBrush);
				StandardIconSurface.Background = CreateSoftBrush(accentBrush, 0x40);
				break;
			case ThemeType.Outline:
			default:
				spStandard.Visibility = Visibility.Collapsed;
				spOutline.Visibility = Visibility.Visible;

				grdParent = FindVisualChildren<Grid>(spOutline).FirstOrDefault();
				bdr.BorderBrush = accentBrush;
				break;
		}

		TextBlock lblMessage = FindVisualChildren<TextBlock>(grdParent).FirstOrDefault();
		List<ReduxIcon> icons = FindVisualChildren<ReduxIcon>(grdParent).ToList();
		ReduxIcon statusIcon = icons[0];
		ReduxIcon closeIcon = icons[1];
		lblMessage.Foreground = accentBrush;

		if (_IconVisibility == false)
		{
			statusIcon.Visibility = Visibility.Collapsed;
			grdParent.ColumnDefinitions.RemoveAt(0);
			lblMessage.SetValue(Grid.ColumnProperty, 0);
			closeIcon.SetValue(Grid.ColumnProperty, 1);
			lblMessage.Margin = new Thickness(10, 4, 0, 4);
			lblMessage.Height = 16;
		}
		else
		{
			statusIcon.Data = iconData;
			statusIcon.Foreground = accentBrush;
		}

		lblMessage.Text = msg;
		grdWrapper.Visibility = Visibility.Visible;
		key1.KeyTime = new TimeSpan(0, 0, (secs <= 0 ? 0 : secs - 1));
		key2.KeyTime = new TimeSpan(0, 0, secs);
		RaiseShowEvent();

		if (AutomationPeer.ListenerExists(AutomationEvents.AutomationFocusChanged))
		{
			if (barText != msg)
			{
				barText = msg;
				AutomationProperties.SetHelpText(this, barText);
				var peer = UIElementAutomationPeer.FromElement(this);
				if (peer == null)
					peer = UIElementAutomationPeer.CreatePeerForElement(this);
				peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
				//peer.RaiseAutomationEvent(AutomationEvents.TextPatternOnTextChanged);
			}
		}
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
	{
		if (depObj != null)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child != null && child is T)
				{
					yield return (T)child;
				}

				foreach (T childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}
	}

	public List<TextBlock> GetTextElements()
	{
		Grid grdParent;
		switch (_Theme)
		{
			case ThemeType.Standard:
				grdParent = FindVisualChildren<Grid>(spStandard).FirstOrDefault();
				break;
			case ThemeType.Outline:
			default:
				grdParent = FindVisualChildren<Grid>(spOutline).FirstOrDefault();
				break;
		}

		var textElements = FindVisualChildren<TextBlock>(grdParent).ToList();
		return textElements;
	}


	/// <summary>
	/// Shows a Danger Alert
	/// </summary>
	/// <param name="message">The message for the alert</param>
	/// <param name="timeoutInSeconds">Alert will auto-close in this amount of seconds</param>
	public void SetDangerAlert(string message, int timeoutInSeconds = 0)
	{
		_syncContext.Post(o =>
		{
			TransformStage(message, timeoutInSeconds, GetSemanticBrush("ReduxErrorBrush", "#D95D6A"), (Geometry)FindResource("Redux.Icon.Danger"));
		}, null);
	}

	/// <summary>
	/// Shows a warning Alert
	/// </summary>
	/// <param name="message">The message for the alert</param>
	/// <param name="timeoutInSeconds">Alert will auto-close in this amount of seconds</param>
	public void SetWarningAlert(string message, int timeoutInSeconds = 0)
	{
		_syncContext.Post(o =>
		{
			TransformStage(message, timeoutInSeconds, GetSemanticBrush("ReduxWarningBrush", "#D7A24B"), (Geometry)FindResource("Redux.Icon.Warning"));
		}, null);
	}

	/// <summary>
	/// Shows a Success Alert
	/// </summary>
	/// <param name="message">The message for the alert</param>
	/// <param name="timeoutInSeconds">Alert will auto-close in this amount of seconds</param>
	public void SetSuccessAlert(string message, int timeoutInSeconds = 0)
	{
		_syncContext.Post(o =>
		{
			TransformStage(message, timeoutInSeconds, GetSemanticBrush("ReduxSuccessBrush", "#3FA37A"), (Geometry)FindResource("Redux.Icon.Success"));
		}, null);
	}


	/// <summary>
	/// Shows an Information Alert
	/// </summary>
	/// <param name="message">The message for the alert</param>
	/// <param name="timeoutInSeconds">Alert will auto-close in this amount of seconds</param>
	public void SetInformationAlert(string message, int timeoutInSeconds = 0)
	{
		_syncContext.Post(o =>
		{
			TransformStage(message, timeoutInSeconds, GetSemanticBrush("ReduxAccentBrush", "#8A6AF1"), (Geometry)FindResource("Redux.Icon.Information"));
		}, null);
	}

	public enum ThemeType
	{
		Standard = 0,
		Outline = 1
	}

	private ThemeType _Theme = ThemeType.Standard;
	private bool _IconVisibility = true;

	/// <summary>
	/// Hide or show icons in the messages.
	/// </summary>
	public bool? IconVisibility
	{
		set
		{
			if (value == null)
			{
				return;
			}
			_IconVisibility = value ?? false;
		}
		get
		{
			return _IconVisibility;
		}
	}

	public ThemeType? Theme
	{
		set
		{
			if (value == null)
			{
				return;
			}
			if (Enum.IsDefined(typeof(ThemeType), value))
			{
				_Theme = value ?? ThemeType.Standard;
			}
		}

		get
		{
			return _Theme;
		}
	}

	/// <summary>
	/// Remove a message if one is currently being shown.
	/// </summary>
	public void Clear()
	{
		grdWrapper.Visibility = Visibility.Collapsed;
	}

	private void Image_MouseUp(object sender, MouseButtonEventArgs e)
	{
		Clear();
	}

	private void AnimationObject_Completed(object sender, EventArgs e)
	{
		if (grdWrapper.Opacity == 0)
		{
			//If you call msgbar.setErrorMessage("Whateva") in MainWindow() of your WPF the window is not rendered yet.  So opacity is 0.  If you have a timeout of 0 then it would call this immediately
			if (key1.KeyTime.TimeSpan.Seconds > 0)
			{
				Clear();
			}
		}
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new AlertBarAutomationPeer(this);
	}
}
