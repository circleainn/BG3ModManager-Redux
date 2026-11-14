using System;
using System.Threading.Tasks;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.ViewModels;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

public sealed class BatchInstallUiTests
{
	public void AutomaticNxmImportsOnlyAcceptNewInactivePackages()
	{
		var folder = System.IO.Directory.CreateTempSubdirectory("redux-auto-import-");
		try
		{
			var incoming = new RegressionModData { UUID = Guid.NewGuid().ToString(), Name = "Example" };
			var package = new DivinityModManager.Models.PackagePreflightReport("Example.pak", incoming, 2, 30, []);
			bool Accept(DivinityModManager.Util.ArchivePackagePreflightResult? archive = null,
				params DivinityModManager.Models.DivinityModData[] existing) =>
				MainWindowViewModel.CanAutoInstallToInactive([package], archive!, existing, folder.FullName, package.PackagePath);
			RegressionAssert.False(new DivinityModManager.Models.DivinityModManagerSettings().AutoInstallNxmToInactive);
			RegressionAssert.True(Accept());
			RegressionAssert.False(Accept(null, incoming));
			var archive = new DivinityModManager.Util.ArchivePackagePreflightResult("download.zip", 2, 100,
				[package], [], entryNames: ["Mods/Example.pak", "readme.txt"]);
			RegressionAssert.True(Accept(archive));
			RegressionAssert.False(Accept(new DivinityModManager.Util.ArchivePackagePreflightResult("hybrid.zip", 2, 100,
				[package], [], DivinityModManager.Util.ArchivePackagePreflightKind.Mixed)));
			incoming.IsForceLoaded = true;
			RegressionAssert.False(Accept());
			incoming.IsForceLoaded = false;
			System.IO.File.WriteAllText(System.IO.Path.Combine(folder.FullName, "Example.pak"), "existing file");
			RegressionAssert.False(Accept(archive));
			RegressionAssert.False(Accept());
		}
		finally { folder.Delete(true); }
	}

	public void ToolbarPrioritizesFailuresAndClearsWhenPackagesAreInstalled()
	{
		var ready = new NxmDownloadItem { State = NxmDownloadState.Downloaded };
		var attention = new NxmDownloadItem { State = NxmDownloadState.NeedsFreshLink };
		var failure = new NxmDownloadItem { State = NxmDownloadState.InstallFailed };
		RegressionAssert.Equal("Ready", MainWindowViewModel.GetDownloadManagerStatus([ready]));
		RegressionAssert.Equal("Warning", MainWindowViewModel.GetDownloadManagerStatus([ready, attention]));
		RegressionAssert.Equal("Error", MainWindowViewModel.GetDownloadManagerStatus([ready, attention, failure]));
		ready.State = NxmDownloadState.Installed;
		RegressionAssert.Equal("Normal", MainWindowViewModel.GetDownloadManagerStatus([ready]));
	}

	public void CloseRequestsCancellationAndReleasesAfterFailure()
	{
		var progress = new ReduxInstallProgressWindow(null!);
		var ran = false;
		try
		{
			progress.Run(async () =>
			{
				ran = true;
				RegressionAssert.False(progress.CancellationToken.IsCancellationRequested);
				progress.Close();
				RegressionAssert.True(progress.CancellationToken.IsCancellationRequested);
				RegressionAssert.True(progress.IsVisible);
				progress.Close();
				await progress.ReportAsync("Checking package", "Example mod", 1, 2);
				throw new InvalidOperationException("fixture failure");
			});
			throw new Exception("Expected operation failure");
		}
		catch (InvalidOperationException ex) when (ex.Message == "fixture failure") { }
		RegressionAssert.True(ran);
		RegressionAssert.False(progress.IsVisible);
	}

	public void CanceledBatchWaitsForWorkerCleanupBeforeClosing()
	{
		var progress = new ReduxInstallProgressWindow(null!);
		var cleanedUp = false;
		try
		{
			progress.Run(async () =>
			{
				try
				{
					progress.Close();
					await Task.Delay(1);
					RegressionAssert.True(progress.IsVisible);
					progress.CancellationToken.ThrowIfCancellationRequested();
				}
				finally { cleanedUp = true; }
			});
			throw new Exception("Expected cancellation");
		}
		catch (OperationCanceledException) { }
		RegressionAssert.True(cleanedUp);
		RegressionAssert.False(progress.IsVisible);
	}
}
