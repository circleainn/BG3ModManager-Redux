using DivinityModManager;

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DivinityModManager.Util;

/// <summary>
/// Adds a layout-neutral transition between item-based wheel positions. This
/// intentionally never enables pixel scrolling: the VirtualizingStackPanel
/// remains authoritative so mixed-height separator rows cannot re-enter WPF's
/// expensive pixel-anchor feedback path.
/// </summary>
public static class SmoothLogicalScrollBehavior
{
	private static readonly ConditionalWeakTable<ItemsControl, SmoothScrollState> States = new();
	private static readonly List<WeakReference<SmoothScrollState>> ActiveStates = new();
	private static bool _initialized;

	public static readonly DependencyProperty IsInteractionSuppressedProperty = DependencyProperty.RegisterAttached(
		"IsInteractionSuppressed",
		typeof(bool),
		typeof(SmoothLogicalScrollBehavior),
		new PropertyMetadata(false, InteractionSuppressionChanged));

	public static bool GetIsInteractionSuppressed(DependencyObject element) =>
		(bool)element.GetValue(IsInteractionSuppressedProperty);

	public static void SetIsInteractionSuppressed(DependencyObject element, bool value) =>
		element.SetValue(IsInteractionSuppressedProperty, value);

	internal static void ConfigureReducedMotion(bool reduceMotion)
	{
		Initialize();
		if (!reduceMotion) return;

		for (var index = ActiveStates.Count - 1; index >= 0; index--)
		{
			if (ActiveStates[index].TryGetTarget(out var state)) state.RefreshMotionPreference();
			else ActiveStates.RemoveAt(index);
		}
	}

	internal static void Initialize()
	{
		if (_initialized) return;
		_initialized = true;
		EventManager.RegisterClassHandler(
			typeof(ListBox),
			UIElement.PreviewMouseWheelEvent,
			new MouseWheelEventHandler(ListBox_PreviewMouseWheel),
			true);
	}

