using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;

using WpfScreenHelper;

namespace DivinityModManager.Util;

/// <summary>
/// Shared sizing and motion behavior for secondary Redux windows. Primary-window
/// persistence remains owned by MainWindow.
/// </summary>
public static class ReduxWindowBehavior
{
	private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(140);
	private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(150);
	private static readonly ConditionalWeakTable<Window, AnimatedCloseState> AnimatedCloseStates = new();
	private static readonly ConditionalWeakTable<Window, AdaptiveSizingState> AdaptiveSizingStates = new();
	private static readonly ConditionalWeakTable<Window, WorkAreaState> WorkAreaStates = new();
	private static readonly ConditionalWeakTable<Window, RoundedCornerState> RoundedCornerStates = new();
	private const int WmGetMinMaxInfo = 0x0024;
	private const uint MonitorDefaultToNearest = 0x00000002;
	private const int DwmWindowCornerPreferenceAttribute = 33;
	private const int DwmWindowBorderColorAttribute = 34;
	private const int DwmWindowCornerPreferenceDoNotRound = 1;
	private const int DwmWindowCornerPreferenceRound = 2;
	private const int DwmColorNone = unchecked((int)0xFFFFFFFE);

	private sealed class AnimatedCloseState
	{
		public bool IsClosing { get; set; }
		public bool BypassAnimation { get; set; }
	}

	private sealed class AdaptiveSizingState
	{
		public bool IsInitialized { get; set; }
		public double DeclaredMinWidth { get; set; }
		public double DeclaredMinHeight { get; set; }
		public double DeclaredMaxWidth { get; set; }
		public double DeclaredMaxHeight { get; set; }
	}

	private sealed class WorkAreaState
	{
		public bool IsAttached { get; set; }
	}

