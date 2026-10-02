using DivinityModManager.AppServices;
using DivinityModManager.Models;

namespace DivinityModManager.Util;

/// <summary>Reuses parsed metadata only while its source package remains locked.</summary>
public sealed class LeasedArchivePreflight
{
	private readonly VerifiedPackageReadLease _lease;
	private readonly string _sourcePath;
	private readonly IReadOnlyList<PackagePreflightReport> _packages;

	public LeasedArchivePreflight(VerifiedPackageReadLease lease, ArchivePackagePreflightResult inspection)
		: this(lease, (inspection ?? throw new ArgumentNullException(nameof(inspection))).ArchivePath, inspection.Packages) { }

	public LeasedArchivePreflight(VerifiedPackageReadLease lease, string sourcePath,
		IReadOnlyList<PackagePreflightReport> packages)
	{
		ArgumentNullException.ThrowIfNull(lease);
		ArgumentNullException.ThrowIfNull(packages);
		lease.RequirePath(sourcePath);
		_lease = lease;
		_sourcePath = sourcePath;
		_packages = packages;
	}

	public void RequirePath(string path) => _lease.RequirePath(path);

	public IReadOnlyList<PackagePreflightReport> RecheckPackages(IEnumerable<DivinityModData> installedMods)
	{
		_lease.RequirePath(_sourcePath);
		var installed = installedMods.ToArray();
		return _packages.Select(report => report.IsReadable
			? PackagePreflightService.AnalyzeLoadedPackage(report.PackagePath, report.Mod, installed)
				.WithSource(report.PackagePath, report.PackageSize)
			: report).ToArray();
	}
}
