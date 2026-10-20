using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ReduxUpdater;

internal static class SnapshotRunner
{
	public static int Run(string requestPath)
	{
		var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
		var title = new TextBlock { Text = "Preparing snapshot…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
		var bar = new ProgressBar { Height = 8, IsIndeterminate = true, Margin = new Thickness(0, 0, 0, 16) };
		var cancel = new Button { Content = "Cancel", MinWidth = 100, Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right };
		var panel = new StackPanel { Margin = new Thickness(24) };
		panel.Children.Add(title); panel.Children.Add(bar); panel.Children.Add(cancel);
		var window = new Window { Title = "Redux Backups", Width = 520, SizeToContent = SizeToContent.Height,
			ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = panel };
		using var cancellation = new CancellationTokenSource();
		var finished = false;
		var canCancel = true;
		cancel.Click += (_, _) => { cancellation.Cancel(); cancel.IsEnabled = false; title.Text = "Canceling safely…"; };
		window.Closing += (_, e) => { if (!finished) { e.Cancel = true; if (canCancel) cancellation.Cancel(); } };
		window.Loaded += async (_, _) =>
		{
			ReduxSnapshotStore.Request? request = null;
			ReduxSnapshotStore? store = null;
			var success = false;
			var message = "";
			try
			{
				request = ReduxSnapshotStore.ReadRequest(Path.GetFullPath(requestPath));
				if (request.SchemaVersion != 1 || request.ParentProcessId <= 0 || request.ParentProcessId == Environment.ProcessId
					|| request.Operation is not ("create" or "restore" or "recover")) throw new InvalidDataException("Invalid snapshot request.");
				store = new ReduxSnapshotStore(request.ApplicationDirectory);
				await Task.Run(() => Program.WaitForReduxToExit(request.ParentProcessId));
				// Hold the same per-user instance mutex on the UI thread across awaits.
				// A browser link or second shortcut must not reopen Redux during replacement.
				using var instanceGuard = new ReduxInstanceGuard();
				if (!instanceGuard.TryAcquire() || ReduxInstanceGuard.HasOlderRunningInstance())
					throw new IOException("Another Redux instance is open. Close it before retrying the snapshot.");
				var progress = new Progress<ReduxSnapshotStore.Progress>(p =>
				{
					canCancel = p.CanCancel;
					cancel.IsEnabled = canCancel && !cancellation.IsCancellationRequested;
					title.Text = p.Message;
					bar.IsIndeterminate = p.Total <= 0;
					if (p.Total > 0) bar.Value = Math.Clamp(100d * p.Current / p.Total, 0, 100);
				});
				await Task.Run(async () =>
				{
					foreach (var name in new[] { "bg3", "bg3_dx11" })
					{
						var processes = Process.GetProcessesByName(name);
						try { if (processes.Length > 0) throw new IOException("Close Baldur's Gate 3 before creating or restoring a snapshot."); }
						finally { foreach (var process in processes) process.Dispose(); }
					}
					if (store.NeedsRecovery)
					{
						await store.RecoverAsync(request.Scopes, progress);
						message = "The interrupted snapshot restore was rolled back. Review Backups before trying again.";
					}
					else if (request.Operation == "create")
					{
						await store.CreateAsync(request.Name, request.Version, request.IncludeMods, request.Scopes, progress, cancellation.Token);
						message = "Snapshot saved. Browse it in Preferences > Backups.";
					}
					else if (request.Operation == "restore")
					{
						await store.RestoreAsync(request.SnapshotFileName, request.Scopes, request.Version, progress, cancellation.Token);
						message = "Snapshot restored. Your previous state is saved as a return snapshot in Preferences > Backups.";
					}
					else message = "No interrupted snapshot restore needs recovery.";
				});
				success = true;
			}
			catch (OperationCanceledException) { message = "Snapshot canceled. The installation was not changed."; }
			catch (Exception ex) { message = "Snapshot operation failed: " + ex.Message; }
			var pendingRecovery = store?.NeedsRecovery == true;
			if (pendingRecovery) message += "\n\nRecovery is still required. Keep the snapshot library and this runner folder. Retry the original request after resolving the error.\n" + Path.GetDirectoryName(Path.GetFullPath(requestPath));
			if (request != null)
			{
				try
				{
					ReduxUpdateTransaction.WriteResult(request.ResultPath, new ReduxUpdateResult { Succeeded = success,
						DisplayVersion = request.Version, Message = message, CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
				}
				catch { }
			}
			if (!success) MessageBox.Show(window, message, "Redux Backups", MessageBoxButton.OK, MessageBoxImage.Warning);
			finished = true;
			window.Close();
			if (request != null && !pendingRecovery)
			{
				try
				{
					Process.Start(Program.CreateRelaunchStartInfo(new ReduxUpdateRequest { TargetDirectory = request.ApplicationDirectory }, Environment.ProcessPath));
				}
				catch (Exception ex) { MessageBox.Show("Start Redux manually. " + ex.Message, "Redux Backups"); }
			}
			application.Shutdown(success ? 0 : 1);
		};
		return application.Run(window);
	}
}
