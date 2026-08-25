using DivinityModManager.Extensions;
using DivinityModManager.Models;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.Views;

using DynamicData.Binding;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DivinityModManager.Controls;

public class ModListView : ListView
{
	private static readonly MethodInfo _itemInfoFromContainer = typeof(ItemsControl)
		.GetMethod("ItemInfoFromContainer", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly MethodInfo _updateAnchorAndActionItem = typeof(ListBox)
		.GetMethod("UpdateAnchorAndActionItem", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly PropertyInfo _actualColumnIndex = typeof(GridViewColumn)
		.GetProperty("ActualIndex", BindingFlags.NonPublic | BindingFlags.Instance);

	public bool Resizing { get; set; }
	public bool UserResizedColumns { get; set; }

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

			grid.Columns.CollectionChanged -= OnTargetGridCollectionChanged;
			grid.Columns.CollectionChanged += OnTargetGridCollectionChanged;

			foreach (var col in grid.Columns)
			{
				pd.RemoveValueChanged(col, OnColumnWidthChanged_Copy);
				pd.AddValueChanged(col, OnColumnWidthChanged_Copy);
			}
		}
	}

	private void OnTargetGridCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Move)
		{
			if (sender is GridViewColumnCollection colList)
			{
				var view = this.View as GridView;
				var indexOrder = colList.Select(x => GetColumnActualIndex(x)).ToList();
				DivinityApp.Log($"[Order] indexOrder({String.Join(";", indexOrder)})");
				var len = view.Columns.Count;
				for (int i = 0; i < len; i++)
				{
					var col = view.Columns[i];
					var nextIndex = indexOrder.IndexOf(GetColumnActualIndex(col));
					view.Columns.Move(i, nextIndex);
				}
			}
		}
	}

	private void OnColumnWidthChanged_Copy(object sender, EventArgs e)
	{
		if (sender is GridViewColumn col)
		{
			var thisView = this.View as GridView;
			var index = GetColumnActualIndex(col);
			var myCol = thisView.Columns.FirstOrDefault(x => GetColumnActualIndex(x) == index);
			if (myCol != null)
			{
				myCol.Width = col.Width;
			}
		}
	}

	public ModListView() : base()
	{
		if (!HideHeader)
		{
			Loaded += (o, e) =>
			{
				PropertyDescriptor pd = DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
				if (this.View is GridView grid)
				{
					//Capture user-resizing of the name column to disable auto-resizing
					var nameColumn = grid.Columns[1];
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

	private static int GetColumnActualIndex(GridViewColumn col)
	{
		return _actualColumnIndex?.GetValue(col) is int index ? index : -1;
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
