using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using LSLib.LS;

namespace DivinityModManager.AppServices;

/// <summary>Checks that every part required by a package is available.</summary>
public static class PakImportCompatibility
{
	public const string MultipartFindingTitle = "Incomplete multipart PAK";

	public static PackagePreflightFinding GetUnsupportedLayoutFinding(string packagePath, string displayFileName = null)
	{
		try
		{
			PakFileSet.GetPaths(packagePath);
			return null;
		}
		catch (InvalidDataException ex)
		{
			return new PackagePreflightFinding(ModHealthSeverity.Error, MultipartFindingTitle, ex.Message);
		}
	}

	public static void RequireSupportedLayout(string packagePath, string displayFileName = null)
	{
		var finding = GetUnsupportedLayoutFinding(packagePath, displayFileName);
		if (finding != null) throw new InvalidDataException(finding.Message);
	}
}
