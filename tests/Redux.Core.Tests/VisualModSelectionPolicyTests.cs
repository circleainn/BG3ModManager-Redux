using DivinityModManager.Models;
using DivinityModManager.Util;

using System.Windows;

namespace Redux.Core.Tests;

public sealed class VisualModSelectionPolicyTests
{
	public void SelectAllIncludesOnlyVisibleModRows()
	{
		var first = CreateMod("first");
		var divider = CreateMod("divider");
		divider.IsVisualDivider = true;
		var filtered = CreateMod("filtered");
		filtered.Visibility = Visibility.Collapsed;
		var second = CreateMod("second");

		var selected = VisualModSelectionPolicy.ResolveSelectAllItems(
			new[] { first, divider, filtered, second });

		RegressionAssert.SequenceEqual(new[] { first, second }, selected);
	}

	public void FilterProjectionOmitsCollapsedRowsFromTheItemsSource()
	{
		var first = CreateMod("first");
		var filtered = CreateMod("filtered");
		filtered.IsSelected = true;
		filtered.Visibility = Visibility.Collapsed;
		var second = CreateMod("second");

		var displayed = VisualModFilterProjectionPolicy.ResolveVisibleMods(
			new[] { first, filtered, second });

		RegressionAssert.SequenceEqual(new[] { first, second }, displayed);
		RegressionAssert.True(filtered.IsSelected);
	}

	private static DivinityModData CreateMod(string uuid) => new()
	{
		UUID = uuid,
		Name = uuid,
		Visibility = Visibility.Visible
	};
}
