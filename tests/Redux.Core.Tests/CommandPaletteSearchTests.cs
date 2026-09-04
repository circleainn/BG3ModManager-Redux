using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class CommandPaletteSearchTests
{
	public void AliasesAndWordOrderMakeActionsDiscoverable()
	{
		var separator = new ReduxCommandPaletteItem(
			"Create Separator...",
			"Separators",
			"Add a separator to the active list.",
			string.Empty,
			"marker-diamond",
			() => { },
			searchTerms: "insert new divider section");

		RegressionAssert.True(separator.Matches("create"));
		RegressionAssert.True(separator.Matches("divider"));
		RegressionAssert.True(separator.Matches("active create"));
		RegressionAssert.True(separator.Matches("insert-section"));
		RegressionAssert.False(separator.Matches("delete"));
	}

	public void MinimumQueryLengthStillProtectsLargeDynamicLists()
	{
		var mod = new ReduxCommandPaletteItem(
			"Open mod: Example",
			"Mods",
			string.Empty,
			string.Empty,
			"package",
			() => { },
			minimumQueryLength: 2);

		RegressionAssert.False(mod.Matches("e"));
		RegressionAssert.True(mod.Matches("ex"));
	}
}
