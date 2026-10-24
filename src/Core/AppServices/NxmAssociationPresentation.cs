using DivinityModManager.Localization;

namespace DivinityModManager.AppServices;

/// <summary>
/// Describes the existing registry result without treating Wine-prefix registration
/// as proof that the native Linux desktop has a working link handler.
/// </summary>
public static class NxmAssociationPresentation
{
	public const string WineScopeNote = "Linux browser links need a separate desktop handler that forwards to this Wine prefix. Its status is not checked here.";

	public static string GetToggleLabel(bool isWine) => ReduxLanguageService.Current.Translate(isWine
		? "Use Redux for Wine-prefix links"
		: "Use Redux for NXM links");

	public static string GetStatusText(NxmAssociationResult result, bool isWine)
	{
		ArgumentNullException.ThrowIfNull(result);
		if (!result.Success) return ReduxLanguageService.Current.Translate(isWine ? "Wine-prefix link status unavailable" : "NXM status unavailable");
		return ReduxLanguageService.Current.Translate((result.Status, isWine) switch
		{
			(NxmAssociationStatus.Owned, true) => "Wine-prefix links use Redux",
			(NxmAssociationStatus.NeedsRepair, true) => "Wine-prefix links need repair",
			(NxmAssociationStatus.OwnedByAnotherHandler, true) => "Wine-prefix links use another app",
			(_, true) => "Wine-prefix links are not registered to Redux",
			(NxmAssociationStatus.Owned, false) => "NXM links open in Redux",
			(NxmAssociationStatus.NeedsRepair, false) => "NXM links need repair",
			(NxmAssociationStatus.OwnedByAnotherHandler, false) => "NXM links use another app",
			_ => "NXM links are off"
		});
	}

	public static string GetDetail(NxmAssociationResult result, bool isWine) => isWine
		? $"{result.Message}\n\n{ReduxLanguageService.Current.Translate(WineScopeNote)}"
		: result.Message;

	public static string GetConfigurePrompt(NxmAssociationResult result, bool isWine)
	{
		ArgumentNullException.ThrowIfNull(result);
		if (isWine)
		{
			var prompt = result.Status switch
			{
				NxmAssociationStatus.Owned => ReduxLanguageService.Current.Translate("Stop Redux from handling NXM links inside this Wine prefix and restore its previous handler?"),
				NxmAssociationStatus.NeedsRepair => ReduxLanguageService.Current.Translate("Repair Redux's NXM link registration inside this Wine prefix?"),
				NxmAssociationStatus.OwnedByAnotherHandler => ReduxLanguageService.Current.Format($"Use this Redux installation for NXM links inside this Wine prefix instead?\n\nCurrent handler: {result.CurrentHandler ?? ReduxLanguageService.Current.Translate("Unknown")}\n\nThe current handler will be saved so Redux can restore it later."),
				_ => ReduxLanguageService.Current.Format($"Let Redux handle NXM links inside this Wine prefix?\n\nCurrent handler: {result.CurrentHandler ?? ReduxLanguageService.Current.Translate("None")}\n\nNon-BG3 links are forwarded to the previous handler when it can be invoked safely.")
			};
			return $"{prompt}\n\n{ReduxLanguageService.Current.Translate(WineScopeNote)}";
		}

		return result.Status switch
		{
			NxmAssociationStatus.Owned => ReduxLanguageService.Current.Translate("Stop Redux from handling NXM links and restore the previous per-user handler?"),
			NxmAssociationStatus.NeedsRepair => ReduxLanguageService.Current.Translate("Repair this Redux installation's NXM link registration?"),
			NxmAssociationStatus.OwnedByAnotherHandler => ReduxLanguageService.Current.Format($"Use this Redux installation for Nexus Mod Manager links instead?\n\nCurrent handler: {result.CurrentHandler ?? ReduxLanguageService.Current.Translate("Unknown")}\n\nThe current handler will be saved so Redux can restore it later."),
			_ => ReduxLanguageService.Current.Format($"Let Redux handle Nexus Mod Manager links for this Windows account?\n\nCurrent handler: {result.CurrentHandler ?? ReduxLanguageService.Current.Translate("None")}\n\nNon-BG3 links are forwarded to the previous handler when it can be invoked safely.")
		};
	}
}
