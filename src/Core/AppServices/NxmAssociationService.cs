using Microsoft.Win32;

namespace DivinityModManager.AppServices;

public interface INxmAssociationService
{
	NxmAssociationResult GetStatus();
	NxmAssociationResult Enable();
	NxmAssociationResult TakeOver();
	NxmAssociationResult Repair();
	NxmAssociationResult Disable();
}

public sealed class NxmAssociationService : INxmAssociationService
{
	public const string OwnerValueName = "BG3ModManagerReduxOwner";
	public const string ExecutableValueName = "BG3ModManagerReduxExecutable";
	public const string DotNetHostValueName = "BG3ModManagerReduxDotNetHost";
	private const string CommandSubkey = @"shell\open\command";

	private readonly INxmRegistryStore _store;
	private readonly string _ownerId;
	private readonly ReduxLaunchTarget _launchTarget;

	public NxmAssociationService(INxmRegistryStore store, string ownerId, string executablePath)
		: this(store, ownerId, ReduxLaunchTarget.ForExecutable(executablePath)) { }

	public NxmAssociationService(INxmRegistryStore store, string ownerId, ReduxLaunchTarget launchTarget)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_ownerId = Guid.TryParseExact(ownerId, "D", out var parsedOwner)
			? parsedOwner.ToString("D")
			: throw new ArgumentException("A canonical association owner GUID is required.", nameof(ownerId));
		_launchTarget = launchTarget ?? throw new ArgumentNullException(nameof(launchTarget));
	}

	public NxmAssociationResult GetStatus()
	{
		try
		{
			var userKey = _store.ReadUserKey();
			var effectiveKey = userKey ?? _store.ReadMachineKey();
			var currentCommand = effectiveKey?.GetString(CommandSubkey, "");
			var owner = userKey?.GetString("", OwnerValueName);
			if (!String.Equals(owner, _ownerId, StringComparison.Ordinal))
			{
				var status = String.IsNullOrEmpty(owner) ? NxmAssociationStatus.Available : NxmAssociationStatus.OwnedByAnotherHandler;
				return new NxmAssociationResult(true, status, "Redux does not own the current NXM association.", currentCommand);
			}

			if (!TryGetOwnedLaunch(userKey, currentCommand, out var registered))
			{
				return new NxmAssociationResult(true, NxmAssociationStatus.OwnedByAnotherHandler,
					"The NXM command changed after Redux registered it.", currentCommand);
			}

			var healthy = PathsEqual(registered.ApplicationPath, _launchTarget.ApplicationPath)
				&& PathsEqual(registered.ExecutablePath, _launchTarget.ExecutablePath)
				&& registered.IsDotNetHosted == _launchTarget.IsDotNetHosted;
			return new NxmAssociationResult(true,
				healthy ? NxmAssociationStatus.Owned : NxmAssociationStatus.NeedsRepair,
				healthy ? "Redux handles Nexus Mod Manager links." : "The Redux NXM association points to a previous executable location.",
				currentCommand);
		}
		catch (Exception ex) when (IsRegistryFailure(ex))
		{
			return new NxmAssociationResult(false, NxmAssociationStatus.OwnedByAnotherHandler,
				"Windows did not allow Redux to read the Nexus link association.");
		}
	}

	public NxmAssociationResult Enable()
	{
		var status = GetStatus();
		if (!status.Success) return status;
		if (status.Status == NxmAssociationStatus.Owned) return status;
		if (status.Status == NxmAssociationStatus.NeedsRepair) return Repair();
		if (status.Status == NxmAssociationStatus.OwnedByAnotherHandler)
			return new NxmAssociationResult(false, status.Status, "Another Redux installation or application changed the marked association.", status.CurrentHandler);

		return ReplaceCurrentAssociation(status);
	}

	public NxmAssociationResult TakeOver()
	{
		var status = GetStatus();
		if (!status.Success) return status;
		if (status.Status == NxmAssociationStatus.Owned) return status;
		if (status.Status == NxmAssociationStatus.NeedsRepair) return Repair();
		if (status.Status != NxmAssociationStatus.OwnedByAnotherHandler)
			return Enable();

		return ReplaceCurrentAssociation(status);
	}

	private NxmAssociationResult ReplaceCurrentAssociation(NxmAssociationResult status)
	{
		var userKey = _store.ReadUserKey();
		var effectiveKey = userKey ?? _store.ReadMachineKey();
		var backup = new NxmAssociationBackup(userKey != null, userKey, effectiveKey?.GetString(CommandSubkey, ""));
		try
		{
			_store.WriteBackup(_ownerId, backup);
			_store.WriteUserKey(CreateOwnedKey(_ownerId, _launchTarget));
			return GetStatus();
		}
		catch (Exception ex) when (IsRegistryFailure(ex))
		{
			var restored = TryRestoreUserKey(userKey);
			try { _store.DeleteBackup(_ownerId); } catch (Exception cleanupError) when (IsRegistryFailure(cleanupError)) { }
			return new NxmAssociationResult(false, NxmAssociationStatus.Available,
				restored
					? "Windows could not update the Nexus link association. The previous handler was retained."
					: "Windows could not update or restore the Nexus link association. Check the Windows default-app setting.",
				status.CurrentHandler);
		}
	}

	public NxmAssociationResult Repair()
	{
		var status = GetStatus();
		if (!status.Success) return status;
		if (status.Status == NxmAssociationStatus.Owned) return status;
		if (status.Status != NxmAssociationStatus.NeedsRepair)
			return new NxmAssociationResult(false, status.Status, "Redux cannot repair an association it no longer owns.", status.CurrentHandler);

		var current = _store.ReadUserKey();
		try
		{
			_store.WriteUserKey(CreateOwnedKey(_ownerId, _launchTarget));
			return GetStatus();
		}
		catch (Exception ex) when (IsRegistryFailure(ex))
		{
			var restored = TryRestoreUserKey(current);
			return new NxmAssociationResult(false, NxmAssociationStatus.NeedsRepair,
				restored
					? "Windows could not repair the Nexus link association. The previous registration was retained."
					: "Windows could not repair or restore the Nexus link association. Check the Windows default-app setting.",
				status.CurrentHandler);
		}
	}

	public NxmAssociationResult Disable()
	{
		var status = GetStatus();
		if (!status.Success) return status;
		if (status.Status is not (NxmAssociationStatus.Owned or NxmAssociationStatus.NeedsRepair))
			return new NxmAssociationResult(false, NxmAssociationStatus.OwnedByAnotherHandler,
				"Redux cannot restore an association it no longer owns.", status.CurrentHandler);

		var backup = _store.ReadBackup(_ownerId);
		if (backup == null)
			return new NxmAssociationResult(false, status.Status, "The previous NXM association snapshot is unavailable.", status.CurrentHandler);

		var ownedKey = _store.ReadUserKey();
		try
		{
			_store.DeleteUserKey();
			if (backup.HadUserKey && backup.UserKey != null) _store.WriteUserKey(backup.UserKey);
			_store.DeleteBackup(_ownerId);
			var restored = _store.ReadUserKey() ?? _store.ReadMachineKey();
			return new NxmAssociationResult(true, NxmAssociationStatus.Available,
				"The previous Nexus Mod Manager link handler was restored.", restored?.GetString(CommandSubkey, ""));
		}
		catch (Exception ex) when (IsRegistryFailure(ex))
		{
			var restored = TryRestoreUserKey(ownedKey);
			return new NxmAssociationResult(false, status.Status,
				restored
					? "Windows could not restore the previous handler. Redux kept its association and recovery snapshot."
					: "Windows could not restore either handler. The recovery snapshot was kept; check the Windows default-app setting.",
				status.CurrentHandler);
		}
	}

	public static string BuildCommand(string executablePath) => BuildCommand(ReduxLaunchTarget.ForExecutable(executablePath));
	public static string BuildCommand(ReduxLaunchTarget launchTarget) => $"{launchTarget.CommandPrefix} --nxm \"%1\"";

	public static NxmRegistryKeySnapshot CreateOwnedKey(string ownerId, string executablePath) =>
		CreateOwnedKey(ownerId, ReduxLaunchTarget.ForExecutable(executablePath));

	public static NxmRegistryKeySnapshot CreateOwnedKey(string ownerId, ReduxLaunchTarget launchTarget)
	{
		var key = new NxmRegistryKeySnapshot();
		key.SetValue("", "", "URL:Nexus Mods Protocol", RegistryValueKind.String);
		key.SetValue("", "URL Protocol", String.Empty, RegistryValueKind.String);
		key.SetValue("", OwnerValueName, ownerId, RegistryValueKind.String);
		key.SetValue("", ExecutableValueName, launchTarget.ApplicationPath, RegistryValueKind.String);
		if (launchTarget.IsDotNetHosted)
			key.SetValue("", DotNetHostValueName, launchTarget.DotNetHostPath, RegistryValueKind.String);
		key.SetValue(CommandSubkey, "", BuildCommand(launchTarget), RegistryValueKind.String);
		return key;
	}

	private static bool TryGetOwnedLaunch(NxmRegistryKeySnapshot key, string command, out ReduxLaunchTarget launchTarget)
	{
		launchTarget = null;
		var application = key?.GetString("", ExecutableValueName);
		var host = key?.GetString("", DotNetHostValueName);
		try
		{
			if (String.IsNullOrWhiteSpace(application)) return false;
			launchTarget = String.IsNullOrEmpty(host) ? ReduxLaunchTarget.ForExecutable(application)
				: ReduxLaunchTarget.ForManagedApplication(application, host);
			return String.Equals(command, BuildCommand(launchTarget), StringComparison.Ordinal);
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return false;
		}
	}

	private static bool PathsEqual(string left, string right) =>
		String.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
			Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
			StringComparison.OrdinalIgnoreCase);

	private bool TryRestoreUserKey(NxmRegistryKeySnapshot snapshot)
	{
		try
		{
			if (snapshot == null) _store.DeleteUserKey();
			else _store.WriteUserKey(snapshot);
			return true;
		}
		catch (Exception ex) when (IsRegistryFailure(ex)) { return false; }
	}

	private static bool IsRegistryFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
}
