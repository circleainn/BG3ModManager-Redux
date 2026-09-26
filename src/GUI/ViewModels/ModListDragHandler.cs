using DivinityModManager.Models;

using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;

using DivinityModManager.Util;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DivinityModManager.ViewModels;

public class ManualDragInfo : IDragInfo
{
	public DataFormat DataFormat { get; set; }
	public object Data { get; set; }
	public Point DragStartPosition { get; }
	public Point PositionInDraggedItem { get; }
	public DragDropEffects Effects { get; set; }
	public MouseButton MouseButton { get; set; }
	public System.Collections.IEnumerable SourceCollection { get; set; }
	public int SourceIndex { get; set; }
	public object SourceItem { get; set; }
	public System.Collections.IEnumerable SourceItems { get; set; }
	public CollectionViewGroup SourceGroup { get; set; }
	public UIElement VisualSource { get; set; }
	public UIElement VisualSourceItem { get; set; }
	public FlowDirection VisualSourceFlowDirection { get; set; }
	public object DataObject { get; set; }
	public Func<DependencyObject, object, DragDropEffects, DragDropEffects> DragDropHandler { get; set; }
	public DragDropKeyStates DragDropCopyKeyState { get; set; }

	public void RefreshSourceItems(object sender)
	{
		if (sender is not ItemsControl itemsControl)
		{
			return;
		}

		var selectedItems = itemsControl.GetSelectedItems().OfType<object>().Where(i => i != CollectionView.NewItemPlaceholder).ToList();
		this.SourceItems = selectedItems;

		if (selectedItems.Count <= 1 || this.SourceItem != null && !selectedItems.Contains(this.SourceItem))
		{
			this.SourceItems = Enumerable.Repeat(this.SourceItem, 1);
		}
	}
}

public class ModListDragHandler : DefaultDragHandler
{
	private MainWindowViewModel _viewModel;

	public ModListDragHandler(MainWindowViewModel vm) : base()
	{
		_viewModel = vm;
	}

	private IDragInfo _lastDragInfo;

	private IDisposable _stopDraggingFallbackTask;
	public bool CanDropOnPane(bool active)
	{
		var items = (_lastDragInfo?.Data as IEnumerable<DivinityModData>)?.ToArray() ?? [];
		if (active && _viewModel.IsDraggingHeldOverride) return true;
		if (active && _viewModel.IsOverrideVisualModCollection(_lastDragInfo?.SourceCollection)) return false;
		if (!active && _viewModel.IsOverrideVisualModCollection(_lastDragInfo?.SourceCollection))
			return items.Length > 0 && items.All(mod => mod.IsForceLoaded &&
				!mod.IsForceLoadedMergedMod && !mod.IsVisualDivider);
		return VisualDividerDragPolicy.CanDropOnPane(items, active);
	}

	public bool CanDropOnOverridePane()
	{
		if (_viewModel.IsOverrideVisualModCollection(_lastDragInfo?.SourceCollection)) return true;
		var items = (_lastDragInfo?.Data as IEnumerable<DivinityModData>)?.ToArray() ?? [];
		var fromInactive = _viewModel.IsInactiveVisualModCollection(_lastDragInfo?.SourceCollection)
			|| items.Length > 0 && items.All(mod => mod.IsHeldOverride &&
				_viewModel.DisplayInactiveMods.Contains(mod));
		return fromInactive && items.Length > 0 && items.All(mod => mod.IsForceLoaded &&
			!mod.IsForceLoadedMergedMod && !mod.IsVisualDivider);
	}

	private void StopDragTracking()
	{
		_viewModel.IsDragging = false;
		_viewModel.IsDraggingHeldOverride = false;
		_stopDraggingFallbackTask?.Dispose();
		_stopDraggingFallbackTask = null;

		// A ListBoxItem can retain mouse capture when the pointer is released over
		// an invalid target or just outside the pane. The drag operation is already
		// terminal whenever this method runs, so no control should continue owning
		// capture and blocking interaction in the other panes.
		if (Mouse.Captured != null)
			Mouse.Capture(null);
	}

	public void CompleteDragTracking() => StopDragTracking();

