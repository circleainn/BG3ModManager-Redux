using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;

using WpfScreenHelper;

namespace DivinityModManager.Util;

public enum ReduxPreferredPopupPlacement
{
	Default,
	BelowRightward,
	BesideRightward
}

/// <summary>
/// Freeze-safe opacity animation that preserves existing visual-state triggers
/// and snaps to their destination under Reduce Motion.
/// </summary>
public sealed class ReduxDoubleAnimation : DoubleAnimation
{
	protected override double GetCurrentValueCore(
		double defaultOriginValue,
		double defaultDestinationValue,
		AnimationClock animationClock)
	{
		if (ReduxWindowBehavior.ReduceMotion)
		{
			return To ?? defaultDestinationValue;
		}

		return base.GetCurrentValueCore(defaultOriginValue, defaultDestinationValue, animationClock);
	}

	protected override Freezable CreateInstanceCore() => new ReduxDoubleAnimation();
}

/// <summary>
/// The short row-selection confirmation flash, suppressed under Reduce Motion
/// while leaving the persistent selected surface and rail untouched.
/// </summary>
public sealed class ReduxSelectionFlashAnimation : DoubleAnimationBase
{
	private const double PeakOpacity = 0.52;
	private const double PeakProgress = 60d / 220d;

	protected override double GetCurrentValueCore(
		double defaultOriginValue,
		double defaultDestinationValue,
		AnimationClock animationClock)
	{
		if (ReduxWindowBehavior.ReduceMotion || animationClock.CurrentProgress is not double progress)
		{
			return 0;
		}

		if (progress <= PeakProgress)
		{
			var entering = progress / PeakProgress;
			return PeakOpacity * EaseOut(entering);
		}

		var exiting = (progress - PeakProgress) / (1 - PeakProgress);
		return PeakOpacity * (1 - EaseOut(exiting));
	}

	protected override Freezable CreateInstanceCore() => new ReduxSelectionFlashAnimation();

	private static double EaseOut(double progress) => 1 - Math.Pow(1 - progress, 2);
}

