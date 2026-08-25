using DivinityModManager.Controls;

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace DivinityModManager.Util.ScreenReader;

public class ModEntryGridAutomationPeer : CachedAutomationPeer
{
	private ModEntryGrid grid;
	public ModEntryGridAutomationPeer(ModEntryGrid owner) : base(owner)
	{
		grid = owner;
	}

	protected override string GetNameCore()
	{
		return grid.GetValue(AutomationProperties.NameProperty) as string ?? string.Empty;
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.ListItem;
	}

	override public List<AutomationPeer> GetPeersFromElements()
	{
		var text = ElementHelper.FindChild<TextBlock>(grid, "ModNameText");
		var peer = text == null ? null : UIElementAutomationPeer.CreatePeerForElement(text);
		return peer == null ? null : new List<AutomationPeer>(1) { peer };
	}
}
