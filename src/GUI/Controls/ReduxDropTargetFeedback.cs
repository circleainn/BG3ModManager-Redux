using DivinityModManager.Util;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DivinityModManager.Controls;

/// <summary>
/// One persistent insertion rail shared by Categories and both mod lists. Keeping
/// ownership at the pane level prevents row hit-test changes from recreating the
/// visual while the pointer crosses an item midpoint.
/// </summary>
internal sealed class ReduxDropIndicatorAdorner : Adorner
{
	private readonly Brush _brush;
	private double _followTarget;
	private TimeSpan? _lastRenderingTime;
	private bool _isFollowing;
	private static readonly DependencyProperty IndicatorOffsetProperty = DependencyProperty.Register(
		nameof(IndicatorOffset),
		typeof(double),
		typeof(ReduxDropIndicatorAdorner),
		new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

	internal ReduxDropIndicatorAdorner(UIElement adornedElement, double offset, Brush brush)
		: base(adornedElement)
	{
		_brush = brush;
		IndicatorOffset = offset;
		IsHitTestVisible = false;

		if (ReduxWindowBehavior.ReduceMotion) return;
		Opacity = 0;
		BeginAnimation(OpacityProperty, new DoubleAnimation(1, ReduxDropFeedback.MotionDuration)
		{
			EasingFunction = ReduxDropFeedback.MotionEase
		});
	}

	private double IndicatorOffset
	{
		get => (double)GetValue(IndicatorOffsetProperty);
		set => SetValue(IndicatorOffsetProperty, value);
	}

	internal void MoveTo(double offset)
	{
		StopFollowing();
		if (Math.Abs(IndicatorOffset - offset) <= 0.5) return;
		if (ReduxWindowBehavior.ReduceMotion)
		{
			BeginAnimation(IndicatorOffsetProperty, null);
			IndicatorOffset = offset;
			return;
		}

		BeginAnimation(IndicatorOffsetProperty, new DoubleAnimation
		{
			From = IndicatorOffset,
			To = offset,
			Duration = ReduxDropFeedback.MotionDuration,
			EasingFunction = ReduxDropFeedback.MotionEase
		}, HandoffBehavior.SnapshotAndReplace);
	}

	/// <summary>
	/// Tracks a continuously changing pointer position without starting a new WPF
	/// animation for every drag event. This is used by dense mod lists, where
	/// discrete row-to-row animations otherwise feel like snapping or queued motion.
	/// </summary>
	internal void FollowTo(double offset)
	{
		_followTarget = offset;
		if (ReduxWindowBehavior.ReduceMotion)
		{
			StopFollowing();
			BeginAnimation(IndicatorOffsetProperty, null);
			IndicatorOffset = offset;
			return;
		}

		if (_isFollowing) return;
		BeginAnimation(IndicatorOffsetProperty, null);
		_isFollowing = true;
		_lastRenderingTime = null;
		CompositionTarget.Rendering += FollowPointerOnRendering;
	}

	internal void Detach()
	{
		StopFollowing();
		BeginAnimation(IndicatorOffsetProperty, null);
		BeginAnimation(OpacityProperty, null);
	}

	private void FollowPointerOnRendering(object sender, EventArgs e)
	{
		if (e is not RenderingEventArgs renderingEvent) return;
		if (_lastRenderingTime is null)
		{
			_lastRenderingTime = renderingEvent.RenderingTime;
			return;
		}

		var elapsed = Math.Clamp(
			(renderingEvent.RenderingTime - _lastRenderingTime.Value).TotalSeconds,
			1d / 240d,
			0.05d);
		_lastRenderingTime = renderingEvent.RenderingTime;

		// A short exponential response smooths irregular drag-event delivery without
		// making the rail trail behind the pointer or accumulate queued animations.
		var blend = 1d - Math.Exp(-elapsed / 0.018d);
		var remaining = _followTarget - IndicatorOffset;
		if (Math.Abs(remaining) <= 0.05d)
		{
			IndicatorOffset = _followTarget;
			return;
		}

		IndicatorOffset += remaining * blend;
	}

	private void StopFollowing()
	{
		if (!_isFollowing) return;
		CompositionTarget.Rendering -= FollowPointerOnRendering;
		_isFollowing = false;
		_lastRenderingTime = null;
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		const double thickness = 2;
		var y = IndicatorOffset;
		var width = Math.Max(0, AdornedElement.RenderSize.Width - 12);
		var line = new Rect(6, y - (thickness / 2), width, thickness);

		drawingContext.PushOpacity(0.18);
		drawingContext.DrawRoundedRectangle(_brush, null, new Rect(6, y - 3, width, 6), 3, 3);
		drawingContext.Pop();
		drawingContext.DrawRoundedRectangle(_brush, null, line, 1, 1);
	}
}

internal sealed class ReduxDropTargetMotionState
{
	internal FrameworkElement Target { get; init; }
	internal object OriginalLocalTransform { get; init; }
	internal TransformGroup CompositeTransform { get; init; }
	internal TranslateTransform Translation { get; init; }
	internal double Destination { get; set; }
}

/// <summary>
/// Holds the shared insertion state and the optional, layout-neutral displacement
/// used by category and mod-list rows. It always restores any transform it temporarily owns.
/// </summary>
internal static class ReduxDropFeedback
{
	private const double Displacement = 4;
	private static readonly DependencyProperty StableInsertIndexProperty = DependencyProperty.RegisterAttached(
		"StableInsertIndex",
		typeof(int),
		typeof(ReduxDropFeedback),
		new FrameworkPropertyMetadata(-1));
	internal static TimeSpan MotionDuration =>
		Application.Current?.TryFindResource("Redux.Motion.Fast") is Duration { HasTimeSpan: true } duration
			? duration.TimeSpan
			: TimeSpan.FromMilliseconds(120);
	internal static IEasingFunction MotionEase =>
		Application.Current?.TryFindResource("Redux.Motion.EaseOut") as IEasingFunction
		?? new QuadraticEase { EasingMode = EasingMode.EaseOut };