/// <summary>
/// Shared sizing and motion behavior for secondary Redux windows. Primary-window
/// persistence remains owned by MainWindow.
/// </summary>
public static class ReduxWindowBehavior
{
	private const int GwlExStyle = -20;
	private const int WsExAppWindow = 0x00040000;
	private const int WsExToolWindow = 0x00000080;

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
	private static extern int GetWindowLong(IntPtr window, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
	private static extern int SetWindowLong(IntPtr window, int index, int value);

	private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(140);
	private static readonly Duration ExitDuration = TimeSpan.FromMilliseconds(150);
	private static readonly ConditionalWeakTable<Window, AnimatedCloseState> AnimatedCloseStates = new();
	private static readonly ConditionalWeakTable<Window, AdaptiveSizingState> AdaptiveSizingStates = new();
	private static readonly ConditionalWeakTable<Window, WorkAreaState> WorkAreaStates = new();
	private static readonly ConditionalWeakTable<Window, RoundedCornerState> RoundedCornerStates = new();
	private static readonly ConditionalWeakTable<Window, ResizeFeedbackState> ResizeFeedbackStates = new();
	private static readonly ConditionalWeakTable<Window, WindowMotionPreferenceState> WindowMotionPreferenceStates = new();
	private static readonly ConditionalWeakTable<Window, object> TaskbarPresenceStates = new();
	private static readonly ConditionalWeakTable<Window, OwnerBackdropState> OwnerBackdropStates = new();
	private static readonly ConditionalWeakTable<Window, BackdropLeaseState> BackdropLeaseStates = new();
	private static readonly ConditionalWeakTable<FrameworkElement, HoverMotionState> HoverMotionStates = new();
	private static readonly ConditionalWeakTable<ContextMenu, ContextMenuMotionState> ContextMenuMotionStates = new();
	private static readonly List<WeakReference<Window>> BackdropOwners = new();
	private static readonly List<WeakReference<FrameworkElement>> HoverMotionElements = new();
	private static readonly List<WeakReference<Popup>> ManagedPopups = new();
	private static readonly List<WeakReference<ContextMenu>> ManagedContextMenus = new();
	private const int WmGetMinMaxInfo = 0x0024;
	private const int WmCancelMode = 0x001F;
	private const int WmSizing = 0x0214;
	private const int WmCaptureChanged = 0x0215;
	private const int WmMoving = 0x0216;
	private const int WmExitSizeMove = 0x0232;
	private const uint MonitorDefaultToNearest = 0x00000002;
	private const int DwmWindowCornerPreferenceAttribute = 33;
	private const int DwmWindowBorderColorAttribute = 34;
	private const int DwmTransitionsForcedDisabledAttribute = 3;
	private const int DwmWindowCornerPreferenceDoNotRound = 1;
	private const int DwmWindowCornerPreferenceRound = 2;
	private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
	private static readonly MethodInfo ContextMenuHookupParentPopupMethod =
		typeof(ContextMenu).GetMethod("HookupParentPopup", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo ContextMenuParentPopupField =
		typeof(ContextMenu).GetField("_parentPopup", BindingFlags.Instance | BindingFlags.NonPublic);

	private sealed class AnimatedCloseState
	{
		public bool IsAttached { get; set; }
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

	private sealed class ResizeFeedbackState
	{
		public bool IsConfigured { get; set; }
		public bool IsAttached { get; set; }
		public bool IsVisible { get; set; }
		public Border Glow { get; set; }
		public HwndSource Source { get; set; }
		public HwndSourceHook Hook { get; set; }
	}

	private sealed class WindowMotionPreferenceState
	{
		public bool IsAttached { get; set; }
	}

	private sealed class OwnerBackdropState
	{
		public int LeaseCount { get; set; }
		public Effect PreviousEffect { get; set; }
		public double PreviousOpacity { get; set; } = 1;
		public bool EffectsApplied { get; set; }
	}

	private sealed class BackdropLeaseState
	{
		public Window Owner { get; set; }
		public bool IsActive { get; set; }
	}

	private sealed class HoverMotionState
	{
		public bool IsAttached { get; set; }
	}

	private sealed class ContextMenuMotionState
	{
		public bool IsAttached { get; set; }
	}

	public static readonly DependencyProperty SyncActionIconForegroundProperty = DependencyProperty.RegisterAttached(
		"SyncActionIconForeground",
		typeof(bool),
		typeof(ReduxWindowBehavior),
		new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

	public static bool GetSyncActionIconForeground(DependencyObject element) =>
		(bool)element.GetValue(SyncActionIconForegroundProperty);

	public static void SetSyncActionIconForeground(DependencyObject element, bool value) =>
		element.SetValue(SyncActionIconForegroundProperty, value);

	public static readonly DependencyProperty ActionIconForegroundProperty = DependencyProperty.RegisterAttached(
		"ActionIconForeground",
		typeof(Brush),
		typeof(ReduxWindowBehavior),
		new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

	public static Brush GetActionIconForeground(DependencyObject element) =>
		(Brush)element.GetValue(ActionIconForegroundProperty);

	public static void SetActionIconForeground(DependencyObject element, Brush value) =>
		element.SetValue(ActionIconForegroundProperty, value);

	public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
		"HoverLift",
		typeof(double),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(0d, HoverMotionPropertyChanged));

	public static readonly DependencyProperty HoverScaleProperty = DependencyProperty.RegisterAttached(
		"HoverScale",
		typeof(double),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(1d, HoverMotionPropertyChanged));

	public static readonly DependencyProperty SuppressHoverMotionProperty = DependencyProperty.RegisterAttached(
		"SuppressHoverMotion",
		typeof(bool),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(false, HoverMotionPropertyChanged));

	public static double GetHoverLift(DependencyObject element) =>
		(double)element.GetValue(HoverLiftProperty);

	public static void SetHoverLift(DependencyObject element, double value) =>
		element.SetValue(HoverLiftProperty, value);

	public static double GetHoverScale(DependencyObject element) =>
		(double)element.GetValue(HoverScaleProperty);

	public static void SetHoverScale(DependencyObject element, double value) =>
		element.SetValue(HoverScaleProperty, value);

	public static bool GetSuppressHoverMotion(DependencyObject element) =>
		(bool)element.GetValue(SuppressHoverMotionProperty);

	public static void SetSuppressHoverMotion(DependencyObject element, bool value) =>
		element.SetValue(SuppressHoverMotionProperty, value);

	public static readonly DependencyProperty ManagedPopupAnimationProperty = DependencyProperty.RegisterAttached(
		"ManagedPopupAnimation",
		typeof(PopupAnimation),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(PopupAnimation.None, ManagedPopupAnimationPropertyChanged));

	public static PopupAnimation GetManagedPopupAnimation(DependencyObject element) =>
		(PopupAnimation)element.GetValue(ManagedPopupAnimationProperty);

	public static void SetManagedPopupAnimation(DependencyObject element, PopupAnimation value) =>
		element.SetValue(ManagedPopupAnimationProperty, value);

	public static readonly DependencyProperty PreferredPopupPlacementProperty = DependencyProperty.RegisterAttached(
		"PreferredPopupPlacement",
		typeof(ReduxPreferredPopupPlacement),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(ReduxPreferredPopupPlacement.Default, PreferredPopupPlacementPropertyChanged));

	public static ReduxPreferredPopupPlacement GetPreferredPopupPlacement(DependencyObject element) =>
		(ReduxPreferredPopupPlacement)element.GetValue(PreferredPopupPlacementProperty);

	public static void SetPreferredPopupPlacement(DependencyObject element, ReduxPreferredPopupPlacement value) =>
		element.SetValue(PreferredPopupPlacementProperty, value);

	public static readonly DependencyProperty ManageContextMenuMotionProperty = DependencyProperty.RegisterAttached(
		"ManageContextMenuMotion",
		typeof(bool),
		typeof(ReduxWindowBehavior),
		new PropertyMetadata(false, ManageContextMenuMotionPropertyChanged));

	public static bool GetManageContextMenuMotion(DependencyObject element) =>
		(bool)element.GetValue(ManageContextMenuMotionProperty);

	public static void SetManageContextMenuMotion(DependencyObject element, bool value) =>
		element.SetValue(ManageContextMenuMotionProperty, value);

	public static bool ReduceMotion { get; private set; }
	public static bool BackgroundEffectsDisabled { get; private set; }
	private static bool ShouldAnimateOpacity => !ReduceMotion && SystemParameters.ClientAreaAnimation;

	public static void ConfigureAccessibility(bool reduceMotion, bool disableBackgroundEffects)
	{
		ReduceMotion = reduceMotion;
		SmoothLogicalScrollBehavior.ConfigureReducedMotion(reduceMotion);
		if (BackgroundEffectsDisabled != disableBackgroundEffects)
		{
			BackgroundEffectsDisabled = disableBackgroundEffects;
			RefreshActiveBackdrops();
		}

		ApplyPopupMotionPreference(Application.Current?.Resources);
		if (Application.Current != null)
		{
			foreach (Window window in Application.Current.Windows)
			{
				ApplyPopupMotionPreference(window.Resources);
				AttachWindowMotionPreference(window);
				ApplyWindowMotionPreference(window);
			}
		}
		RefreshPopupMotion();
		RefreshHoverMotion();
	}

	private static void ManagedPopupAnimationPropertyChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is not Popup popup)
		{
			return;
		}

		if (e.OldValue == DependencyProperty.UnsetValue)
		{
			ManagedPopups.Add(new WeakReference<Popup>(popup));
		}
		else if (!ManagedPopups.Any(reference =>
			reference.TryGetTarget(out var existing) && ReferenceEquals(existing, popup)))
		{
			ManagedPopups.Add(new WeakReference<Popup>(popup));
		}

		ApplyManagedPopupMotion(popup);
	}

	private static void PreferredPopupPlacementPropertyChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs e)
	{
		ApplyPreferredPopupPlacement(
			dependencyObject,
			(ReduxPreferredPopupPlacement)e.NewValue);
	}

	private static void ApplyPreferredPopupPlacement(
		DependencyObject dependencyObject,
		ReduxPreferredPopupPlacement placement)
	{
		CustomPopupPlacementCallback callback = placement switch
		{
			ReduxPreferredPopupPlacement.BelowRightward => GetBelowRightwardPlacements,
			ReduxPreferredPopupPlacement.BesideRightward => GetBesideRightwardPlacements,
			_ => null
		};

		switch (dependencyObject)
		{
			case Popup popup:
				popup.CustomPopupPlacementCallback = callback;
				if (callback != null)
				{
					popup.Placement = PlacementMode.Custom;
				}
				break;
			case ContextMenu contextMenu:
				contextMenu.CustomPopupPlacementCallback = callback;
				if (callback != null)
				{
					contextMenu.Placement = PlacementMode.Custom;
				}
				break;
		}
	}

	public static CustomPopupPlacement[] GetBelowRightwardPlacements(
		Size popupSize,
		Size targetSize,
		Point offset) =>
	[
		// Align the popup's left edge to the target first, so ordinary menus grow
		// toward the right. The remaining candidates are deliberate screen-edge
		// fallbacks rather than the system-wide left-handed menu preference.
		new CustomPopupPlacement(
			new Point(offset.X, targetSize.Height + offset.Y),
			PopupPrimaryAxis.Horizontal),
		new CustomPopupPlacement(
			new Point(targetSize.Width - popupSize.Width + offset.X, targetSize.Height + offset.Y),
			PopupPrimaryAxis.Horizontal),
		new CustomPopupPlacement(
			new Point(offset.X, -popupSize.Height - offset.Y),
			PopupPrimaryAxis.Horizontal),
		new CustomPopupPlacement(
			new Point(targetSize.Width - popupSize.Width + offset.X, -popupSize.Height - offset.Y),
			PopupPrimaryAxis.Horizontal)
	];

	public static CustomPopupPlacement[] GetBesideRightwardPlacements(
		Size popupSize,
		Size targetSize,
		Point offset) =>
	[
		new CustomPopupPlacement(
			new Point(targetSize.Width + offset.X, offset.Y),
			PopupPrimaryAxis.Vertical),
		new CustomPopupPlacement(
			new Point(-popupSize.Width - offset.X, offset.Y),
			PopupPrimaryAxis.Vertical)
	];

	public static void OpenRightwardDropDown(
		ContextMenu contextMenu,
		FrameworkElement placementTarget,
		double verticalOffset = 0)
	{
		if (contextMenu == null || placementTarget == null)
		{
			return;
		}

		contextMenu.PlacementTarget = placementTarget;
		contextMenu.HorizontalOffset = 0;
		contextMenu.VerticalOffset = verticalOffset;
		SetPreferredPopupPlacement(contextMenu, ReduxPreferredPopupPlacement.BelowRightward);
		ApplyPreferredPopupPlacement(contextMenu, ReduxPreferredPopupPlacement.BelowRightward);
		contextMenu.IsOpen = true;
	}

	private static void ManageContextMenuMotionPropertyChanged(
		DependencyObject dependencyObject,
		DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is not ContextMenu contextMenu || e.NewValue is not true)
		{
			return;
		}

		if (!ManagedContextMenus.Any(reference =>
			reference.TryGetTarget(out var existing) && ReferenceEquals(existing, contextMenu)))
		{
			ManagedContextMenus.Add(new WeakReference<ContextMenu>(contextMenu));
		}

		var state = ContextMenuMotionStates.GetOrCreateValue(contextMenu);
		if (!state.IsAttached)
		{
			state.IsAttached = true;
			contextMenu.Opened += ManagedContextMenu_Opened;
		}

		ApplyManagedContextMenuMotion(contextMenu);
	}