	private static void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is not ListBox owner ||
			!SmoothLogicalScrollPolicy.CanAnimate(
				ReduxWindowBehavior.ReduceMotion,
				GetIsInteractionSuppressed(owner),
				SystemParameters.ClientAreaAnimation)) return;

		// A routed wheel event can pass through nested list controls. Only the
		// list nearest the original source should consume and animate it.
		if (e.OriginalSource is DependencyObject source && !ReferenceEquals(source, owner))
		{
			var nearestList = source.FindVisualParent<ListBox>();
			if (nearestList != null && !ReferenceEquals(nearestList, owner)) return;
		}

		States.GetValue(owner, CreateState).OnPreviewMouseWheel(e);
	}

	private static SmoothScrollState CreateState(ItemsControl owner)
	{
		var state = new SmoothScrollState(owner);
		ActiveStates.Add(new WeakReference<SmoothScrollState>(state));
		return state;
	}

	private static void InteractionSuppressionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
	{
		if (dependencyObject is ItemsControl owner && States.TryGetValue(owner, out var state))
			state.RefreshMotionPreference();
	}

	private sealed class SmoothScrollState
	{
		private const double MinimumVisiblePixels = 0.5d;
		private static readonly Duration AnimationDuration = TimeSpan.FromMilliseconds(110);
		private static readonly IEasingFunction AnimationEase =
			new QuadraticEase { EasingMode = EasingMode.EaseOut };

		private readonly ItemsControl _owner;
		private ScrollViewer _scrollViewer;
		private VirtualizingStackPanel _itemsHost;
		private TranslateTransform _translation;
		private TransformGroup _compositeTransform;
		private object _originalLocalTransform;
		private int _wheelDeltaRemainder;
		private int _animationGeneration;

		public SmoothScrollState(ItemsControl owner)
		{
			_owner = owner;
			_owner.PreviewMouseDown += Owner_PreviewMouseDown;
			_owner.PreviewTouchDown += Owner_PreviewTouchDown;
			_owner.PreviewKeyDown += Owner_PreviewKeyDown;
			_owner.Loaded += Owner_Loaded;
			_owner.Unloaded += Owner_Unloaded;
			_owner.ItemContainerGenerator.ItemsChanged += ItemContainerGenerator_ItemsChanged;
		}

		public void RefreshMotionPreference()
		{
			if (!CanAnimate())
			{
				_wheelDeltaRemainder = 0;
				RestoreHostTransform();
			}
		}

		private bool CanAnimate() => SmoothLogicalScrollPolicy.CanAnimate(
			ReduxWindowBehavior.ReduceMotion,
			GetIsInteractionSuppressed(_owner),
			SystemParameters.ClientAreaAnimation);

		private void Owner_Loaded(object sender, RoutedEventArgs e)
		{
			// The visual host is acquired lazily on wheel input so untouched lists
			// add no transforms or layout work.
			_wheelDeltaRemainder = 0;
		}

		private void Owner_Unloaded(object sender, RoutedEventArgs e)
		{
			_wheelDeltaRemainder = 0;
			RestoreHostTransform();
		}

		private void Owner_PreviewMouseDown(object sender, MouseButtonEventArgs e) => CancelMotion();

		private void Owner_PreviewTouchDown(object sender, TouchEventArgs e) => CancelMotion();

		private void Owner_PreviewKeyDown(object sender, KeyEventArgs e) => CancelMotion();

		private void ItemContainerGenerator_ItemsChanged(object sender, ItemsChangedEventArgs e)
		{
			// Filtering, separator expansion/collapse, and collection edits all take
			// precedence over cosmetic motion.
			_wheelDeltaRemainder = 0;
			CancelMotion();
		}

		public void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			try
			{
				HandleMouseWheel(e);
			}
			catch (Exception exception)
			{
				// This behavior is cosmetic. A changing visual tree
				// must never turn a failed transition into an application failure.
				_wheelDeltaRemainder = 0;
				CancelMotion();
				DivinityApp.Log($"Smooth logical scrolling skipped a transition:\n{exception}");
			}
		}

		private void HandleMouseWheel(MouseWheelEventArgs e)
		{
			if (!CanAnimate())
			{
				CancelMotion();
				return;
			}
			if (Mouse.LeftButton != MouseButtonState.Released ||
				Mouse.MiddleButton != MouseButtonState.Released ||
				Mouse.RightButton != MouseButtonState.Released)
			{
				CancelMotion();
				return;
			}
			if (!EnsureHost() ||
				!ScrollViewer.GetCanContentScroll(_owner) ||
				VirtualizingPanel.GetScrollUnit(_owner) != ScrollUnit.Item)
			{
				CancelMotion();
				return;
			}

			var rows = SmoothLogicalScrollPolicy.ConsumeRows(
				ref _wheelDeltaRemainder,
				e.Delta,
				SystemParameters.WheelScrollLines);
			e.Handled = true;
			if (rows == 0) return;

			EnsureTransform();
			var currentTranslation = FreezeCurrentTranslation();
			var previousOffset = _scrollViewer.VerticalOffset;
			var previousFirstIndex = Math.Clamp(
				(int)Math.Floor(previousOffset),
				0,
				Math.Max(0, _owner.Items.Count - 1));
			var actualRows = SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(
				rows,
				previousOffset,
				_scrollViewer.ScrollableHeight);
			if (actualRows == 0)
			{
				AnimateToRest(currentTranslation);
				return;
			}
			var compensation = MeasureLogicalTravel(previousFirstIndex, actualRows);

			if (actualRows > 0)
			{
				for (var index = 0; index < actualRows; index++) _scrollViewer.LineUp();
			}
			else
			{
				for (var index = 0; index > actualRows; index--) _scrollViewer.LineDown();
			}

			// Measure the small set of rows crossed by the requested logical change
			// from already-realized containers. This avoids waiting for WPF's deferred
			// offset update or forcing a synchronous layout of the entire ListView on
			// every wheel event. The mod-list style keeps one extra row beyond the maximum
			// three-row wheel step cached on both sides of the viewport.
			var maximumTranslation = Math.Max(48d, _scrollViewer.ActualHeight);
			var start = Math.Clamp(currentTranslation + compensation, -maximumTranslation, maximumTranslation);
			AnimateToRest(start);
		}

		private bool EnsureHost()
		{
			if (_itemsHost != null && _itemsHost.IsDescendantOf(_owner) && _scrollViewer != null)
				return true;

			RestoreHostTransform();
			_itemsHost = _owner.FindVisualChildren<VirtualizingStackPanel>()
				.FirstOrDefault(panel => ReferenceEquals(ItemsControl.GetItemsOwner(panel), _owner));
			_scrollViewer = _itemsHost?.FindVisualParent<ScrollViewer>();
			return _itemsHost != null && _scrollViewer != null;
		}

		private void EnsureTransform()
		{
			if (_itemsHost == null) return;
			if (_compositeTransform != null && ReferenceEquals(_itemsHost.RenderTransform, _compositeTransform)) return;

			_compositeTransform = null;
			_translation = new TranslateTransform();
			_originalLocalTransform = _itemsHost.ReadLocalValue(UIElement.RenderTransformProperty);
			var originalTransform = _itemsHost.RenderTransform;
			var composite = new TransformGroup();
			if (originalTransform != null && !originalTransform.Value.IsIdentity)
				composite.Children.Add(new MatrixTransform(originalTransform.Value));
			composite.Children.Add(_translation);
			_compositeTransform = composite;
			_itemsHost.RenderTransform = composite;
		}

		private double FreezeCurrentTranslation()
		{
			if (_translation == null) return 0;
			var current = _translation.Y;
			_animationGeneration++;
			_translation.BeginAnimation(TranslateTransform.YProperty, null);
			_translation.Y = current;
			return current;
		}

		private double MeasureLogicalTravel(int previousFirstIndex, int rows)
		{
			if (_itemsHost == null || rows == 0) return 0;

			var count = Math.Abs(rows);
			var firstCrossedIndex = rows > 0
				? previousFirstIndex - count
				: previousFirstIndex;
			var realizedHeights = new List<double>(count);
			var missingCount = 0;
			for (var offset = 0; offset < count; offset++)
			{
				var itemIndex = firstCrossedIndex + offset;
				if (itemIndex >= 0 && itemIndex < _owner.Items.Count &&
					_owner.ItemContainerGenerator.ContainerFromIndex(itemIndex) is FrameworkElement row &&
					row.ActualHeight > 0)
				{
					realizedHeights.Add(GetLayoutHeight(row));
				}
				else missingCount++;
			}

			var fallbackHeight = realizedHeights.Count > 0
				? realizedHeights.Average()
				: _itemsHost.Children
					.OfType<FrameworkElement>()
					.Where(row => row.ActualHeight > 0)
					.Select(GetLayoutHeight)
					.DefaultIfEmpty(36d)
					.Average();
			return SmoothLogicalScrollPolicy.CalculateVisualCompensation(
				rows,
				realizedHeights,
				missingCount,
				fallbackHeight);
		}

		private static double GetLayoutHeight(FrameworkElement row)
		{
			var height = row.ActualHeight + row.Margin.Top + row.Margin.Bottom;
			if (double.IsFinite(height) && height > 0) return height;
			return 36d;
		}

		private void AnimateToRest(double start)
		{
			if (_translation == null) return;
			_animationGeneration++;
			var generation = _animationGeneration;
			_translation.BeginAnimation(TranslateTransform.YProperty, null);
			_translation.Y = 0;

			if (Math.Abs(start) < MinimumVisiblePixels) return;
			var animation = new DoubleAnimation(start, 0, AnimationDuration)
			{
				EasingFunction = AnimationEase,
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (_, _) =>
			{
				if (generation != _animationGeneration || _translation == null) return;
				_translation.BeginAnimation(TranslateTransform.YProperty, null);
				_translation.Y = 0;
			};
			_translation.BeginAnimation(
				TranslateTransform.YProperty,
				animation,
				HandoffBehavior.SnapshotAndReplace);
		}

		private void CancelMotion()
		{
			_animationGeneration++;
			if (_translation == null) return;
			_translation.BeginAnimation(TranslateTransform.YProperty, null);
			_translation.Y = 0;
		}

		private void RestoreHostTransform()
		{
			CancelMotion();
			if (_itemsHost != null &&
				_compositeTransform != null &&
				ReferenceEquals(_itemsHost.RenderTransform, _compositeTransform))
			{
				if (_originalLocalTransform == DependencyProperty.UnsetValue)
					_itemsHost.ClearValue(UIElement.RenderTransformProperty);
				else
					_itemsHost.SetValue(UIElement.RenderTransformProperty, _originalLocalTransform);
			}
			_scrollViewer = null;
			_itemsHost = null;
			_translation = null;
			_compositeTransform = null;
			_originalLocalTransform = null;
		}
	}
}
