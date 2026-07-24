using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

using WpfScreenHelper;

namespace DivinityModManager.Util;

/// <summary>
/// Shared sizing and motion behavior for secondary Redux windows. Primary-window
/// persistence remains owned by MainWindow.
/// </summary>
public static class ReduxWindowBehavior
{
	private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(140);
	private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(120);
	private static readonly ConditionalWeakTable<Window, AnimatedCloseState> AnimatedCloseStates = new();
	private static readonly ConditionalWeakTable<Window, AdaptiveSizingState> AdaptiveSizingStates = new();

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

	public static void AttachDialogTransitions(Window window, double workAreaMargin = 48)
	{
		AttachAdaptiveSizing(window, workAreaMargin);
		var state = AnimatedCloseStates.GetOrCreateValue(window);
		window.Opacity = SystemParameters.ClientAreaAnimation ? 0 : 1;
		window.Loaded += (_, _) => AnimateEntrance(window, 0);
		window.Closing += (_, e) => AnimateDialogClosing(window, state, e);
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

	public static void AnimateEntrance(Window window, double fromOpacity = 0.92)
	{
		window.BeginAnimation(UIElement.OpacityProperty, null);
		if (!SystemParameters.ClientAreaAnimation)
		{
			window.Opacity = 1;
			return;
		}

		window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(fromOpacity, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		});
	}

	public static void AnimateExit(Window window, Action completed)
	{
		if (!window.IsVisible || !SystemParameters.ClientAreaAnimation)
		{
			completed();
			return;
		}

		var animation = new DoubleAnimation(window.Opacity, 0, ExitDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		animation.Completed += (_, _) =>
		{
			window.BeginAnimation(UIElement.OpacityProperty, null);
			window.Opacity = 1;
			completed();
		};
		window.BeginAnimation(UIElement.OpacityProperty, animation);
	}
}
