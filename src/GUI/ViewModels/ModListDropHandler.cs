

using DivinityModManager.Models;
using DivinityModManager.Controls;
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
	public override void DragOver(IDropInfo dropInfo)
	{
		if (!_viewModel.AllowDrop)
		{
			DivinityApp.Log($"[AllowDrop] IsRefreshing({_viewModel.IsRefreshing}) IsInitialized({_viewModel.IsInitialized}) IsLoadingOrder({_viewModel.IsLoadingOrder})");
			dropInfo.Effects = DragDropEffects.None;
			return;
		}
		base.DragOver(dropInfo);
		if (dropInfo.Effects != DragDropEffects.None && dropInfo.DropTargetAdorner == DropTargetAdorners.Insert)
			dropInfo.DropTargetAdorner = null;
		if (ReferenceEquals(dropInfo.TargetCollection, _viewModel.DisplayInactiveMods) &&
			ExtractData(dropInfo.Data).OfType<DivinityModData>().Any(item => item.IsVisualDivider))
		{
			dropInfo.Effects = DragDropEffects.None;
			dropInfo.DropTargetAdorner = null;
			return;
		}
		if (dropInfo.Effects == DragDropEffects.None && dropInfo.Data is DataObject data && data.ContainsFileDropList())
		{
			var files = data.GetFileDropList();
			foreach (var file in files)
			{
				var ext = Path.GetExtension(file).ToLower();
				if (MainWindowViewModel.IsImportableFile(ext))
				{
					dropInfo.Effects = DragDropEffects.Copy | DragDropEffects.Move;
					dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
					break;
				}
			}
		}
	}

	override public void Drop(IDropInfo dropInfo)
	{
		_viewModel.IsDragging = false;

		if (dropInfo == null) return;

		if (!_viewModel.AllowDrop)
		{
			return;
		}

		bool isActive = dropInfo.TargetCollection == _viewModel.ActiveMods || dropInfo.TargetCollection == _viewModel.DisplayActiveMods;

		if (dropInfo.Data is DataObject dropFileData)
		{
			if (dropFileData.ContainsFileDropList())
			{
				var files = dropFileData.GetFileDropList()?.Cast<string>().ToList();
				if (files != null)
				{
					_viewModel.ImportMods(files, isActive);
				}
			}
			return;
		}

		if (dropInfo.DragInfo == null) return;

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
			var destinationActive = ReferenceEquals(dropInfo.TargetCollection, _viewModel.DisplayActiveMods);
			var visualData = ExtractData(dropInfo.Data).OfType<DivinityModData>().ToList();
			if (!destinationActive && visualData.Any(item => item.IsVisualDivider)) return;
			_viewModel.ApplyVisualModListDrop(visualData, destinationActive, insertIndex);
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
