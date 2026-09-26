

using DivinityModManager.Models;
using DivinityModManager.Controls;
using DivinityModManager.Util;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DivinityModManager.ViewModels;

public class ManualDropInfo : IDropInfo
{
	public object Data { get; private set; }
	public IDragInfo DragInfo { get; }
	public Point DropPosition { get; }
	public Type DropTargetAdorner { get; set; }
	public DragDropEffects Effects { get; set; }
	public int InsertIndex { get; }
	public int UnfilteredInsertIndex { get; }
	public System.Collections.IEnumerable TargetCollection { get; set; }
	public object TargetItem { get; }
	public CollectionViewGroup TargetGroup { get; }
	public UIElement VisualTarget { get; }
	public UIElement VisualTargetItem { get; }
	public Orientation VisualTargetOrientation { get; }
	public FlowDirection VisualTargetFlowDirection { get; }
	public string DestinationText { get; set; }
	public string EffectText { get; set; }
	public RelativeInsertPosition InsertPosition { get; }
	public DragDropKeyStates KeyStates { get; }
	public bool NotHandled { get; set; }
	public bool IsSameDragDropContextAsSource { get; }
	public EventType EventType { get; }
	object IDropInfo.Data
	{
		get => Data;
		set => Data = value;
	}

	private readonly ScrollViewer _targetScrollViewer;
	private readonly ScrollingMode _targetScrollingMode;

	ScrollViewer IDropInfo.TargetScrollViewer => _targetScrollViewer;
	ScrollingMode IDropInfo.TargetScrollingMode => _targetScrollingMode;

	public Type DropTargetHintAdorner { get; set; }
	public DropHintState DropTargetHintState { get; set; }
	public string DropHintText { get; set; }
	public bool AcceptChildItem { get; set; }

	public ManualDropInfo(List<DivinityModData> data, int index, UIElement visualTarget, System.Collections.IEnumerable targetCollection, System.Collections.IEnumerable sourceCollection)
	{
		UnfilteredInsertIndex = index;
		VisualTarget = visualTarget;
		TargetCollection = targetCollection;
		Data = data;
		var scrollViewer = visualTarget.FindVisualChildren<ScrollViewer>().FirstOrDefault();
		if (scrollViewer != null)
		{
			_targetScrollViewer = scrollViewer;
			_targetScrollingMode = ScrollingMode.VerticalOnly;
		}
		DragInfo = new ManualDragInfo()
		{
			SourceCollection = sourceCollection,
			Data = data
		};

		DropTargetHintAdorner = typeof(DropTargetHintAdorner);
	}
}


public class ModListDropHandler : DefaultDropHandler
{
	private static bool IsCategoryPayload(object data) =>
		data is ModCategoryFilterItem ||
		data is IDataObject dataObject && dataObject.GetDataPresent(typeof(ModCategoryFilterItem));

	private bool IsOverrideTransfer(IDropInfo dropInfo, out bool activate)
	{
		activate = _viewModel.IsOverrideVisualModCollection(dropInfo.TargetCollection);
		var sourceOverride = _viewModel.IsOverrideVisualModCollection(dropInfo.DragInfo?.SourceCollection);
		var targetInactive = _viewModel.IsInactiveVisualModCollection(dropInfo.TargetCollection);
		var sourceInactive = _viewModel.IsInactiveVisualModCollection(dropInfo.DragInfo?.SourceCollection);
		var items = ExtractData(dropInfo.Data).OfType<DivinityModData>().ToArray();
		// Filtered ListViews can provide a view wrapper for SourceCollection. A held
		// Override still has an unambiguous source when it is visible in Inactive Mods.
		if (!sourceInactive && items.Length > 0)
			sourceInactive = items.All(mod => mod.IsHeldOverride &&
				_viewModel.DisplayInactiveMods.Contains(mod));
		if (!(sourceOverride && targetInactive || sourceInactive && activate)) return false;
		return items.Length > 0 && items.All(mod => mod.IsForceLoaded &&
			!mod.IsForceLoadedMergedMod && !mod.IsVisualDivider);
	}

	private bool IsHeldOverrideActivationOnActivePane(IDropInfo dropInfo)
	{
		if (dropInfo.TargetCollection != _viewModel.ActiveMods &&
			!_viewModel.IsActiveVisualModCollection(dropInfo.TargetCollection)) return false;
		var items = ExtractData(dropInfo.Data).OfType<DivinityModData>().ToArray();
		return items.Length > 0 && items.All(mod => mod.IsHeldOverride && mod.IsForceLoaded &&
			!mod.IsForceLoadedMergedMod && !mod.IsVisualDivider &&
			_viewModel.DisplayInactiveMods.Contains(mod));
	}

