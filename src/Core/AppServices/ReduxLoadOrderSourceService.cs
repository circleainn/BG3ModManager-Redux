using DivinityModManager.Models;
using DivinityModManager.Models.Modio;
using DivinityModManager.Models.NexusMods;

namespace DivinityModManager.AppServices;

/// <summary>
/// Converts the currently selected public source for a mod into the small,
/// portable record used by a Redux modlist. Source links are deliberately
/// separate from load-order import so the recipient must opt in before local
/// provider associations are changed.
/// </summary>
public static class ReduxLoadOrderSourceService
{
	public static ReduxLoadOrderSourceLink CreatePortableLink(DivinityModData mod)
	{
		if (mod == null || String.IsNullOrWhiteSpace(mod.UUID)) return null;

		var nexus = mod.NexusModsData;
		var nexusIsExplicit = nexus?.MetadataOrigin is NexusMetadataOrigin.Manual
			or NexusMetadataOrigin.NexusArchiveImport
			or NexusMetadataOrigin.ReduxBundleImport;
		if (nexusIsExplicit && nexus.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START)
			return CreateNexusLink(mod.UUID, nexus);

		if (mod.ModioData?.HasMetadata == true)
			return CreateModioLink(mod.UUID, mod.ModioData);

		return nexus?.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START
			? CreateNexusLink(mod.UUID, nexus)
			: null;
	}

	public static void ApplyToInstalledMod(DivinityModData mod, ReduxLoadOrderSourceLink link)
	{
		if (mod == null || link == null ||
			!String.Equals(mod.UUID, link.ModUuid, StringComparison.OrdinalIgnoreCase))
			return;

		if (String.Equals(link.Provider, ReduxLoadOrderSourceLink.NexusProvider, StringComparison.Ordinal))
		{
			mod.ModioData = new ModioModData { UUID = mod.UUID };
			mod.NexusModsData.ResetSourceAssociation();
			mod.NexusModsData.Update(CreateNexusMetadata(link));
		}
		else if (String.Equals(link.Provider, ReduxLoadOrderSourceLink.ModioProvider, StringComparison.Ordinal))
		{
			mod.NexusModsData.ResetSourceAssociation();
			mod.ModioData = CreateModioMetadata(link);
		}
	}

	public static NexusModsModData CreateNexusMetadata(ReduxLoadOrderSourceLink link) => new()
	{
		UUID = link.ModUuid,
		ModId = link.ProjectId,
		LastFileId = link.FileId,
		Name = link.Name,
		Author = link.Author,
		UploadedBy = link.Uploader,
		Version = link.Version,
		CategoryId = link.CategoryId,
		Available = true,
		MetadataOrigin = NexusMetadataOrigin.ReduxBundleImport
	};

	public static ModioModData CreateModioMetadata(ReduxLoadOrderSourceLink link) => new()
	{
		UUID = link.ModUuid,
		ModId = link.ProjectId,
		Name = link.Name,
		ProfileUrl = link.PageUrl,
		SubmittedBy = String.IsNullOrWhiteSpace(link.Author)
			? null
			: new ModioUserData { DisplayName = link.Author },
		ModFile = link.FileId > 0 || !String.IsNullOrWhiteSpace(link.Version)
			? new ModioFileData { FileId = Math.Max(0, link.FileId), Version = link.Version }
			: null,
		MetadataOrigin = ModioMetadataOrigin.ReduxBundleImport
	};

	private static ReduxLoadOrderSourceLink CreateNexusLink(string uuid, NexusModsModData data) => new()
	{
		ModUuid = uuid,
		Provider = ReduxLoadOrderSourceLink.NexusProvider,
		ProjectId = data.ModId,
		FileId = data.LastFileId,
		Name = data.Name ?? String.Empty,
		Author = data.Author ?? String.Empty,
		Uploader = data.UploadedBy ?? String.Empty,
		Version = data.Version ?? String.Empty,
		PageUrl = data.SourcePageUrl,
		CategoryId = data.CategoryId
	};

	private static ReduxLoadOrderSourceLink CreateModioLink(string uuid, ModioModData data) => new()
	{
		ModUuid = uuid,
		Provider = ReduxLoadOrderSourceLink.ModioProvider,
		ProjectId = data.ModId,
		FileId = data.ModFile?.FileId ?? -1,
		Name = data.Name ?? String.Empty,
		Author = data.Author ?? String.Empty,
		Version = data.Version ?? String.Empty,
		PageUrl = NormalizePublicPageUrl(data.SourcePageUrl, ReduxLoadOrderSourceLink.ModioProvider)
	};

	private static string NormalizePublicPageUrl(string url, string provider)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
			!String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
			!uri.IsDefaultPort || !String.IsNullOrWhiteSpace(uri.UserInfo))
			return String.Empty;

		var hostIsValid = provider switch
		{
			ReduxLoadOrderSourceLink.NexusProvider =>
				uri.Host.Equals("nexusmods.com", StringComparison.OrdinalIgnoreCase) ||
				uri.Host.EndsWith(".nexusmods.com", StringComparison.OrdinalIgnoreCase),
			ReduxLoadOrderSourceLink.ModioProvider =>
				uri.Host.Equals("mod.io", StringComparison.OrdinalIgnoreCase) ||
				uri.Host.EndsWith(".mod.io", StringComparison.OrdinalIgnoreCase),
			_ => false
		};
		return hostIsValid ? uri.GetLeftPart(UriPartial.Path) : String.Empty;
	}
}
