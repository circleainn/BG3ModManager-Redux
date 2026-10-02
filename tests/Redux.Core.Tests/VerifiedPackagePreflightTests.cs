using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Models.Health;
using DivinityModManager.Util;

using DynamicData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Redux.Core.Tests;

public sealed class VerifiedPackagePreflightTests
{
	public void VerifiedLeaseAllowsReadersAndBlocksChangesUntilDisposed()
	{
		WithPackage((path, contents) =>
		{
			using var lease = VerifiedPackageReadLease.OpenAsync(path, contents.Length, Digest(contents)).GetAwaiter().GetResult();
			lease.RequirePath(path);
			using (var reader = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				RegressionAssert.Equal((long)contents.Length, reader.Length);
			RegressionAssert.Throws<IOException>(() =>
			{
				using var writer = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
			});
			RegressionAssert.Throws<IOException>(() => File.Delete(path));
			RegressionAssert.Throws<ArgumentException>(() => lease.RequirePath(path + ".other"));

			lease.Dispose();
			RegressionAssert.Throws<ObjectDisposedException>(() => lease.RequirePath(path));
			File.Delete(path);
			RegressionAssert.False(File.Exists(path));
		});
	}

	public void InvalidOrCanceledVerificationReleasesThePackageHandle()
	{
		WithPackage((path, contents) =>
		{
			var changed = contents.ToArray();
			changed[0] ^= 1;
			RegressionAssert.Throws<InvalidDataException>(() => VerifiedPackageReadLease
				.OpenAsync(path, contents.Length, Digest(changed)).GetAwaiter().GetResult());
			AssertExclusiveAccess(path);
			RegressionAssert.Throws<InvalidDataException>(() => VerifiedPackageReadLease
				.OpenAsync(path, contents.Length + 1, Digest(contents)).GetAwaiter().GetResult());
			AssertExclusiveAccess(path);
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();
			RegressionAssert.Throws<OperationCanceledException>(() => VerifiedPackageReadLease
				.OpenAsync(path, contents.Length, Digest(contents), cancellation.Token).GetAwaiter().GetResult());
			AssertExclusiveAccess(path);
		});
	}

	public void PackageChangedBetweenReviewAndInstallationIsRejected()
	{
		WithPackage((path, contents) =>
		{
			var reviewedDigest = Digest(contents);
			using (var reviewLease = VerifiedPackageReadLease.OpenAsync(path, contents.Length, reviewedDigest).GetAwaiter().GetResult())
				reviewLease.RequirePath(path);
			// Review releases each batch input; a same-length replacement must not be
			// accepted when that package reaches installation later in the batch.
			var replacement = contents.ToArray();
			replacement[0] ^= 1;
			File.WriteAllBytes(path, replacement);
			RegressionAssert.Throws<InvalidDataException>(() => VerifiedPackageReadLease
				.OpenAsync(path, contents.Length, reviewedDigest).GetAwaiter().GetResult());
			AssertExclusiveAccess(path);

			File.WriteAllBytes(path, contents);
			using (var installLease = VerifiedPackageReadLease.OpenAsync(path, contents.Length, reviewedDigest).GetAwaiter().GetResult())
			{
				var incoming = CreateMod("1f06c8c3-f582-4d29-8f91-638d3d3c9e22", "Incoming");
				var report = PackagePreflightService.AnalyzeLoadedPackage(path, incoming, []);
				var prepared = new LeasedArchivePreflight(installLease, path, [report]);
				RegressionAssert.Equal(1, prepared.RecheckPackages([]).Count);
				RegressionAssert.Throws<IOException>(() => File.Delete(path));
			}
			File.Delete(path);
			RegressionAssert.False(File.Exists(path));
		});
	}

	public void ReusedArchiveMetadataRefreshesDependenciesAndPreservesSourceDetails()
	{
		WithPackage((path, contents) =>
		{
			using var lease = VerifiedPackageReadLease.OpenAsync(path, contents.Length, Digest(contents)).GetAwaiter().GetResult();
			var incoming = CreateMod("1f06c8c3-f582-4d29-8f91-638d3d3c9e22", "Incoming");
			var required = CreateMod("710f439a-fbb3-4c08-a124-571905f0d60f", "Required");
			incoming.Dependencies.AddOrUpdate(new ModuleShortDesc { UUID = required.UUID, Name = required.Name });
			var sourcePath = path + "::Packages/Incoming.pak";
			var report = PackagePreflightService.AnalyzeLoadedPackage("removed-staging-file.pak", incoming, [])
				.WithSource(sourcePath, 987654321);
			var inspection = new ArchivePackagePreflightResult(path, 1, contents.Length, [report], []);
			var prepared = new LeasedArchivePreflight(lease, inspection);

			var missing = prepared.RecheckPackages([]).Single();
			RegressionAssert.True(missing.Findings.Any(finding => finding.Title == "Missing dependency"));
			var available = prepared.RecheckPackages([required]).Single();
			RegressionAssert.False(available.Findings.Any(finding => finding.Title == "Missing dependency"));
			RegressionAssert.Equal(sourcePath, available.PackagePath);
			RegressionAssert.Equal(987654321L, available.PackageSize);
			RegressionAssert.Equal(2, available.InternalFileCount);
			RegressionAssert.True(prepared.RecheckPackages([]).Single().Findings
				.Any(finding => finding.Title == "Missing dependency"));

			lease.Dispose();
			RegressionAssert.Throws<ObjectDisposedException>(() => prepared.RecheckPackages([]));
		});
	}

	public void ReusedPreflightRejectsAnotherArchiveAndKeepsUnreadableFindings()
	{
		WithPackage((path, contents) =>
		{
			using var lease = VerifiedPackageReadLease.OpenAsync(path, contents.Length, Digest(contents)).GetAwaiter().GetResult();
			var unreadable = new PackagePreflightReport(path + "::Broken.pak", null!, 0, 17,
				[new PackagePreflightFinding(ModHealthSeverity.Error, "Unreadable package", "Original inspection failure")]);
			var inspection = new ArchivePackagePreflightResult(path, 1, contents.Length, [unreadable], []);
			var prepared = new LeasedArchivePreflight(lease, inspection);
			RegressionAssert.True(ReferenceEquals(unreadable, prepared.RecheckPackages([]).Single()));
			RegressionAssert.Throws<ArgumentException>(() => new LeasedArchivePreflight(lease,
				new ArchivePackagePreflightResult(path + ".other", 1, contents.Length, [unreadable], [])));
		});
	}

	private static DivinityModData CreateMod(string uuid, string name) => new()
	{
		FilePath = name + ".pak",
		HasMetadata = true,
		Name = name,
		Folder = name,
		UUID = uuid,
		Author = "Test author",
		Version = DivinityModVersion2.FromInt(1),
		Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			$"Mods/{name}/meta.lsx",
			$"Public/{name}/Stats/Generated/Data/example.txt"
		}
	};

	private static string Digest(byte[] contents) => Convert.ToHexString(SHA256.HashData(contents));

	private static void AssertExclusiveAccess(string path)
	{
		using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	private static void WithPackage(Action<string, byte[]> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxVerifiedPreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		var path = Path.Combine(directory, "Package.zip");
		var contents = Encoding.UTF8.GetBytes("Verified package contents for lease tests");
		File.WriteAllBytes(path, contents);
		try { action(path, contents); }
		finally { Directory.Delete(directory, true); }
	}
}
