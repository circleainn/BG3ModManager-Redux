using DivinityModManager.Extensions;
using DivinityModManager.Models;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.Views;

using DynamicData.Binding;

using System.ComponentModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DivinityModManager.Controls;

public class ModListView : ListView
{
	protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
	{
		base.OnItemsSourceChanged(oldValue, newValue);
		if (newValue == null || System.Windows.Data.CollectionViewSource.GetDefaultView(newValue) is not ICollectionViewLiveShaping live) return;
		if (live.CanChangeLiveSorting)
		{
			if (!live.LiveSortingProperties.Contains(nameof(DivinityModData.ListDisplayTitle)))
				live.LiveSortingProperties.Add(nameof(DivinityModData.ListDisplayTitle));
			live.IsLiveSorting = true;
		}
		if (live.CanChangeLiveFiltering)
		{
			foreach (var property in new[] { nameof(DivinityModData.CustomAlias), nameof(DivinityModData.HasCustomAlias), nameof(DivinityModData.ListDisplayTitle) })
				if (!live.LiveFilteringProperties.Contains(property)) live.LiveFilteringProperties.Add(property);
			live.IsLiveFiltering = true;
		}
	}

	private static readonly MethodInfo _itemInfoFromContainer = typeof(ItemsControl)
		.GetMethod("ItemInfoFromContainer", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly MethodInfo _updateAnchorAndActionItem = typeof(ListBox)
		.GetMethod("UpdateAnchorAndActionItem", BindingFlags.NonPublic | BindingFlags.Instance);
	public bool Resizing { get; set; }
	public bool UserResizedColumns { get; set; }
	public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.RegisterAttached(
		"MinimumColumnWidth", typeof(double), typeof(ModListView), new PropertyMetadata(0d));
	public static void SetMinimumColumnWidth(DependencyObject column, double value) => column.SetValue(MinimumColumnWidthProperty, value);
	public static double GetMinimumColumnWidth(DependencyObject column) => (double)column.GetValue(MinimumColumnWidthProperty);
	public static double GetColumnWidthFloor(string name) => name switch
	{
		"#" => 45, "Name" or "File Name" => 100, "Version" => 60,
		"Last Updated" or "Author" or "Category" => 70, "Last Modified" => 75,
		"Source" => 90, _ => 60
	};
	public static readonly DependencyProperty IsColumnResizingProperty = DependencyProperty.Register(
		nameof(IsColumnResizing), typeof(bool), typeof(ModListView), new PropertyMetadata(false));
	public bool IsColumnResizing => (bool)GetValue(IsColumnResizingProperty);
	public static readonly DependencyProperty ColumnResizeOffsetProperty = DependencyProperty.Register(
		nameof(ColumnResizeOffset), typeof(double), typeof(ModListView), new PropertyMetadata(0d));
	public double ColumnResizeOffset => (double)GetValue(ColumnResizeOffsetProperty);
	private GridViewColumnHeader _resizingHeader;

	private GridViewColumnHeader ResizeHeader(RoutedEventArgs e)
	{
		if (!UsesSeparatorHeaders || e.OriginalSource is not Thumb { Name: "PART_HeaderGripper" } thumb) return null;
		var header = thumb.FindVisualParent<GridViewColumnHeader>();
		return header?.FindVisualParent<ModListView>() == this ? header : null;
	}

	private void OnColumnResizeStarted(object sender, DragStartedEventArgs e)
	{
		_resizingHeader = ResizeHeader(e);
		if (_resizingHeader?.Column == null) return;
		UserResizedColumns = true;
		SetValue(IsColumnResizingProperty, true);
		ClampResizingColumn();
	}

	private void OnColumnResizeDelta(object sender, DragDeltaEventArgs e)
	{
		if (IsColumnResizing && ResizeHeader(e) == _resizingHeader) ClampResizingColumn();
	}

	private void OnColumnResizeCompleted(object sender, DragCompletedEventArgs e)
	{
		if (!IsColumnResizing || ResizeHeader(e) != _resizingHeader) return;
		ClampResizingColumn();
		SetValue(IsColumnResizingProperty, false);
		_resizingHeader = null;
	}

	private void ClampResizingColumn()
	{
		if (_resizingHeader?.Column is not { } column) return;
		var minimum = GetMinimumColumnWidth(column);
		if (minimum <= 0) minimum = GetColumnWidthFloor(GetColumnKey(column));
		// Zero is a valid hidden-column sentinel outside a drag. During a drag it
		// must be clamped too, before another layout can collapse the header.
		if (Double.IsNaN(column.Width) || column.Width < minimum) column.Width = minimum;
		var edge = _resizingHeader.TransformToAncestor(this).Transform(new Point(column.Width, 0)).X;
		SetValue(ColumnResizeOffsetProperty, Math.Max(0, edge - 4));
	}

	// One 20px hierarchy step is shared by buttons, icons/numbers, labels and feedback.
	public const double NameHierarchyGutterWidth = 48d;
	public static readonly DependencyProperty GroupingContentInsetProperty = DependencyProperty.Register(
		nameof(GroupingContentInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(0)));
	public Thickness GroupingContentInset => (Thickness)GetValue(GroupingContentInsetProperty);
	public static readonly DependencyProperty RootModNameInsetProperty = DependencyProperty.Register(
		nameof(RootModNameInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(0)));
	public Thickness RootModNameInset => (Thickness)GetValue(RootModNameInsetProperty);
	public static readonly DependencyProperty RootModIndexInsetProperty = DependencyProperty.Register(
		nameof(RootModIndexInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(2, 0, 2, 0)));
	public Thickness RootModIndexInset => (Thickness)GetValue(RootModIndexInsetProperty);
	public static readonly DependencyProperty RootModFeedbackInsetProperty = DependencyProperty.Register(
		nameof(RootModFeedbackInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(0)));
	public Thickness RootModFeedbackInset => (Thickness)GetValue(RootModFeedbackInsetProperty);
	public const double HierarchyLevelStep = 20d;
	public const double SeparatorBranchCenter = 17d;
	public static Thickness RootSeparatorToggleInset => new(SeparatorBranchCenter - 12, 0, 0, 0);
	public static Thickness ChildSeparatorToggleInset => new(SeparatorBranchCenter - 12 + HierarchyLevelStep, 0, 0, 0);
	public const double SeparatorLabelLeft = 56d;
	public const double SeparatorMarkerCenter = 39d;
	public const double HierarchyIndexWidth = 32d;
	public static Thickness RootSeparatorLabelInset => new(SeparatorMarkerCenter - 7, 0, 14, 0);
	public static Thickness ChildSeparatorLabelInset => new(SeparatorMarkerCenter - 7 + HierarchyLevelStep, 0, 14, 0);
	public static readonly DependencyProperty ChildModNameInsetProperty = DependencyProperty.Register(
		nameof(ChildModNameInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(0)));
	public Thickness ChildModNameInset => (Thickness)GetValue(ChildModNameInsetProperty);
	public static readonly DependencyProperty ChildModFeedbackInsetProperty = DependencyProperty.Register(
		nameof(ChildModFeedbackInset), typeof(Thickness), typeof(ModListView), new PropertyMetadata(new Thickness(0)));
	public Thickness ChildModFeedbackInset => (Thickness)GetValue(ChildModFeedbackInsetProperty);
	public static readonly DependencyProperty UsesHierarchyIndexAlignmentProperty = DependencyProperty.Register(
		nameof(UsesHierarchyIndexAlignment), typeof(bool), typeof(ModListView), new PropertyMetadata(false));
	public bool UsesHierarchyIndexAlignment => (bool)GetValue(UsesHierarchyIndexAlignmentProperty);
	internal readonly record struct GroupingRange(DivinityModData Item, int Start, int End);
	private readonly List<GroupingRange> _groupingRanges = new();
	internal IReadOnlyList<GroupingRange> GroupingRanges
	{
		get
		{
			// A same-size reorder need not arrange the outer ListView again.
			// Drawing and layout observers must always see the current item order.
			if (_groupingRangesDirty) { UpdateGroupingRanges(); _groupingRangesDirty = false; }
			return _groupingRanges;
		}
	}
	// The existing row transition owns the clock; the gutter only redraws on its frames.
	internal event EventHandler GroupingAnimationFrame;
	internal void RefreshGroupingAnimation() => GroupingAnimationFrame?.Invoke(this, EventArgs.Empty);
	private bool _groupingRangesDirty = true;
	private void UpdateGroupingRanges()
	{
		_groupingRanges.Clear();
		int parent = -1, child = -1;
		void End(int slot, int end)
		{
			if (slot >= 0) _groupingRanges[slot] = _groupingRanges[slot] with { End = end };
		}
		for (int index = 0; index < Items.Count; index++)
		{
			if (Items[index] is not DivinityModData { IsVisualDivider: true } divider) continue;
			End(child, index - 1); child = -1;
			if (!divider.IsChildVisualDivider) { End(parent, index - 1); parent = _groupingRanges.Count; }
			else child = _groupingRanges.Count;
			_groupingRanges.Add(new(divider, index, Items.Count - 1));
		}
	}
	public const double ColumnCellInset = 19d;
	// GridView's header presenter supplies 2px that the row presenter does not.
	public static Thickness ColumnHeaderPadding => new(ColumnCellInset - 2, 0, 12, 0);
	private int _separatorRowCount;
	public static readonly DependencyProperty CurrentSortDirectionProperty =
		DependencyProperty.RegisterAttached("CurrentSortDirection", typeof(ListSortDirection?), typeof(ModListView),
			new PropertyMetadata(null));
	public static ListSortDirection? GetCurrentSortDirection(DependencyObject column) =>
		(ListSortDirection?)column.GetValue(CurrentSortDirectionProperty);
	public static void SetCurrentSortDirection(DependencyObject column, ListSortDirection? value) =>
		column.SetValue(CurrentSortDirectionProperty, value);
	public static PropertyPath ColumnSortDirectionPath => new("Column.(0)", CurrentSortDirectionProperty);

	internal static string GetSortProperty(string column) => column switch
	{
		"Name" => "ListDisplayTitle",
		"File Name" => "FileName",
		"Version" => "Version.Version",
		"Modes" => "Targets",
		"Last Updated" => "DisplayLastUpdated",
		"Last Modified" => "LastModified",
		"Category" => "DisplayCategory",
		"Source" => "DisplaySource",
		_ => column
	};

	private void UpdateColumnSortIndicators()
	{
		if (View is not GridView grid) return;
		// Read the actual view, including programmatic sorting and clearing. A header
		// click cache can outlive a refresh or refer to a different pane.
		var sort = Items.SortDescriptions.FirstOrDefault();
		foreach (var column in grid.Columns)
		{
			var property = GetSortProperty(GetColumnKey(column));
			ListSortDirection? direction = Items.SortDescriptions.Count > 0 && property == sort.PropertyName
				? sort.Direction : null;
			if (GetCurrentSortDirection(column) != direction)
				SetCurrentSortDirection(column, direction);
		}
	}
	public static readonly DependencyProperty UsesSeparatorHeadersProperty =
		DependencyProperty.Register(nameof(UsesSeparatorHeaders), typeof(bool), typeof(ModListView),
			new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));
	public bool UsesSeparatorHeaders
	{
		get => (bool)GetValue(UsesSeparatorHeadersProperty);
		set => SetValue(UsesSeparatorHeadersProperty, value);
	}
	private static readonly DependencyPropertyKey HasSeparatorRowsPropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(HasSeparatorRows), typeof(bool), typeof(ModListView), new PropertyMetadata(false));
	public static readonly DependencyProperty HasSeparatorRowsProperty = HasSeparatorRowsPropertyKey.DependencyProperty;
	public bool HasSeparatorRows => (bool)GetValue(HasSeparatorRowsProperty);

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
	{
		base.OnItemsChanged(e);
		_groupingRangesDirty = true;
		InvalidateArrange();
		if (e.Action == NotifyCollectionChangedAction.Move) return;
		var hadSeparators = _separatorRowCount > 0;
		// Track incremental changes so loading thousands of ordinary rows does not
		// repeatedly scan the whole list just to decide whether a gutter is needed.
		if (e.Action == NotifyCollectionChangedAction.Reset)
			_separatorRowCount = Items.OfType<DivinityModData>().Count(item => item.IsVisualDivider);
		else
		{
			_separatorRowCount -= e.OldItems?.OfType<DivinityModData>().Count(item => item.IsVisualDivider) ?? 0;
			_separatorRowCount += e.NewItems?.OfType<DivinityModData>().Count(item => item.IsVisualDivider) ?? 0;
		}
		if (hadSeparators != (_separatorRowCount > 0))
		{
			SetValue(HasSeparatorRowsPropertyKey, _separatorRowCount > 0);
			InvalidateArrange();
		}
	}

	protected override Size ArrangeOverride(Size arrangeBounds)
	{
		if (_groupingRangesDirty) { UpdateGroupingRanges(); _groupingRangesDirty = false; }
		if (UsesSeparatorHeaders)
		{
			// Only the leading number/name cells may use the hierarchy space.
			// Never pull a reordered cell over another data column.
			var columns = (View as GridView)?.Columns;
			var first = columns?.Count > 0 ? GetColumnKey(columns[0]) : String.Empty;
			var nameCanShift = first == "Name" || (first == "#" && columns.Count > 1 && GetColumnKey(columns[1]) == "Name");
			// Keep content inside its actual data column. Numbered lists need only
			// 8px before the index; a leading Name column replaces the number lane.
			var gutter = !HasSeparatorRows ? 0 : first == "#" ? 8 : first == "Name" ? 17 : NameHierarchyGutterWidth;
			var nameShift = 0d;
			var alignIndex = HasSeparatorRows && first == "#";
			var indexShift = alignIndex ? SeparatorMarkerCenter + 4 - HierarchyIndexWidth / 2 - gutter - ColumnCellInset : 2;
			void UpdateInset(DependencyProperty property, Thickness value)
			{
				if ((Thickness)GetValue(property) != value) SetValue(property, value);
			}
			UpdateInset(GroupingContentInsetProperty, new Thickness(gutter, 0, 0, 0));
			UpdateInset(RootModNameInsetProperty, new Thickness(nameShift, 0, 0, 0));
			UpdateInset(ChildModNameInsetProperty, new Thickness(nameShift + (HasSeparatorRows && nameCanShift ? HierarchyLevelStep : 0), 0, 0, 0));
			UpdateInset(RootModIndexInsetProperty, new Thickness(indexShift, 0, 2, 0));
			if (UsesHierarchyIndexAlignment != alignIndex) SetValue(UsesHierarchyIndexAlignmentProperty, alignIndex);
			// Without a number cell, leave a little more room between the
			// interaction rail and the name without moving the column content.
			var feedbackShift = HasSeparatorRows && first is "#" or "Name" ? (first == "Name" ? 19d : 23d) - gutter : 0;
			UpdateInset(RootModFeedbackInsetProperty, new Thickness(feedbackShift, 0, 0, 0));
			UpdateInset(ChildModFeedbackInsetProperty, new Thickness(feedbackShift + (feedbackShift != 0 ? HierarchyLevelStep : 0), 0, 0, 0));
			// Preserve the native presenter's 2px cell inset.
			var margin = new Thickness(2 + gutter, 0, 2, 0);
			foreach (var header in this.FindVisualChildren<GridViewHeaderRowPresenter>())
				if (header.Margin != margin) header.Margin = margin;
		}
		var result = base.ArrangeOverride(arrangeBounds);
		if (!UsesSeparatorHeaders) return result;
		UpdateColumnSortIndicators();
		return result;
	}

	private ModListView _copyHeaderView = null;

	public bool HideHeader
	{
		get { return (bool)GetValue(HideHeaderProperty); }
		set { SetValue(HideHeaderProperty, value); }
	}
	public static readonly DependencyProperty HideHeaderProperty =
		DependencyProperty.Register("HideHeader", typeof(bool), typeof(ModListView), new PropertyMetadata(false));

	public ModListView LinkedHeaderListView
	{
		get { return (ModListView)GetValue(LinkedHeaderListViewProperty); }
		set { SetValue(LinkedHeaderListViewProperty, value); }
	}

	// Using a DependencyProperty as the backing store for LinkedHeaderListView.  This enables animation, styling, binding, etc...
	public static readonly DependencyProperty LinkedHeaderListViewProperty =
		DependencyProperty.Register("LinkedHeaderListView", typeof(ModListView), typeof(ModListView), new PropertyMetadata(null, new PropertyChangedCallback(OnLinkedHeaderListViewSet)));

	private static void OnLinkedHeaderListViewSet(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ModListView view)
		{
			if (e.OldValue is ModListView lastView)
				lastView.Loaded -= view.OnTargetGridLoaded;
			if (e.NewValue is ModListView targetView)
			{
				view._copyHeaderView = targetView;
				targetView.Loaded -= view.OnTargetGridLoaded;
				targetView.Loaded += view.OnTargetGridLoaded;
				if (targetView.IsLoaded)
				{
					view.OnTargetGridLoaded(targetView, new EventArgs());
				}
			}
			else view._copyHeaderView = null;
		}
	}

	private void OnTargetGridLoaded(object sender, EventArgs e)
	{
		if (sender is ModListView targetView && targetView.View is GridView grid)
		{
			PropertyDescriptor pd = DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));

			foreach (var col in grid.Columns)
			{
				pd.RemoveValueChanged(col, OnColumnWidthChanged_Copy);
				pd.AddValueChanged(col, OnColumnWidthChanged_Copy);
			}

			SynchronizeLinkedColumnWidths(grid);
		}
	}

	private void OnColumnWidthChanged_Copy(object sender, EventArgs e)
	{
		if (sender is GridViewColumn sourceColumn && View is GridView linkedView)
		{
			var key = GetColumnKey(sourceColumn);
			var linkedColumn = linkedView.Columns.FirstOrDefault(column =>
				String.Equals(GetColumnKey(column), key, StringComparison.OrdinalIgnoreCase));
			if (linkedColumn != null)
			{
				linkedColumn.Width = sourceColumn.Width;
			}
		}
	}

	private void SynchronizeLinkedColumnWidths(GridView sourceView)
	{
		if (View is not GridView linkedView) return;

		foreach (var sourceColumn in sourceView.Columns)
		{
			var key = GetColumnKey(sourceColumn);
			if (String.IsNullOrWhiteSpace(key)) continue;

			var linkedColumn = linkedView.Columns.FirstOrDefault(column =>
				String.Equals(GetColumnKey(column), key, StringComparison.OrdinalIgnoreCase));
			if (linkedColumn == null) continue;

			linkedColumn.Width = sourceColumn.Width;
		}
	}

	private static string GetColumnKey(GridViewColumn column) => column.Header switch
	{
		string header => header,
		TextBlock textBlock => textBlock.Text,
		_ => String.Empty
	};

    private void StyleColumnDropIndicator()
    {
        foreach (var presenter in this.FindVisualChildren<GridViewHeaderRowPresenter>())
        {
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(presenter); index++)
            {
                if (System.Windows.Media.VisualTreeHelper.GetChild(presenter, index) is not Separator indicator) continue;
                indicator.SetResourceReference(Control.TemplateProperty, "ReduxColumnDropIndicatorTemplate");
                indicator.Width = 6;
                indicator.IsHitTestVisible = false;
            }
        }
    }

	public ModListView() : base()
	{
		AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnColumnResizeStarted), true);
		AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnColumnResizeDelta), true);
		AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnColumnResizeCompleted), true);
        Loaded += (_, _) => StyleColumnDropIndicator();
        AddHandler(GridViewColumnHeader.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler((_, e) =>
            {
                if (e.OriginalSource is DependencyObject source &&
                    (source is GridViewColumnHeader || source.FindVisualParent<GridViewColumnHeader>() != null))
                    StyleColumnDropIndicator();
            }), true);

		if (!HideHeader)
		{
			Loaded += (o, e) =>
			{
				PropertyDescriptor pd = DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
				if (this.View is GridView grid)
				{
					//Capture user-resizing of the name column to disable auto-resizing
					var nameColumn = grid.Columns.FirstOrDefault(column => column.Header as string == "Name"
                        || column.Header is TextBlock text && text.Text == "Name");
					if (nameColumn != null)
					{
						pd.AddValueChanged(nameColumn, NameColumnWidthChanged);
					}
				}
			};
		}

		AddHandler(ListViewItem.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseLeftButtonDown), true);
		AddHandler(ListViewItem.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseLeftButtonUp), true);
	}

	private static void TryUpdateAnchor(ModListView listView, ListViewItem item)
	{
		try
		{
			var itemInfo = _itemInfoFromContainer?.Invoke(listView, [item]);
			if (itemInfo != null)
			{
				_updateAnchorAndActionItem?.Invoke(listView, [itemInfo]);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error updating anchor:\n{ex}");
		}
	}

	private bool _isSingleSelect = false;

	private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_isSingleSelect = false;
		if (IsEmbeddedButtonInput(e.OriginalSource as DependencyObject)) return;
		//Allow deselecting with just left click, if no modifiers are pressed and a single item is selected
		if (Keyboard.Modifiers == ModifierKeys.None && e.LeftButton == MouseButtonState.Pressed && !MainWindow.Self.ViewModel.IsDragging)
		{
			if (sender is ModListView listView && e.OriginalSource is UIElement element && element.FindVisualParent<ListViewItem>() is ListViewItem item && item.IsSelected)
			{
				if (listView.SelectedItems.Count == 1)
				{
					_isSingleSelect = true;
				}
			}
		}
	}

	private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (IsEmbeddedButtonInput(e.OriginalSource as DependencyObject))
		{
			_isSingleSelect = false;
			return;
		}
		if (Keyboard.Modifiers == ModifierKeys.None && _isSingleSelect && !MainWindow.Self.ViewModel.IsDragging)
		{
			if (sender is ModListView listView && e.OriginalSource is UIElement element && element.FindVisualParent<ListViewItem>() is ListViewItem item && item.IsSelected)
			{
				if (listView.SelectedItems.Count == 1)
				{
					item.IsSelected = false;
					TryUpdateAnchor(listView, item);
					e.Handled = true;
				}
			}
		}
		_isSingleSelect = false;
	}

	private static bool IsEmbeddedButtonInput(DependencyObject source) =>
		source is ButtonBase || source?.FindVisualParent<ButtonBase>() != null;

	private void NameColumnWidthChanged(object sender, EventArgs e)
	{
		if (!Resizing)
		{
			UserResizedColumns = true;
		}
		else
		{
			Resizing = false;
		}
	}

	protected override AutomationPeer OnCreateAutomationPeer()
	{
		return new ModListViewAutomationPeer(this);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (HideHeader)
		{
			base.OnKeyDown(e);
			return;
		}
		bool handled = false;

		if (SelectedItem != null && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && ItemsSource is ObservableCollectionExtended<DivinityModData> list)
		{
			var key = e.SystemKey;
			switch (key)
			{
				case Key.Up:
				case Key.Down:
				case Key.Right:
				case Key.Left:
					var selectedItems = list.Where(x => x.IsSelected).ToList();
					var lastIndexes = selectedItems.SafeToDictionary(m => m.UUID, m => list.IndexOf(m));
					int nextIndex = -1;
					int targetScrollIndex = -1;

					if (key == Key.Up)
					{
						for (int i = 0; i < selectedItems.Count; i++)
						{
							var m = selectedItems[i];
							int modIndex = list.IndexOf(m);
							nextIndex = Math.Max(0, modIndex - 1);
							var existingMod = list.ElementAtOrDefault(nextIndex);
							if (existingMod != null && existingMod.IsSelected)
							{
								var lastIndex = lastIndexes[existingMod.UUID];
								if (list.IndexOf(existingMod) == lastIndex)
								{
									// The selected mod at the target index
									// didn't get moved up/down, so skip moving the next one
									continue;
								}
							}
							if (targetScrollIndex == -1) targetScrollIndex = nextIndex;
							list.Move(modIndex, nextIndex);
						}
					}
					else if (key == Key.Down)
					{
						for (int i = selectedItems.Count - 1; i >= 0; i--)
						{
							var m = selectedItems[i];
							int modIndex = list.IndexOf(m);
							nextIndex = Math.Min(list.Count - 1, modIndex + 1);
							var existingMod = list.ElementAtOrDefault(nextIndex);
							if (existingMod != null && existingMod.IsSelected)
							{
								var lastIndex = lastIndexes[existingMod.UUID];
								if (list.IndexOf(existingMod) == lastIndex)
								{
									continue;
								}
							}
							if (targetScrollIndex == -1) targetScrollIndex = nextIndex;
							list.Move(modIndex, nextIndex);
						}
					}

					if (targetScrollIndex > -1)
					{
						var item = Items.GetItemAt(targetScrollIndex);
						ScrollIntoView(item);
					}

					handled = true;
					break;
			}
		}

		if (!handled)
		{
			base.OnKeyDown(e);

			// Fixes CTRL + Arrow keys not updating the anchored item, which then causes Shift selection to select everything between the new and old focused items
			switch (e.Key)
			{
				case Key.Up:
				case Key.Down:
					if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
					{
						var focused = Keyboard.FocusedElement as DependencyObject;
						var focusedItem = focused as ListViewItem ?? focused?.FindVisualParent<ListViewItem>();
						if (focusedItem != null) TryUpdateAnchor(this, focusedItem);
					}
					break;
			}
		}
	}
}
