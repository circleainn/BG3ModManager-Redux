using DivinityModManager.Controls;
using DivinityModManager.Converters;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;
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
	private const string PrivateNoteMenuTag = "ReduxPrivateNote";
	private const string BulkActionsMenuTag = "ReduxBulkActions";
	private const string BulkHiddenSeparatorTag = "ReduxBulkHiddenSeparator";
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
	private object _focusedList = null;
	private double _lastExpandedModDetailsRowHeight = DefaultModDetailsRowHeight;
	private double _lastExpandedCategoriesWidth = DefaultExpandedCategoriesWidth;
	private double _lastExpandedInactiveModsWidth;
	private double _minimumExpandedCategoriesWidth = MinimumExpandedCategoriesWidth;
	private System.Threading.CancellationTokenSource _categoriesTransition;
	private System.Threading.CancellationTokenSource _inactiveModsTransition;
	private System.Threading.CancellationTokenSource _modDetailsTransition;
	private System.Threading.CancellationTokenSource _overrideModsTransition;
	private System.Threading.CancellationTokenSource _visualDividerTransition;
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
		return ReduxMessageBox.Show(owner, message, caption, buttons, image,
			buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK);
	}

	private void CategoriesContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		ShowEmptyCategoriesMenuItem.IsChecked = !ViewModel.Settings.HideEmptyModCategories;
		SaveCategoryFilterMenuItem.IsChecked = ViewModel.Settings.SaveModCategoryFilterBetweenSessions;
		DisableNewModIndicatorsMenuItem.IsChecked = ViewModel.Settings.DisableNewModCategoryIndicators;
		EditCategoryMenuItem.IsEnabled = !String.IsNullOrWhiteSpace(ViewModel.SelectedModCategory) &&
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
		_draggedCategory = null;
		ClearCategoryDropIndicator();
		if (e.OriginalSource is DependencyObject source && source.FindVisualParent<ListBoxItem>()?.DataContext is ModCategoryFilterItem category)
			ViewModel.MarkModCategorySeen(category.Name);
	}

	private void CategoryListBox_LostMouseCapture(object sender, MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed) return;
		_draggedCategory = null;
		ClearCategoryDropIndicator();
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
		var brush = TryFindResource("ReduxAccentHoverBrush") as Brush ?? System.Windows.Media.Brushes.Gray;
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
		var dialog = new CategoryNameDialog(color: ViewModel.GetSuggestedCustomCategoryColor(),
			savedColors: ViewModel.Settings.SavedCategoryColors,
			useCategoryColorsForSidebarSelection: ViewModel.Settings.UseCategoryColorsForInteractions,
			useCategoryColorsForSidebarText: ViewModel.Settings.UseCategoryColorsForSidebarText,
			showInterfaceIcons: ViewModel.Settings.ShowCategoryIconsInPills) { Owner = Window.GetWindow(this) };
		ReduxThemeService.Apply(dialog.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
		var result = dialog.ShowDialog();
		SaveCategoryDialogColors(dialog);
		if (result == true && !ViewModel.TryAddCustomModCategory(dialog.CategoryName, dialog.CategoryColor,
			dialog.CategoryIconId, dialog.CategoryDescription, out var error))
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

	private void EditCategoryMenuItem_Click(object sender, RoutedEventArgs e)
	{
		var category = ViewModel.SelectedModCategory;
		if (String.IsNullOrWhiteSpace(category) || category.Equals(MainWindowViewModel.AllModsCategory, StringComparison.OrdinalIgnoreCase)) return;
		var dialog = new CategoryNameDialog(category, ViewModel.GetCurrentCategoryColor(category), false, ViewModel.Settings.SavedCategoryColors,
			iconId: ViewModel.GetCurrentCategoryIcon(category),
			canResetToDefault: ViewModel.CanResetCategoryStyle(category),
			description: ViewModel.GetCurrentCategoryDescription(category),
			useCategoryColorsForSidebarSelection: ViewModel.Settings.UseCategoryColorsForInteractions,
			useCategoryColorsForSidebarText: ViewModel.Settings.UseCategoryColorsForSidebarText,
			showInterfaceIcons: ViewModel.Settings.ShowCategoryIconsInPills) { Owner = Window.GetWindow(this) };
		ReduxThemeService.Apply(dialog.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
		var result = dialog.ShowDialog();
		SaveCategoryDialogColors(dialog);
		if (result != true) return;
		if (dialog.ResetToDefaultRequested)
		{
			ViewModel.ResetCategoryStyle(category);
			return;
		}
		if (!ViewModel.TrySetCategoryStyle(category, dialog.CategoryColor, dialog.CategoryIconId,
			dialog.CategoryDescription, out var error))
		{
			ShowCategoryMessage(error, "Edit Category", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void ModListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		if (sender is not ModListView listView || e.OriginalSource is not DependencyObject source) return;
		var item = source.FindVisualParent<ListViewItem>();
		var mod = item?.DataContext as DivinityModData;
		var menu = item?.ContextMenu ?? listView.ContextMenu;
		if (menu == null) return;
		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, BulkActionsMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}
		foreach (var baseItem in menu.Items.OfType<MenuItem>().Where(IsSingleModContextAction))
		{
			baseItem.ClearValue(VisibilityProperty);
		}
		foreach (var hiddenSeparator in menu.Items.OfType<Separator>().Where(entry => Equals(entry.Tag, BulkHiddenSeparatorTag)))
		{
			hiddenSeparator.Tag = null;
			hiddenSeparator.ClearValue(VisibilityProperty);
		}
		foreach (var hiddenEntry in menu.Items.OfType<FrameworkElement>().Where(entry => Equals(entry.Tag, "ReduxHiddenForDivider")).ToList())
		{
			hiddenEntry.Tag = null;
			hiddenEntry.ClearValue(VisibilityProperty);
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
				Icon = ReduxIcon.FromResource("Redux.Icon.Create", true)
			};
			edit.Click += (_, _) => ShowEditVisualDividerDialog(mod);
			var remove = new MenuItem
			{
				Header = "Remove Separator",
				Tag = VisualDividerMenuTag,
				Icon = ReduxIcon.FromResource("Redux.Icon.Trash", true, "ReduxErrorBrush")
			};
			ApplySemanticMenuHover(remove, "ReduxErrorPillBackground", "ReduxErrorBrush");
			remove.Click += (_, _) => ViewModel.RemoveVisualDivider(mod);
			menu.Items.Add(edit);
			menu.Items.Add(remove);
			return;
		}

		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, CategoryAssignmentMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}
		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, PrivateNoteMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}

		var categoryTargets = listView.SelectedItems
			.OfType<DivinityModData>()
			.Where(selected =>
				selected != null
				&& !selected.IsVisualDivider
				&& !String.IsNullOrWhiteSpace(selected.UUID))
			.GroupBy(selected => selected.UUID, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToList();
		if (categoryTargets.Count <= 1
			|| !categoryTargets.Any(selected =>
				String.Equals(selected.UUID, mod.UUID, StringComparison.OrdinalIgnoreCase)))
		{
			categoryTargets = [mod];
		}
		var hasBulkCategoryTargets = categoryTargets.Count > 1;
		var categoryMenu = new MenuItem
		{
			Header = hasBulkCategoryTargets
				? $"Assign Category to {categoryTargets.Count} Mods"
				: "Assign Category",
			Tag = CategoryAssignmentMenuTag,
			Icon = ReduxIcon.FromResource("Redux.Icon.Pricetag", true),
			ToolTip = hasBulkCategoryTargets
				? "Category changes apply to every selected mod."
				: null
		};
		var automaticItem = new MenuItem
		{
			Header = "Automatic",
			IsCheckable = true,
			IsChecked = categoryTargets.All(target => !ViewModel.HasModCategoryOverride(target))
		};
		automaticItem.Click += (_, _) => ViewModel.SetModCategoryAssignments(categoryTargets, null);
		categoryMenu.Items.Add(automaticItem);
		var noCategoryItem = new MenuItem
		{
			Header = "No Category",
			IsCheckable = true,
			IsChecked = categoryTargets.All(ViewModel.HasNoCategoryAssignment),
			Icon = ViewModel.Settings.ShowCategoryIconsInPills
				? CreateCategoryAssignmentIcon(MainWindowViewModel.UncategorizedModsCategory)
				: null
		};
		noCategoryItem.Click += (_, _) => ViewModel.SetModCategoryAssignments(
			categoryTargets,
			MainWindowViewModel.NoCategoryAssignment);
		categoryMenu.Items.Add(noCategoryItem);
		categoryMenu.Items.Add(new Separator());

		foreach (var category in ViewModel.GetAssignableModCategories())
		{
			var allTargetsHaveCategory = categoryTargets.All(target =>
				ViewModel.HasModCategoryOverride(target, category));
			var categoryItem = new MenuItem
			{
				Header = category,
				IsCheckable = true,
				IsChecked = allTargetsHaveCategory,
				Icon = ViewModel.Settings.ShowCategoryIconsInPills ? CreateCategoryAssignmentIcon(category) : null
			};
			if (ColorConverter.ConvertFromString(ViewModel.GetCurrentCategoryColor(category)) is Color categoryColor)
			{
				var labelBrush = new SolidColorBrush(categoryColor);
				if (labelBrush.CanFreeze) labelBrush.Freeze();
				if (DivinityApp.UseCategoryColorsForText)
				{
					categoryItem.Foreground = labelBrush;
				}

				// Same alpha the category pills use for their soft fill, so the row's hover
				// matches the pill that the assignment produces.
				var hoverBrush = new SolidColorBrush(Color.FromArgb(0x4D, categoryColor.R, categoryColor.G, categoryColor.B));
				if (hoverBrush.CanFreeze) hoverBrush.Freeze();
				ReduxMenuItemExtension.SetSemanticHoverBrush(categoryItem, hoverBrush);
				ReduxMenuItemExtension.SetSemanticRailBrush(categoryItem, labelBrush);
				ReduxMenuItemExtension.SetUseSemanticHover(categoryItem, true);
			}
			categoryItem.Click += (_, _) => ViewModel.SetModCategoryAssignments(
				categoryTargets,
				category,
				!allTargetsHaveCategory);
			categoryMenu.Items.Add(categoryItem);
		}

		var privateNoteItem = new MenuItem
		{
			Header = hasBulkCategoryTargets
				? $"Set Note for {categoryTargets.Count} Mods..."
				: mod.HasPrivateNote ? "Edit Note..." : "Add Note...",
			Tag = PrivateNoteMenuTag,
			Icon = ReduxIcon.FromResource("Redux.Icon.ScrollText", true),
			ToolTip = hasBulkCategoryTargets
				? "Saving applies the same note to every selected mod."
				: null
		};
		privateNoteItem.Click += (_, _) => ShowModNoteDialog(categoryTargets);

		if (hasBulkCategoryTargets)
		{
			var bulkTargetsAreActive = listView == ActiveModsListView;
			var bulkActions = new MenuItem
			{
				Header = $"Selected Mods ({categoryTargets.Count})",
				Tag = BulkActionsMenuTag,
				Icon = ReduxIcon.FromResource("Redux.Icon.ListStroke", true)
			};
			if (listView == ActiveModsListView || listView == InactiveModsListView)
			{
				var moveSelected = new MenuItem
				{
					Header = bulkTargetsAreActive ? "Move to Inactive Mods" : "Move to Active Mods",
					Icon = ReduxIcon.FromResource(
						bulkTargetsAreActive ? "Redux.Icon.ArrowForwardStroke" : "Redux.Icon.ArrowBackStroke",
						true)
				};
				moveSelected.Click += (_, _) => MoveSelectedMods(listView);
				bulkActions.Items.Add(moveSelected);
			}
			bulkActions.Items.Add(categoryMenu);
			bulkActions.Items.Add(privateNoteItem);

			if (categoryTargets.Any(target => target.HasPrivateNote))
			{
				var clearNotes = new MenuItem
				{
					Header = "Clear Notes",
					Icon = ReduxIcon.FromResource("Redux.Icon.RemoveCircle", true)
				};
				clearNotes.Click += (_, _) => ClearSelectedModNotes(categoryTargets);
				bulkActions.Items.Add(clearNotes);
			}

			bulkActions.Items.Add(new Separator());
			var deleteSelected = new MenuItem
			{
				Header = $"Delete {categoryTargets.Count} Selected Mods...",
				IsEnabled = categoryTargets.Any(target => target.CanDelete),
				Icon = ReduxIcon.FromResource("Redux.Icon.Trash", true, "ReduxErrorBrush")
			};
			ApplySemanticMenuHover(deleteSelected, "ReduxErrorPillBackground", "ReduxErrorBrush");
			deleteSelected.Click += (_, _) => ViewModel.DeleteSelectedMods(categoryTargets);
			bulkActions.Items.Add(deleteSelected);

			var clearSelection = new MenuItem
			{
				Header = "Clear Selection",
				Icon = ReduxIcon.FromResource("Redux.Icon.CloseCircle", true)
			};
			clearSelection.Click += (_, _) => DeselectAll();
			bulkActions.Items.Add(clearSelection);
			menu.Items.Insert(0, bulkActions);

			foreach (var baseItem in menu.Items.OfType<MenuItem>().Where(IsSingleModContextAction))
			{
				baseItem.Visibility = Visibility.Collapsed;
			}
			var firstBaseSeparator = menu.Items
				.OfType<Separator>()
				.FirstOrDefault(separator => separator.Tag == null);
			if (firstBaseSeparator != null)
			{
				firstBaseSeparator.Tag = BulkHiddenSeparatorTag;
				firstBaseSeparator.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			menu.Items.Insert(Math.Min(2, menu.Items.Count), categoryMenu);
			menu.Items.Insert(Math.Min(3, menu.Items.Count), privateNoteItem);
		}

		foreach (var generatedItem in menu.Items.OfType<MenuItem>().Where(entry => Equals(entry.Tag, SourceLinkMenuTag)).ToList())
		{
			menu.Items.Remove(generatedItem);
		}

		if (ViewModel.Modules.SourceIntegrationsEnabled)
		{
			var sourceMenu = new MenuItem
			{
				Header = hasBulkCategoryTargets ? $"Source Link for {mod.DisplayName}" : "Source Link",
				Tag = SourceLinkMenuTag,
				Icon = ReduxIcon.FromResource("Redux.Icon.LinkStroke", true)
			};
			if (mod.Metadata.SourceType == ModSourceType.MODIO)
			{
				sourceMenu.Items.Add(new MenuItem
				{
					Header = "Native mod.io identity detected",
					IsEnabled = false,
					ToolTip = "Redux keeps the stronger mod.io identity for this package.",
					Icon = ReduxIcon.FromResource("Redux.Icon.Information", true, "ReduxInfoBrush")
				});
			}
			else
			{
				var hasNexusLink = mod.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START;
				var linkItem = new MenuItem
				{
					Header = hasNexusLink ? "Change Nexus Mods Link..." : "Link to Nexus Mods...",
					Icon = ReduxIcon.FromResource("Redux.Icon.LinkStroke", true, "Redux.Pill.Nexus.Border")
				};
				ApplySemanticMenuHover(linkItem, "Redux.Pill.Nexus.Background", "Redux.Pill.Nexus.Border");
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
					ApplySemanticMenuHover(unlinkItem, "ReduxErrorPillBackground", "ReduxErrorBrush");
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
		}

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
			Icon = ReduxIcon.FromResource("Redux.Icon.AddStroke", true)
		};
		var visualIndex = listView.Items.IndexOf(mod);
		var addAbove = new MenuItem
		{
			Header = "Add Separator Above...",
			Icon = ReduxIcon.FromResource("Redux.Icon.AddStroke", true)
		};
		addAbove.Click += (_, _) => ShowAddVisualDividerDialog(listView == ActiveModsListView, visualIndex);
		var addBelow = new MenuItem
		{
			Header = "Add Separator Below...",
			Icon = ReduxIcon.FromResource("Redux.Icon.AddStroke", true)
		};
		addBelow.Click += (_, _) => ShowAddVisualDividerDialog(listView == ActiveModsListView, visualIndex + 1);
		dividerMenu.Items.Add(addAbove);
		dividerMenu.Items.Add(addBelow);
		menu.Items.Insert(Math.Min(3, menu.Items.Count), dividerMenu);
	}

	private static bool IsSingleModContextAction(MenuItem item)
	{
		if (item == null) return false;
		if (ReferenceEquals(item.Command, DivinityApp.Commands.MoveModToActiveCommand)
			|| ReferenceEquals(item.Command, DivinityApp.Commands.MoveModToInactiveCommand)
			|| ReferenceEquals(item.Command, DivinityApp.Commands.DeleteModCommand))
			return true;

		return item.Header is string header
			&& (header == "Move to Active Mods"
				|| header == "Move to Inactive Mods"
				|| header == "Delete Mod...");
	}

	private void ShowModNoteDialog(DivinityModData mod) =>
		ShowModNoteDialog(mod == null ? [] : [mod]);

	private void ShowModNoteDialog(IReadOnlyList<DivinityModData> targetMods)
	{
		var targets = (targetMods ?? [])
			.Where(mod => mod != null && !mod.IsVisualDivider && !String.IsNullOrWhiteSpace(mod.UUID))
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToArray();
		if (targets.Length == 0) return;

		var dialog = new ReduxModNoteWindow(Window.GetWindow(this), targets);
		dialog.ShowDialog();
		if (!dialog.Accepted) return;
		if (!ViewModel.TrySetModPrivateNotes(targets, dialog.Note, out var error))
			ShowCategoryMessage(error, "Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
	}

	private void ClearSelectedModNotes(IReadOnlyList<DivinityModData> targetMods)
	{
		var targets = (targetMods ?? [])
			.Where(mod => mod != null && !mod.IsVisualDivider && mod.HasPrivateNote)
			.GroupBy(mod => mod.UUID, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.ToArray();
		if (targets.Length == 0) return;

		var result = ShowCategoryMessage(
			$"Clear notes from {targets.Length} selected {(targets.Length == 1 ? "mod" : "mods")}?\n\nInstalled packages and load orders will not be changed.",
			"Clear Notes",
			MessageBoxButton.YesNo,
			MessageBoxImage.Question);
		if (result != MessageBoxResult.Yes) return;

		if (!ViewModel.TrySetModPrivateNotes(targets, String.Empty, out var error))
			ShowCategoryMessage(error, "Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
	}

	private void EditPrivateNoteButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: DivinityModData mod })
			ShowModNoteDialog(mod);
	}

	private FrameworkElement CreateCategoryAssignmentIcon(string category)
	{
		var colorValue = ViewModel.GetCurrentCategoryColor(category);
		var brush = ColorConverter.ConvertFromString(colorValue) is Color color
			? new SolidColorBrush(color)
			: TryFindResource("ReduxTextSecondaryBrush") as Brush ?? System.Windows.Media.Brushes.Gray;
		if (brush.CanFreeze) brush.Freeze();

		var iconId = ViewModel.GetCurrentCategoryIcon(category);
		if (!String.IsNullOrWhiteSpace(iconId))
		{
			return new ReduxIcon
			{
				Width = 14,
				Height = 14,
				IconKey = iconId,
				Foreground = brush
			};
		}

		return new Border
		{
			Width = 7,
			Height = 7,
			CornerRadius = new CornerRadius(4),
			Background = brush,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private void ShowManualNexusLinkDialog(DivinityModData mod)
	{
		var currentLink = mod.NexusModsData?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
			? mod.NexusModsData.SourcePageUrl
			: null;
		var dialog = new NexusManualLinkDialog(currentLink) { Owner = Window.GetWindow(this) };
		ReduxThemeService.Apply(dialog.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
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
		var dialog = new CategoryNameDialog(color: ViewModel.GetSuggestedCustomCategoryColor(),
			savedColors: ViewModel.Settings.SavedCategoryColors, visualDividerMode: true,
			useCategoryColorsForHover: ViewModel.Settings.UseCategoryColorsForInteractions)
			{ Owner = Window.GetWindow(this) };
		ReduxThemeService.Apply(dialog.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
		if (dialog.ShowDialog() != true) { SaveCategoryDialogColors(dialog); return; }
		SaveCategoryDialogColors(dialog);
		ViewModel.AddVisualDivider(activeList, position, dialog.CategoryName, dialog.CategoryColor,
			dialog.CategoryIconId, dialog.HideSeparatorLine, dialog.CategoryDescription);
	}

	private void AddActiveSeparatorButton_Click(object sender, RoutedEventArgs e)
	{
		var position = ActiveModsListView.SelectedIndex >= 0
			? ActiveModsListView.SelectedIndex + 1
			: ActiveModsListView.Items.Count;
		ShowAddVisualDividerDialog(true, position);
	}

	private void ShowEditVisualDividerDialog(DivinityModData item)
	{
		var divider = ViewModel.GetVisualDivider(item);
		if (divider == null) return;
		var dialog = new CategoryNameDialog(divider.Title, divider.Color, true,
			ViewModel.Settings.SavedCategoryColors, true, divider.IconId,
			useCategoryColorsForHover: ViewModel.Settings.UseCategoryColorsForInteractions,
			description: divider.Description,
			hideSeparatorLine: divider.HideLine)
			{ Owner = Window.GetWindow(this) };
		ReduxThemeService.Apply(dialog.Resources, ViewModel.Settings.ColorTheme, ReduxThemeService.GetActiveTheme(ViewModel.Settings));
		if (dialog.ShowDialog() != true) { SaveCategoryDialogColors(dialog); return; }
		SaveCategoryDialogColors(dialog);
		ViewModel.UpdateVisualDivider(item, dialog.CategoryName, dialog.CategoryColor,
			dialog.CategoryIconId, dialog.HideSeparatorLine, dialog.CategoryDescription);
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

	public void FocusDiagnosticSnapshot(ModHealthSnapshot snapshot)
	{
		FocusModEntry(snapshot?.Mod);
	}

	private void ApplySemanticMenuHover(MenuItem menuItem, string hoverResourceKey, string railResourceKey)
	{
		if (menuItem == null) return;
		ReduxMenuItemExtension.SetSemanticHoverBrush(menuItem, TryFindResource(hoverResourceKey) as Brush);
		ReduxMenuItemExtension.SetSemanticRailBrush(menuItem, TryFindResource(railResourceKey) as Brush);
		ReduxMenuItemExtension.SetUseSemanticHover(menuItem, true);
	}

	public void FocusModEntry(DivinityModData mod)
	{
		if (mod == null) return;

		var targetList = mod.IsForceLoaded && !mod.IsForceLoadedMergedMod && !mod.ForceAllowInLoadOrder
			? ForceLoadedModsListView
			: mod.IsActive
				? ActiveModsListView
				: InactiveModsListView;

		ViewModel.SelectedModCategory = MainWindowViewModel.AllModsCategory;
		ViewModel.ActiveModFilterText = String.Empty;
		ViewModel.InactiveModFilterText = String.Empty;
		Dispatcher.BeginInvoke(new Action(() =>
		{
			DeselectAll();
			targetList.SelectedItem = mod;
			targetList.ScrollIntoView(mod);
			targetList.Focus();
		}));
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

	private async void ModListView_ButtonClick(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is ButtonBase { Tag: "ReduxDividerToggle", DataContext: DivinityModData item } && item.IsVisualDivider)
		{
			e.Handled = true;
			await AnimateVisualDividerSectionAsync(item);
		}
	}

	private async System.Threading.Tasks.Task AnimateVisualDividerSectionAsync(DivinityModData dividerItem)
	{
		var listView = ActiveModsListView.Items.Contains(dividerItem)
			? ActiveModsListView
			: InactiveModsListView.Items.Contains(dividerItem)
				? InactiveModsListView
				: null;
		if (listView == null)
		{
			ViewModel.ToggleVisualDividerCollapsed(dividerItem);
			return;
		}

		_visualDividerTransition?.Cancel();
		_visualDividerTransition = new System.Threading.CancellationTokenSource();
		var token = _visualDividerTransition.Token;
		var dividerId = dividerItem.VisualDividerId;
		var isExpanding = dividerItem.IsVisualDividerCollapsed;

		DivinityModData GetCurrentDividerItem() =>
			listView.Items.OfType<DivinityModData>()
				.FirstOrDefault(candidate => candidate.IsVisualDivider &&
					String.Equals(candidate.VisualDividerId, dividerId, StringComparison.OrdinalIgnoreCase));

		List<ListViewItem> GetRealizedSectionRows()
		{
			var rows = new List<ListViewItem>();
			var dividerIndex = listView.Items.IndexOf(GetCurrentDividerItem());
			if (dividerIndex < 0) return rows;

			for (var index = dividerIndex + 1; index < listView.Items.Count; index++)
			{
				if (listView.Items[index] is DivinityModData { IsVisualDivider: true }) break;
				if (listView.ItemContainerGenerator.ContainerFromIndex(index) is ListViewItem row)
					rows.Add(row);
			}
			return rows;
		}

		static void ClearAnimatedRows(IEnumerable<ListViewItem> rows)
		{
			foreach (var row in rows)
			{
				row.ClearValue(FrameworkElement.HeightProperty);
				row.ClearValue(FrameworkElement.MinHeightProperty);
				row.ClearValue(FrameworkElement.MarginProperty);
				row.ClearValue(UIElement.OpacityProperty);
				row.ClearValue(UIElement.ClipToBoundsProperty);
			}
		}

		if (!isExpanding)
		{
			var rows = GetRealizedSectionRows();
			var heights = rows.Select(row => Math.Max(1, row.ActualHeight)).ToArray();
			var margins = rows.Select(row => row.Margin).ToArray();
			foreach (var row in rows)
			{
				row.ClipToBounds = true;
				row.MinHeight = 0;
				row.Height = Math.Max(1, row.ActualHeight);
			}
			var startingChevronAngle = dividerItem.VisualDividerChevronAngle;

			var completed = await AnimatePanelValueAsync(0, 1, progress =>
			{
				dividerItem.VisualDividerChevronAngle =
					startingChevronAngle + ((-90d - startingChevronAngle) * progress);
				for (var index = 0; index < rows.Count; index++)
				{
					rows[index].Height = heights[index] * (1 - progress);
					rows[index].Margin = new Thickness(
						margins[index].Left,
						margins[index].Top,
						margins[index].Right,
						margins[index].Bottom * (1 - progress));
					rows[index].Opacity = 1 - progress;
				}
			}, token);
			if (!completed)
			{
				dividerItem.VisualDividerChevronAngle = 0;
				ClearAnimatedRows(rows);
				return;
			}
			dividerItem.VisualDividerChevronAngle = -90;
			ViewModel.ToggleVisualDividerCollapsed(dividerItem);
			// Recycling virtualization reuses these containers for unrelated rows later;
			// without this, a completed (not just a cancelled) collapse leaves Height=0/
			// Opacity=0/ClipToBounds=true permanently set, so a later row silently
			// inheriting one of these containers would render invisible.
			ClearAnimatedRows(rows);
			return;
		}

		ViewModel.ToggleVisualDividerCollapsed(dividerItem);
		listView.UpdateLayout();
		var expandedDividerItem = GetCurrentDividerItem();
		if (expandedDividerItem != null) expandedDividerItem.VisualDividerChevronAngle = -90;
		var expandedRows = GetRealizedSectionRows();
		var expandedHeights = expandedRows.Select(row => Math.Max(1, row.ActualHeight)).ToArray();
		var expandedMargins = expandedRows.Select(row => row.Margin).ToArray();
		foreach (var row in expandedRows)
		{
			row.ClipToBounds = true;
			row.MinHeight = 0;
			row.Height = 0;
			row.Margin = new Thickness(row.Margin.Left, row.Margin.Top, row.Margin.Right, 0);
			row.Opacity = 0;
		}

		var expanded = await AnimatePanelValueAsync(0, 1, progress =>
		{
			if (expandedDividerItem != null)
				expandedDividerItem.VisualDividerChevronAngle = -90d * (1 - progress);
			for (var index = 0; index < expandedRows.Count; index++)
			{
				expandedRows[index].Height = expandedHeights[index] * progress;
				expandedRows[index].Margin = new Thickness(
					expandedMargins[index].Left,
					expandedMargins[index].Top,
					expandedMargins[index].Right,
					expandedMargins[index].Bottom * progress);
				expandedRows[index].Opacity = progress;
			}
		}, token);
		ClearAnimatedRows(expandedRows);
		if (expandedDividerItem != null) expandedDividerItem.VisualDividerChevronAngle = 0;
		if (!expanded) return;
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

	private async void ModDetailsToggleButton_Checked(object sender, RoutedEventArgs e)
	{
		await AnimateModDetailsLayoutAsync(true);
	}

	private async void ModDetailsToggleButton_Unchecked(object sender, RoutedEventArgs e)
	{
		RememberExpandedModDetailsHeight();
		await AnimateModDetailsLayoutAsync(false);
	}

	private async System.Threading.Tasks.Task AnimateModDetailsLayoutAsync(bool isExpanded)
	{
		if (ModDetailsPanel.Visibility != Visibility.Visible || !IsLoaded)
		{
			UpdateModDetailsLayout(ModDetailsPanel.Visibility == Visibility.Visible);
			return;
		}

		_modDetailsTransition?.Cancel();
		_modDetailsTransition = new System.Threading.CancellationTokenSource();
		var token = _modDetailsTransition.Token;
		var startHeight = Math.Max(CollapsedModDetailsRowHeight, ModDetailsRow.ActualHeight);
		var targetHeight = isExpanded
			? Math.Max(MinimumExpandedModDetailsRowHeight, _lastExpandedModDetailsRowHeight)
			: CollapsedModDetailsRowHeight;

		ModDetailsRow.MinHeight = 0;
		if (isExpanded)
		{
			ModDetailsGridSplitter.Visibility = Visibility.Visible;
			ModDetailsSplitterRow.Height = new GridLength(ModDetailsSplitterHeight);
		}

		var completed = await AnimatePanelValueAsync(
			startHeight,
			targetHeight,
			value => ModDetailsRow.Height = new GridLength(value),
			token);
		if (!completed) return;

		UpdateModDetailsLayout(true);
	}

	private static int GetPanelMotionMilliseconds()
	{
		return Application.Current?.TryFindResource("Redux.Motion.PanelMilliseconds") is int duration
			? duration
			: 200;
	}

	private static System.Threading.Tasks.Task<bool> AnimatePanelValueAsync(
		double from,
		double to,
		Action<double> update,
		System.Threading.CancellationToken token)
	{
		if (ReduxWindowBehavior.ReduceMotion)
		{
			if (token.IsCancellationRequested)
				return System.Threading.Tasks.Task.FromResult(false);
			update(to);
			return System.Threading.Tasks.Task.FromResult(true);
		}

		var duration = GetPanelMotionMilliseconds();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var completion = new System.Threading.Tasks.TaskCompletionSource<bool>();
		EventHandler renderingHandler = null;
		renderingHandler = (_, _) =>
		{
			if (token.IsCancellationRequested)
			{
				CompositionTarget.Rendering -= renderingHandler;
				completion.TrySetResult(false);
				return;
			}

			var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration, 0, 1);
			// Sine ease-in/out avoids the sharper acceleration change of the former cubic loop.
			var eased = 0.5 - (Math.Cos(Math.PI * progress) / 2);
			update(from + ((to - from) * eased));
			if (progress < 1) return;

			CompositionTarget.Rendering -= renderingHandler;
			completion.TrySetResult(true);
		};
		CompositionTarget.Rendering += renderingHandler;
		return completion.Task;
	}

	private void ModDetailsGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		Dispatcher.BeginInvoke(new Action(RememberExpandedModDetailsHeight));
	}

	private async void UpdateOverrideModsLayout(bool hasAlwaysLoadedMods, bool isExpanded)
	{
		var showContents = hasAlwaysLoadedMods && isExpanded;
		if (!IsLoaded || !hasAlwaysLoadedMods || ActiveModsListForcedModsRow.ActualHeight <= 0)
		{
			ApplyOverrideModsLayout(hasAlwaysLoadedMods, showContents);
			return;
		}

		_overrideModsTransition?.Cancel();
		_overrideModsTransition = new System.Threading.CancellationTokenSource();
		var token = _overrideModsTransition.Token;
		var startHeight = ActiveModsListForcedModsRow.ActualHeight;

		ActiveModsListRow.Height = new GridLength(1, GridUnitType.Star);
		ActiveModsListForcedModsRow.MinHeight = 0;
		if (showContents)
			ForceLoadedModsListView.Visibility = Visibility.Visible;

		var availableWidth = Math.Max(1, ActiveModListGrid.ActualWidth);
		AlwaysLoadedSectionGrid.Measure(new Size(availableWidth, double.PositiveInfinity));
		var headerHeight = AlwaysLoadedHeaderGrid.ActualHeight
			+ AlwaysLoadedHeaderGrid.Margin.Top
			+ AlwaysLoadedHeaderGrid.Margin.Bottom;
		var targetHeight = showContents
			? Math.Max(headerHeight, AlwaysLoadedSectionGrid.DesiredSize.Height)
			: Math.Max(1, headerHeight);

		var completed = await AnimatePanelValueAsync(
			startHeight,
			targetHeight,
			value => ActiveModsListForcedModsRow.Height = new GridLength(value),
			token);
		if (completed) ApplyOverrideModsLayout(hasAlwaysLoadedMods, showContents);
	}

	private void ApplyOverrideModsLayout(bool hasAlwaysLoadedMods, bool showContents)
	{
		ForceLoadedModsListView.Visibility = BoolToVisibilityConverter.FromBool(showContents);
		ActiveModsListForcedModsRow.MinHeight = 0;
		ActiveModsListRow.Height = new GridLength(1, GridUnitType.Star);

		if (!hasAlwaysLoadedMods)
		{
			ActiveModsListForcedModsRow.Height = new GridLength(0);
			return;
		}

		ActiveModsListForcedModsRow.Height = GridLength.Auto;
	}

	private async void UpdateInactiveModsLayout(bool isExpanded)
	{
		if (!IsLoaded || ActiveModsColumn.ActualWidth <= 0 || InactiveModsColumn.ActualWidth <= 0)
		{
			ApplyInactiveModsLayout(isExpanded);
			return;
		}

		_inactiveModsTransition?.Cancel();
		_inactiveModsTransition = new System.Threading.CancellationTokenSource();
		var token = _inactiveModsTransition.Token;
		var startActiveWidth = ActiveModsColumn.ActualWidth;
		var startInactiveWidth = InactiveModsColumn.ActualWidth;
		var availableWidth = startActiveWidth + startInactiveWidth;
		if (!isExpanded && startInactiveWidth > CollapsedCategoriesWidth)
			_lastExpandedInactiveModsWidth = startInactiveWidth;

		var desiredExpandedWidth = _lastExpandedInactiveModsWidth > CollapsedCategoriesWidth
			? _lastExpandedInactiveModsWidth
			: availableWidth / 2;
		var minimumPaneWidth = Math.Min(MinimumExpandedCategoriesWidth, availableWidth / 2);
		var maximumInactiveWidth = Math.Max(minimumPaneWidth, availableWidth - MinimumExpandedCategoriesWidth);
		var restoredInactiveWidth = Math.Clamp(desiredExpandedWidth, minimumPaneWidth, maximumInactiveWidth);
		var targetInactiveWidth = isExpanded ? restoredInactiveWidth : CollapsedCategoriesWidth;
		var targetActiveWidth = availableWidth - targetInactiveWidth;

		InactiveModsColumn.MaxWidth = Double.PositiveInfinity;
		InactiveModsColumn.MinWidth = 0;
		var completed = await AnimatePanelValueAsync(
			0,
			1,
			progress =>
			{
				ActiveModsColumn.Width = new GridLength(startActiveWidth + ((targetActiveWidth - startActiveWidth) * progress));
				InactiveModsColumn.Width = new GridLength(startInactiveWidth + ((targetInactiveWidth - startInactiveWidth) * progress));
			},
			token);
		if (completed)
		{
			if (isExpanded)
				_lastExpandedInactiveModsWidth = targetInactiveWidth;
			ApplyInactiveModsLayout(isExpanded, targetActiveWidth, targetInactiveWidth);
		}
	}

	private void ApplyInactiveModsLayout(bool isExpanded, double activeWeight = 1, double inactiveWeight = 1)
	{
		if (!isExpanded)
		{
			InactiveModsColumn.MinWidth = CollapsedCategoriesWidth;
			InactiveModsColumn.MaxWidth = CollapsedCategoriesWidth;
			InactiveModsColumn.Width = new GridLength(CollapsedCategoriesWidth);
			ActiveModsColumn.Width = new GridLength(1, GridUnitType.Star);
			return;
		}

		InactiveModsColumn.MaxWidth = Double.PositiveInfinity;
		InactiveModsColumn.MinWidth = 0;
		// Preserve the user's splitter ratio while retaining responsive star sizing.
		ActiveModsColumn.Width = new GridLength(Math.Max(1, activeWeight), GridUnitType.Star);
		InactiveModsColumn.Width = new GridLength(Math.Max(1, inactiveWeight), GridUnitType.Star);
	}

	private async void UpdateCategoriesLayout(bool isExpanded)
	{
		if (!IsLoaded || CategoriesColumn.ActualWidth <= 0)
		{
			ApplyCategoriesLayout(isExpanded);
			return;
		}

		if (!isExpanded && CategoriesColumn.ActualWidth >= _minimumExpandedCategoriesWidth)
			_lastExpandedCategoriesWidth = CategoriesColumn.ActualWidth;

		_categoriesTransition?.Cancel();
		_categoriesTransition = new System.Threading.CancellationTokenSource();
		var token = _categoriesTransition.Token;
		var startWidth = CategoriesColumn.ActualWidth;
		var targetWidth = isExpanded
			? Math.Max(_minimumExpandedCategoriesWidth, _lastExpandedCategoriesWidth)
			: CollapsedCategoriesWidth;

		CategoriesGridSplitter.IsEnabled = false;
		CategoriesColumn.MinWidth = 0;
		CategoriesColumn.MaxWidth = Double.PositiveInfinity;
		var completed = await AnimatePanelValueAsync(
			startWidth,
			targetWidth,
			value => CategoriesColumn.Width = new GridLength(value),
			token);
		if (completed) ApplyCategoriesLayout(isExpanded);
	}

	private void ApplyCategoriesLayout(bool isExpanded)
	{
		if (!isExpanded)
		{
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
			// Keep the informational list compact, but measure its real rows instead of
			// assuming a fixed height. The extra chrome allowance preserves the linked
			// horizontal scrollbar at every text scale and Windows DPI.
			Dispatcher.BeginInvoke(new Action(UpdateForceLoadedModsListHeight),
				System.Windows.Threading.DispatcherPriority.Loaded);

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

	private void UpdateForceLoadedModsListHeight()
	{
		const int maximumVisibleRows = 3;
		const double fallbackRowHeight = 32;

		var visibleRows = Math.Clamp(ForceLoadedModsListView.Items.Count, 1, maximumVisibleRows);
		var measuredRowsHeight = 0d;
		for (var index = 0; index < visibleRows; index++)
		{
			if (ForceLoadedModsListView.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement row)
				measuredRowsHeight += Math.Max(row.ActualHeight, row.DesiredSize.Height);
			else
				measuredRowsHeight += fallbackRowHeight;
		}

		var horizontalScrollChrome = SystemParameters.HorizontalScrollBarHeight + 3;
		ForceLoadedModsListView.Height = Math.Ceiling(measuredRowsHeight + horizontalScrollChrome);
	}

	private IDisposable _updateScroll;

	private void MoveSelectedMods(ModListView sourceList = null)
	{
		if (sourceList == ActiveModsListView || (sourceList == null && ListHasFocus(ActiveModsListView)))
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
				}

				if (nextSelectedIndex >= ViewModel.ActiveMods.Count)
				{
					nextSelectedIndex = ViewModel.ActiveMods.Count - 1;
				}

				ActiveModsListView.SelectedIndex = nextSelectedIndex;
			});
		}
		else if (sourceList == InactiveModsListView || (sourceList == null && ListHasFocus(InactiveModsListView)))
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
				}

				if (nextSelectedIndex >= ViewModel.InactiveMods.Count)
				{
					nextSelectedIndex = ViewModel.InactiveMods.Count - 1;
				}

				InactiveModsListView.SelectedIndex = nextSelectedIndex;
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
				d(this.ViewModel.WhenAnyValue(x => x.IsInactiveModsExpanded)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(UpdateInactiveModsLayout));
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
						this.AutoSizeInitialCategoryColumns();
					}
				}));

				ViewModel.Layout = this;
				RestorePersistedModListColumnWidths();
				ApplyModListColumnVisibility();
				d(ViewModel.Modules.WhenAnyValue(x => x.SourceIntegrationsEnabled)
					.Skip(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(_ => ApplyModListColumnVisibility()));

				d(this.OneWayBind(ViewModel, vm => vm.DisplayActiveMods, v => v.ActiveModsListView.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.DisplayInactiveMods, v => v.InactiveModsListView.ItemsSource));
				d(this.OneWayBind(ViewModel, vm => vm.ForceLoadedMods, v => v.ForceLoadedModsListView.ItemsSource));

				d(this.OneWayBind(ViewModel, vm => vm.HasForceLoadedMods, v => v.AlwaysLoadedSectionGrid.Visibility, BoolToVisibilityConverter.FromBool));
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

				d(ViewModel.WhenAnyValue(x => x.HasForceLoadedMods, x => x.IsAlwaysLoadedExpanded)
					.ObserveOn(RxApp.MainThreadScheduler).Subscribe((state) =>
				{
					UpdateOverrideModsLayout(state.Item1, state.Item2);
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

	private Dictionary<string, double> GetPersistedColumnWidths(ModListView listView)
	{
		if (ViewModel?.Settings == null)
		{
			return null;
		}

		if (ReferenceEquals(listView, InactiveModsListView))
		{
			return ViewModel.Settings.InactiveModListColumnWidths ??=
				new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
		}

		return ViewModel.Settings.ActiveModListColumnWidths ??=
			new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
	}

	private ModListView GetOwningModListView(GridView gridView)
	{
		if (ReferenceEquals(ActiveModsListView.View, gridView) || ReferenceEquals(ForceLoadedModsListView.View, gridView))
		{
			return ActiveModsListView;
		}
		return ReferenceEquals(InactiveModsListView.View, gridView) ? InactiveModsListView : null;
	}

	private void RestorePersistedModListColumnWidths()
	{
		CaptureModListColumnWidths();
		foreach (var (gridView, registry) in _modListColumnRegistry)
		{
			var listView = GetOwningModListView(gridView);
			var persisted = GetPersistedColumnWidths(listView);
			if (listView == null || persisted == null || persisted.Count == 0)
			{
				continue;
			}

			foreach (var (columnName, entry) in registry)
			{
				if (persisted.TryGetValue(columnName, out var width) && Double.IsFinite(width) && width > 0)
				{
					entry.Column.Width = width;
					_visibleModListColumnWidths[entry.Column] = width;
				}
			}
			listView.UserResizedColumns = true;
		}
	}

	private void PersistModListColumnWidths(ModListView listView, bool queueSave = true)
	{
		if (listView?.View is not GridView gridView)
		{
			return;
		}

		CaptureModListColumnWidths();
		var persisted = GetPersistedColumnWidths(listView);
		if (persisted == null || !_modListColumnRegistry.TryGetValue(gridView, out var registry))
		{
			return;
		}

		foreach (var (columnName, entry) in registry)
		{
			var width = gridView.Columns.Contains(entry.Column)
				? entry.Column.Width
				: _visibleModListColumnWidths.TryGetValue(entry.Column, out var hiddenWidth)
					? hiddenWidth
					: entry.Column.Width;
			if (Double.IsFinite(width) && width > 0)
			{
				persisted[columnName] = width;
			}
		}

		if (queueSave)
		{
			ViewModel.QueueSave();
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
			"Source" => ViewModel.Modules.SourceIntegrationsEnabled && ViewModel.Settings.ShowModListSourceColumn,
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
			"Category" => 175,
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

	/// <summary>
	/// Resolves the live Redux.FontSize.11 token pills actually render at, rather than
	/// assuming a literal 11 - that token scales with the Compact/Default/Large text-size
	/// preset, so a hardcoded value under-measures pill content whenever the preset isn't Default.
	/// </summary>
	private static double GetPillFontSize(ModListView listView)
	{
		return listView?.TryFindResource("Redux.FontSize.11") is double size ? size : 11d;
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

	private int GetModNameIconCount(DivinityModData mod)
	{
		var count = 0;
		if (mod.OsirisStatusVisibility == Visibility.Visible) count++;
		if (mod.ExtenderStatusVisibility == Visibility.Visible) count++;
		if (mod.ToolkitIconVisibility == Visibility.Visible) count++;
		if (mod.HasInvalidUUIDVisibility == Visibility.Visible) count++;
		if (mod.MissingDependencyIconVisibility == Visibility.Visible) count++;
		if (mod.HealthSnapshot?.HasGeneralHealthAttention == true) count++;
		if (mod.HealthSnapshot?.HasLoadOrderAdvice == true ||
			(mod.IsActive &&
			 ViewModel?.Settings.DebugModeEnabled == true &&
			 ViewModel.Modules?.LoadOrderGuidanceEnabled == true))
		{
			count++;
		}
		return count;
	}

	private double GetModNameAdornmentWidth(DivinityModData mod)
	{
		// Each status glyph occupies 16px plus 2px horizontal margin per side.
		// The newly-detected marker has its own 8px surface and 10px of spacing.
		return (GetModNameIconCount(mod) * 20d) + (mod.IsNewlyDetected ? 18d : 0d);
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
		var sourceIconsOnly = ViewModel?.Settings.ShowCategoryIconsInPills == true &&
			ViewModel.Settings.UseIconsOnly;
		var fallbackWidth = columnName == "Source" && sourceIconsOnly
			? 64d
			: GetFallbackMinimumColumnWidth(columnName);
		return Math.Ceiling(Math.Max(headerWidth, fallbackWidth));
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
		IEnumerable<DivinityModData> measurementMods = listView.Items
			.OfType<DivinityModData>()
			.Where(item => !item.IsVisualDivider);

		// Override mods render in a separate headerless list whose columns are linked
		// to the Active list. Include those rows when sizing the shared Active columns
		// so override category/source pills and other values are not clipped.
		if (ReferenceEquals(listView, ActiveModsListView) && ViewModel?.ForceLoadedMods != null)
		{
			measurementMods = measurementMods
				.Concat(ViewModel.ForceLoadedMods.Where(item => !item.IsVisualDivider))
				.Distinct();
		}

		// Measure every real row represented by the list, including rows temporarily
		// collapsed by a separator or still completing their first visibility binding.
		// Otherwise the first auto-size pass after startup can measure only a partial
		// set and produce a narrower result than a second click.
		foreach (var mod in measurementMods)
		{
			double candidateWidth;
			switch (columnName)
			{
				case "#":
					candidateWidth = MeasureColumnText(listView, mod.Index.ToString(CultureInfo.CurrentCulture)) + 20;
					break;
				case "Name":
					candidateWidth = MeasureColumnText(listView, mod.DisplayTitle) + 28 + GetModNameAdornmentWidth(mod);
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
					// Per-pill overhead: 16 padding + 3 border + 5 margin, plus the 17px
					// icon+margin reserve whenever category icons are shown (the pill's
					// actual chrome, not a guess), plus a generous cushion - the pill's
					// FontSize comes from the Redux.FontSize.11 token, which scales with the
					// Compact/Default/Large text-size preset, so a hardcoded "11" here
					// under-measures whenever text size isn't Default.
					var categoryIconsOnly = ViewModel?.Settings.ShowCategoryIconsInPills == true &&
						ViewModel.Settings.UseIconsOnly;
					if (categoryIconsOnly)
					{
						candidateWidth = ((mod.DisplayCategories?.Count ?? 0) * 29d) + 20d;
					}
					else
					{
						var categoryIconAllowance = ViewModel?.Settings.ShowCategoryIconsInPills == true ? 17 : 0;
						var pillFontSize = GetPillFontSize(listView);
						candidateWidth = (mod.DisplayCategories?.Sum(category => MeasureColumnText(listView, category.Name, pillFontSize, FontWeights.SemiBold) + 24 + categoryIconAllowance) ?? 0) + 20;
					}
					break;
				case "Source":
					// 14px provider icon + 6 margin + 16 padding + 3 border + a generous cushion.
					var sourceIconsOnly = ViewModel?.Settings.ShowCategoryIconsInPills == true &&
						ViewModel.Settings.UseIconsOnly;
					candidateWidth = sourceIconsOnly
						? 40
						: MeasureColumnText(listView, mod.DisplaySource, GetPillFontSize(listView), FontWeights.SemiBold) +
							(ViewModel?.Settings.ShowCategoryIconsInPills == true ? 60 : 36);
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

	private static bool IsModListColumnVisibleByDefault(string columnName)
	{
		return columnName switch
		{
			"Author" => true,
			"Last Updated" => true,
			"Category" => true,
			"Source" => true,
			_ => false
		};
	}

	private double GetInitialCategoryColumnWidth(ModListView listView)
	{
		if (ViewModel?.Settings.ShowCategoryIconsInPills == true && ViewModel.Settings.UseIconsOnly)
		{
			var widestIconRow = listView.Items
				.OfType<DivinityModData>()
				.Where(mod => !mod.IsVisualDivider)
				.Select(mod => ((mod.DisplayCategories?.Count ?? 0) * 29d) + 20d)
				.DefaultIfEmpty(GetDefaultColumnWidth("Category"))
				.Max();
			return Math.Ceiling(Math.Min(Math.Max(widestIconRow, GetHeaderMinimumColumnWidth(listView, "Category")), 260d));
		}

		var iconAllowance = ViewModel?.Settings.ShowCategoryIconsInPills == true ? 17 : 0;
		var pillFontSize = GetPillFontSize(listView);
		var widestPill = listView.Items
			.OfType<DivinityModData>()
			.Where(mod => !mod.IsVisualDivider)
			.SelectMany(mod => mod.DisplayCategories ?? Enumerable.Empty<ModCategoryDisplayData>())
			.Select(category => MeasureColumnText(listView, category.Name, pillFontSize, FontWeights.SemiBold) + 44 + iconAllowance)
			.DefaultIfEmpty(GetDefaultColumnWidth("Category"))
			.Max();

		return Math.Ceiling(Math.Min(Math.Max(widestPill, GetHeaderMinimumColumnWidth(listView, "Category")), 260d));
	}

	private void AutoSizeInitialCategoryColumns()
	{
		RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(350), () =>
		{
			foreach (var listView in new[] { ActiveModsListView, InactiveModsListView })
			{
				if (listView.UserResizedColumns || listView.View is not GridView gridView) continue;
				var categoryColumn = gridView.Columns.FirstOrDefault(column => GetColumnName(column) == "Category");
				if (categoryColumn == null) continue;

				// Initial sizing may grow the default width to fit a real pill, but never
				// shrinks a restored/user-selected layout.
				categoryColumn.Width = Math.Max(categoryColumn.Width, GetInitialCategoryColumnWidth(listView));
				_visibleModListColumnWidths[categoryColumn] = categoryColumn.Width;
			}
		});
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
		Dispatcher.BeginInvoke(new Action(() =>
		{
			ClampModListColumnWidth(listView, resizedColumn);
			PersistModListColumnWidths(listView);
		}), System.Windows.Threading.DispatcherPriority.Background);
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

	private void ResetModListColumnsToDefaults()
	{
		CaptureModListColumnWidths();

		foreach (var columnName in OptionalModListColumns)
		{
			SetModListColumnSetting(columnName, IsModListColumnVisibleByDefault(columnName));
		}

		foreach (var gridView in GetModListGridViews())
		{
			if (!_modListColumnRegistry.TryGetValue(gridView, out var registry))
			{
				continue;
			}

			var registeredColumns = registry.Values
				.Select(entry => entry.Column)
				.ToHashSet();
			var defaultOrder = registry.Values
				.Select(entry => (entry.Column, entry.Index))
				.Concat(gridView.Columns
					.Cast<GridViewColumn>()
					.Where(column => !registeredColumns.Contains(column))
					.Select((column, index) => (Column: column, Index: index)))
				.OrderBy(entry => entry.Index)
				.Select(entry => entry.Column)
				.ToList();

			gridView.Columns.Clear();
			foreach (var column in defaultOrder)
			{
				gridView.Columns.Add(column);
				var columnName = GetColumnName(column);
				if (!String.IsNullOrWhiteSpace(columnName))
				{
					column.Width = GetDefaultColumnWidth(columnName);
					_visibleModListColumnWidths[column] = column.Width;
				}
			}
		}

		ApplyModListColumnVisibility();
		ActiveModsListView.UserResizedColumns = false;
		InactiveModsListView.UserResizedColumns = false;
		PersistModListColumnWidths(ActiveModsListView, false);
		PersistModListColumnWidths(InactiveModsListView, false);
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
			if (columnName == "Source" && !ViewModel.Modules.SourceIntegrationsEnabled)
			{
				continue;
			}

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
			// Let WPF finish the current menu/layout transaction before measuring,
			// then quietly verify once more after late row bindings have settled.
			Dispatcher.BeginInvoke(new Action(() => AutoSizeModListColumns(listView, false)),
				System.Windows.Threading.DispatcherPriority.Render);
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(140), () =>
			{
				if (listView.IsLoaded)
				{
					AutoSizeModListColumns(listView, true);
				}
			});
		};
		menu.Items.Add(autoSizeItem);
		var resetItem = new MenuItem
		{
			Header = "Reset Columns",
			Icon = ReduxIcon.FromResource("Redux.Icon.RefreshStroke", true)
		};
		resetItem.Click += (_, _) =>
		{
			ResetModListColumnsToDefaults();
			ViewModel.QueueSave();
		};
		menu.Items.Add(resetItem);

		menu.IsOpen = true;
		e.Handled = true;
	}

	private void AutoSizeModListColumns(ModListView listView, bool persist)
	{
		if (listView?.View is not GridView gridView)
		{
			return;
		}

		listView.UpdateLayout();
		foreach (var column in gridView.Columns)
		{
			column.Width = GetContentAutoSizeColumnWidth(listView, GetColumnName(column));
			_visibleModListColumnWidths[column] = column.Width;
		}
		listView.UserResizedColumns = true;
		if (persist)
		{
			PersistModListColumnWidths(listView);
		}
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
				if (ActiveModsListView.UserResizedColumns) return;
				count = Math.Max(ViewModel.ActiveMods.Count, ViewModel.ForceLoadedMods.Count);
				if (count > 0)
				{
					var targetWidth = ViewModel.Mods
						.Where(mod => mod.IsActive || mod.IsForceLoaded)
						.Where(mod => !String.IsNullOrWhiteSpace(mod.DisplayTitle))
						.Select(mod =>
							MeasureColumnText(ActiveModsListView, mod.DisplayTitle) +
							_FontSizeMeasurePadding +
							GetModNameAdornmentWidth(mod))
						.DefaultIfEmpty(0d)
						.Max();

					if (targetWidth > 0)
					{
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
			var targetWidth = ViewModel.InactiveMods
				.Where(mod => !String.IsNullOrWhiteSpace(mod.DisplayTitle))
				.Select(mod =>
					MeasureColumnText(InactiveModsListView, mod.DisplayTitle) +
					_FontSizeMeasurePadding +
					GetModNameAdornmentWidth(mod))
				.DefaultIfEmpty(0d)
				.Max();

			if (targetWidth > 0)
			{
				InactiveModsListView.Resizing = true;
				gridView.Columns[0].Width = targetWidth;
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