	private void ScheduleStopDraggingFallback()
	{
		_stopDraggingFallbackTask?.Dispose();
		_stopDraggingFallbackTask = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromSeconds(1), () =>
		{
			if (Mouse.LeftButton == MouseButtonState.Released)
			{
				StopDragTracking();
			}
			else
			{
				ScheduleStopDraggingFallback();
			}
		});
	}

	public override void StartDrag(IDragInfo dragInfo)
	{
		if (dragInfo != null)
		{
			_lastDragInfo = dragInfo;
			var originalData = dragInfo.Data;
			var sourceItem = dragInfo.SourceItem as DivinityModData;
			if (sourceItem == null && dragInfo.VisualSourceItem is FrameworkElement sourceElement)
				sourceItem = sourceElement.DataContext as DivinityModData;
			if (sourceItem == null)
				sourceItem = originalData as DivinityModData;
			if (sourceItem == null && originalData is IEnumerable<DivinityModData> originalItems)
				sourceItem = originalItems.FirstOrDefault();
			dragInfo.Data = null;
			if (_viewModel.IsActiveVisualModCollection(dragInfo.SourceCollection))
			{
				var selected = VisualDividerDragPolicy.ResolveDragItems(
					_viewModel.DisplayActiveMods,
					sourceItem,
					x => x.Visibility == Visibility.Visible && x.CanDrag);
				dragInfo.Data = selected.Count > 0 ? selected : null;
			}
			else if (_viewModel.IsInactiveVisualModCollection(dragInfo.SourceCollection))
			{
				var selected = VisualDividerDragPolicy.ResolveDragItems(
					_viewModel.DisplayInactiveMods,
					sourceItem,
					x => x.Visibility == Visibility.Visible && x.CanDrag);
				dragInfo.Data = selected.Count > 0 ? selected : null;
			}
			else if (_viewModel.IsOverrideVisualModCollection(dragInfo.SourceCollection))
			{
				// Pure override packages deliberately have CanDrag=false because they
				// cannot enter the normal active/inactive load order. That restriction
				// must not prevent presentation-only ordering inside Override Mods.
				var selected = VisualDividerDragPolicy.ResolveDragItems(
					_viewModel.DisplayOverrideMods,
					sourceItem,
					x => x.Visibility == Visibility.Visible &&
						VisualDividerDragPolicy.CanStartDrag(x, withinOverridePane: true));
				dragInfo.Data = selected.Count > 0 ? selected : null;
			}
			else if (dragInfo.SourceCollection == _viewModel.ActiveMods)
			{
				var selected = _viewModel.ActiveMods.Where(x => x.IsSelected && x.Visibility == Visibility.Visible);
				dragInfo.Data = selected;
			}
			else if (dragInfo.SourceCollection == _viewModel.InactiveMods)
			{
				var selected = _viewModel.InactiveMods.Where(x => x.IsSelected && x.Visibility == Visibility.Visible && x.CanDrag);
				dragInfo.Data = selected;
			}
			if (dragInfo.Data != null)
			{
			var draggedMods = (dragInfo.Data as IEnumerable<DivinityModData>)?.ToArray() ?? [];
			_viewModel.IsDraggingHeldOverride = draggedMods.Length > 0
				&& draggedMods.All(mod => mod.IsHeldOverride && mod.IsForceLoaded
					&& !mod.IsForceLoadedMergedMod && !mod.IsVisualDivider)
				&& draggedMods.All(mod => _viewModel.DisplayInactiveMods.Contains(mod));
				_viewModel.IsDragging = true;
				ScheduleStopDraggingFallback();
			}
			dragInfo.Effects = dragInfo.Data != null ? DragDropEffects.Copy | DragDropEffects.Move : DragDropEffects.None;
		}
	}

	public override void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
	{
		StopDragTracking();
	}

	public override bool CanStartDrag(IDragInfo dragInfo)
	{
		if (!_viewModel.AllowDrop)
		{
			return false;
		}
		if (_viewModel.IsActiveListMetadataSorted &&
			(_viewModel.IsActiveVisualModCollection(dragInfo.SourceCollection) ||
			 ReferenceEquals(dragInfo.SourceCollection, _viewModel.ActiveMods)))
		{
			// A row index in a sorted ICollectionView is not a real load-order index.
			// Keep sorting view-only by allowing reordering only in the # view.
			return false;
		}
		if (_viewModel.IsOverrideListMetadataSorted &&
			_viewModel.IsOverrideVisualModCollection(dragInfo.SourceCollection))
			return false;
		// A sorted or filtered inactive projection is still a safe source when the
		// destination is Active Mods: the payload is resolved by mod identity, not by
		// its display index. ModListDropHandler continues to reject reordering inside
		// that projected inactive view.
		var reorderingOverrideMods = _viewModel.IsOverrideVisualModCollection(dragInfo.SourceCollection);
		if (dragInfo.Data is DivinityModData draggedMod &&
			!VisualDividerDragPolicy.CanStartDrag(draggedMod, reorderingOverrideMods))
		{
			return false;
		}
		if (!reorderingOverrideMods && dragInfo.Data is ISelectable d && !d.CanDrag)
		{
			return false;
		}
		else if (!reorderingOverrideMods && dragInfo.Data is IEnumerable<DivinityModData> modData)
		{
			if (modData.All(x => !x.CanDrag))
			{
				return false;
			}
		}
		return true;
	}

	public override void DragCancelled()
	{
		StopDragTracking();
		if (_lastDragInfo != null)
		{
			_lastDragInfo.Effects = DragDropEffects.None;
		}
	}

	public override bool TryCatchOccurredException(Exception exception)
	{
		StopDragTracking();
		if (exception is COMException)
		{
			return true;
		}
		return false;
	}
}
