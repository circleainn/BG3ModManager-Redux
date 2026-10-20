#nullable disable

using System;
using System.IO;

using Microsoft.Win32;

using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class NxmAssociationTests
{
	private const string Owner = "11111111-1111-1111-1111-111111111111";
	private const string Executable = @"C:\Portable\Redux\Redux.exe";

	public void EnableAndDisableRestoresPriorUserHandler()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "MO2 marker");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone() };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.True(service.Enable().Success);
		RegressionAssert.Equal(Owner, store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(Executable), store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.True(service.Disable().Success);
		RegressionAssert.Equal("MO2 marker", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(@"C:\Tools\MO2\nxmhandler.exe", store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.Equal(null, store.Backup);
	}

	public void DisableRevealsMachineHandlerWhenNoUserHandlerExisted()
	{
		var store = new MemoryNxmRegistryStore { MachineKey = Handler(@"C:\Program Files\Vortex\Vortex.exe", null) };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.True(service.Enable().Success);
		RegressionAssert.True(service.Disable().Success);
		RegressionAssert.Equal(null, store.UserKey);
		RegressionAssert.Equal(@"C:\Program Files\Vortex\Vortex.exe", store.MachineKey.GetString(@"shell\open\command", ""));
	}

	public void RepairUpdatesOnlyOwnedMovedRegistration()
	{
		var oldExecutable = @"D:\Old Redux\Redux.exe";
		var store = new MemoryNxmRegistryStore
		{
			UserKey = NxmAssociationService.CreateOwnedKey(Owner, oldExecutable),
			Backup = new NxmAssociationBackup(false, null, @"C:\Vortex\Vortex.exe")
		};
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.Equal(NxmAssociationStatus.NeedsRepair, service.GetStatus().Status);
		RegressionAssert.True(service.Repair().Success);
		RegressionAssert.Equal(NxmAssociationStatus.Owned, service.GetStatus().Status);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(Executable), store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void DisableNeverOverwritesAnInterveningHandler()
	{
		var store = new MemoryNxmRegistryStore { UserKey = Handler(@"C:\Tools\MO2\nxmhandler.exe", null) };
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);

		store.UserKey = Handler(@"C:\Program Files\Vortex\Vortex.exe", "new owner");
		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, result.Status);
		RegressionAssert.Equal("new owner", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(@"C:\Program Files\Vortex\Vortex.exe", store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void DifferentReduxInstallationCannotRepairOrDisableOwner()
	{
		var store = new MemoryNxmRegistryStore { UserKey = NxmAssociationService.CreateOwnedKey("other-owner", @"D:\Redux\Redux.exe") };
		var service = new NxmAssociationService(store, Owner, Executable);

		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, service.GetStatus().Status);
		RegressionAssert.False(service.Repair().Success);
		RegressionAssert.False(service.Disable().Success);
		RegressionAssert.Equal("other-owner", store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
	}

	public void DifferentReduxInstallationCanBeReassociatedByExplicitTakeover()
	{
		var previous = NxmAssociationService.CreateOwnedKey("22222222-2222-2222-2222-222222222222", @"D:\Old Redux\Redux.exe");
		var store = new MemoryNxmRegistryStore { UserKey = previous.Clone() };
		var service = new NxmAssociationService(store, Owner, Executable);

		var result = service.TakeOver();

		RegressionAssert.True(result.Success);
		RegressionAssert.Equal(NxmAssociationStatus.Owned, result.Status);
		RegressionAssert.Equal(Owner, store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(Executable), store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.Equal("22222222-2222-2222-2222-222222222222",
			store.Backup.UserKey.GetString("", NxmAssociationService.OwnerValueName));
	}

	public void RegistrySnapshotPreservesValueKindsAndSubkeys()
	{
		var prior = Handler(@"C:\Handler.exe", null);
		prior.SetValue("", "Number", 42, RegistryValueKind.DWord);
		prior.SetValue("metadata", "Bytes", new byte[] { 1, 2, 3 }, RegistryValueKind.Binary);
		var clone = prior.Clone();

		RegressionAssert.Equal(RegistryValueKind.DWord, clone.GetValue("", "Number").Kind);
		RegressionAssert.Equal(42, clone.GetValue("", "Number").Value);
		RegressionAssert.Equal(RegistryValueKind.Binary, clone.GetValue("metadata", "Bytes").Kind);
		RegressionAssert.SequenceEqual(new byte[] { 1, 2, 3 }, (byte[])clone.GetValue("metadata", "Bytes").Value);
	}

	public void ProductionRegistryStoreRoundTripsOnlyDisposableHkcuPaths()
	{
		var root = $@"Software\BG3MMReduxTests\{Guid.NewGuid():N}";
		try
		{
			var store = new NxmRegistryStore($@"{root}\nxm", $@"{root}\backups");
			var prior = Handler(@"C:\Handler.exe", "marker");
			prior.SetValue("metadata", "Number", 42, RegistryValueKind.DWord);
			store.WriteUserKey(prior);
			store.WriteBackup(Owner, new NxmAssociationBackup(true, prior, @"C:\Handler.exe"));

			RegressionAssert.Equal("marker", store.ReadUserKey().GetString("", "CustomValue"));
			RegressionAssert.Equal(42, store.ReadBackup(Owner).UserKey.GetValue("metadata", "Number").Value);

			store.DeleteUserKey();
			store.DeleteBackup(Owner);
			RegressionAssert.Equal(null, store.ReadUserKey());
			RegressionAssert.Equal(null, store.ReadBackup(Owner));
		}
		finally
		{
			Registry.CurrentUser.DeleteSubKeyTree(root, false);
		}
	}

	public void FailedEnableRestoresPriorHandler()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "before");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone(), FailNextUserWrite = true };
		var service = new NxmAssociationService(store, Owner, Executable);

		var result = service.Enable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal("before", store.UserKey.GetString("", "CustomValue"));
		RegressionAssert.Equal(null, store.Backup);
	}

	public void FailedDisableKeepsOwnedHandlerAndBackup()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "before");
		var store = new MemoryNxmRegistryStore { UserKey = prior.Clone() };
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.FailNextUserWrite = true;

		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(Owner, store.UserKey.GetString("", NxmAssociationService.OwnerValueName));
		RegressionAssert.True(store.Backup != null);
	}

	public void ReduxShapedInterveningCommandIsNotOwned()
	{
		var store = new MemoryNxmRegistryStore();
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.UserKey.SetValue(@"shell\open\command", "", NxmAssociationService.BuildCommand(@"C:\Other\Redux.exe"), RegistryValueKind.String);

		var result = service.Disable();

		RegressionAssert.False(result.Success);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(@"C:\Other\Redux.exe"),
			store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void MissingExecutableMarkerIsAnOwnershipConflict()
	{
		var store = new MemoryNxmRegistryStore();
		var service = new NxmAssociationService(store, Owner, Executable);
		RegressionAssert.True(service.Enable().Success);
		store.UserKey.Values.Remove(NxmAssociationService.ExecutableValueName);

		var result = service.GetStatus();

		RegressionAssert.True(result.Success);
		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, result.Status);
	}

	public void ChangedCommandWithStaleMarkerCanOnlyBeReclaimedExplicitly()
	{
		foreach (var command in new[] { "\"C:\\Vortex\\Vortex.exe\" \"%1\"", NxmAssociationService.BuildCommand(@"C:\Other\Redux.exe") })
		{
			var store = new MemoryNxmRegistryStore();
			var service = new NxmAssociationService(store, Owner, Executable);
			RegressionAssert.True(service.Enable().Success);
			store.UserKey.SetValue(@"shell\open\command", "", command, RegistryValueKind.String);
			RegressionAssert.True(service.GetStatus().Success);
			RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, service.GetStatus().Status);
			RegressionAssert.False(service.Enable().Success);
			RegressionAssert.False(service.Repair().Success);
			RegressionAssert.False(service.Disable().Success);
			RegressionAssert.Equal(command, store.UserKey.GetString(@"shell\open\command", ""));
			RegressionAssert.True(service.TakeOver().Success);
			RegressionAssert.Equal(command, store.Backup.UserKey.GetString(@"shell\open\command", ""));
			RegressionAssert.True(service.Disable().Success);
			RegressionAssert.Equal(command, store.UserKey.GetString(@"shell\open\command", ""));
		}
	}

	public void PreviousHandlerCommandIsParsedWithoutShell()
	{
		var executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
		var command = $"\"{executable}\" --from-nexus \"%1\"";
		const string link = "nxm://skyrim/mods/42/files/99?key=secret";

		RegressionAssert.True(NxmPreviousHandlerForwarder.TryCreateStartInfo(command, link,
			@"C:\Redux\Redux.exe", out var startInfo, out _));
		RegressionAssert.Equal(executable, startInfo.FileName);
		RegressionAssert.False(startInfo.UseShellExecute);
		RegressionAssert.SequenceEqual(new[] { "--from-nexus", link }, startInfo.ArgumentList);
	}

	public void DotNetHostedAssociationRecordsAppAndHostAndRepairsMovedHosts()
	{
		var prior = Handler(@"C:\Tools\MO2\nxmhandler.exe", "prior");
		var store = new MemoryNxmRegistryStore { UserKey = prior };
		var launch = ReduxLaunchTarget.Resolve(@"C:\Program Files\dotnet\dotnet.exe", @"D:\Mod Tools & Games\Redux\Redux.dll", @"D:\Mod Tools & Games\Redux");
		var service = new NxmAssociationService(store, Owner, launch);
		RegressionAssert.True(service.Enable().Success);
		RegressionAssert.Equal(NxmAssociationStatus.Owned, service.GetStatus().Status);
		RegressionAssert.Equal(launch.ApplicationPath, store.UserKey.GetString("", NxmAssociationService.ExecutableValueName));
		RegressionAssert.Equal(launch.ExecutablePath, store.UserKey.GetString("", NxmAssociationService.DotNetHostValueName));
		RegressionAssert.Equal("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"D:\\Mod Tools & Games\\Redux\\Redux.dll\" --nxm \"%1\"",
			store.UserKey.GetString(@"shell\open\command", ""));
		var moved = ReduxLaunchTarget.ForManagedApplication(launch.ApplicationPath, @"E:\Windows Runtime\dotnet.exe");
		var newService = new NxmAssociationService(store, Owner, moved);
		RegressionAssert.Equal(NxmAssociationStatus.NeedsRepair, newService.GetStatus().Status);
		RegressionAssert.True(newService.Repair().Success);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(moved), store.UserKey.GetString(@"shell\open\command", ""));
		RegressionAssert.True(newService.Disable().Success);
		RegressionAssert.Equal("prior", store.UserKey.GetString("", "CustomValue"));
	}

	public void HostedAssociationNeverClaimsChangedApplicationHostOrArguments()
	{
		var launch = ReduxLaunchTarget.ForManagedApplication(@"C:\Redux\Redux.dll", @"C:\Runtime\dotnet.exe");
		foreach (var changedCommand in new[]
		{
			NxmAssociationService.BuildCommand(ReduxLaunchTarget.ForManagedApplication(@"C:\Other\Redux.dll", launch.ExecutablePath)),
			NxmAssociationService.BuildCommand(ReduxLaunchTarget.ForManagedApplication(launch.ApplicationPath, @"C:\Other Runtime\dotnet.exe")),
			NxmAssociationService.BuildCommand(launch) + " --changed"
		})
		{
			var store = new MemoryNxmRegistryStore();
			var service = new NxmAssociationService(store, Owner, launch);
			RegressionAssert.True(service.Enable().Success);
			store.UserKey.SetValue(@"shell\open\command", "", changedCommand, RegistryValueKind.String);
			RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, service.GetStatus().Status);
			RegressionAssert.False(service.Disable().Success);
			RegressionAssert.False(service.Repair().Success);
			RegressionAssert.Equal(changedCommand, store.UserKey.GetString(@"shell\open\command", ""));
		}
		var missingHost = new MemoryNxmRegistryStore { UserKey = NxmAssociationService.CreateOwnedKey(Owner, launch) };
		missingHost.UserKey.Values.Remove(NxmAssociationService.DotNetHostValueName);
		RegressionAssert.Equal(NxmAssociationStatus.OwnedByAnotherHandler, new NxmAssociationService(missingHost, Owner, launch).GetStatus().Status);
	}

	public void OldHostOnlyRegistrationCanBeRepairedWithoutReplacingItsBackup()
	{
		var launch = ReduxLaunchTarget.ForManagedApplication(@"C:\Redux\Redux.dll", @"C:\Runtime\dotnet.exe");
		var store = new MemoryNxmRegistryStore
		{
			UserKey = NxmAssociationService.CreateOwnedKey(Owner, launch.ExecutablePath),
			Backup = new NxmAssociationBackup(false, null, "original handler")
		};
		var service = new NxmAssociationService(store, Owner, launch);
		RegressionAssert.Equal(NxmAssociationStatus.NeedsRepair, service.GetStatus().Status);
		RegressionAssert.True(service.Repair().Success);
		RegressionAssert.Equal("original handler", store.Backup.PreviousCommand);
		RegressionAssert.Equal(NxmAssociationService.BuildCommand(launch), store.UserKey.GetString(@"shell\open\command", ""));
	}

	public void HostedPreviousHandlerPreservesSignedLinkAndRejectsOnlyApplicationRecursion()
	{
		var root = Path.Combine(Path.GetTempPath(), "Redux host & quoting " + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var host = Path.Combine(root, "dotnet.exe");
			File.WriteAllText(host, "fake host; never executed");
			var current = ReduxLaunchTarget.ForManagedApplication(Path.Combine(root, "Redux.dll"), host);
			var previous = ReduxLaunchTarget.ForManagedApplication(Path.Combine(root, "Other Handler.dll"), host);
			const string signed = "nxm://skyrim/mods/42/files/99?key=secret%2Bvalue%3D&expires=123&user_id=456";
			RegressionAssert.True(NxmPreviousHandlerForwarder.TryCreateStartInfo(NxmAssociationService.BuildCommand(previous), signed,
				current, out var start, out _));
			RegressionAssert.Equal(host, start.FileName);
			RegressionAssert.False(start.UseShellExecute);
			RegressionAssert.SequenceEqual(new[] { previous.ApplicationPath, "--nxm", signed }, start.ArgumentList);
			RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(NxmAssociationService.BuildCommand(current), signed,
				current, out _, out _));
			var native = Path.Combine(root, "Redux.exe");
			File.WriteAllText(native, "fake apphost; never executed");
			RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(NxmAssociationService.BuildCommand(native), signed,
				current, out _, out _));
		}
		finally { Directory.Delete(root, recursive: true); }
	}

	public void PreviousHandlerRejectsEmbeddedPlaceholderAndReduxRecursion()
	{
		var executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
		RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(
			$"\"{executable}\" --url=%1", "nxm://skyrim/mods/42/files/99", @"C:\Redux\Redux.exe", out _, out _));
		RegressionAssert.False(NxmPreviousHandlerForwarder.TryCreateStartInfo(
			"\"C:\\Redux\\Redux.exe\" \"%1\"", "nxm://skyrim/mods/42/files/99", @"C:\Redux\Redux.exe", out _, out _));
	}

	private static NxmRegistryKeySnapshot Handler(string command, string marker)
	{
		var key = new NxmRegistryKeySnapshot();
		key.SetValue(@"shell\open\command", "", command, RegistryValueKind.String);
		if (marker != null) key.SetValue("", "CustomValue", marker, RegistryValueKind.String);
		return key;
	}

	private sealed class MemoryNxmRegistryStore : INxmRegistryStore
	{
		public NxmRegistryKeySnapshot UserKey { get; set; }
		public NxmRegistryKeySnapshot MachineKey { get; set; }
		public NxmAssociationBackup Backup { get; set; }
		public bool FailNextUserWrite { get; set; }

		public NxmRegistryKeySnapshot ReadUserKey() => UserKey?.Clone();
		public NxmRegistryKeySnapshot ReadMachineKey() => MachineKey?.Clone();
		public void WriteUserKey(NxmRegistryKeySnapshot key)
		{
			if (FailNextUserWrite)
			{
				FailNextUserWrite = false;
				UserKey = null;
				throw new IOException("simulated registry write failure");
			}
			UserKey = key?.Clone();
		}
		public void DeleteUserKey() => UserKey = null;
		public NxmAssociationBackup ReadBackup(string ownerId) => Backup?.DeepCopy();
		public void WriteBackup(string ownerId, NxmAssociationBackup backup) => Backup = backup?.DeepCopy();
		public void DeleteBackup(string ownerId) => Backup = null;
	}
}
