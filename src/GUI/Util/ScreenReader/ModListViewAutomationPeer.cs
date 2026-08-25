using DivinityModManager.Controls;
using DivinityModManager.Models;

using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace DivinityModManager.Util.ScreenReader;

/// <summary>
/// Exposes only the mod rows currently realized by the virtualizing panel.
///
/// WPF's stock ListViewAutomationPeer creates item peers for the complete Items
/// collection. On .NET 8, an attached UI Automation client can leave those peers
/// in a perpetual UpdateSubtree cycle as rows are filtered or recycled. Keeping
/// this peer bounded to the viewport preserves useful list-item accessibility
/// without allowing automation work to scale with the installed mod count.
/// </summary>
public sealed class ModListViewAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
	private readonly ModListView _listView;
	private readonly ConditionalWeakTable<ListViewItem, ModListItemAutomationPeer> _itemPeers = new();

	/// <summary>
	/// Represents a realized row as one semantic list item. Letting WPF create its
	/// stock ListBoxItemAutomationPeer recursively exposes every GridView cell,
	/// pill, icon, and button in the row. Recycling those deep peer subtrees while
	/// scrolling can keep ContextLayoutManager.fireAutomationEvents on the UI thread
	/// indefinitely. This leaf peer keeps the row accessible without exposing its
	/// presentation-only visual tree.
	/// </summary>
	private sealed class ModListItemAutomationPeer : FrameworkElementAutomationPeer, ISelectionItemProvider
	{
		private readonly ListViewItem _row;
		private readonly ModListView _listView;
		private readonly ModListViewAutomationPeer _listPeer;

		public ModListItemAutomationPeer(
			ListViewItem row,
			ModListView listView,
			ModListViewAutomationPeer listPeer) : base(row)
		{
			_row = row;
			_listView = listView;
			_listPeer = listPeer;
		}

		protected override string GetNameCore()
		{
			if (_row.DataContext is DivinityModData mod)
			{
				var title = mod.DisplayTitle ?? mod.DisplayName ?? String.Empty;
				return mod.IsVisualDivider ? $"Separator: {title}" : title;
			}
			return _row.GetValue(AutomationProperties.NameProperty) as string ?? String.Empty;
		}

		protected override string GetHelpTextCore()
		{
			if (_row.DataContext is DivinityModData mod)
			{
				return mod.IsVisualDivider
					? mod.VisualDividerDescription ?? String.Empty
					: mod.HelpText ?? String.Empty;
			}
			return _row.GetValue(AutomationProperties.HelpTextProperty) as string ?? String.Empty;
		}

		protected override AutomationControlType GetAutomationControlTypeCore() =>
			AutomationControlType.ListItem;

		protected override string GetClassNameCore() => nameof(ListViewItem);

		protected override bool IsContentElementCore() => true;

		protected override List<AutomationPeer> GetChildrenCore() => null;

		public override object GetPattern(PatternInterface patternInterface) =>
			patternInterface == PatternInterface.SelectionItem
				? this
				: base.GetPattern(patternInterface);

		bool ISelectionItemProvider.IsSelected => _row.IsSelected;

		IRawElementProviderSimple ISelectionItemProvider.SelectionContainer =>
			ProviderFromPeer(_listPeer);

		void ISelectionItemProvider.AddToSelection()
		{
			if (_listView.SelectionMode == SelectionMode.Single)
			{
				((ISelectionItemProvider)this).Select();
				return;
			}
			_row.IsSelected = true;
		}

		void ISelectionItemProvider.RemoveFromSelection() => _row.IsSelected = false;

		void ISelectionItemProvider.Select()
		{
			_listView.UnselectAll();
			_row.IsSelected = true;
		}
	}

	public ModListViewAutomationPeer(ModListView owner) : base(owner)
	{
		_listView = owner;
	}

	protected override string GetNameCore()
	{
		return Owner.GetValue(AutomationProperties.NameProperty) as string ?? String.Empty;
	}

	protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

	protected override string GetClassNameCore() => nameof(ModListView);

	protected override bool IsContentElementCore() => true;

	private List<ListViewItem> GetRealizedRows()
	{
		var itemsHost = _listView.FindVisualChildren<VirtualizingStackPanel>().FirstOrDefault();
		IEnumerable<ListViewItem> rows = itemsHost != null
			? itemsHost.Children.OfType<ListViewItem>()
			: _listView.FindVisualChildren<ListViewItem>();
		return rows
			.Where(row => row.IsVisible &&
				ItemsControl.ItemsControlFromItemContainer(row) == _listView)
			.ToList();
	}

	private ModListItemAutomationPeer GetItemPeer(ListViewItem row) =>
		_itemPeers.GetValue(row, item => new ModListItemAutomationPeer(item, _listView, this));

	protected override List<AutomationPeer> GetChildrenCore()
	{
		// The panel's Children collection contains only realized containers when
		// virtualization is enabled. Avoid iterating Items/ContainerFromIndex: that
		// would make every accessibility refresh O(total installed mods).
		var peers = GetRealizedRows()
			.Select(GetItemPeer)
			.Cast<AutomationPeer>()
			.ToList();
		return peers.Count == 0 ? null : peers;
	}

	public override object GetPattern(PatternInterface patternInterface)
	{
		return patternInterface == PatternInterface.Selection
			? this
			: base.GetPattern(patternInterface);
	}

	bool ISelectionProvider.CanSelectMultiple => _listView.SelectionMode != SelectionMode.Single;

	bool ISelectionProvider.IsSelectionRequired => false;

	IRawElementProviderSimple[] ISelectionProvider.GetSelection()
	{
		return GetRealizedRows()
			.Where(row => row.IsSelected)
			.Select(row => ProviderFromPeer(GetItemPeer(row)))
			.ToArray();
	}
}
