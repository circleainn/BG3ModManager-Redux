using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace DivinityModManager.Util.ScreenReader;

public class CachedAutomationPeer : FrameworkElementAutomationPeer
{
	public CachedAutomationPeer(FrameworkElement owner) : base(owner) { }

	private List<AutomationPeer> _cachedAutomationPeers;

	private static AutomationPeer CreatePeerForElementSafe(UIElement element)
	{
		try
		{
			return FrameworkElementAutomationPeer.CreatePeerForElement(element);
		}
		catch (Exception)
		{
			return null;
		}
	}

	internal static List<AutomationPeer> GetChildrenRecursively(UIElement uiElement)
	{
		List<AutomationPeer> children = new List<AutomationPeer>();
		int childrenCount = VisualTreeHelper.GetChildrenCount(uiElement);

		for (int child = 0; child < childrenCount; child++)
		{
			if (!(VisualTreeHelper.GetChild(uiElement, child) is UIElement element))
				continue;

			AutomationPeer peer = CreatePeerForElementSafe(element);
			if (peer != null)
				children.Add(peer);
			else
			{
				List<AutomationPeer> returnedChildren = GetChildrenRecursively(element);
				if (returnedChildren != null)
					children.AddRange(returnedChildren);
			}
		}

		if (children.Count == 0)
			return null;

		return children;
	}

	public virtual bool HasNullChildElement()
	{
		foreach (var c in this.Owner.FindVisualChildren<UIElement>())
		{
			if (c == null)
			{
				return true;
			}
		}
		return false;
	}

	public virtual List<AutomationPeer> GetPeersFromElements()
	{
		return GetChildrenRecursively(Owner);
	}

	protected override List<AutomationPeer> GetChildrenCore()
	{
		// The semantic children of the window and main view are stable after they are
		// first realized. Rebuilding this list for every UI Automation navigation request
		// recursively walks the complete visual tree and can monopolize the UI thread while
		// a screen reader or automation client is attached. Retry only while the initial
		// visual tree has not produced any peers; otherwise reuse the stable peer instances.
		if (_cachedAutomationPeers == null)
		{
			var peers = GetPeersFromElements();
			peers?.RemoveAll(peer => peer == null);
			if (peers?.Count > 0)
			{
				_cachedAutomationPeers = peers;
			}
		}
		return _cachedAutomationPeers;
	}
}