	internal static int GetStableInsertIndex(DependencyObject target) =>
		target == null ? -1 : (int)target.GetValue(StableInsertIndexProperty);

	internal static void SetStableInsertIndex(DependencyObject target, int value) =>
		target?.SetValue(StableInsertIndexProperty, value);

	internal static void SetTarget(ref ReduxDropTargetMotionState state, FrameworkElement target, bool insertAfter)
	{
		if (target == null)
		{
			Clear(ref state);
			return;
		}

		var destination = ReduxWindowBehavior.ReduceMotion ? 0 : insertAfter ? -Displacement : Displacement;
		if (state?.Target == target)
		{
			if (Math.Abs(state.Destination - destination) <= 0.01d) return;
			state.Destination = destination;
			Animate(state.Translation, destination);
			return;
		}

		Clear(ref state);
		if (ReduxWindowBehavior.ReduceMotion) return;

		var originalLocal = target.ReadLocalValue(UIElement.RenderTransformProperty);
		var originalTransform = target.RenderTransform;
		var translation = new TranslateTransform();
		var composite = new TransformGroup();
		if (originalTransform != null && !originalTransform.Value.IsIdentity)
			composite.Children.Add(new MatrixTransform(originalTransform.Value));
		composite.Children.Add(translation);
		target.RenderTransform = composite;

		state = new ReduxDropTargetMotionState
		{
			Target = target,
			OriginalLocalTransform = originalLocal,
			CompositeTransform = composite,
			Translation = translation,
			Destination = destination
		};
		Animate(translation, destination);
	}

	internal static void Clear(ref ReduxDropTargetMotionState state)
	{
		var previous = state;
		state = null;
		if (previous?.Target == null) return;

		if (ReduxWindowBehavior.ReduceMotion)
		{
			Restore(previous);
			return;
		}

		var animation = CreateAnimation(0);
		animation.Completed += (_, _) => Restore(previous);
		previous.Translation.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
	}

	private static void Restore(ReduxDropTargetMotionState state)
	{
		if (state?.Target == null || !ReferenceEquals(state.Target.RenderTransform, state.CompositeTransform)) return;
		if (state.OriginalLocalTransform == DependencyProperty.UnsetValue)
			state.Target.ClearValue(UIElement.RenderTransformProperty);
		else
			state.Target.SetValue(UIElement.RenderTransformProperty, state.OriginalLocalTransform);
	}

	private static void Animate(TranslateTransform transform, double destination) =>
		transform.BeginAnimation(
			TranslateTransform.YProperty,
			CreateAnimation(destination),
			HandoffBehavior.SnapshotAndReplace);

	private static DoubleAnimation CreateAnimation(double destination) => new()
	{
		To = destination,
		Duration = MotionDuration,
		EasingFunction = MotionEase
	};
}
