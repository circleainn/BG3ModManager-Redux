using DivinityModManager.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager.Controls;

/// <summary>One continuous outline outside the data columns, measured from realized rows.
/// Drawing never changes item height or realizes off-screen containers.</summary>
public sealed class SeparatorGroupingGutter : FrameworkElement
{
	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush),
		typeof(SeparatorGroupingGutter), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
	public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
	public SeparatorGroupingGutter()
	{
		// Branches describe structure rather than actionable text. Use the theme's
		// neutral stroke role, which also preserves its subtle hue in custom themes.
		SetResourceReference(StrokeProperty, "ReduxBorderStrongBrush");
		Loaded += (_, _) =>
		{
			if (Owner is not { } owner) return;
			owner.LayoutUpdated -= LayoutChanged;
			owner.LayoutUpdated += LayoutChanged;
			owner.GroupingAnimationFrame -= AnimationFrame;
			owner.GroupingAnimationFrame += AnimationFrame;
			InvalidateVisual();
		};
		Unloaded += (_, _) => { if (Owner is { } owner) { owner.LayoutUpdated -= LayoutChanged; owner.GroupingAnimationFrame -= AnimationFrame; } };
	}
	private int _layoutSignature;
	public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner),
		typeof(ModListView), typeof(SeparatorGroupingGutter), new PropertyMetadata(null, OwnerChanged));
	public ModListView Owner { get => (ModListView)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
	private static void OwnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var gutter = (SeparatorGroupingGutter)d;
		if (e.OldValue is ModListView oldOwner) { oldOwner.LayoutUpdated -= gutter.LayoutChanged; oldOwner.GroupingAnimationFrame -= gutter.AnimationFrame; }
		if (e.NewValue is ModListView newOwner) { newOwner.LayoutUpdated += gutter.LayoutChanged; newOwner.GroupingAnimationFrame += gutter.AnimationFrame; }
		gutter.InvalidateVisual();
	}
	private void AnimationFrame(object sender, EventArgs e) => InvalidateVisual();
	private static double Expansion(DivinityModData item) => Math.Clamp(1 + item.VisualDividerChevronAngle / 90d, 0, 1);
	private void LayoutChanged(object sender, EventArgs e)
	{
		// Rendering itself can raise LayoutUpdated. Invalidate only when geometry
		// changes, otherwise WPF never reaches idle (and animations can stall).
		if (Owner is not { } owner) return;
		var signature = new HashCode();
		signature.Add(owner.HasSeparatorRows);
		signature.Add(RenderSize);
		if (Find<VirtualizingStackPanel>(owner) is { } panel)
			foreach (UIElement element in panel.Children)
				if (element is ListViewItem item)
				{
					signature.Add(owner.ItemContainerGenerator.IndexFromContainer(item));
					signature.Add(item.TransformToAncestor(owner).TransformBounds(new Rect(item.RenderSize)));
					signature.Add(item.Visibility);
				}
		foreach (var range in owner.GroupingRanges)
		{
			signature.Add(range);
			signature.Add(range.Item.IsVisualDividerCollapsed);
			signature.Add(range.Item.IsChildVisualDivider);
			signature.Add(range.Item.VisualDividerChevronAngle);
		}
		var value = signature.ToHashCode();
		if (_layoutSignature == value) return;
		_layoutSignature = value;
		InvalidateVisual();
	}
	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (Owner is not { HasSeparatorRows: true } owner) return;
		var panel = Find<VirtualizingStackPanel>(owner);
		// The first presenter in GridView belongs to its horizontally scrolling
		// header. Clip against the items viewport instead.
		DependencyObject ancestor = panel;
		while (ancestor != null && ancestor is not ScrollContentPresenter)
			ancestor = VisualTreeHelper.GetParent(ancestor);
		var presenter = ancestor as ScrollContentPresenter;
		if (presenter == null || panel == null) return;
		var viewport = presenter.TransformToAncestor(owner).TransformBounds(new Rect(presenter.RenderSize));
		var pen = new Pen(Stroke, 1) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
		var rows = new List<(int Index, Rect Bounds)>();
		foreach (UIElement element in panel.Children)
		{
			if (element is not ListViewItem item || item.ActualHeight <= 0 || item.Visibility != Visibility.Visible) continue;
			var index = owner.ItemContainerGenerator.IndexFromContainer(item);
			if (index < 0) continue;
			var bounds = item.TransformToAncestor(owner).TransformBounds(new Rect(item.RenderSize));
			rows.Add((index, bounds));
		}
		if (rows.Count == 0) return;
		rows.Sort((left, right) => left.Index.CompareTo(right.Index));
		var offset = rows[0].Bounds.Left;
		dc.PushClip(new RectangleGeometry(new Rect(Math.Max(0, offset), viewport.Top, Math.Max(0, 48 + Math.Min(0, offset)), viewport.Height)));
		foreach (var group in owner.GroupingRanges)
		{
			if (group.Item.IsVisualDividerCollapsed) continue;
			var expansion = Expansion(group.Item);
			var opacity = expansion;
			if (group.Item.IsChildVisualDivider)
			{
				var parent = owner.GroupingRanges.LastOrDefault(candidate => !candidate.Item.IsChildVisualDivider && candidate.Start < group.Start && candidate.End >= group.End);
				if (parent.Item != null) opacity *= Expansion(parent.Item);
			}
			if (opacity <= 0) continue;
			var visible = rows.Where(row => row.Index >= group.Start && row.Index <= group.End).ToArray();
			if (visible.Length == 0 || group.End == group.Start) continue;
			var first = visible[0];
			var last = visible[^1];
			var x = offset + ModListView.SeparatorBranchCenter + (group.Item.IsChildVisualDivider ? ModListView.HierarchyLevelStep : 0);
			// Start below the complete heading surface, not inside its hover fill
			// at the chevron's bottom edge. Button feedback must not move the branch.
			var top = first.Index == group.Start ? first.Bounds.Bottom - 2 : viewport.Top;
			var bottom = last.Index == group.End ? last.Bounds.Bottom - 4 : viewport.Bottom;
			// When the last child contracts, its parent bracket follows that endpoint too.
			if (!group.Item.IsChildVisualDivider)
			{
				var child = owner.GroupingRanges.LastOrDefault(candidate => candidate.Item.IsChildVisualDivider && candidate.Start > group.Start && candidate.End == group.End);
				if (child.Item != null && Expansion(child.Item) < 1)
				{
					var childRow = rows.FirstOrDefault(row => row.Index == child.Start);
					if (childRow.Bounds.Height > 0)
						bottom = childRow.Bounds.Bottom - 4 + (bottom - childRow.Bounds.Bottom + 4) * Expansion(child.Item);
				}
			}
			bottom = top + (bottom - top) * expansion;
			if (bottom <= top) continue;
			// One stroke avoids a darker overlap where two separately drawn lines meet.
			var outline = new StreamGeometry();
			using (var context = outline.Open())
			{
				context.BeginFigure(new Point(x, top), false, false);
				if (last.Index == group.End)
				{
					var radius = Math.Min(4, (bottom - top) / 2);
					context.LineTo(new Point(x, bottom - radius), true, false);
					context.ArcTo(new Point(x + radius, bottom), new Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true, false);
					context.LineTo(new Point(x + 7, bottom), true, false);
				}
				else context.LineTo(new Point(x, bottom), true, false);
			}
			outline.Freeze();
			var guides = new GuidelineSet();
			guides.GuidelinesX.Add(x - 0.5);
			guides.GuidelinesX.Add(x + 0.5);
			guides.GuidelinesY.Add(bottom - 0.5);
			guides.GuidelinesY.Add(bottom + 0.5);
			dc.PushGuidelineSet(guides);
			dc.PushOpacity(opacity);
			dc.DrawGeometry(null, pen, outline);
			dc.Pop();
			dc.Pop();
		}
		dc.Pop();
	}
	private static T Find<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T match) return match;
			if (Find<T>(child) is T nested) return nested;
		}
		return null;
	}
}
