using System;
using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

public sealed class NxmAssociationPresentationTests
{
	public void WineNxmStatusDescribesOnlyPrefixRegistryFacts()
	{
		foreach (var (status, expected) in new[]
		{
			(NxmAssociationStatus.Available, "Wine-prefix links are not registered to Redux"),
			(NxmAssociationStatus.Owned, "Wine-prefix links use Redux"),
			(NxmAssociationStatus.NeedsRepair, "Wine-prefix links need repair"),
			(NxmAssociationStatus.OwnedByAnotherHandler, "Wine-prefix links use another app")
		})
		{
			var result = new NxmAssociationResult(true, status, "Registry result from this prefix", "previous-handler --nxm %1");
			var original = result with { };
			RegressionAssert.Equal(expected, NxmAssociationPresentation.GetStatusText(result, isWine: true));
			var detail = NxmAssociationPresentation.GetDetail(result, isWine: true);
			RegressionAssert.Contains(detail, result.Message);
			RegressionAssert.Contains(detail, "separate desktop handler");
			RegressionAssert.Contains(detail, "status is not checked here");
			RegressionAssert.Equal(original, result);

			var failure = result with { Success = false, Message = "Registry access failed" };
			RegressionAssert.Equal("Wine-prefix link status unavailable", NxmAssociationPresentation.GetStatusText(failure, isWine: true));
			RegressionAssert.Contains(NxmAssociationPresentation.GetDetail(failure, isWine: true), "Registry access failed");
			RegressionAssert.Equal(status, failure.Status);
			RegressionAssert.Equal(result.CurrentHandler, failure.CurrentHandler);
		}
		RegressionAssert.Equal("Use Redux for Wine-prefix links", NxmAssociationPresentation.GetToggleLabel(isWine: true));
	}

	public void WindowsNxmPresentationRetainsExistingText()
	{
		foreach (var (status, expected, prompt) in new[]
		{
			(NxmAssociationStatus.Available, "NXM links are off", "Let Redux handle Nexus Mod Manager links for this Windows account?\n\nCurrent handler: None\n\nNon-BG3 links are forwarded to the previous handler when it can be invoked safely."),
			(NxmAssociationStatus.Owned, "NXM links open in Redux", "Stop Redux from handling NXM links and restore the previous per-user handler?"),
			(NxmAssociationStatus.NeedsRepair, "NXM links need repair", "Repair this Redux installation's NXM link registration?"),
			(NxmAssociationStatus.OwnedByAnotherHandler, "NXM links use another app", "Use this Redux installation for Nexus Mod Manager links instead?\n\nCurrent handler: Unknown\n\nThe current handler will be saved so Redux can restore it later.")
		})
		{
			var result = new NxmAssociationResult(true, status, "Original registry detail");
			RegressionAssert.Equal(expected, NxmAssociationPresentation.GetStatusText(result, isWine: false));
			RegressionAssert.Equal(result.Message, NxmAssociationPresentation.GetDetail(result, isWine: false));
			RegressionAssert.Equal(prompt, NxmAssociationPresentation.GetConfigurePrompt(result, isWine: false));
			RegressionAssert.Equal("NXM status unavailable", NxmAssociationPresentation.GetStatusText(result with { Success = false }, isWine: false));
		}
		RegressionAssert.Equal("Use Redux for NXM links", NxmAssociationPresentation.GetToggleLabel(isWine: false));
	}

	public void WineNxmPromptsKeepDesktopForwardingSeparate()
	{
		foreach (var status in Enum.GetValues<NxmAssociationStatus>())
		{
			var result = new NxmAssociationResult(true, status, "Registry detail", "prior-handler.exe --nxm %1");
			var prompt = NxmAssociationPresentation.GetConfigurePrompt(result, isWine: true);
			RegressionAssert.Contains(prompt, "inside this Wine prefix");
			RegressionAssert.Contains(prompt, NxmAssociationPresentation.WineScopeNote);
			RegressionAssert.False(prompt.Contains("this Windows account", StringComparison.Ordinal));
			if (status is NxmAssociationStatus.Available or NxmAssociationStatus.OwnedByAnotherHandler)
				RegressionAssert.Contains(prompt, result.CurrentHandler);
		}
	}
}
