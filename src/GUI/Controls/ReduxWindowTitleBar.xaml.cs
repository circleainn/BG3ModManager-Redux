using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace DivinityModManager.Controls;

public partial class ReduxWindowTitleBar : UserControl
{
	private bool _windowActionRunning;

	public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
		nameof(IconData),
		typeof(Geometry),
		typeof(ReduxWindowTitleBar),
		new PropertyMetadata(null));

	public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
		nameof(IconSource),
		typeof(ImageSource),
		typeof(ReduxWindowTitleBar),
		new PropertyMetadata(null));

	public static readonly DependencyProperty BottomBorderThicknessProperty = DependencyProperty.Register(
		nameof(BottomBorderThickness),
		typeof(Thickness),
		typeof(ReduxWindowTitleBar),
		new PropertyMetadata(new Thickness(0, 0, 0, 1)));

	public Geometry? IconData
	{
		get => (Geometry?)GetValue(IconDataProperty);
		set => SetValue(IconDataProperty, value);
	}

	public ImageSource? IconSource
	{
		get => (ImageSource?)GetValue(IconSourceProperty);
		set => SetValue(IconSourceProperty, value);
	}

	public Thickness BottomBorderThickness
	{
		get => (Thickness)GetValue(BottomBorderThicknessProperty);
		set => SetValue(BottomBorderThicknessProperty, value);
	}

	public ReduxWindowTitleBar()
	{
		InitializeComponent();
		Loaded += (_, _) =>
		{
			if (OwnerWindow is { } window)
			{
				EnsureWindowChrome(window);
				ReduxWindowBehavior.AttachWindowMotionPreference(window);
				ReduxWindowBehavior.AttachRoundedCorners(window);
				ReduxWindowBehavior.AttachWorkAreaMaximize(window);
				window.StateChanged -= OwnerWindow_StateChanged;
				window.StateChanged += OwnerWindow_StateChanged;
			}
			UpdateWindowState();
		};
		Unloaded += (_, _) =>
		{
			if (OwnerWindow is { } window)
			{
				window.StateChanged -= OwnerWindow_StateChanged;
			}
		};
	}

	private Window OwnerWindow => Window.GetWindow(this);

	private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (OwnerWindow is not { } window || e.ChangedButton != MouseButton.Left)
		{
			return;
		}

		if (e.ClickCount == 2 && CanResize(window))
		{
			ToggleWindowState(window);
			e.Handled = true;
			return;
		}

		if (e.ClickCount == 1)
		{
			window.DragMove();
		}
	}

	private void MinimizeButton_Click(object sender, RoutedEventArgs e)
	{
		if (_windowActionRunning || OwnerWindow is not { } window)
		{
			return;
		}

		SystemCommands.MinimizeWindow(window);
	}

	private void MaximizeButton_Click(object sender, RoutedEventArgs e)
	{
		if (OwnerWindow is not { } window)
		{
			return;
		}

		if (_windowActionRunning || !CanResize(window))
		{
			return;
		}

		ToggleWindowState(window);
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		if (_windowActionRunning || OwnerWindow is not { } window)
		{
			return;
		}

		if (window is IReduxAnimatedHideWindow animatedWindow)
		{
			animatedWindow.HideWithTransition();
			return;
		}

		if (ReduxWindowBehavior.ReduceMotion)
		{
			// Hide first so neither the Redux opacity transition nor Windows'
			// visible close composition can produce a fade.
			window.Hide();
			ReduxWindowBehavior.RemoveOwnerBackdrop(window);
			SystemCommands.CloseWindow(window);
			return;
		}

		_windowActionRunning = true;
		ReduxWindowBehavior.AnimateExit(window, () =>
		{
			_windowActionRunning = false;
			SystemCommands.CloseWindow(window);
		});
	}

	private void OwnerWindow_StateChanged(object sender, EventArgs e)
	{
		UpdateWindowState();
	}

	private void ToggleWindowState(Window window)
	{
		if (!CanResize(window))
		{
			return;
		}

		if (window.WindowState == WindowState.Maximized)
		{
			SystemCommands.RestoreWindow(window);
		}
		else
		{
			SystemCommands.MaximizeWindow(window);
		}
	}

	private void UpdateWindowState()
	{
		var window = OwnerWindow;
		if (window == null)
		{
			return;
		}

		var canResize = CanResize(window);
		MaximizeButton.Visibility = canResize ? Visibility.Visible : Visibility.Collapsed;
		MinimizeButton.Visibility = window.ResizeMode == ResizeMode.NoResize
			? Visibility.Collapsed
			: Visibility.Visible;
		MaximizeIcon.Visibility = window.WindowState == WindowState.Maximized
			? Visibility.Collapsed
			: Visibility.Visible;
		RestoreIcon.Visibility = window.WindowState == WindowState.Maximized
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	private static bool CanResize(Window window)
	{
		return window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
	}

	private static void EnsureWindowChrome(Window window)
	{
		if (WindowChrome.GetWindowChrome(window) != null)
		{
			return;
		}

		WindowChrome.SetWindowChrome(window, new WindowChrome
		{
			CaptionHeight = 0,
			CornerRadius = new CornerRadius(0),
			GlassFrameThickness = new Thickness(0),
			ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
			UseAeroCaptionButtons = false
		});
	}
}