	private bool IsOverrideModDropOnActivePane(IDropInfo dropInfo) =>
		(dropInfo.TargetCollection == _viewModel.ActiveMods ||
		 _viewModel.IsActiveVisualModCollection(dropInfo.TargetCollection)) &&
		ExtractData(dropInfo.Data).OfType<DivinityModData>().Any(mod => mod.IsForceLoaded) &&
		!IsHeldOverrideActivationOnActivePane(dropInfo);

	public override void DragOver(IDropInfo dropInfo)
	{
		if (IsCategoryPayload(dropInfo?.Data))
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		if (IsOverrideModDropOnActivePane(dropInfo))
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		if (IsHeldOverrideActivationOnActivePane(dropInfo))
		{
			dropInfo.Effects = _viewModel.AllowDrop ? DragDropEffects.Move : DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}

		if (!_viewModel.AllowDrop ||
			(_viewModel.IsOverrideListMetadataSorted && _viewModel.IsOverrideVisualModCollection(dropInfo.TargetCollection)) ||
			(_viewModel.IsInactiveListMetadataSorted && _viewModel.IsInactiveVisualModCollection(dropInfo.TargetCollection)) ||
			(_viewModel.IsActiveListMetadataSorted && _viewModel.IsActiveVisualModCollection(dropInfo.TargetCollection)))
		{
			DivinityApp.Log($"[AllowDrop] IsRefreshing({_viewModel.IsRefreshing}) IsInitialized({_viewModel.IsInitialized}) IsLoadingOrder({_viewModel.IsLoadingOrder})");
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		var overrideTransfer = IsOverrideTransfer(dropInfo, out _);
		if (overrideTransfer)
		{
			dropInfo.Effects = DragDropEffects.Move;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		base.DragOver(dropInfo);
		if (dropInfo.Effects != DragDropEffects.None && dropInfo.DropTargetAdorner == DropTargetAdorners.Insert)
			dropInfo.DropTargetAdorner = null;
		var targetsOverride = _viewModel.IsOverrideVisualModCollection(dropInfo.TargetCollection);
		var startsInOverride = _viewModel.IsOverrideVisualModCollection(dropInfo.DragInfo?.SourceCollection);
		if (targetsOverride != startsInOverride)
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		if (_viewModel.IsInactiveVisualModCollection(dropInfo.TargetCollection) &&
			!VisualDividerDragPolicy.CanDropOnPane(
				ExtractData(dropInfo.Data).OfType<DivinityModData>(),
				destinationActive: false))
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		// External packages are handled once by MainWindow's full-window install target.
		// A mod pane must never imply that dropping on it selects the install destination.
		if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
		}
	}

	override public void Drop(IDropInfo dropInfo)
	{
		_viewModel.IsDragging = false;

		if (dropInfo == null) return;
		if (IsCategoryPayload(dropInfo.Data))
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}

		if (IsHeldOverrideActivationOnActivePane(dropInfo))
		{
			if (_viewModel.AllowDrop)
				_viewModel.TransferOverrideMods(ExtractData(dropInfo.Data).OfType<DivinityModData>(), true);
			return;
		}
		if (IsOverrideModDropOnActivePane(dropInfo) || !_viewModel.AllowDrop ||
			(_viewModel.IsOverrideListMetadataSorted && _viewModel.IsOverrideVisualModCollection(dropInfo.TargetCollection)) ||
			(_viewModel.IsInactiveListMetadataSorted && _viewModel.IsInactiveVisualModCollection(dropInfo.TargetCollection)) ||
			(_viewModel.IsActiveListMetadataSorted && _viewModel.IsActiveVisualModCollection(dropInfo.TargetCollection)))
		{
			return;
		}

		bool isActive = dropInfo.TargetCollection == _viewModel.ActiveMods || dropInfo.TargetCollection == _viewModel.DisplayActiveMods;

		if (dropInfo.Data is DataObject dropFileData && dropFileData.ContainsFileDropList()) return;

		if (dropInfo.DragInfo == null) return;
		if (IsOverrideTransfer(dropInfo, out var activateOverride))
		{
			var previewInsertIndex = dropInfo.VisualTarget is DependencyObject visualTarget
				? ReduxDropFeedback.GetStableInsertIndex(visualTarget)
				: -1;
			var transferInsertIndex = previewInsertIndex >= 0 ? previewInsertIndex : dropInfo.UnfilteredInsertIndex;
			var visibleDestinationItems = (dropInfo.VisualTarget as ItemsControl)?.Items
				.OfType<DivinityModData>().ToList();
			if (dropInfo.VisualTarget is DependencyObject target)
				ReduxDropFeedback.SetStableInsertIndex(target, -1);
			_viewModel.TransferOverrideMods(
				ExtractData(dropInfo.Data).OfType<DivinityModData>(),
				activateOverride,
				transferInsertIndex,
				visibleDestinationItems);
			return;
		}
		var insertIndex = dropInfo.UnfilteredInsertIndex;
		if (_viewModel.IsVisualModCollection(dropInfo.TargetCollection))
		{
			var previewInsertIndex = dropInfo.VisualTarget is DependencyObject visualTarget
				? ReduxDropFeedback.GetStableInsertIndex(visualTarget)
				: -1;
			if (previewInsertIndex >= 0)
				insertIndex = previewInsertIndex;
			if (dropInfo.VisualTarget is DependencyObject target)
				ReduxDropFeedback.SetStableInsertIndex(target, -1);
			var visualData = ExtractData(dropInfo.Data).OfType<DivinityModData>().ToList();
			if (_viewModel.IsOverrideVisualModCollection(dropInfo.TargetCollection))
			{
				if (!_viewModel.IsOverrideVisualModCollection(dropInfo.DragInfo.SourceCollection)) return;
				var visibleOverrideItems = (dropInfo.VisualTarget as ItemsControl)?.Items
					.OfType<DivinityModData>()
					.ToList();
				_viewModel.ApplyOverrideVisualModListDrop(visualData, insertIndex, visibleOverrideItems);
				RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(20), () =>
					_viewModel.Layout.SelectMods(visualData));
				return;
			}
			if (_viewModel.IsOverrideVisualModCollection(dropInfo.DragInfo.SourceCollection)) return;
			var destinationActive = _viewModel.IsActiveVisualModCollection(dropInfo.TargetCollection);
			if (!VisualDividerDragPolicy.CanDropOnPane(visualData, destinationActive)) return;
			_viewModel.ApplyVisualModListDrop(visualData, destinationActive, insertIndex);
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(20), () =>
				_viewModel.Layout.SelectMods(visualData));
			return;
		}

		var itemsControl = dropInfo.VisualTarget as ItemsControl;
		if (itemsControl != null && itemsControl.Items is IEditableCollectionView editableItems)
		{
			var newItemPlaceholderPosition = editableItems.NewItemPlaceholderPosition;
			if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning && insertIndex == 0)
			{
				++insertIndex;
			}
			else if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd && insertIndex == itemsControl.Items.Count)
			{
				--insertIndex;
			}
		}

		var destinationList = dropInfo.TargetCollection.TryGetList();
		var data = ExtractData(dropInfo.Data).OfType<DivinityModData>().ToList();

		var sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();
		if (sourceList != null)
		{
			foreach (var o in data)
			{
				var index = sourceList.IndexOf(o);
				if (index != -1)
				{
					sourceList.RemoveAt(index);
					// so, is the source list the destination list too ?
					if (destinationList != null && Equals(sourceList, destinationList) && index < insertIndex)
					{
						--insertIndex;
					}
				}
			}
		}

		if (destinationList != null)
		{
			if (insertIndex < 0)
			{
				insertIndex = 0;
			}

			if (destinationList.Count == 0)
			{
				foreach (var o in data)
				{
					destinationList.Add(o);
				}
			}
			else
			{
				foreach (var o in data)
				{
					try
					{
						if (insertIndex < destinationList.Count)
						{
							destinationList.Insert(insertIndex, o);
							insertIndex++;
						}
						else
						{
							destinationList.Add(o);
							insertIndex++;
						}
					}
					catch (Exception ex)
					{
						DivinityApp.Log($"Error adding drop operation item to destinationList at {insertIndex}:\n{ex}");
						destinationList.Add(o);
					}
				}
			}

			var selectDroppedItems = itemsControl is TabControl || (itemsControl != null && GongSolutions.Wpf.DragDrop.DragDrop.GetSelectDroppedItems(itemsControl));
			if (selectDroppedItems)
			{
				SelectDroppedItems(dropInfo, data);
			}
		}

		var selectedUUIDs = data.Select(x => x.UUID).ToHashSet();

		for (var index = 0; index < _viewModel.ActiveMods.Count; index++)
		{
			_viewModel.ActiveMods[index].Index = index;
		}

		foreach (var mod in _viewModel.Mods)
		{
			if (selectedUUIDs.Contains(mod.UUID))
			{
				mod.IsActive = isActive;
				mod.IsSelected = true;
			}
			else
			{
				mod.IsSelected = false;
			}
		}

		RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(20), () =>
		{
			_viewModel.Layout.SelectMods(data);
			if(isActive)
			{
				_viewModel.Layout.RefreshDataView(_viewModel.Layout.ActiveModsView);
				_viewModel.Layout.RefreshDataView(_viewModel.Layout.ForceLoadedModsView);
			}
			else
			{
				_viewModel.Layout.RefreshDataView(_viewModel.Layout.InactiveModsView);
			}
		});

		if (isActive)
		{
			_viewModel.OnFilterTextChanged(_viewModel.ActiveModFilterText, _viewModel.ActiveMods);
		}
		else
		{
			_viewModel.OnFilterTextChanged(_viewModel.InactiveModFilterText, _viewModel.InactiveMods);
		}

	}

	private readonly MainWindowViewModel _viewModel;

	public ModListDropHandler(MainWindowViewModel vm) : base()
	{
		_viewModel = vm;
	}
}
