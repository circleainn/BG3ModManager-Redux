using DivinityModManager.Models;
using DivinityModManager.Models.Health;

using LSLib.LS;

namespace DivinityModManager.AppServices;

/// <summary>Checks package layouts that the single-file installer cannot preserve.</summary>
public static class PakImportCompatibility
{
	public const string MultipartFindingTitle = "Multipart PAK installation is not supported";

	public static PackagePreflightFinding GetUnsupportedLayoutFinding(string packagePath, string displayFileName = null)
	{
		uint partCount;
		try
		{
			// Read only the header: sibling parts need not exist at the temporary import path.
			using var package = new PackageReader().Read(packagePath, metadataOnly: true);
			partCount = package.Metadata.NumParts;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
			or NotAPackageException or ArgumentException)
		{
			// Malformed/unreadable PAKs retain the package loader's existing validation failure.
			return null;
		}

		if (partCount <= 1) return null;
		var name = displayFileName ?? Path.GetFileName(packagePath);
		return new PackagePreflightFinding(
			ModHealthSeverity.Error,
			MultipartFindingTitle,
			$"'{name}' is a multipart PAK that requires {partCount} files. Redux cannot install multipart PAK sets yet. "
			+ "Use a single-file PAK version, or follow the mod author's manual installation instructions and keep all parts together.");
	}

	public static void RequireSupportedLayout(string packagePath, string displayFileName = null)
	{
		var finding = GetUnsupportedLayoutFinding(packagePath, displayFileName);
		if (finding != null) throw new InvalidDataException(finding.Message);
	}
}
