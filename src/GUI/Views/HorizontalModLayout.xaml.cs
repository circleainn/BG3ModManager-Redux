using DivinityModManager.Controls;
using DivinityModManager.Converters;
using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.Util.ScreenReader;
using DivinityModManager.ViewModels;

using GongSolutions.Wpf.DragDrop.Utilities;

using AdonisUI;

using ReactiveMarbles.ObservableEvents;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DivinityModManager.Views;

public interface IModViewLayout
{
	void UpdateViewSelection(IEnumerable<ISelectable> dataList, ListView listView = null);
	void SelectMods(IEnumerable<DivinityModData> mods);
	void DeselectAll();
	void FixActiveModsScrollbar();
	void RefreshDataView(ListView target);
	ModListView ActiveModsView { get; }
	ModListView InactiveModsView { get; }
	ModListView ForceLoadedModsView { get; }
}

public class HorizontalModLayoutBase : ReactiveUserControl<MainWindowViewModel> { }

/// <summary>
/// Interaction logic for HorizonalModLayout.xaml
/// </summary>
public partial class HorizontalModLayout : HorizontalModLayoutBase, IModViewLayout
{
	private const string CategoryAssignmentMenuTag = "ReduxCategoryAssignment";
	private const string SourceLinkMenuTag = "ReduxSourceLink";
	private Point _categoryDragStart;
	private ModCategoryFilterItem _draggedCategory;
	private CategoryDropIndicatorAdorner _categoryDropIndicator;
	private ListBoxItem _categoryDropTarget;
	private bool _categoryDropAfter;
	private const string VisualDividerMenuTag = "ReduxVisualDivider";
	private const double DefaultModDetailsRowHeight = 295;
	private const double MinimumExpandedModDetailsRowHeight = 295;
	private const double CollapsedModDetailsRowHeight = 58;
	private const double ModDetailsSplitterHeight = 6;
	private const double CollapsedCategoriesWidth = 52;
	// Fallback seed only, used before the panel has real category data to measure against.
	private const double MinimumExpandedCategoriesWidth = 180;
	private const double DefaultExpandedCategoriesWidth = 220;
	private const double DefaultOverrideModsRowHeight = 160;
	private const double MinimumExpandedOverrideModsRowHeight = 90;

	private object _focusedList = null;
	private double _lastExpandedModDetailsRowHeight = DefaultModDetailsRowHeight;
	private double _lastExpandedCategoriesWidth = DefaultExpandedCategoriesWidth;
	private double _minimumExpandedCategoriesWidth = MinimumExpandedCategoriesWidth;
	private double _lastExpandedOverrideModsRowHeight = DefaultOverrideModsRowHeight;
	private readonly Dictionary<GridViewColumn, double> _visibleModListColumnWidths = new();
	private readonly Dictionary<GridView, Dictionary<string, (GridViewColumn Column, int Index)>> _modListColumnRegistry = new();
	private static readonly string[] OptionalModListColumns =
	[
		"File Name",
		"Version",
		"Last Updated",
		"Last Modified",
		"Author",
		"Category",
		"Source"
	];

	private MessageBoxResult ShowCategoryMessage(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
	{
		var owner = Window.GetWindow(this) as MainWindow ?? MainWindow.Self;
		return Xceed.Wpf.Toolkit.MessageBox.Show(owner, message, caption, buttons, image,
			buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK,
			owner?.MessageBoxStyle);
	}

	private void CategoriesContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		ShowEmptyCategoriesMenuItem.IsChecked = !ViewModel.Settings.HideEmptyModCategories;
		SaveCategoryFilterMenuItem.IsChecked = ViewModel.Settings.SaveModCategoryFilterBetweenSessions;
		DisableNewModIndicatorsMenuItem.IsChecked = ViewModel.Settings.DisableNewModCategoryIndicators;
		ChangeCategoryColorMenuItem.IsEnabled = !String.IsNullOrWhiteSpace(ViewModel.SelectedModCategory) &&
			!ViewModel.SelectedModCategory.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase);

		EnableCategoriesMenuItem.Items.Clear();
		foreach (var category in ViewModel.GetAllModCategories())
		{
			var item = new MenuItem { Header = category, IsCheckable = true, IsChecked = ViewModel.IsModCategoryEnabled(category) };
			item.Click += (_, _) => ViewModel.SetModCategoryEnabled(category, item.IsChecked);
			EnableCategoriesMenuItem.Items.Add(item);
		}