	private sealed class RoundedCornerState
	{
		public bool IsAttached { get; set; }
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MinMaxInfo
	{
		public NativePoint Reserved;
		public NativePoint MaxSize;
		public NativePoint MaxPosition;
		public NativePoint MinTrackSize;
		public NativePoint MaxTrackSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MonitorInfo
	{
		public int Size;
		public NativeRect Monitor;
		public NativeRect WorkArea;
		public uint Flags;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(
		IntPtr windowHandle,
		int attribute,
		ref int attributeValue,
		int attributeSize);

	public static void AttachAdaptiveSizing(Window window, double workAreaMargin = 48)
	{
		var state = AdaptiveSizingStates.GetOrCreateValue(window);
		window.Loaded += (_, _) =>
		{
			if (!state.IsInitialized)
			{
				state.DeclaredMinWidth = window.MinWidth;
				state.DeclaredMinHeight = window.MinHeight;
				state.DeclaredMaxWidth = window.MaxWidth;
				state.DeclaredMaxHeight = window.MaxHeight;
				state.IsInitialized = true;
			}
			ClampToWorkArea(window, state, workAreaMargin);
		};
	}

	public static void AttachWorkAreaMaximize(Window window)
	{
		var state = WorkAreaStates.GetOrCreateValue(window);
		if (state.IsAttached)
		{
			return;
		}

		void AttachHook()
		{
			if (state.IsAttached || PresentationSource.FromVisual(window) is not HwndSource source)
			{
				return;
			}

			source.AddHook(ConstrainMaximizedWindowToWorkArea);
			state.IsAttached = true;
		}

		if (PresentationSource.FromVisual(window) is HwndSource)
		{
			AttachHook();
		}
		else
		{
			window.SourceInitialized += (_, _) => AttachHook();
		}
	}

	public static void AttachRoundedCorners(Window window)
	{
		var state = RoundedCornerStates.GetOrCreateValue(window);
		if (state.IsAttached)
		{
			return;
		}

		state.IsAttached = true;
		void Apply() => ApplyRoundedCornerPreference(window);

		if (PresentationSource.FromVisual(window) is HwndSource)
		{
			Apply();
		}
		else
		{
			window.SourceInitialized += (_, _) => Apply();
		}

		window.StateChanged += (_, _) => Apply();
	}

	private static void ApplyRoundedCornerPreference(Window window)
	{
		var chrome = WindowChrome.GetWindowChrome(window);
		var fallbackRadius = window.WindowState == WindowState.Maximized
			? new CornerRadius(0)
			: new CornerRadius(8);

		try
		{
			var handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
			{
				return;
			}

			var preference = window.WindowState == WindowState.Maximized
				? DwmWindowCornerPreferenceDoNotRound
				: DwmWindowCornerPreferenceRound;
			var result = DwmSetWindowAttribute(
				handle,
				DwmWindowCornerPreferenceAttribute,
				ref preference,
				sizeof(int));
			if (result == 0)
			{
				// Windows 11 otherwise adds its own light system border around the
				// rounded HWND. Redux paints a semantic, theme-aware outline inside
				// the window template, so suppress the duplicate native stroke.
				var borderColor = DwmColorNone;
				DwmSetWindowAttribute(
					handle,
					DwmWindowBorderColorAttribute,
					ref borderColor,
					sizeof(int));
			}

			// DWM owns the outer curve on supported Windows versions. Keeping a WPF
			// WindowChrome radius as well creates a second hard-edged region that clips
			// the one-pixel Redux outline. Fall back to the WPF radius only when the DWM
			// preference is unavailable.
			if (chrome != null)
			{
				chrome.CornerRadius = result == 0
					? new CornerRadius(0)
					: fallbackRadius;
			}
		}
		catch (DllNotFoundException)
		{
			if (chrome != null) chrome.CornerRadius = fallbackRadius;
		}
		catch (EntryPointNotFoundException)
		{
			if (chrome != null) chrome.CornerRadius = fallbackRadius;
		}
	}

	private static IntPtr ConstrainMaximizedWindowToWorkArea(
		IntPtr windowHandle,
		int message,
		IntPtr wParam,
		IntPtr lParam,
		ref bool handled)
	{
		if (message != WmGetMinMaxInfo)
		{
			return IntPtr.Zero;
		}

		var monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
		if (monitorHandle == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}

		var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
		if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
		{
			return IntPtr.Zero;
		}

		var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
		minMaxInfo.MaxPosition.X = monitorInfo.WorkArea.Left - monitorInfo.Monitor.Left;
		minMaxInfo.MaxPosition.Y = monitorInfo.WorkArea.Top - monitorInfo.Monitor.Top;
		minMaxInfo.MaxSize.X = monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
		minMaxInfo.MaxSize.Y = monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
		Marshal.StructureToPtr(minMaxInfo, lParam, false);
		handled = true;
		return IntPtr.Zero;
	}

	public static void AttachDialogTransitions(Window window, double workAreaMargin = 48)
	{
		AttachAdaptiveSizing(window, workAreaMargin);
		var state = AnimatedCloseStates.GetOrCreateValue(window);
		PrepareEntrance(window);
		window.Loaded += (_, _) => AnimateEntrance(window, 0);
		window.Closing += (_, e) => AnimateDialogClosing(window, state, e);
	}

	public static bool? ShowDialogWithOwnerBackdrop(Window dialog, Window owner)
	{
		if (owner?.Content is not UIElement ownerContent)
		{
			return dialog.ShowDialog();
		}

		var previousEffect = ownerContent.Effect;
		var previousOpacity = ownerContent.Opacity;
		ownerContent.Effect = new BlurEffect
		{
			Radius = 2.5,
			RenderingBias = RenderingBias.Performance
		};
		ownerContent.Opacity = 0.88;

		try
		{
			return dialog.ShowDialog();
		}
		finally
		{
			ownerContent.Effect = previousEffect;
			ownerContent.Opacity = previousOpacity;
		}
	}

	// Windows are not layered (AllowsTransparency=False), so animating Window.Opacity directly
	// routes through OS-level layered-window compositing instead of WPF's own render pipeline,
	// producing choppy, black-flashing fades. Animating the content root keeps the fade entirely
	// inside WPF while the native frame/shadow stays solid.
	private static UIElement GetAnimationTarget(Window window) => window.Content as UIElement ?? window;

	public static void PrepareEntrance(Window window, double fromOpacity = 0)
	{
		var target = GetAnimationTarget(window);
		target.BeginAnimation(UIElement.OpacityProperty, null);
		target.Opacity = SystemParameters.ClientAreaAnimation
			? Math.Clamp(fromOpacity, 0, 1)
			: 1;
	}

	private static void AnimateDialogClosing(Window window, AnimatedCloseState state, CancelEventArgs e)
	{
		if (state.BypassAnimation || !window.IsVisible || !SystemParameters.ClientAreaAnimation) return;

		e.Cancel = true;
		if (state.IsClosing) return;
		state.IsClosing = true;
		var result = window.DialogResult;
		AnimateExit(window, () =>
		{
			state.BypassAnimation = true;
			try
			{
				if (result.HasValue) window.DialogResult = result;
				else window.Close();
			}
			finally
			{
				state.IsClosing = false;
				state.BypassAnimation = false;
			}
		});
	}

	private static void ClampToWorkArea(Window window, AdaptiveSizingState state, double workAreaMargin)
	{
		var referenceWindow = window.Owner ?? window;
		var workArea = referenceWindow.IsLoaded
			? Screen.FromWindow(referenceWindow).WorkingArea
			: SystemParameters.WorkArea;
		var availableWidth = Math.Max(320, workArea.Width - workAreaMargin);
		var availableHeight = Math.Max(240, workArea.Height - workAreaMargin);

		window.MinWidth = Math.Min(state.DeclaredMinWidth, availableWidth);
		window.MinHeight = Math.Min(state.DeclaredMinHeight, availableHeight);
		window.MaxWidth = Double.IsNaN(state.DeclaredMaxWidth) || Double.IsInfinity(state.DeclaredMaxWidth)
			? availableWidth
			: Math.Min(state.DeclaredMaxWidth, availableWidth);
		window.MaxHeight = Double.IsNaN(state.DeclaredMaxHeight) || Double.IsInfinity(state.DeclaredMaxHeight)
			? availableHeight
			: Math.Min(state.DeclaredMaxHeight, availableHeight);

		if (!Double.IsNaN(window.Width))
			window.Width = Math.Clamp(window.Width, window.MinWidth, window.MaxWidth);
		if (!Double.IsNaN(window.Height))
			window.Height = Math.Clamp(window.Height, window.MinHeight, window.MaxHeight);
	}

	/// <summary>
	/// Completes on the next composition frame.
	/// </summary>
	/// <remarks>
	/// A WPF animation clock runs in wall-clock time whether or not frames are being
	/// presented. Starting one in the same dispatcher pass as heavy work (building a large
	/// visual tree, moving a window onscreen) means the clock elapses while the UI thread is
	/// blocked, so the next frame the compositor presents is already the end state and the
	/// animation is never seen. Awaiting a real frame at the start value first is what makes
	/// these transitions visible.
	/// </remarks>
	public static Task WaitForRenderFrameAsync()
	{
		var completion = new TaskCompletionSource();
		EventHandler handler = null;
		handler = (_, _) =>
		{
			CompositionTarget.Rendering -= handler;
			completion.TrySetResult();
		};
		CompositionTarget.Rendering += handler;
		return completion.Task;
	}

	/// <summary>
	/// Awaitable form of <see cref="AnimateEntrance"/>, so a caller can sequence work against
	/// the entrance actually finishing rather than against the call that started it.
	/// </summary>
	public static Task AnimateEntranceAsync(Window window, double fromOpacity, Duration? duration = null)
	{
		var target = GetAnimationTarget(window);
		target.BeginAnimation(UIElement.OpacityProperty, null);
		if (!SystemParameters.ClientAreaAnimation)
		{
			target.Opacity = 1;
			return Task.CompletedTask;
		}

		var completion = new TaskCompletionSource();
		var animation = new DoubleAnimation(fromOpacity, 1, duration ?? EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		animation.Completed += (_, _) => completion.TrySetResult();
		target.BeginAnimation(UIElement.OpacityProperty, animation);
		return completion.Task;
	}

	public static void AnimateEntrance(Window window, double fromOpacity = 0.92)
	{
		var target = GetAnimationTarget(window);
		target.BeginAnimation(UIElement.OpacityProperty, null);
		if (!SystemParameters.ClientAreaAnimation)
		{
			target.Opacity = 1;
			return;
		}

		target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(fromOpacity, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		});
	}

	public static void AnimateExit(Window window, Action completed)
	{
		var target = GetAnimationTarget(window);
		if (!window.IsVisible || !SystemParameters.ClientAreaAnimation)
		{
			completed();
			return;
		}

		var animation = new DoubleAnimation(target.Opacity, 0, ExitDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		animation.Completed += (_, _) =>
		{
			target.BeginAnimation(UIElement.OpacityProperty, null);
			target.Opacity = 1;
			completed();
		};
		target.BeginAnimation(UIElement.OpacityProperty, animation);
	}

}
