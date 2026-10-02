using DivinityModManager.AppServices;
using DivinityModManager.Models;
using JsonException = Newtonsoft.Json.JsonException;
using System;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class ArchiveLoadOrderTests
{
	public void BundledMetadataIsNotImportedAsAnEmptyLoadOrder()
	{
		foreach (var text in new[] { "{}", "{\"Mods\":[]}", "{\"Name\":\"Current\",\"Version\":1}", "[]" })
			RegressionAssert.True(ArchiveLoadOrderService.Read("Current.json", text) == null);
	}

	public void ExplicitEmptyLoadOrderAndNestedEntryNamesRemainSupported()
	{
		var empty = ArchiveLoadOrderService.Read("folder\\Current.json", "{\"Order\":[]}")!;
		RegressionAssert.Equal("Current", empty.Name);
		RegressionAssert.Equal(0, empty.Order.Count);
		var filled = ArchiveLoadOrderService.Read("folder/Adventure.json",
			"{\"order\":[{\"uuid\":\"mod-1\",\"Name\":\"One\"}],\"OverrideModFiles\":[\"texture.pak\"]}")!;
		RegressionAssert.Equal("Adventure", filled.Name);
		RegressionAssert.Equal("mod-1", filled.Order.Single().UUID);
		RegressionAssert.Equal("texture.pak", filled.OverrideModFiles.Single());
	}

	public void MalformedLoadOrdersCannotBecomePartiallyEmptyOrders()
	{
		foreach (var text in new[]
		{
			"{\"Order\":null}", "{\"Order\":{}}", "{\"Order\":[null]}",
			"{\"Order\":[{}]}", "{\"Order\":[{\"UUID\":5}]}", "{\"Order\":[{\"UUID\":\" \"}]}",
			"{\"Order\":[{\"UUID\":\"valid\"},{}]}", "{\"Order\":[],\"order\":null}", "{\"Order\":["
		})
		{
			var rejected = false;
			try { ArchiveLoadOrderService.Read("Current.json", text); }
			catch (JsonException) { rejected = true; }
			if (!rejected) throw new InvalidOperationException($"Invalid order was accepted: {text}");
		}
	}

	public void ArchivedCurrentUpdatesOnlyGameBackedOrderAndClonesItsContents()
	{
		var named = new DivinityLoadOrder { Name = "My order", FilePath = "mine.json",
			Order = [new() { UUID = "named-mod" }] };
		var current = new DivinityLoadOrder { Name = "Current", IsModSettings = true, FilePath = "modsettings.lsx",
			Order = [new() { UUID = "old-mod" }] };
		var imported = ArchiveLoadOrderService.Read("current.json",
			"{\"Order\":[{\"UUID\":\"imported-mod\"}],\"OverrideModFiles\":[\"new.pak\"],\"VisualDividers\":[]}")!;
		var applied = ArchiveLoadOrderService.ApplyCurrent([named, current], imported);
		RegressionAssert.True(ReferenceEquals(current, applied));
		RegressionAssert.Equal("named-mod", named.Order.Single().UUID);
		RegressionAssert.Equal("mine.json", named.FilePath);
		RegressionAssert.Equal("Current", current.Name);
		RegressionAssert.Equal("modsettings.lsx", current.FilePath);
		RegressionAssert.True(current.IsModSettings);
		RegressionAssert.Equal("imported-mod", current.Order.Single().UUID);
		RegressionAssert.Equal("new.pak", current.OverrideModFiles.Single());
		RegressionAssert.Equal(0, current.VisualDividers.Count);
		imported.Order[0].UUID = "changed";
		imported.OverrideModFiles.Clear();
		RegressionAssert.Equal("imported-mod", current.Order.Single().UUID);
		RegressionAssert.Equal("new.pak", current.OverrideModFiles.Single());
	}

	public void ArchivedCurrentDoesNotUseANamedOrderAsFallback()
	{
		var named = new DivinityLoadOrder { Name = "Current", Order = [new() { UUID = "keep" }] };
		var imported = ArchiveLoadOrderService.Read("Current.json", "{\"Order\":[]}")!;
		RegressionAssert.True(ArchiveLoadOrderService.ApplyCurrent([named], imported) == null);
		RegressionAssert.Equal("keep", named.Order.Single().UUID);
	}
}