	private static void ManagedContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		if (sender is not ContextMenu contextMenu) return;
		ApplyManagedContextMenuMotion(contextMenu);
		ReduxMenuItemExtension.ApplySemanticHoverToMenu(contextMenu);
	}

	private static void RefreshPopupMotion()
	{
		for (var index = ManagedPopups.Count - 1; index >= 0; index--)
		{
			if (!ManagedPopups[index].TryGetTarget(out var popup))
			{
				ManagedPopups.RemoveAt(index);
				continue;
			}

			ApplyManagedPopupMotion(popup);
		}

		for (var index = ManagedContextMenus.Count - 1; index >= 0; index--)
		{
			if (!ManagedContextMenus[index].TryGetTarget(out var contextMenu))
			{
				ManagedContextMenus.RemoveAt(index);
				continue;
			}

			ApplyManagedContextMenuMotion(contextMenu);
		}
	}

	private static void ApplyManagedPopupMotion(Popup popup)
	{
		popup.PopupAnimation = NormalizePopupAnimation(GetManagedPopupAnimation(popup));
	}

	private static void ApplyManagedContextMenuMotion(ContextMenu contextMenu)
	{
		var animation = NormalizePopupAnimation(PopupAnimation.Fade);
		contextMenu.Resources[SystemParameters.MenuPopupAnimationKey] = animation;

		// ContextMenu is hosted by a private framework-owned Popup. Its PopupAnimation
		// value is copied when that host is created, so changing resources afterward is
		// insufficient. Create/reuse the host before first open and set the concrete
		// property; the reflection is narrowly isolated and safely falls back to the
		// resource override if a future WPF implementation changes these internals.
		try
		{
			var parentPopup = ContextMenuParentPopupField?.GetValue(contextMenu) as Popup;
			if (parentPopup == null && ContextMenuHookupParentPopupMethod != null)
			{
				ContextMenuHookupParentPopupMethod.Invoke(contextMenu, null);
				parentPopup = ContextMenuParentPopupField?.GetValue(contextMenu) as Popup;
			}

			if (parentPopup != null)
			{
				parentPopup.PopupAnimation = animation;
			}
		}
		catch (TargetInvocationException)
		{
			// The resource override above remains the compatibility fallback.
		}
		catch (MemberAccessException)
		{
			// The resource override above remains the compatibility fallback.
		}
		catch (InvalidOperationException)
		{
			// The resource override above remains the compatibility fallback.
		}
	}

	private static void HoverMotionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is not FrameworkElement element)
		{
			return;
		}

		var state = HoverMotionStates.GetOrCreateValue(element);
		if (!state.IsAttached)
		{
			state.IsAttached = true;
			element.Loaded += HoverMotionElement_Loaded;
			element.MouseEnter += HoverMotionElement_MouseEnter;
			element.MouseLeave += HoverMotionElement_MouseLeave;
			HoverMotionElements.Add(new WeakReference<FrameworkElement>(element));
		}

		ApplyHoverMotion(element, element.IsMouseOver);
	}

	private static void HoverMotionElement_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement element) ApplyHoverMotion(element, element.IsMouseOver);
	}

	private static void HoverMotionElement_MouseEnter(object sender, EventArgs e)
	{
		if (sender is FrameworkElement element) ApplyHoverMotion(element, true);
	}

	private static void HoverMotionElement_MouseLeave(object sender, EventArgs e)
	{
		if (sender is FrameworkElement element) ApplyHoverMotion(element, false);
	}

	private static void RefreshHoverMotion()
	{
		for (var index = HoverMotionElements.Count - 1; index >= 0; index--)
		{
			if (!HoverMotionElements[index].TryGetTarget(out var element))
			{
				HoverMotionElements.RemoveAt(index);
				continue;
			}

			ApplyHoverMotion(element, element.IsMouseOver);
		}
	}

	private static void ApplyHoverMotion(FrameworkElement element, bool isHovered)
	{
		var configuredLift = Math.Max(0, GetHoverLift(element));
		var configuredScale = Math.Max(1, GetHoverScale(element));
		var transform = GetMutableHoverTransform(element);
		if (ReduceMotion || GetSuppressHoverMotion(element))
		{
			ResetHoverMotion(
				transform,
				configuredLift > 0,
				configuredScale > 1);
			return;
		}

		var duration = TimeSpan.FromMilliseconds(isHovered ? 120 : 160);
		var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
		ApplyHoverMotion(
			transform,
			isHovered ? -configuredLift : 0,
			isHovered ? configuredScale : 1,
			duration,
			easing,
			configuredLift > 0,
			configuredScale > 1);
	}

	private static Transform GetMutableHoverTransform(FrameworkElement element)
	{
		var transform = element.RenderTransform;
		if (transform == null || ReferenceEquals(transform, Transform.Identity))
		{
			return transform;
		}

		if (!transform.IsFrozen)
		{
			return transform;
		}

		// Freezables supplied by a sealed Style can be shared and frozen. Animating
		// that object is a no-op, and clearing it for Reduce Motion can leave every
		// control using the style without its normal hover path. Give this element a
		// local mutable copy so accessibility changes remain isolated and reversible.
		var mutableTransform = transform.CloneCurrentValue();
		element.RenderTransform = mutableTransform;
		return mutableTransform;
	}

	private static void ResetHoverMotion(Transform transform, bool resetTranslation, bool resetScale)
	{
		if (transform == null || transform.IsFrozen)
		{
			return;
		}

		switch (transform)
		{
			case TranslateTransform translate when resetTranslation:
				if (translate.HasAnimatedProperties)
				{
					translate.BeginAnimation(TranslateTransform.YProperty, null);
				}
				if (Math.Abs(translate.Y) > 0.001d)
				{
					translate.Y = 0;
				}
				break;
			case ScaleTransform scale when resetScale:
				if (scale.HasAnimatedProperties)
				{
					scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
					scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
				}
				if (Math.Abs(scale.ScaleX - 1d) > 0.001d)
				{
					scale.ScaleX = 1;
				}
				if (Math.Abs(scale.ScaleY - 1d) > 0.001d)
				{
					scale.ScaleY = 1;
				}
				break;
			case TransformGroup group:
				foreach (var child in group.Children)
				{
					ResetHoverMotion(child, resetTranslation, resetScale);
				}
				break;
		}
	}

	private static void ApplyHoverMotion(
		Transform transform,
		double translateY,
		double scale,
		TimeSpan duration,
		IEasingFunction easing,
		bool animateTranslation,
		bool animateScale)
	{
		if (transform == null || transform.IsFrozen)
		{
			return;
		}

		switch (transform)
		{
			case TranslateTransform translate when animateTranslation:
				var currentY = translate.Y;
				translate.Y = translateY;
				translate.BeginAnimation(
					TranslateTransform.YProperty,
					new DoubleAnimation(currentY, translateY, duration)
					{
						EasingFunction = easing,
						// The base value already holds the destination, so the clock can
						// stop instead of remaining attached in its fill period.
						FillBehavior = FillBehavior.Stop
					},
					HandoffBehavior.SnapshotAndReplace);
				break;
			case ScaleTransform scaleTransform when animateScale:
				var currentScaleX = scaleTransform.ScaleX;
				var currentScaleY = scaleTransform.ScaleY;
				scaleTransform.ScaleX = scale;
				scaleTransform.ScaleY = scale;
				var scaleXAnimation = new DoubleAnimation(currentScaleX, scale, duration)
				{
					EasingFunction = easing,
					FillBehavior = FillBehavior.Stop
				};
				var scaleYAnimation = new DoubleAnimation(currentScaleY, scale, duration)
				{
					EasingFunction = easing,
					FillBehavior = FillBehavior.Stop
				};
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation, HandoffBehavior.SnapshotAndReplace);
				scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation, HandoffBehavior.SnapshotAndReplace);
				break;
			case TransformGroup group:
				foreach (var child in group.Children)
				{
					ApplyHoverMotion(child, translateY, scale, duration, easing, animateTranslation, animateScale);
				}
				break;
		}
	}

	private static void ApplyPopupMotionPreference(ResourceDictionary resources)
	{
		if (resources == null) return;
		var fadeAnimation = NormalizePopupAnimation(PopupAnimation.Fade);
		var slideAnimation = NormalizePopupAnimation(PopupAnimation.Slide);

		resources["Redux.Motion.PopupFadeAnimation"] = fadeAnimation;
		resources["Redux.Motion.PopupSlideAnimation"] = slideAnimation;

		// ContextMenu and MenuItem popups created by WPF's stock templates do not
		// consume Redux's named motion resources. They resolve this system resource
		// instead, so override it alongside the Redux templates to keep Reduce Motion
		// effective for right-click menus and any framework-owned submenus.
		resources[SystemParameters.MenuPopupAnimationKey] = fadeAnimation;
	}

	private static PopupAnimation NormalizePopupAnimation(PopupAnimation requested)
	{
		if (!SystemParameters.ClientAreaAnimation)
		{
			return PopupAnimation.None;
		}

		return ReduceMotion ? PopupAnimation.None : requested;
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
		AttachWindowMotionPreference(window);
		var state = AdaptiveSizingStates.GetOrCreateValue(window);
		window.Loaded += (_, _) =>
		{
			ApplyPopupMotionPreference(window.Resources);
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
		window.StateChanged += (_, _) =>
		{
			if (!state.IsInitialized)
			{
				return;
			}

			if (window.WindowState == WindowState.Maximized)
			{
				RestoreDeclaredMaximums(window, state);
			}
			else
			{
				ClampToWorkArea(window, state, workAreaMargin);
			}
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

	public static bool SupportsResizeFeedback(Window window) =>
		window != null
		&& window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

	public static bool SupportsMoveFeedback(Window window) => window != null;

	/// <summary>
	/// Shows the shared, theme-aware window outline only while Windows is performing
	/// an interactive move or resize. Programmatic layout and initial sizing stay quiet.
	/// </summary>
	public static void AttachResizeFeedback(Window window)
	{
		// Every Redux window can be moved, even compact NoResize dialogs. Resizing
		// remains naturally limited by ResizeMode and therefore never raises WM_SIZING.
		if (!SupportsMoveFeedback(window)) return;
		var state = ResizeFeedbackStates.GetOrCreateValue(window);
		if (state.IsConfigured) return;

		window.ApplyTemplate();
		// MainWindow owns move and resize feedback together. Leave that existing path
		// intact while secondary Redux windows use the shared template surface.
		if (window.FindName("WindowResizeGlow") is FrameworkElement) return;
		state.Glow = window.Template?.FindName("SharedResizeGlow", window) as Border
			?? FindVisualElementByName<Border>(window, "SharedResizeGlow");
		if (state.Glow == null) return;
		state.IsConfigured = true;

		void HideImmediately()
		{
			state.IsVisible = false;
			state.Glow.BeginAnimation(UIElement.OpacityProperty, null);
			state.Glow.Opacity = 0;
		}

		void EndFeedback()
		{
			if (!state.IsVisible)
			{
				return;
			}

			state.IsVisible = false;
			if (ReduceMotion)
			{
				HideImmediately();
				return;
			}

			var currentOpacity = state.Glow.Opacity;
			state.Glow.BeginAnimation(UIElement.OpacityProperty, null);
			state.Glow.Opacity = 0;
			state.Glow.BeginAnimation(UIElement.OpacityProperty, new ReduxDoubleAnimation
			{
				From = currentOpacity,
				To = 0,
				Duration = TimeSpan.FromMilliseconds(180),
				FillBehavior = FillBehavior.Stop
			});
		}

		void ShowFeedback()
		{
			if (ReduceMotion || window.WindowState == WindowState.Maximized)
			{
				HideImmediately();
				return;
			}

			state.IsVisible = true;
			state.Glow.BeginAnimation(UIElement.OpacityProperty, null);
			state.Glow.Opacity = 0.9;
		}

		state.Hook = (IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
		{
			switch (message)
			{
				case WmSizing:
				case WmMoving:
					ShowFeedback();
					break;
				case WmExitSizeMove:
				case WmCancelMode:
				case WmCaptureChanged:
					EndFeedback();
					break;
			}
			return IntPtr.Zero;
		};

		void AttachHook()
		{
			if (state.IsAttached || PresentationSource.FromVisual(window) is not HwndSource source) return;
			state.Source = source;
			source.AddHook(state.Hook);
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

		window.StateChanged += (_, _) =>
		{
			if (window.WindowState == WindowState.Maximized) HideImmediately();
		};
		window.Closed += (_, _) =>
		{
			HideImmediately();
			if (state.Source != null && state.Hook != null) state.Source.RemoveHook(state.Hook);
			state.Source = null;
			state.IsAttached = false;
		};
	}

	private static T FindVisualElementByName<T>(DependencyObject root, string name) where T : FrameworkElement
	{
		if (root == null) return null;
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
		{
			var child = VisualTreeHelper.GetChild(root, index);
			if (child is T match && String.Equals(match.Name, name, StringComparison.Ordinal)) return match;
			var descendant = FindVisualElementByName<T>(child, name);
			if (descendant != null) return descendant;
		}
		return null;
	}

	public static void AttachRoundedCorners(Window window)
	{
		AttachWindowMotionPreference(window);
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
		// This hook handles WM_GETMINMAXINFO so Windows will not continue into WPF's
		// normal min/max pass. Preserve the Window constraints explicitly; without
		// these values custom-chrome dialogs can be dragged narrower than MinWidth and
		// their fixed summary/action content is clipped.
		if (HwndSource.FromHwnd(windowHandle)?.RootVisual is Window window)
		{
			var dpi = VisualTreeHelper.GetDpi(window);
			minMaxInfo.MinTrackSize.X = Math.Max(minMaxInfo.MinTrackSize.X,
				(int)Math.Ceiling(window.MinWidth * dpi.DpiScaleX));
			minMaxInfo.MinTrackSize.Y = Math.Max(minMaxInfo.MinTrackSize.Y,
				(int)Math.Ceiling(window.MinHeight * dpi.DpiScaleY));
			if (!Double.IsNaN(window.MaxWidth) && !Double.IsInfinity(window.MaxWidth))
				minMaxInfo.MaxTrackSize.X = (int)Math.Ceiling(window.MaxWidth * dpi.DpiScaleX);
			if (!Double.IsNaN(window.MaxHeight) && !Double.IsInfinity(window.MaxHeight))
				minMaxInfo.MaxTrackSize.Y = (int)Math.Ceiling(window.MaxHeight * dpi.DpiScaleY);
		}
		Marshal.StructureToPtr(minMaxInfo, lParam, false);
		handled = true;
		return IntPtr.Zero;
	}

	public static void AttachDialogTransitions(
		Window window,
		double workAreaMargin = 48,
		bool dimOwner = true)
	{
		AttachTaskbarPresence(window);
		AttachAdaptiveSizing(window, workAreaMargin);
		var state = AnimatedCloseStates.GetOrCreateValue(window);
		if (state.IsAttached) return;
		state.IsAttached = true;
		PrepareEntrance(window);
		window.Loaded += (_, _) =>
		{
			if (dimOwner)
			{
				ApplyOwnerBackdrop(window, window.Owner);
			}
			AnimateEntrance(window, 0);
		};
		window.Closing += (_, e) => AnimateDialogClosing(window, state, e);
		if (dimOwner)
		{
			window.Closed += (_, _) => RemoveOwnerBackdrop(window);
		}
	}

	public static void AttachWindowMotionPreference(Window window)
	{
		if (window == null)
		{
			return;
		}

		var state = WindowMotionPreferenceStates.GetOrCreateValue(window);
		if (state.IsAttached)
		{
			ApplyWindowMotionPreference(window);
			return;
		}

		state.IsAttached = true;
		window.SourceInitialized += (_, _) => ApplyWindowMotionPreference(window);
		ApplyWindowMotionPreference(window);
	}

	private static void ApplyWindowMotionPreference(Window window)
	{
		try
		{
			var handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
			{
				return;
			}

			var transitionsDisabled = ReduceMotion ? 1 : 0;
			DwmSetWindowAttribute(
				handle,
				DwmTransitionsForcedDisabledAttribute,
				ref transitionsDisabled,
				sizeof(int));
		}
		catch (DllNotFoundException)
		{
			// DWM is unavailable on legacy Windows versions.
		}
		catch (EntryPointNotFoundException)
		{
			// The preference is optional; Redux's own animations are still disabled.
		}
	}

	public static bool? ShowDialogWithOwnerBackdrop(Window dialog, Window owner)
	{
		AttachTaskbarPresence(dialog);
		ApplyOwnerBackdrop(dialog, owner);
		try
		{
			return dialog.ShowDialog();
		}
		finally
		{
			RemoveOwnerBackdrop(dialog);
		}
	}

	/// <summary>Give secondary windows their own taskbar and Alt-Tab entry even when owned.</summary>
	public static void AttachTaskbarPresence(Window window)
	{
		if (window == null || TaskbarPresenceStates.TryGetValue(window, out _)) return;
		TaskbarPresenceStates.Add(window, new object());
		window.ShowInTaskbar = true;
		void ApplyStyle()
		{
			var handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero) return;
			var style = GetWindowLong(handle, GwlExStyle);
			SetWindowLong(handle, GwlExStyle, (style | WsExAppWindow) & ~WsExToolWindow);
		}
		window.SourceInitialized += (_, _) => ApplyStyle();
		ApplyStyle();
	}

	public static bool HasDialogTransitions(Window window) =>
		window != null && AnimatedCloseStates.TryGetValue(window, out _);

	/// <summary>
	/// Dims the owning window while a Redux secondary surface is visible. Leases
	/// are reference-counted so nested or simultaneous secondary windows cannot
	/// restore the owner prematurely.
	/// </summary>
	public static bool HasActiveChild(Window owner) => owner != null && OwnerBackdropStates.TryGetValue(owner, out var state) && state.LeaseCount > 0;

	public static void ApplyOwnerBackdrop(Window child, Window owner)
	{
		if (child == null || owner?.Content is not UIElement ownerContent)
		{
			return;
		}

		var lease = BackdropLeaseStates.GetOrCreateValue(child);
		if (lease.IsActive && ReferenceEquals(lease.Owner, owner))
		{
			return;
		}
		if (lease.IsActive)
		{
			RemoveOwnerBackdrop(child);
		}

		var state = OwnerBackdropStates.GetOrCreateValue(owner);
		if (state.LeaseCount == 0)
		{
			state.PreviousEffect = ownerContent.Effect;
			state.PreviousOpacity = ownerContent.Opacity;
			if (!BackdropOwners.Any(reference =>
				reference.TryGetTarget(out var trackedOwner)
				&& ReferenceEquals(trackedOwner, owner)))
			{
				BackdropOwners.Add(new WeakReference<Window>(owner));
			}
		}
		if (!BackgroundEffectsDisabled && !state.EffectsApplied)
		{
			ownerContent.Effect = new BlurEffect
			{
				Radius = 2.5,
				RenderingBias = RenderingBias.Performance
			};
			ownerContent.Opacity = 0.88;
			state.EffectsApplied = true;
		}
		state.LeaseCount++;
		lease.Owner = owner;
		lease.IsActive = true;
	}

	public static void RemoveOwnerBackdrop(Window child)
	{
		if (child == null
			|| !BackdropLeaseStates.TryGetValue(child, out var lease)
			|| !lease.IsActive)
		{
			return;
		}

		var owner = lease.Owner;
		lease.IsActive = false;
		lease.Owner = null;
		if (owner?.Content is not UIElement ownerContent
			|| !OwnerBackdropStates.TryGetValue(owner, out var state))
		{
			return;
		}

		state.LeaseCount = Math.Max(0, state.LeaseCount - 1);
		if (state.LeaseCount > 0)
		{
			return;
		}

		if (state.EffectsApplied)
		{
			ownerContent.Effect = state.PreviousEffect;
			ownerContent.Opacity = state.PreviousOpacity;
			state.EffectsApplied = false;
		}
		state.PreviousEffect = null;
		state.PreviousOpacity = 1;
		RestoreOwnerActivation(owner);
	}

	private static void RestoreOwnerActivation(Window owner)
	{
		if (owner == null || !owner.IsVisible || owner.WindowState == WindowState.Minimized)
		{
			return;
		}

		// Closing and hiding owned windows can leave Windows without an active Redux
		// surface. Defer until WPF has completed the native dismissal and re-enabled a
		// modal owner; activating synchronously from Closed/Hide can be immediately undone.
		owner.Dispatcher.BeginInvoke(new Action(() =>
		{
			if (!owner.IsVisible || owner.WindowState == WindowState.Minimized || !owner.IsEnabled)
			{
				return;
			}
			owner.Activate();
			owner.Focus();
		}), System.Windows.Threading.DispatcherPriority.ContextIdle);
	}

	private static void RefreshActiveBackdrops()
	{
		for (var index = BackdropOwners.Count - 1; index >= 0; index--)
		{
			if (!BackdropOwners[index].TryGetTarget(out var owner))
			{
				BackdropOwners.RemoveAt(index);
				continue;
			}
			if (owner.Content is not UIElement ownerContent
				|| !OwnerBackdropStates.TryGetValue(owner, out var state)
				|| state.LeaseCount <= 0)
			{
				continue;
			}

			if (BackgroundEffectsDisabled)
			{
				if (!state.EffectsApplied) continue;
				ownerContent.Effect = state.PreviousEffect;
				ownerContent.Opacity = state.PreviousOpacity;
				state.EffectsApplied = false;
			}
			else if (!state.EffectsApplied)
			{
				ownerContent.Effect = new BlurEffect
				{
					Radius = 2.5,
					RenderingBias = RenderingBias.Performance
				};
				ownerContent.Opacity = 0.88;
				state.EffectsApplied = true;
			}
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
		target.Opacity = ShouldAnimateOpacity
			? Math.Clamp(fromOpacity, 0, 1)
			: 1;
	}

	private static void AnimateDialogClosing(Window window, AnimatedCloseState state, CancelEventArgs e)
	{
		if (state.BypassAnimation || e.Cancel) return;
		if (state.IsClosing)
		{
			e.Cancel = true;
			return;
		}
		if (!window.IsVisible || !ShouldAnimateOpacity) return;

		e.Cancel = true;
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

	private static void RestoreDeclaredMaximums(Window window, AdaptiveSizingState state)
	{
		window.MaxWidth = state.DeclaredMaxWidth;
		window.MaxHeight = state.DeclaredMaxHeight;
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
		if (!ShouldAnimateOpacity) return Task.CompletedTask;

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
		if (!ShouldAnimateOpacity)
		{
			target.Opacity = 1;
			return Task.CompletedTask;
		}

		var completion = new TaskCompletionSource();
		var animation = new DoubleAnimation(fromOpacity, 1, duration ?? EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		animation.Completed += (_, _) =>
		{
			// Commit the final value and release the clock. Leaving a completed
			// opacity animation attached to the main content root keeps WPF's timing
			// and composition paths alive for the lifetime of the application.
			target.Opacity = 1;
			target.BeginAnimation(UIElement.OpacityProperty, null);
			completion.TrySetResult();
		};
		target.BeginAnimation(UIElement.OpacityProperty, animation);
		return completion.Task;
	}

	public static void AnimateEntrance(Window window, double fromOpacity = 0.92)
	{
		var target = GetAnimationTarget(window);
		target.BeginAnimation(UIElement.OpacityProperty, null);
		if (!ShouldAnimateOpacity)
		{
			target.Opacity = 1;
			return;
		}

		var animation = new DoubleAnimation(fromOpacity, 1, EntranceDuration)
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		animation.Completed += (_, _) =>
		{
			target.Opacity = 1;
			target.BeginAnimation(UIElement.OpacityProperty, null);
		};
		target.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	public static void AnimateExit(Window window, Action completed)
	{
		var target = GetAnimationTarget(window);
		if (!window.IsVisible || !ShouldAnimateOpacity)
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
			// Removing the clock exposes the base value immediately, so commit zero first.
			target.Opacity = 0;
			target.BeginAnimation(UIElement.OpacityProperty, null);
			try
			{
				// Close or hide while the content is still transparent. Restoring opacity
				// first makes the window flash back for one frame and looks like a second close.
				completed();
			}
			finally
			{
				target.Opacity = 1;
			}
		};
		target.BeginAnimation(UIElement.OpacityProperty, animation);
	}

}