		DeleteCustomCategoryMenuItem.Items.Clear();
		foreach (var category in ViewModel.Settings.CustomModCategories ?? Enumerable.Empty<string>())
		{
			var item = new MenuItem { Header = category };
			item.Click += (_, _) =>
			{
				var result = ShowCategoryMessage(
					$"Delete the custom category '{category}'?\n\nThe mods themselves and their load-order positions will not be changed.",
					"Delete Custom Category", MessageBoxButton.YesNo, MessageBoxImage.Warning);
				if (result == MessageBoxResult.Yes) ViewModel.DeleteCustomModCategory(category);
			};
			DeleteCustomCategoryMenuItem.Items.Add(item);
		}
		DeleteCustomCategoryMenuItem.IsEnabled = DeleteCustomCategoryMenuItem.Items.Count > 0;
	}

	private void DisableNewModIndicatorsMenuItem_Click(object sender, RoutedEventArgs e) =>
		ViewModel.SetNewModCategoryIndicatorsDisabled(DisableNewModIndicatorsMenuItem.IsChecked);

	private void CategoryListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource is DependencyObject source && source.FindVisualParent<ListBoxItem>()?.DataContext is ModCategoryFilterItem category)
			ViewModel.MarkModCategorySeen(category.Name);
	}

	private void CategoryListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_categoryDragStart = e.GetPosition(null);
		_draggedCategory = (e.OriginalSource as DependencyObject)?.FindVisualParent<ListBoxItem>()?.DataContext as ModCategoryFilterItem;
		if (_draggedCategory?.Name.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase) == true)
			_draggedCategory = null;
	}

	private void CategoryListBox_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed || _draggedCategory == null || sender is not ListBox listBox) return;
		var position = e.GetPosition(null);
		if (Math.Abs(position.X - _categoryDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
			Math.Abs(position.Y - _categoryDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

		var dragged = _draggedCategory;
		_draggedCategory = null;
		DragDrop.DoDragDrop(listBox, dragged, DragDropEffects.Move);
	}

	private void CategoryListBox_DragOver(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(typeof(ModCategoryFilterItem)) ||
			(e.OriginalSource as DependencyObject)?.FindVisualParent<ListBoxItem>() is not ListBoxItem targetItem)
		{
			ClearCategoryDropIndicator();
			e.Effects = DragDropEffects.None;
			e.Handled = true;
			return;
		}

		var insertAfter = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
		if (targetItem.DataContext is ModCategoryFilterItem target &&
			target.Name.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase)) insertAfter = true;
		ShowCategoryDropIndicator(targetItem, insertAfter);
		e.Effects = DragDropEffects.Move;
		e.Handled = true;
	}

	private void CategoryListBox_DragLeave(object sender, DragEventArgs e) => ClearCategoryDropIndicator();

	private void CategoryListBox_Drop(object sender, DragEventArgs e)
	{
		if (e.Data.GetData(typeof(ModCategoryFilterItem)) is not ModCategoryFilterItem source ||
			(e.OriginalSource as DependencyObject)?.FindVisualParent<ListBoxItem>() is not ListBoxItem targetItem ||
			targetItem.DataContext is not ModCategoryFilterItem target)
		{
			ClearCategoryDropIndicator();
			return;
		}

		var insertAfter = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
		if (target.Name.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase)) insertAfter = true;
		ClearCategoryDropIndicator();
		ViewModel.MoveModCategory(source.Name, target.Name, insertAfter);
		e.Handled = true;
	}

	private void ShowCategoryDropIndicator(ListBoxItem target, bool insertAfter)
	{
		if (_categoryDropTarget == target && _categoryDropAfter == insertAfter && _categoryDropIndicator != null) return;
		ClearCategoryDropIndicator();
		var layer = AdornerLayer.GetAdornerLayer(target);
		if (layer == null) return;
		var brush = TryFindResource("ReduxAccentHoverBrush") as Brush ?? System.Windows.Media.Brushes.MediumPurple;
		_categoryDropTarget = target;
		_categoryDropAfter = insertAfter;
		_categoryDropIndicator = new CategoryDropIndicatorAdorner(target, insertAfter, brush);
		layer.Add(_categoryDropIndicator);
	}

	private void ClearCategoryDropIndicator()
	{
		if (_categoryDropIndicator != null)
			AdornerLayer.GetAdornerLayer(_categoryDropIndicator.AdornedElement)?.Remove(_categoryDropIndicator);
		_categoryDropIndicator = null;
		_categoryDropTarget = null;
	}

	private sealed class CategoryDropIndicatorAdorner : Adorner
	{
		private readonly bool _after;
		private readonly Pen _pen;

		public CategoryDropIndicatorAdorner(UIElement adornedElement, bool after, Brush brush) : base(adornedElement)
		{
			_after = after;
			_pen = new Pen(brush, 2);
			IsHitTestVisible = false;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			var y = _after ? AdornedElement.RenderSize.Height - 1 : 1;
			drawingContext.DrawLine(_pen, new Point(3, y), new Point(Math.Max(3, AdornedElement.RenderSize.Width - 3), y));
		}
	}

	private void SaveCategoryFilterMenuItem_Click(object sender, RoutedEventArgs e)
	{
		ViewModel.Settings.SaveModCategoryFilterBetweenSessions = SaveCategoryFilterMenuItem.IsChecked;
		ViewModel.Settings.SavedModCategoryFilter = SaveCategoryFilterMenuItem.IsChecked
			? ViewModel.SelectedModCategory
			: MainWindowViewModel.AllModsCategory;
		ViewModel.SaveSettings();
	}

	private void ShowEmptyCategoriesMenuItem_Click(object sender, RoutedEventArgs e)
	{
		ViewModel.Settings.HideEmptyModCategories = !ShowEmptyCategoriesMenuItem.IsChecked;
		ViewModel.SaveSettings();
	}

	private void AddCustomCategoryMenuItem_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new CategoryNameDialog(color: ViewModel.GetSuggestedCustomCategoryColor(), savedColors: ViewModel.Settings.SavedCategoryColors) { Owner = Window.GetWindow(this) };
		ResourceLocator.SetColorScheme(dialog.Resources, DivinityApp.GetThemeUri(ViewModel.Settings.ColorTheme));
		var result = dialog.ShowDialog();
		SaveCategoryDialogColors(dialog);
		if (result == true && !ViewModel.TryAddCustomModCategory(dialog.CategoryName, dialog.CategoryColor, out var error))
		{
			ShowCategoryMessage(error, "Add Mod Category", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void SaveCategoryDialogColors(CategoryNameDialog dialog)
	{
		var colors = dialog.SavedColors.ToList();
		if ((ViewModel.Settings.SavedCategoryColors ?? new List<string>()).SequenceEqual(colors, StringComparer.OrdinalIgnoreCase)) return;
		ViewModel.Settings.SavedCategoryColors = colors;
		ViewModel.SaveSettings();
	}

	private void ChangeCategoryColorMenuItem_Click(object sender, RoutedEventArgs e)
	{
		var category = ViewModel.SelectedModCategory;
		if (String.IsNullOrWhiteSpace(category) || category.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase)) return;
		var dialog = new CategoryNameDialog(category, ViewModel.GetCurrentCategoryColor(category), false, ViewModel.Settings.SavedCategoryColors) { Owner = Window.GetWindow(this) };
		ResourceLocator.SetColorScheme(dialog.Resources, DivinityApp.GetThemeUri(ViewModel.Settings.ColorTheme));
		var result = dialog.ShowDialog();
		SaveCategoryDialogColors(dialog);
		if (result == true && !ViewModel.TrySetCategoryColor(category, dialog.CategoryColor, out var error))
		{
			ShowCategoryMessage(error, "Change Category Color", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void ModListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (sender is not ModListView listView || e.OriginalSource is not DependencyObject source) return;
		var item = source.FindVisualParent<ListViewItem>();
		var mod = item?.DataContext as DivinityModData;
		var menu = item?.ContextMenu ?? listView.ContextMenu;
		if (menu == null) return;
		foreach (var hiddenEntry in menu.Items.OfType<FrameworkElement>().Where(entry => Equals(entry.Tag, "ReduxHiddenForDivider")).ToList())
		{
			hiddenEntry.Tag = null;
			hiddenEntry.Visibility = Visibility.Visible;
		}

		if (mod == null)
		{
			menu.Items.Clear();
			var point = Mouse.GetPosition(listView);
			var insertIndex = GetVisualInsertionIndex(listView, point);
			var activeList = listView == ActiveModsListView;
			var addHere = new MenuItem
			{
				Header = activeList ? "Insert Separator Here..." : "Insert Separator (Inactive mods do not retain a load order)",
				IsEnabled = activeList,
				ToolTip = activeList ? null : "Inactive mods do not retain a load order.",
				Icon = ReduxIcon.FromResource("Redux.Icon.AddStroke", true)
			};
			addHere.Click += (_, _) => ShowAddVisualDividerDialog(activeList, insertIndex);
			menu.Items.Add(addHere);
			return;
		}

		if (mod.IsVisualDivider)
		{
			foreach (var oldGenerated in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, VisualDividerMenuTag)).ToList()) menu.Items.Remove(oldGenerated);
			foreach (var entry in menu.Items.OfType<FrameworkElement>().ToList())
			{
				entry.Tag = "ReduxHiddenForDivider";
				entry.Visibility = Visibility.Collapsed;
			}
			var edit = new MenuItem
			{
				Header = "Edit Separator...",
				Tag = VisualDividerMenuTag,
				Icon = ReduxIcon.FromResource("Redux.Icon.Create")
			};
			edit.Click += (_, _) => ShowEditVisualDividerDialog(mod);
			var remove = new MenuItem
			{
				Header = "Remove Separator",
				Tag = VisualDividerMenuTag,
				Icon = ReduxIcon.FromResource("Redux.Icon.Trash", foregroundResourceKey: "ReduxErrorBrush")
			};
			remove.Click += (_, _) => ViewModel.RemoveVisualDivider(mod);
			menu.Items.Add(edit);
			menu.Items.Add(remove);
			return;
		}

		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, CategoryAssignmentMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}

		var categoryMenu = new MenuItem
		{
			Header = "Assign Category",
			Tag = CategoryAssignmentMenuTag,
			Icon = ReduxIcon.FromResource("Redux.Icon.Pricetag")
		};
		var automaticItem = new MenuItem
		{
			Header = "Automatic",
			IsCheckable = true,
			IsChecked = !ViewModel.HasModCategoryOverride(mod)
		};
		automaticItem.Click += (_, _) => ViewModel.ToggleModCategoryAssignment(mod, null);
		categoryMenu.Items.Add(automaticItem);
		var noCategoryItem = new MenuItem
		{
			Header = "No Category",
			IsCheckable = true,
			IsChecked = ViewModel.HasNoCategoryAssignment(mod)
		};
		noCategoryItem.Click += (_, _) => ViewModel.ToggleModCategoryAssignment(mod, MainWindowViewModel.NoCategoryAssignment);
		categoryMenu.Items.Add(noCategoryItem);
		categoryMenu.Items.Add(new Separator());

		foreach (var category in ViewModel.GetAssignableModCategories())
		{
			var categoryItem = new MenuItem
			{
				Header = category,
				IsCheckable = true,
				IsChecked = ViewModel.HasModCategoryOverride(mod, category)
			};
			categoryItem.Click += (_, _) => ViewModel.ToggleModCategoryAssignment(mod, category);
			categoryMenu.Items.Add(categoryItem);
		}

		menu.Items.Insert(Math.Min(2, menu.Items.Count), categoryMenu);

		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, SourceLinkMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}

		var sourceMenu = new MenuItem
		{
			Header = "Source Link",
			Tag = SourceLinkMenuTag,
			Icon = ReduxIcon.FromResource("Redux.Icon.LinkStroke", true)
		};
		if (mod.ModioData?.HasMetadata == true)
		{
			sourceMenu.Items.Add(new MenuItem
			{
				Header = "Native mod.io identity detected",
				IsEnabled = false,
				ToolTip = "Redux keeps the stronger mod.io identity for this package.",
				Icon = ReduxIcon.FromResource("Redux.Icon.Information", foregroundResourceKey: "ReduxInfoBrush")
			});
		}
		else
		{
			var hasNexusLink = mod.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START;
			var linkItem = new MenuItem
			{
				Header = hasNexusLink ? "Change Nexus Mods Link..." : "Link to Nexus Mods...",
				Icon = ReduxIcon.FromResource("Redux.Icon.LinkStroke", true)
			};
			linkItem.Click += (_, _) => ShowManualNexusLinkDialog(mod);
			sourceMenu.Items.Add(linkItem);
			if (hasNexusLink)
			{
				sourceMenu.Items.Add(new Separator());
				var unlinkItem = new MenuItem
				{
					Header = "Unlink Nexus Mods",
					Icon = ReduxIcon.FromResource("Redux.Icon.UnlinkStroke", true, "ReduxErrorBrush")
				};
				unlinkItem.Click += (_, _) =>
				{
					var result = ShowCategoryMessage(
						$"Remove the Nexus Mods source link from '{mod.DisplayName}'?\n\nThe installed package and its load-order position will not be changed.",
						"Unlink Nexus Mods", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (result == MessageBoxResult.Yes) ViewModel.UnlinkNexusMod(mod);
				};
				sourceMenu.Items.Add(unlinkItem);
			}
		}
		menu.Items.Insert(Math.Min(3, menu.Items.Count), sourceMenu);

		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, VisualDividerMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}

		var activeModList = listView == ActiveModsListView;
		var dividerMenu = new MenuItem
		{
			Header = activeModList ? "Separator" : "Separator (Inactive mods do not retain a load order)",
			Tag = VisualDividerMenuTag,
			IsEnabled = activeModList,
			ToolTip = activeModList ? null : "Inactive mods do not retain a load order.",
			Icon = ReduxIcon.FromResource("Redux.Icon.ReorderStroke", true)
		};
		var visualIndex = listView.Items.IndexOf(mod);
		var addAbove = new MenuItem
		{
			Header = "Add Separator Above...",
			Icon = ReduxIcon.FromResource("Redux.Icon.ChevronUpStroke", true)
		};
		addAbove.Click += (_, _) => ShowAddVisualDividerDialog(listView == ActiveModsListView, visualIndex);
		var addBelow = new MenuItem
		{
			Header = "Add Separator Below...",
			Icon = ReduxIcon.FromResource("Redux.Icon.ChevronDownStroke", true)
		};
		addBelow.Click += (_, _) => ShowAddVisualDividerDialog(listView == ActiveModsListView, visualIndex + 1);
		dividerMenu.Items.Add(addAbove);
		dividerMenu.Items.Add(addBelow);
		menu.Items.Insert(Math.Min(3, menu.Items.Count), dividerMenu);
	}

	private void ShowManualNexusLinkDialog(DivinityModData mod)
	{
		var currentLink = mod.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
			? mod.NexusModsData.SourcePageUrl
			: null;
		var dialog = new NexusManualLinkDialog(currentLink) { Owner = Window.GetWindow(this) };
		ResourceLocator.SetColorScheme(dialog.Resources, DivinityApp.GetThemeUri(ViewModel.Settings.ColorTheme));
		if (dialog.ShowDialog() != true) return;
		if (!ViewModel.TryManuallyLinkNexusMod(mod, dialog.NexusLink, out var error))
		{
			ShowCategoryMessage(error, "Link Nexus Mods Project", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private int GetVisualInsertionIndex(ListView listView, Point point)
	{
		for (var index = 0; index < listView.Items.Count; index++)
		{
			if (listView.ItemContainerGenerator.ContainerFromIndex(index) is not ListViewItem container) continue;
			var top = container.TranslatePoint(new Point(0, 0), listView).Y;
			if (point.Y < top + container.ActualHeight / 2) return index;
		}
		return listView.Items.Count;
	}

	private void ShowAddVisualDividerDialog(bool activeList, int position)
	{
		if (!activeList) return;
		var dialog = new CategoryNameDialog(color: ViewModel.GetSuggestedCustomCategoryColor(), savedColors: ViewModel.Settings.SavedCategoryColors, visualDividerMode: true)
			{ Owner = Window.GetWindow(this) };
		ResourceLocator.SetColorScheme(dialog.Resources, DivinityApp.GetThemeUri(ViewModel.Settings.ColorTheme));
		if (dialog.ShowDialog() != true) { SaveCategoryDialogColors(dialog); return; }
		SaveCategoryDialogColors(dialog);
		ViewModel.AddVisualDivider(activeList, position, dialog.CategoryName, dialog.CategoryColor);
	}

	private void ShowEditVisualDividerDialog(DivinityModData item)
	{
		var divider = ViewModel.GetVisualDivider(item);
		if (divider == null) return;
		var dialog = new CategoryNameDialog(divider.Title, divider.Color, true, ViewModel.Settings.SavedCategoryColors, true)
			{ Owner = Window.GetWindow(this) };
		ResourceLocator.SetColorScheme(dialog.Resources, DivinityApp.GetThemeUri(ViewModel.Settings.ColorTheme));
		if (dialog.ShowDialog() != true) { SaveCategoryDialogColors(dialog); return; }
		SaveCategoryDialogColors(dialog);
		ViewModel.UpdateVisualDivider(item, dialog.CategoryName, dialog.CategoryColor);
	}

	public ModListView ActiveModsView => ActiveModsListView;
	public ModListView InactiveModsView => InactiveModsListView;
	public ModListView ForceLoadedModsView => ForceLoadedModsListView;

	private bool ListHasFocus(ListView listView)
	{
		if (_focusedList == listView || listView.IsFocused || listView.IsKeyboardFocused)
		{
			return true;
		}
		if (listView.SelectedItem is ListViewItem item && (item.IsFocused || item.IsKeyboardFocused))
		{
			return true;
		}
		return false;
	}

	private bool FocusSelectedItem(ListView lv)
	{
		try
		{
			var listBoxItem = (ListBoxItem)lv.ItemContainerGenerator.ContainerFromItem(lv.SelectedItem);
			if (listBoxItem == null)
			{
				var firstItem = lv.Items.GetItemAt(0);
				if (firstItem != null)
				{
					listBoxItem = (ListBoxItem)lv.ItemContainerGenerator.ContainerFromItem(firstItem);
				}
			}
			if (listBoxItem != null)
			{
				listBoxItem.Focus();
				Keyboard.Focus(listBoxItem);
				return true;
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"{ex}");
		}
		return false;
	}

	private void FocusList(ListView listView)
	{
		if (!FocusSelectedItem(listView))
		{
			listView.Focus();
		}
	}

	private void SetupListView(ListView listView)
	{
		listView.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(ModListView_ButtonClick));
		listView.InputBindings.Add(new KeyBinding(ApplicationCommands.SelectAll, new KeyGesture(Key.A, ModifierKeys.Control)));
		listView.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (_sender, _e) =>
		{
			listView.SelectAll();
		}));

		listView.InputBindings.Add(new KeyBinding(ReactiveCommand.Create(() =>
		{
			listView.SelectedItems.Clear();

		}), new KeyGesture(Key.D, ModifierKeys.Control)));

		listView.ItemContainerStyle = this.FindResource("ListViewItemMouseEvents") as Style;
		listView.GotFocus += (object sender, RoutedEventArgs e) =>
		{
			_focusedList = sender;
		};
		listView.LostFocus += (object sender, RoutedEventArgs e) =>
		{
			if (_focusedList == sender)
			{
				_focusedList = null;
			}
		};
	}

	public void FixActiveModsScrollbar()
	{
		if (ActiveModsListView.FindVisualChildren<ScrollViewer>().FirstOrDefault() is ScrollViewer sv)
		{
			sv.ScrollToHorizontalOffset(0d);
		}
	}

	public void UpdateViewSelection(IEnumerable<ISelectable> dataList, ListView listView = null)
	{
		if (dataList != null)
		{
			if (listView == null)
			{
				if (dataList == ViewModel.ActiveMods)
				{
					listView = ActiveModsListView;
				}
				else if (dataList == ViewModel.InactiveMods)
				{
					listView = InactiveModsListView;
				}
				else if (dataList == ViewModel.ForceLoadedMods)
				{
					listView = ForceLoadedModsListView;
				}
			}

			if (listView != null && dataList.Count() > 0)
			{
				IInputElement focusedItem = FocusManager.GetFocusedElement(listView);
				foreach (var mod in dataList)
				{
					var listItem = (ListViewItem)listView.ItemContainerGenerator.ContainerFromItem(mod);
					if (listItem != null)
					{
						if (mod.Visibility == Visibility.Visible)
						{
							listItem.IsSelected = mod.IsSelected;
							if (listView.IsFocused && focusedItem == null && mod.IsSelected)
							{
								focusedItem = listItem;
								FocusManager.SetFocusedElement(listView, focusedItem);
							}
						}
						else
						{
							listItem.IsSelected = false;
						}
					}
				}
			}
		}
	}

	public void DeselectAll()
	{
		this.ActiveModsListView.ClearSelectedItems();
		this.InactiveModsListView.ClearSelectedItems();
		this.ForceLoadedModsListView.ClearSelectedItems();
	}

	public void SelectMods(IEnumerable<DivinityModData> mods)
	{
		if (mods != null)
		{
			foreach (var mod in mods)
			{
				ModListView listView = null;
				if (mod.IsForceLoaded && !mod.IsForceLoadedMergedMod && !mod.ForceAllowInLoadOrder)
				{
					listView = ForceLoadedModsListView;
				}
				else if (mod.IsActive)
				{
					listView = ActiveModsListView;
				}
				else
				{
					listView = InactiveModsListView;
				}
				if (listView.ItemContainerGenerator.ContainerFromItem(mod) is ListViewItem listItem)
				{
					listItem.IsSelected = mod.Visibility == Visibility.Visible;
				}
			}
		}
	}

	private void UpdateIsSelected(SelectionChangedEventArgs e, IEnumerable<DivinityModData> list)
	{
		if (e != null && list != null)
		{
			var targetUUIDs = list.Select(x => x.UUID).ToHashSet();

			if (e.RemovedItems != null && e.RemovedItems.Count > 0)
			{
				foreach (var removedItem in e.RemovedItems.Cast<DivinityModData>())
				{
					if (targetUUIDs.Contains(removedItem.UUID))
					{
						removedItem.IsSelected = false;
					}
				}
			}

			if (e.AddedItems != null && e.AddedItems.Count > 0)
			{
				foreach (var addedItem in e.AddedItems.Cast<DivinityModData>())
				{
					addedItem.IsSelected = true;
				}
			}
		}
	}

	private void KeepSelectionInSingleList(ModListView selectedList, SelectionChangedEventArgs e)
	{
		// Ctrl/Shift multi-selection remains available inside the active list. Once a
		// user starts selecting in another panel, clear the old panel so Redux has one
		// visually unambiguous selection context.
		if (e?.AddedItems == null || e.AddedItems.Count == 0) return;
		if (!ReferenceEquals(selectedList, ActiveModsListView)) ActiveModsListView.ClearSelectedItems();
		if (!ReferenceEquals(selectedList, InactiveModsListView)) InactiveModsListView.ClearSelectedItems();
		if (!ReferenceEquals(selectedList, ForceLoadedModsListView)) ForceLoadedModsListView.ClearSelectedItems();
	}

	private void ModListView_ButtonClick(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is ButtonBase { Tag: "ReduxDividerToggle", DataContext: DivinityModData item } && item.IsVisualDivider)
		{
			ViewModel.ToggleVisualDividerCollapsed(item);
			e.Handled = true;
		}
	}

	private DivinityModData GetSelectedModForDetails()
	{
		return ActiveModsListView.SelectedItems.OfType<DivinityModData>().LastOrDefault(item => !item.IsVisualDivider)
			?? InactiveModsListView.SelectedItems.OfType<DivinityModData>().LastOrDefault(item => !item.IsVisualDivider)
			?? ForceLoadedModsListView.SelectedItems.OfType<DivinityModData>().LastOrDefault();
	}

	private void UpdateModDetailsSelection(SelectionChangedEventArgs e)
	{
		var detailsWereVisible = ModDetailsPanel.Visibility == Visibility.Visible;
		var selectedMod = e?.AddedItems?.OfType<DivinityModData>().LastOrDefault(item => !item.IsVisualDivider)
			?? GetSelectedModForDetails();
		if (e?.AddedItems?.Count > 0 && selectedMod != null)
		{
			ViewModel.MarkModSeen(selectedMod);
		}

		if (selectedMod == null)
		{
			RememberExpandedModDetailsHeight();
		}

		ModDetailsContent.Content = selectedMod;
		ModDetailsPanel.Visibility = selectedMod != null ? Visibility.Visible : Visibility.Collapsed;

		// Changing the selected mod should replace the drawer content without
		// rebuilding its row. Reapplying the row here could turn a user-sized
		// drawer into the full available height after the main window was resized.
		if (detailsWereVisible != (selectedMod != null))
		{
			UpdateModDetailsLayout(selectedMod != null);
		}
	}

	private void RememberExpandedModDetailsHeight()
	{
		if (ModDetailsRow.ActualHeight >= MinimumExpandedModDetailsRowHeight)
		{
			_lastExpandedModDetailsRowHeight = ModDetailsRow.ActualHeight;
		}
	}

	private void UpdateModDetailsLayout(bool hasSelectedMod)
	{
		if (!hasSelectedMod)
		{
			ModDetailsGridSplitter.Visibility = Visibility.Collapsed;
			ModDetailsSplitterRow.Height = new GridLength(0);
			ModDetailsRow.MinHeight = 0;
			ModDetailsRow.Height = new GridLength(0);
			return;
		}

		if (ModDetailsToggleButton.IsChecked == false)
		{
			ModDetailsGridSplitter.Visibility = Visibility.Collapsed;
			ModDetailsSplitterRow.Height = new GridLength(0);
			ModDetailsRow.MinHeight = CollapsedModDetailsRowHeight;
			ModDetailsRow.Height = new GridLength(CollapsedModDetailsRowHeight);
			return;
		}

		ModDetailsGridSplitter.Visibility = Visibility.Visible;
		ModDetailsSplitterRow.Height = new GridLength(ModDetailsSplitterHeight);
		ModDetailsRow.MinHeight = MinimumExpandedModDetailsRowHeight;
		ModDetailsRow.Height = new GridLength(Math.Max(MinimumExpandedModDetailsRowHeight, _lastExpandedModDetailsRowHeight));
	}

	private void ModDetailsToggleButton_Checked(object sender, RoutedEventArgs e)
	{
		UpdateModDetailsLayout(ModDetailsPanel.Visibility == Visibility.Visible);
	}

	private void ModDetailsToggleButton_Unchecked(object sender, RoutedEventArgs e)
	{
		RememberExpandedModDetailsHeight();
		UpdateModDetailsLayout(ModDetailsPanel.Visibility == Visibility.Visible);
	}

	private void ModDetailsGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		Dispatcher.BeginInvoke(new Action(RememberExpandedModDetailsHeight));
	}

	private void RememberExpandedOverrideModsHeight()
	{
		if (ActiveModsListForcedModsRow.ActualHeight >= MinimumExpandedOverrideModsRowHeight)
			_lastExpandedOverrideModsRowHeight = ActiveModsListForcedModsRow.ActualHeight;
	}

	private void OverrideModsGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		Dispatcher.BeginInvoke(new Action(RememberExpandedOverrideModsHeight));
	}

	private void UpdateCategoriesLayout(bool isExpanded)
	{
		if (!isExpanded)
		{
			if (CategoriesColumn.ActualWidth >= _minimumExpandedCategoriesWidth)
				_lastExpandedCategoriesWidth = CategoriesColumn.ActualWidth;

			CategoriesGridSplitter.IsEnabled = false;
			CategoriesColumn.MinWidth = CollapsedCategoriesWidth;
			CategoriesColumn.MaxWidth = CollapsedCategoriesWidth;
			CategoriesColumn.Width = new GridLength(CollapsedCategoriesWidth);
			return;
		}

		CategoriesColumn.MaxWidth = Double.PositiveInfinity;
		CategoriesColumn.MinWidth = _minimumExpandedCategoriesWidth;
		CategoriesColumn.Width = new GridLength(Math.Max(_minimumExpandedCategoriesWidth, _lastExpandedCategoriesWidth));
		CategoriesGridSplitter.IsEnabled = true;
	}

	/// <summary>
	/// Recomputes the expanded category panel's minimum width from the longest currently
	/// visible category label, so the floor tracks whatever categories are actually shown
	/// (filtering, renaming, enabling/disabling) instead of a permanently fixed value.
	/// Chrome constants below are read directly from the panel's own XAML: the color dot
	/// and its margin, the count badge and its margin, ModCategoryListItemStyle's padding
	/// and border, CategoryListBox's margin, and the panel border/margin/inner-grid margin.
	/// </summary>
	private void UpdateMinimumExpandedCategoriesWidth()
	{
		const double dotAndMargin = 12d + 7d;
		const double badgeAndMargin = 8d + 28d;
		const double itemChrome = (10d * 2) + (1d * 2);
		const double listBoxMargin = 6d * 2;
		const double panelChrome = (1d * 2) + (10d + 5d) + (10d * 2);
		const double fixedChrome = dotAndMargin + badgeAndMargin + itemChrome + listBoxMargin + panelChrome;
		const double measurementBuffer = 4d;

		double longestLabelWidth = 0d;
		var probe = new TextBlock
		{
			FontFamily = CategoryListBox.FontFamily,
			FontSize = CategoryListBox.FontSize,
			FontWeight = CategoryListBox.FontWeight,
			FontStyle = CategoryListBox.FontStyle
		};

		foreach (var entry in CategoryListBox.Items)
		{
			if (entry is ModCategoryFilterItem category && !String.IsNullOrEmpty(category.Name))
			{
				probe.Text = category.Name;
				probe.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
				if (probe.DesiredSize.Width > longestLabelWidth)
				{
					longestLabelWidth = probe.DesiredSize.Width;
				}
			}
		}

		_minimumExpandedCategoriesWidth = fixedChrome + longestLabelWidth + measurementBuffer;

		if (ViewModel != null && ViewModel.IsCategoriesExpanded)
		{
			CategoriesColumn.MinWidth = _minimumExpandedCategoriesWidth;
			if (CategoriesColumn.ActualWidth < _minimumExpandedCategoriesWidth)
			{
				CategoriesColumn.Width = new GridLength(_minimumExpandedCategoriesWidth);
			}
		}
	}

	private IDisposable updatingActiveViewSelection;
	private IDisposable updatingInactiveViewSelection;
	private IDisposable updatingForcedViewSelection;

	private void ActiveModListView_ItemContainerStatusChanged(EventArgs e)
	{
		if (ActiveModsListView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
		{
			if (updatingActiveViewSelection == null)
			{
				updatingActiveViewSelection = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(25), () =>
				{
					UpdateViewSelection(ViewModel.ActiveMods, ActiveModsListView);
					updatingActiveViewSelection.Dispose();
					updatingActiveViewSelection = null;
				});
			}
		}
	}

	private void InactiveModListView_ItemContainerStatusChanged(EventArgs e)
	{
		if (InactiveModsListView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
		{
			if (updatingInactiveViewSelection == null)
			{
				updatingInactiveViewSelection = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(25), () =>
				{
					UpdateViewSelection(ViewModel.InactiveMods, InactiveModsListView);
					updatingInactiveViewSelection.Dispose();
					updatingInactiveViewSelection = null;
				});
			}

		}
	}

	private void ForceLoadedModsListView_ItemContainerStatusChanged(EventArgs e)
	{
		if (ForceLoadedModsListView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
		{
			if (updatingForcedViewSelection == null)
			{
				updatingForcedViewSelection = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(25), () =>
				{
					UpdateViewSelection(ViewModel.ForceLoadedMods, ForceLoadedModsListView);
					updatingForcedViewSelection.Dispose();
					updatingForcedViewSelection = null;
				});
			}

		}
	}

	private IDisposable _updateScroll;

	private void MoveSelectedMods()
	{
		if (ListHasFocus(ActiveModsListView))
		{
			var selectedMods = ViewModel.ActiveMods.Where(x => x.IsSelected).ToList();

			if (selectedMods.Count <= 0) return;

			var selectedMod = selectedMods.FirstOrDefault();
			var nextSelectedIndex = ViewModel.ActiveMods.IndexOf(selectedMod);

			var scrollTargetIndex = InactiveModsListView.SelectedIndex;
			var dropInfo = new ManualDropInfo(selectedMods, InactiveModsListView.SelectedIndex, InactiveModsListView, ViewModel.InactiveMods, ViewModel.ActiveMods);
			InactiveModsListView.UnselectAll();
			ViewModel.DropHandler.Drop(dropInfo);
			string countSuffix = selectedMods.Count > 1 ? "mods" : "mod";
			string text = $"Moved {selectedMods.Count} {countSuffix} to the inactive mods list.";
			if (Services.ScreenReader.IsScreenReaderActive()) Services.ScreenReader.Speak(text);
			ViewModel.ShowAlert(text, AlertType.Info, 10);
			ViewModel.CanMoveSelectedMods = false;

			if (ViewModel.Settings.ShiftListFocusOnSwap)
			{
				InactiveModsListView.Focus();
			}

			_updateScroll?.Dispose();

			_updateScroll = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(250), _ =>
			{
				//InactiveModsListView.UpdateLayout();
				if (scrollTargetIndex <= 0)
				{
					ScrollToTop(InactiveModsListView);
				}
				else if (scrollTargetIndex >= InactiveModsListView.Items.Count)
				{
					ScrollToBottom(InactiveModsListView);
				}
				else
				{
					ScrollToMod(InactiveModsListView, selectedMod);
					//FocusMod(InactiveModsListView, selectedMod);
				}

				if (nextSelectedIndex >= ViewModel.ActiveMods.Count)
				{
					nextSelectedIndex = ViewModel.ActiveMods.Count - 1;
				}

				ActiveModsListView.SelectedIndex = nextSelectedIndex;
				//FocusMod(ActiveModsListView, ActiveModsListView.SelectedItem);
			});
		}
		else if (ListHasFocus(InactiveModsListView))
		{
			var selectedMods = ViewModel.InactiveMods.Where(x => x.IsSelected).ToList();

			if (selectedMods.Count <= 0) return;

			var selectedMod = selectedMods.FirstOrDefault();
			var nextSelectedIndex = ViewModel.InactiveMods.IndexOf(selectedMod);

			var scrollTargetIndex = ActiveModsListView.SelectedIndex;
			var dropInfo = new ManualDropInfo(selectedMods, ActiveModsListView.SelectedIndex, ActiveModsListView, ViewModel.ActiveMods, ViewModel.InactiveMods);
			ActiveModsListView.UnselectAll();
			ViewModel.DropHandler.Drop(dropInfo);

			string countSuffix = selectedMods.Count > 1 ? "mods" : "mod";
			string text = $"Moved {selectedMods.Count} {countSuffix} to the active mods list.";
			if (Services.ScreenReader.IsScreenReaderActive()) Services.ScreenReader.Speak(text);
			ViewModel.ShowAlert(text, AlertType.Info, 10);
			ViewModel.CanMoveSelectedMods = false;

			if (ViewModel.Settings.ShiftListFocusOnSwap)
			{
				ActiveModsListView.Focus();
			}

			_updateScroll?.Dispose();

			_updateScroll = RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(250), _ =>
			{
				//ActiveModsListView.UpdateLayout();
				if (scrollTargetIndex <= 0)
				{
					ScrollToTop(ActiveModsListView);
				}
				else if (scrollTargetIndex >= ActiveModsListView.Items.Count)
				{
					ScrollToBottom(ActiveModsListView);
				}
				else
				{
					ScrollToMod(ActiveModsListView, selectedMod);
					//FocusMod(ActiveModsListView, selectedMod);
				}

				if (nextSelectedIndex >= ViewModel.InactiveMods.Count)
				{
					nextSelectedIndex = ViewModel.InactiveMods.Count - 1;
				}

				InactiveModsListView.SelectedIndex = nextSelectedIndex;
				//FocusMod(InactiveModsListView, InactiveModsListView.SelectedItem);
			});
		}
	}

	public void FocusInitialActiveSelected()
	{
		if (ViewModel.ActiveSelected <= 0)
		{
			ActiveModsListView.SelectedIndex = 0;
		}
		try
		{
			ListViewItem item = (ListViewItem)ActiveModsListView.ItemContainerGenerator.ContainerFromItem(ActiveModsListView.SelectedItem);
			if (item != null)
			{
				Keyboard.Focus(item);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error focusing selected item:{ex}");
		}
	}

	public bool FocusMod(ModListView modListView, object mod)
	{
		if (modListView.ItemContainerGenerator.ContainerFromItem(mod) is ListViewItem item)
		{
			FocusManager.SetFocusedElement(modListView, item);
			//item.BringIntoView();
			return true;
		}
		return false;
	}

	public void ScrollToMod(ModListView modListView, DivinityModData mod)
	{
		var index = modListView.Items.IndexOf(mod);
		if (index > -1)
		{
			modListView.UpdateLayout();
			modListView.ScrollIntoView(modListView.Items[index]);
		}
	}

	public void ScrollToTop(ModListView modListView)
	{
		if (modListView.GetVisualDescendent<ScrollViewer>() is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToTop();
		}
	}

	public void ScrollToBottom(ModListView modListView)
	{
		if (modListView.GetVisualDescendent<ScrollViewer>() is ScrollViewer scrollViewer)
		{
			scrollViewer.ScrollToBottom();
		}
	}

	public HorizontalModLayout()
	{
		InitializeComponent();
		CaptureModListColumnWidths();

		ModDetailsToggleButton.Checked += ModDetailsToggleButton_Checked;
		ModDetailsToggleButton.Unchecked += ModDetailsToggleButton_Unchecked;
		ModDetailsGridSplitter.DragCompleted += ModDetailsGridSplitter_DragCompleted;
		ActiveModListViewGridSplitter.DragCompleted += OverrideModsGridSplitter_DragCompleted;

		SetupListView(ActiveModsListView);
		SetupListView(InactiveModsListView);

		bool setInitialFocus = true;

		this.WhenActivated(d =>
		{
			if (ViewModel != null)
			{
				d(this.ViewModel.WhenAnyValue(x => x.IsCategoriesExpanded)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(UpdateCategoriesLayout));
				d(Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
					h => ((INotifyCollectionChanged)CategoryListBox.Items).CollectionChanged += h,
					h => ((INotifyCollectionChanged)CategoryListBox.Items).CollectionChanged -= h)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(_ => UpdateMinimumExpandedCategoriesWidth()));
				UpdateMinimumExpandedCategoriesWidth();
				d(this.Events().KeyUp.Select(e => e.Key != Key.System ? e.Key : e.SystemKey).Subscribe(ViewModel.OnKeyUp));
				d(this.Events().KeyDown.Select(e => e.Key != Key.System ? e.Key : e.SystemKey).Subscribe(key =>
				{
					ViewModel.OnKeyDown(key);
					HorizontalModLayout_KeyDown(key);
				}));
				d(this.Events().LostFocus.Subscribe((e) => ViewModel.CanMoveSelectedMods = true));
				d(this.Events().Loaded.ObserveOn(RxApp.MainThreadScheduler).Subscribe((e) =>
				{
					if (setInitialFocus)
					{
						this.ActiveModsListView.Focus();
						setInitialFocus = false;
					}
				}));

				d(this.ActiveModsListView.ItemContainerGenerator.Events().StatusChanged.ObserveOn(RxApp.MainThreadScheduler).Subscribe(ActiveModListView_ItemContainerStatusChanged));
				d(this.InactiveModsListView.ItemContainerGenerator.Events().StatusChanged.ObserveOn(RxApp.MainThreadScheduler).Subscribe(InactiveModListView_ItemContainerStatusChanged));
				d(this.ForceLoadedModsListView.ItemContainerGenerator.Events().StatusChanged.ObserveOn(RxApp.MainThreadScheduler).Subscribe(ForceLoadedModsListView_ItemContainerStatusChanged));

				d(Observable.FromEventPattern<SelectionChangedEventArgs>(ActiveModsListView, "SelectionChanged")
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe((e) =>
				{
					KeepSelectionInSingleList(ActiveModsListView, e.EventArgs);
					UpdateIsSelected(e.EventArgs, ViewModel.ActiveMods);
					UpdateModDetailsSelection(e.EventArgs);
				}));

				d(Observable.FromEventPattern<SelectionChangedEventArgs>(InactiveModsListView, "SelectionChanged")
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe((e) =>
				{
					KeepSelectionInSingleList(InactiveModsListView, e.EventArgs);
					UpdateIsSelected(e.EventArgs, ViewModel.InactiveMods);
					UpdateModDetailsSelection(e.EventArgs);
				}));

				d(Observable.FromEventPattern<SelectionChangedEventArgs>(ForceLoadedModsListView, "SelectionChanged")
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe((e) =>
				{
					KeepSelectionInSingleList(ForceLoadedModsListView, e.EventArgs);
					UpdateIsSelected(e.EventArgs, ViewModel.ForceLoadedMods);
					UpdateModDetailsSelection(e.EventArgs);
				}));

				d(this.ViewModel.WhenAnyValue(x => x.OrderJustLoaded).ObserveOn(RxApp.MainThreadScheduler).Subscribe((b) =>
				{
					if (b)
					{
						this.AutoSizeNameColumn_ActiveMods();
						this.AutoSizeNameColumn_InactiveMods();
					}
				}));

				ViewModel.Layout = this;
				ApplyModListColumnVisibility();

				d(this.OneWayBind(ViewModel, vm => vm.DisplayActiveMods, v => v.ActiveModsListView.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.DisplayInactiveMods, v => v.InactiveModsListView.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.ForceLoadedMods, v => v.ForceLoadedModsListView.ItemsSource));

				d(this.OneWayBind(ViewModel, vm => vm.HasForceLoadedMods, v => v.AlwaysLoadedSectionGrid.Visibility, BoolToVisibilityConverter.FromBool));
				d(ViewModel.WhenAnyValue(x => x.HasForceLoadedMods, x => x.IsAlwaysLoadedExpanded)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(state =>
					{
						var showContents = state.Item1 && state.Item2;
						ForceLoadedModsListView.Visibility = BoolToVisibilityConverter.FromBool(showContents);
						ActiveModListViewGridSplitter.Visibility = BoolToVisibilityConverter.FromBool(showContents);
					}));

				d(this.Bind(ViewModel, vm => vm.ActiveModFilterText, v => v.ActiveModsFilterTextBox.Text));
				d(this.Bind(ViewModel, vm => vm.InactiveModFilterText, v => v.InactiveModsFilterTextBox.Text));

				d(this.OneWayBind(ViewModel, vm => vm.ActiveModsFilterResultText, v => v.ActiveModsFilterResultText.Text));
				d(this.OneWayBind(ViewModel, vm => vm.InactiveModsFilterResultText, v => v.InactiveModsFilterResultText.Text));
				d(this.OneWayBind(ViewModel, vm => vm.TotalActiveModsHidden, v => v.ActiveModsFilterResultText.Visibility, IntToVisibilityConverter.FromInt));
				d(this.OneWayBind(ViewModel, vm => vm.TotalInactiveModsHidden, v => v.InactiveModsFilterResultText.Visibility, IntToVisibilityConverter.FromInt));

				d(this.OneWayBind(ViewModel, vm => vm.ActiveSelectedText, v => v.ActiveSelectedText.Text));
				d(this.OneWayBind(ViewModel, vm => vm.ActiveSelected, v => v.ActiveSelectedText.Visibility, IntToVisibilityConverter.FromInt));
				d(this.OneWayBind(ViewModel, vm => vm.InactiveSelectedText, v => v.InactiveSelectedText.Text));
				d(this.OneWayBind(ViewModel, vm => vm.InactiveSelected, v => v.InactiveSelectedText.Visibility, IntToVisibilityConverter.FromInt));

				var gridLengthConverter = new GridLengthConverter();
				var zeroHeight = (GridLength)gridLengthConverter.ConvertFrom(0);
				var activeModsFullHeight = (GridLength)gridLengthConverter.ConvertFrom("1*");

				d(ViewModel.WhenAnyValue(x => x.HasForceLoadedMods, x => x.IsAlwaysLoadedExpanded)
					.ObserveOn(RxApp.MainThreadScheduler).Subscribe((state) =>
				{
					var hasAlwaysLoadedMods = state.Item1;
					var showAlwaysLoadedContents = hasAlwaysLoadedMods && state.Item2;
					ActiveModsListForcedModsRow.MinHeight = showAlwaysLoadedContents ? MinimumExpandedOverrideModsRowHeight : 0;
					if (!showAlwaysLoadedContents)
						RememberExpandedOverrideModsHeight();
					// Keep the main list flexible and restore the exact user-sized Override
					// Mods height. This also clears fixed heights left by the GridSplitter.
					ActiveModsListRow.Height = activeModsFullHeight;
					if (hasAlwaysLoadedMods)
					{
						ActiveModsListGridRow.Height = showAlwaysLoadedContents ? GridLength.Auto : zeroHeight;
						ActiveModsListForcedModsRow.Height = showAlwaysLoadedContents
							? new GridLength(Math.Max(MinimumExpandedOverrideModsRowHeight, _lastExpandedOverrideModsRowHeight))
							: GridLength.Auto;
						ActiveModListViewGridSplitter.IsEnabled = showAlwaysLoadedContents;
					}
					else
					{
						ActiveModsListGridRow.Height = zeroHeight;
						ActiveModsListForcedModsRow.Height = zeroHeight;
						ActiveModListViewGridSplitter.IsEnabled = false;
					}
				}));

				ViewModel.Keys.MoveFocusLeft.AddAction(() =>
				{
					DivinityApp.IsKeyboardNavigating = true;
					this.ActiveModsListView.Focus();

					if (ViewModel != null)
					{
						if (ViewModel.ActiveSelected <= 0)
						{
							ActiveModsListView.SelectedIndex = 0;
						}
					}

					//InactiveModsListView.UnselectAll();
					FocusList(ActiveModsListView);
				});

				ViewModel.Keys.MoveFocusRight.AddAction(() =>
				{
					DivinityApp.IsKeyboardNavigating = true;
					InactiveModsListView.Focus();
					if (ViewModel != null)
					{
						if (ViewModel.ActiveSelected <= 0)
						{
							InactiveModsListView.SelectedIndex = 0;
						}
					}
					//ActiveModsListView.UnselectAll();
					FocusList(InactiveModsListView);
				});


				ViewModel.Keys.SwapListFocus.AddAction(() =>
				{
					if (ListHasFocus(InactiveModsListView))
					{
						DivinityApp.IsKeyboardNavigating = true;
						FocusList(ActiveModsListView);
					}
					else if (ListHasFocus(ActiveModsListView))
					{
						DivinityApp.IsKeyboardNavigating = true;
						FocusList(InactiveModsListView);
					}
				});

				//InactiveModsListView.InputBindings.Add(new InputBinding(ViewModel.MoveLeftCommand, new KeyGesture(Key.Left)));
				ViewModel.Keys.ToggleFilterFocus.AddAction(() =>
				{
					if (ListHasFocus(ActiveModsListView))
					{
						if (!this.ActiveModsFilterTextBox.IsFocused)
						{
							this.ActiveModsFilterTextBox.Focus();
						}
						else
						{
							FocusSelectedItem(ActiveModsListView);
						}
					}
					else
					{
						if (!this.InactiveModsFilterTextBox.IsFocused)
						{
							this.InactiveModsFilterTextBox.Focus();
						}
						else
						{
							FocusSelectedItem(InactiveModsListView);
						}
					}
				});

				//ActiveModsListView.InputBindings.Add(new InputBinding(ViewModel.MoveRightCommand, new KeyGesture(Key.Right)));

				d(ViewModel.WhenAnyValue(x => x.ActiveSelected).Subscribe((c) =>
				{
					if (c > 1 && DivinityApp.IsScreenReaderActive())
					{
						var peer = UIElementAutomationPeer.FromElement(this.ActiveSelectedText);
						if (peer == null)
						{
							peer = UIElementAutomationPeer.CreatePeerForElement(this.ActiveSelectedText);
						}
						peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
					}
				}));

				d(ViewModel.WhenAnyValue(x => x.InactiveSelected).Subscribe((c) =>
				{
					if (c > 1 && DivinityApp.IsScreenReaderActive())
					{
						var peer = UIElementAutomationPeer.FromElement(this.InactiveSelectedText);
						if (peer == null)
						{
							peer = UIElementAutomationPeer.CreatePeerForElement(this.InactiveSelectedText);
						}
						peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
					}
				}));
			}
			//BindingHelper.CreateCommandBinding(ViewModel.View.EditFocusActiveListMenuItem, "MoveLeftCommand", ViewModel);
		});
	}

	private void HorizontalModLayout_KeyDown(Key key)
	{
		var keyIsDown = key == ViewModel.Keys.Confirm.Key && (ViewModel.Keys.Confirm.Modifiers == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ViewModel.Keys.Confirm.Modifiers));
		if (!keyIsDown && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)))
		{
			if (key == Key.Right && ActiveModsListView.IsKeyboardFocusWithin)
			{
				keyIsDown = true;
			}
			else if (key == Key.Left && InactiveModsListView.IsKeyboardFocusWithin)
			{
				keyIsDown = true;
			}
		}
		if (ViewModel.CanMoveSelectedMods && keyIsDown)
		{
			DivinityApp.IsKeyboardNavigating = true;
			if (ViewModel.ActiveSelected > 0 || ViewModel.InactiveSelected > 0)
			{
				MoveSelectedMods();
			}
		}
	}

	private IEnumerable<GridView> GetModListGridViews()
	{
		if (ActiveModsListView.View is GridView activeView)
		{
			yield return activeView;
		}
		if (ForceLoadedModsListView.View is GridView forceLoadedView)
		{
			yield return forceLoadedView;
		}
		if (InactiveModsListView.View is GridView inactiveView)
		{
			yield return inactiveView;
		}
	}

	private static string GetColumnName(GridViewColumn column)
	{
		return column.Header switch
		{
			TextBlock textBlock => textBlock.Text,
			string header => header,
			_ => String.Empty
		};
	}

	private void CaptureModListColumnWidths()
	{
		foreach (var gridView in GetModListGridViews())
		{
			if (!_modListColumnRegistry.TryGetValue(gridView, out var registry))
			{
				registry = new Dictionary<string, (GridViewColumn Column, int Index)>(StringComparer.OrdinalIgnoreCase);
				_modListColumnRegistry[gridView] = registry;
			}
			for (var index = 0; index < gridView.Columns.Count; index++)
			{
				var column = gridView.Columns[index];
				var columnName = GetColumnName(column);
				if (!String.IsNullOrWhiteSpace(columnName) && !registry.ContainsKey(columnName))
				{
					registry[columnName] = (column, index);
				}
				if (!_visibleModListColumnWidths.ContainsKey(column))
				{
					_visibleModListColumnWidths[column] = column.Width;
				}
			}
		}
	}

	private bool IsModListColumnVisible(string columnName)
	{
		if (ViewModel?.Settings == null)
		{
			return true;
		}

		return columnName switch
		{
			"File Name" => ViewModel.Settings.ShowModListFileNameColumn,
			"Version" => ViewModel.Settings.ShowModListVersionColumn,
			"Author" => ViewModel.Settings.ShowModListAuthorColumn,
			"Last Updated" => ViewModel.Settings.ShowModListLastUpdatedColumn,
			"Last Modified" => ViewModel.Settings.ShowModListLastModifiedColumn,
			"Category" => ViewModel.Settings.ShowModListCategoryColumn,
			"Source" => ViewModel.Settings.ShowModListSourceColumn,
			_ => true
		};
	}

	private void SetModListColumnSetting(string columnName, bool isVisible)
	{
		if (ViewModel?.Settings == null)
		{
			return;
		}

		switch (columnName)
		{
			case "File Name":
				ViewModel.Settings.ShowModListFileNameColumn = isVisible;
				break;
			case "Version":
				ViewModel.Settings.ShowModListVersionColumn = isVisible;
				break;
			case "Author":
				ViewModel.Settings.ShowModListAuthorColumn = isVisible;
				break;
			case "Last Updated":
				ViewModel.Settings.ShowModListLastUpdatedColumn = isVisible;
				break;
			case "Last Modified":
				ViewModel.Settings.ShowModListLastModifiedColumn = isVisible;
				break;
			case "Category":
				ViewModel.Settings.ShowModListCategoryColumn = isVisible;
				break;
			case "Source":
				ViewModel.Settings.ShowModListSourceColumn = isVisible;
				break;
		}
	}

	private static double GetDefaultColumnWidth(string columnName)
	{
		return columnName switch
		{
			"#" => 45,
			"Name" => 240,
			"File Name" => 190,
			"Version" => 90,
			"Last Updated" => 115,
			"Last Modified" => 115,
			"Author" => 130,
			"Category" => 135,
			"Source" => 150,
			_ => 100
		};
	}

	private static double GetFallbackMinimumColumnWidth(string columnName)
	{
		return columnName switch
		{
			"#" => 35,
			"Name" => 100,
			"File Name" => 100,
			"Version" => 60,
			"Last Updated" => 70,
			"Last Modified" => 75,
			"Author" => 70,
			"Category" => 70,
			"Source" => 90,
			_ => 60
		};
	}

	private static double MeasureColumnText(ModListView listView, string text, double? fontSize = null, FontWeight? fontWeight = null)
	{
		if (String.IsNullOrEmpty(text))
		{
			return 0;
		}

		return ElementHelper.MeasureText(listView, text,
			listView.FontFamily,
			listView.FontStyle,
			fontWeight ?? listView.FontWeight,
			listView.FontStretch,
			fontSize ?? listView.FontSize).Width;
	}

	private static int GetModNameIconCount(DivinityModData mod)
	{
		var count = 0;
		if (mod.OsirisStatusVisibility == Visibility.Visible) count++;
		if (mod.ExtenderStatusVisibility == Visibility.Visible) count++;
		if (mod.ToolkitIconVisibility == Visibility.Visible) count++;
		if (mod.HasInvalidUUIDVisibility == Visibility.Visible) count++;
		if (mod.MissingDependencyIconVisibility == Visibility.Visible) count++;
		return count;
	}

	/// <summary>
	/// The smallest width a column may be resized/clamped to: its header title plus the
	/// shared fallback floor. Content length deliberately does not raise this minimum —
	/// long values (e.g. UUID-suffixed .pak file names) render with ellipsis instead of
	/// forcing the column wide. Content-based sizing is reserved for the explicit
	/// "Auto Size Columns" action (GetContentAutoSizeColumnWidth).
	/// </summary>
	private double GetHeaderMinimumColumnWidth(ModListView listView, string columnName)
	{
		var headerWidth = listView != null
			? MeasureColumnText(listView, columnName, fontWeight: FontWeights.SemiBold) + 28
			: 0d;
		return Math.Ceiling(Math.Max(headerWidth, GetFallbackMinimumColumnWidth(columnName)));
	}

	/// <summary>
	/// Caps for the explicit auto-size action so free-text columns with unbounded values
	/// cannot claim excessive width; users can still drag wider manually.
	/// </summary>
	private static double GetMaximumAutoSizeColumnWidth(string columnName)
	{
		return columnName switch
		{
			"File Name" => 280,
			"Name" => 340,
			"Author" => 220,
			_ => Double.PositiveInfinity
		};
	}

	private double GetContentAutoSizeColumnWidth(ModListView listView, string columnName)
	{
		var headerWidth = MeasureColumnText(listView, columnName, fontWeight: FontWeights.SemiBold) + 28;
		var contentWidth = 0d;

		foreach (var mod in listView.Items.OfType<DivinityModData>().Where(item => !item.IsVisualDivider && item.Visibility == Visibility.Visible))
		{
			double candidateWidth;
			switch (columnName)
			{
				case "#":
					candidateWidth = MeasureColumnText(listView, mod.Index.ToString(CultureInfo.CurrentCulture)) + 20;
					break;
				case "Name":
					candidateWidth = MeasureColumnText(listView, mod.DisplayTitle) + 28 + (GetModNameIconCount(mod) * 20);
					break;
				case "File Name":
					candidateWidth = MeasureColumnText(listView, mod.FileName) + 24;
					break;
				case "Version":
					candidateWidth = MeasureColumnText(listView, mod.DisplayVersion) + 24;
					break;
				case "Last Updated":
					candidateWidth = MeasureColumnText(listView, mod.DisplayLastUpdated?.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.CurrentCulture)) + 24;
					break;
				case "Last Modified":
					candidateWidth = MeasureColumnText(listView, mod.LastModified?.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.CurrentCulture)) + 24;
					break;
				case "Author":
					var author = mod.NexusModsInformationVisibility == Visibility.Visible && !String.IsNullOrWhiteSpace(mod.NexusModsData?.Author)
						? mod.NexusModsData.Author
						: mod.Author;
					candidateWidth = MeasureColumnText(listView, author) + 24;
					break;
				case "Category":
					candidateWidth = mod.DisplayCategories?.Sum(category => MeasureColumnText(listView, category.Name, 11, FontWeights.SemiBold) + 19) + 8 ?? 8;
					break;
				case "Source":
					candidateWidth = MeasureColumnText(listView, mod.DisplaySource, 11, FontWeights.SemiBold) + 55;
					if (mod.Metadata.ModioWarningVisibility == Visibility.Visible && ViewModel?.Settings.HideModioSourceWarningIcons == false)
					{
						candidateWidth += 22;
					}
					break;
				default:
					candidateWidth = GetFallbackMinimumColumnWidth(columnName);
					break;
			}

			contentWidth = Math.Max(contentWidth, candidateWidth);
		}

		var autoSizeWidth = Math.Max(headerWidth, Math.Max(contentWidth, GetFallbackMinimumColumnWidth(columnName)));
		return Math.Ceiling(Math.Min(autoSizeWidth, GetMaximumAutoSizeColumnWidth(columnName)));
	}

	private void ClampModListColumnWidth(ModListView listView, GridViewColumn column)
	{
		if (column == null || column.Width <= 0)
		{
			return;
		}

		var columnName = GetColumnName(column);
		var minimumWidth = GetHeaderMinimumColumnWidth(listView, columnName);
		if (column.Width < minimumWidth)
		{
			column.Width = minimumWidth;
		}
	}

	private void EnsureReadableColumnWidths(ModListView listView)
	{
		if (listView?.View is not GridView gridView)
		{
			return;
		}

		foreach (var column in gridView.Columns)
		{
			ClampModListColumnWidth(listView, column);
		}
	}

	private void ListViewColumnHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject source)
		{
			return;
		}

		var header = source as GridViewColumnHeader ?? source.FindVisualParent<GridViewColumnHeader>();
		if (header?.Column == null)
		{
			return;
		}

		var resizedColumn = header.Column;
		var listView = sender as ModListView ?? header.FindVisualParent<ModListView>();
		Dispatcher.BeginInvoke(new Action(() => ClampModListColumnWidth(listView, resizedColumn)), System.Windows.Threading.DispatcherPriority.Background);
	}

	private void SetGridViewColumnVisibility(GridView gridView, string columnName, bool isVisible)
	{
		CaptureModListColumnWidths();
		if (!_modListColumnRegistry.TryGetValue(gridView, out var registry) ||
			!registry.TryGetValue(columnName, out var registeredColumn))
		{
			return;
		}
		var column = registeredColumn.Column;

		if (isVisible)
		{
			if (!gridView.Columns.Contains(column))
			{
				var insertionIndex = registry.Values.Count(item => item.Index < registeredColumn.Index && gridView.Columns.Contains(item.Column));
				gridView.Columns.Insert(Math.Min(insertionIndex, gridView.Columns.Count), column);
			}
			column.Width = _visibleModListColumnWidths.TryGetValue(column, out var storedWidth)
				? storedWidth
				: GetDefaultColumnWidth(columnName);
			ClampModListColumnWidth(null, column);
		}
		else
		{
			if (gridView.Columns.Contains(column))
			{
				_visibleModListColumnWidths[column] = column.Width;
				gridView.Columns.Remove(column);
			}
		}
	}

	private void ApplyModListColumnVisibility()
	{
		CaptureModListColumnWidths();
		foreach (var gridView in GetModListGridViews())
		{
			foreach (var columnName in OptionalModListColumns)
			{
				SetGridViewColumnVisibility(gridView, columnName, IsModListColumnVisible(columnName));
			}
		}
	}

	private static MenuItem CreateFixedColumnMenuItem(string header)
	{
		return new MenuItem
		{
			Header = header,
			IsCheckable = true,
			IsChecked = true,
			IsEnabled = false
		};
	}

	private void ListViewColumnHeader_RightClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is not ModListView listView || e.OriginalSource is not UIElement clickedElement)
		{
			return;
		}

		// The routed event is attached to the ListView, so row and empty-space clicks
		// also reach this handler. Only open the chooser for a real column header.
		var clickedHeader = clickedElement as GridViewColumnHeader
			?? clickedElement.FindVisualParent<GridViewColumnHeader>();
		if (clickedHeader == null)
		{
			return;
		}

		var menu = new ContextMenu
		{
			Placement = PlacementMode.MousePoint,
			PlacementTarget = listView
		};
		menu.Items.Add(new MenuItem
		{
			Header = "Visible Columns",
			FontWeight = FontWeights.SemiBold,
			IsEnabled = false
		});
		menu.Items.Add(new Separator());

		if (ReferenceEquals(listView, ActiveModsListView))
		{
			menu.Items.Add(CreateFixedColumnMenuItem("#  (load order — always shown)"));
		}
		menu.Items.Add(CreateFixedColumnMenuItem("Name  (always shown)"));

		foreach (var columnName in OptionalModListColumns)
		{
			var item = new MenuItem
			{
				Header = columnName,
				IsCheckable = true,
				IsChecked = IsModListColumnVisible(columnName)
			};
			item.Click += (_, _) =>
			{
				SetModListColumnSetting(columnName, item.IsChecked);
				ApplyModListColumnVisibility();
				ViewModel.QueueSave();
			};
			menu.Items.Add(item);
		}

		menu.Items.Add(new Separator());
		var autoSizeItem = new MenuItem
		{
			Header = "Auto Size Columns",
			Icon = ReduxIcon.FromResource("Redux.Icon.ReorderStroke", true)
		};
		autoSizeItem.Click += (_, _) =>
		{
			if (listView.View is not GridView visibleGridView) return;
			foreach (var column in visibleGridView.Columns)
			{
				column.Width = GetContentAutoSizeColumnWidth(listView, GetColumnName(column));
				_visibleModListColumnWidths[column] = column.Width;
			}
		};
		menu.Items.Add(autoSizeItem);
		var resetItem = new MenuItem
		{
			Header = "Reset Columns",
			Icon = ReduxIcon.FromResource("Redux.Icon.RefreshStroke", true)
		};
		resetItem.Click += (_, _) =>
		{
			foreach (var columnName in OptionalModListColumns)
			{
				SetModListColumnSetting(columnName, true);
			}
			ApplyModListColumnVisibility();
			ViewModel.QueueSave();
		};
		menu.Items.Add(resetItem);

		menu.IsOpen = true;
		e.Handled = true;
	}

	GridViewColumnHeader _lastHeaderClicked = null;
	ListSortDirection _lastDirection = ListSortDirection.Ascending;

	public GridViewColumnHeader LastSortHeader => _lastHeaderClicked;
	public ListSortDirection LastSortDirection => _lastDirection;

	private void ListView_Click(object sender, RoutedEventArgs e)
	{
		GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
		ListSortDirection direction;

		if (headerClicked != null)
		{
			if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
			{
				if (headerClicked != _lastHeaderClicked)
				{
					direction = ListSortDirection.Ascending;
				}
				else
				{
					if (_lastDirection == ListSortDirection.Ascending)
					{
						direction = ListSortDirection.Descending;
					}
					else
					{
						direction = ListSortDirection.Ascending;
					}
				}

				string header = "";

				if (headerClicked.Column.Header is TextBlock textBlock)
				{
					header = textBlock.Text;
				}
				else if (headerClicked.Column.Header is string gridHeader)
				{
					header = gridHeader;
				}

				Sort(header, direction, sender);

				_lastHeaderClicked = headerClicked;
				_lastDirection = direction;
			}
		}
	}

	public void Sort(string sortBy, ListSortDirection direction, object sender)
	{
		var requestedLoadOrder = sortBy == "#";
		if (sortBy == "Version") sortBy = "Version.Version";
		if (sortBy == "Name") sortBy = "DisplayTitle";
		if (sortBy == "File Name") sortBy = "FileName";
		if (sortBy == "Modes") sortBy = "Targets";
		if (sortBy == "Last Updated") sortBy = "DisplayLastUpdated";
		if (sortBy == "Last Modified") sortBy = "LastModified";
		if (sortBy == "Category") sortBy = "DisplayCategory";
		if (sortBy == "Source") sortBy = "DisplaySource";

		try
		{
			ListView lv = sender as ListView;
			ICollectionView dataView =
			  CollectionViewSource.GetDefaultView(lv.ItemsSource);

			dataView.SortDescriptions.Clear();
			if (requestedLoadOrder)
			{
				// The # column represents the real load order. Rebuild the Redux-only
				// separator rows in their saved visual slots instead of sorting those
				// non-mod rows by a synthetic Index value.
				dataView.Filter = null;
				if (lv == ActiveModsListView && ViewModel != null) ViewModel.IsActiveListMetadataSorted = false;
				ViewModel?.RefreshVisualDividers();
				dataView.Refresh();
				return;
			}

			// Separators describe the real load-order view and have no meaningful
			// position in an alphabetical/date/metadata sort. Hide only those
			// Redux visual rows while sorted; the source collection and exported
			// load order are not modified.
			if (lv == ActiveModsListView || lv == InactiveModsListView)
			{
				if (lv == ActiveModsListView && ViewModel != null) ViewModel.IsActiveListMetadataSorted = true;
				foreach (var mod in lv.ItemsSource.OfType<DivinityModData>().Where(item => !item.IsVisualDivider))
					mod.IsHiddenByVisualDivider = false;
				dataView.Filter = item => item is not DivinityModData mod || !mod.IsVisualDivider;
			}
			SortDescription sd = new SortDescription(sortBy, direction);
			dataView.SortDescriptions.Add(sd);
			dataView.Refresh();
		}
		catch (Exception ex)
		{
			DivinityApp.Log("Error sorting mods:");
			DivinityApp.Log(ex.ToString());
		}
	}

	public void RefreshDataView(ListView target)
	{
		var dataView = CollectionViewSource.GetDefaultView(target.ItemsSource);
		if (dataView != null)
		{
			dataView.Refresh();
		}
		if (target is ModListView modListView)
		{
			Dispatcher.BeginInvoke(new Action(() => EnsureReadableColumnWidths(modListView)), System.Windows.Threading.DispatcherPriority.Background);
		}
	}

	private int _FontSizeMeasurePadding = 48;

	public void AutoSizeNameColumn_ActiveMods()
	{
		if (ViewModel == null || ActiveModsListView.UserResizedColumns) return;
		var count = Math.Max(ViewModel.ActiveMods.Count, ViewModel.ForceLoadedMods.Count);
		if (count > 0 && ActiveModsListView.View is GridView gridView && gridView.Columns.Count >= 2)
		{
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(250), () =>
			{
				count = Math.Max(ViewModel.ActiveMods.Count, ViewModel.ForceLoadedMods.Count);
				if (count > 0)
				{
					//var longestName = ViewModel.ActiveMods.OrderByDescending(m => m.Name.Length).FirstOrDefault()?.Name ?? "";
					//var longestOverrideName = ViewModel.ForceLoadedMods.OrderByDescending(m => m.Name.Length).FirstOrDefault()?.Name ?? "";
					var longestName = "";
					var iconPadding = 0;

					foreach(var mod in ViewModel.Mods)
					{
						if(mod.IsActive || mod.IsForceLoaded)
						{
							if(!String.IsNullOrEmpty(mod.Name) && mod.Name.Length > longestName.Length)
							{
								longestName = mod.Name;
							}
							var modIcons = 0;
							if(mod.OsirisStatusVisibility == Visibility.Visible)
							{
								modIcons++;
							}
							if(mod.ExtenderStatusVisibility == Visibility.Visible)
							{
								modIcons++;
							}
							if(mod.ToolkitIconVisibility == Visibility.Visible)
							{
								modIcons++;
							}
							if(mod.HasInvalidUUIDVisibility == Visibility.Visible)
							{
								modIcons++;
							}
							if(mod.MissingDependencyIconVisibility == Visibility.Visible)
							{
								modIcons++;
							}
							if(modIcons > iconPadding)
							{
								iconPadding = modIcons;
							}
						}
					}

					if (iconPadding > 0) iconPadding *= 16;

					if (!String.IsNullOrEmpty(longestName))
					{
						//DivinityApp.LogMessage($"Autosizing active mods grid for name {longestName}");
						var targetWidth = ElementHelper.MeasureText(ActiveModsListView, longestName,
							ActiveModsListView.FontFamily,
							ActiveModsListView.FontStyle,
							ActiveModsListView.FontWeight,
							ActiveModsListView.FontStretch,
							ActiveModsListView.FontSize).Width + _FontSizeMeasurePadding + iconPadding;
						if (Math.Abs(gridView.Columns[1].Width - targetWidth) >= 30)
						{
							ActiveModsListView.Resizing = true;
							gridView.Columns[1].Width = targetWidth;
						}
					}
				}
			});
		}
	}

	public void AutoSizeNameColumn_InactiveMods()
	{
		if (ViewModel == null || InactiveModsListView.UserResizedColumns) return;
		if (ViewModel.InactiveMods.Count > 0 && InactiveModsListView.View is GridView gridView && gridView.Columns.Count >= 2)
		{
			var longestName = "";
			var iconPadding = 0;

			foreach (var mod in ViewModel.InactiveMods)
			{
				if (!String.IsNullOrEmpty(mod.Name) && mod.Name.Length > longestName.Length)
				{
					longestName = mod.Name;
				}
				var modIcons = 0;
				if (mod.OsirisStatusVisibility == Visibility.Visible)
				{
					modIcons++;
				}
				if (mod.ExtenderStatusVisibility == Visibility.Visible)
				{
					modIcons++;
				}
				if (mod.ToolkitIconVisibility == Visibility.Visible)
				{
					modIcons++;
				}
				if (mod.HasInvalidUUIDVisibility == Visibility.Visible)
				{
					modIcons++;
				}
				if (mod.MissingDependencyIconVisibility == Visibility.Visible)
				{
					modIcons++;
				}
				if (modIcons > iconPadding)
				{
					iconPadding = modIcons;
				}
			}

			if (iconPadding > 0) iconPadding *= 16;

			if (!String.IsNullOrEmpty(longestName))
			{
				InactiveModsListView.Resizing = true;
				//DivinityApp.LogMessage($"Autosizing inactive mods grid for name {longestName}");
				gridView.Columns[0].Width = ElementHelper.MeasureText(InactiveModsListView, longestName,
					InactiveModsListView.FontFamily,
					InactiveModsListView.FontStyle,
					InactiveModsListView.FontWeight,
					InactiveModsListView.FontStretch,
					InactiveModsListView.FontSize).Width + _FontSizeMeasurePadding + iconPadding;
			}
		}
	}

	private void ListViewItem_ModifySelection(object sender, MouseButtonEventArgs e)
	{
		//Fix for when virtualization is enabled, and selected entries outside the view don't get deselected
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
		{
			if (sender is ListViewItem listViewitem)
			{
				if (listViewitem.DataContext is DivinityModData modData)
				{
					if (modData.IsActive)
					{
						foreach (var x in ViewModel.ActiveMods)
						{
							if (x != modData && x.IsSelected) x.IsSelected = false;
						}
					}
					else
					{
						foreach (var x in ViewModel.InactiveMods)
						{
							if (x != modData && x.IsSelected) x.IsSelected = false;
						}
					}
				}
			}
		}
	}
}
