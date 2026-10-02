using DivinityModManager.Models.App;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;

namespace Redux.Core.Tests;

public sealed class RefreshOperationTests
{
	public void RefreshShortcutStaysDisabledUntilOperationCleanupCompletes()
	{
		WithCommand((viewModel, command) =>
		{
			var shortcut = new Hotkey(Key.F5);
			shortcut.AddAction(() => command.Execute().Subscribe(), command.CanExecute);
			using var shortcutCommand = shortcut.Command;
			var available = false;
			using var availability = command.CanExecute.Subscribe(value => available = value);
			RegressionAssert.True(available);
			RegressionAssert.True(shortcut.CanExecuteCommand);
			using var cancellation = new CancellationTokenSource();
			viewModel.MainProgressToken = cancellation;
			viewModel.MainProgressIsActive = true;
			RegressionAssert.False(available);
			RegressionAssert.False(shortcut.CanExecuteCommand);
			cancellation.Cancel();
			RegressionAssert.False(available);
			RegressionAssert.False(shortcut.CanExecuteCommand);
			// Cancellation is only a request; the operation retains ownership through cleanup.
			viewModel.MainProgressToken = null!;
			viewModel.MainProgressIsActive = false;
			RegressionAssert.True(available);
			RegressionAssert.True(shortcut.CanExecuteCommand);
			viewModel.DownloadManagerInstallIsActive = true;
			RegressionAssert.False(available);
			RegressionAssert.False(shortcut.CanExecuteCommand);
			viewModel.DownloadManagerInstallIsActive = false;
			typeof(MainWindowViewModel).GetProperty(nameof(viewModel.IsRefreshing))!.SetValue(viewModel, true);
			RegressionAssert.False(available);
			typeof(MainWindowViewModel).GetProperty(nameof(viewModel.IsRefreshing))!.SetValue(viewModel, false);
			RegressionAssert.True(available);
		});
	}

	public void DirectRefreshDuringFileWorkPreservesProgressAndShutdownCancellation()
	{
		WithCommand((viewModel, command) =>
		{
			using var cancellation = new CancellationTokenSource();
			var tracker = new PendingOperationTracker();
			using var operation = tracker.TryRegister(cancellation.Cancel)!;
			viewModel.MainProgressToken = cancellation;
			viewModel.MainProgressIsActive = true;
			viewModel.MainProgressTitle = "Backing up active mods";
			viewModel.MainProgressWorkText = "Copying package";
			viewModel.MainProgressValue = 0.4;
			viewModel.CanCancelProgress = true;
			// ReactiveCommand.Execute can bypass CanExecute, as a queued/internal call can.
			command.Execute().Wait();
			RegressionAssert.False(viewModel.IsRefreshing);
			RegressionAssert.True(ReferenceEquals(cancellation, viewModel.MainProgressToken));
			RegressionAssert.Equal("Backing up active mods", viewModel.MainProgressTitle);
			RegressionAssert.Equal("Copying package", viewModel.MainProgressWorkText);
			RegressionAssert.Equal(0.4, viewModel.MainProgressValue);
			RegressionAssert.True(viewModel.CanCancelProgress);
			var shutdown = tracker.StopAsync();
			RegressionAssert.True(cancellation.IsCancellationRequested);
			RegressionAssert.False(shutdown.IsCompleted);
			operation.Dispose();
			shutdown.GetAwaiter().GetResult();
		});
	}

	public void DirectRefreshCannotStartDuringBatchInstallOrShutdown()
	{
		WithCommand((viewModel, command) =>
		{
			viewModel.DownloadManagerInstallIsActive = true;
			command.Execute().Wait();
			RegressionAssert.False(viewModel.IsRefreshing);
			RegressionAssert.False(viewModel.MainProgressIsActive);
			viewModel.DownloadManagerInstallIsActive = false;
			typeof(MainWindowViewModel).GetField("_nxmShuttingDown", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(viewModel, true);
			command.Execute().Wait();
			RegressionAssert.False(viewModel.IsRefreshing);
			RegressionAssert.False(viewModel.MainProgressIsActive);
		});
	}

	private static void WithCommand(Action<MainWindowViewModel, ReactiveCommand<Unit, Unit>> action)
	{
		var originalScheduler = RxApp.MainThreadScheduler;
		RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
		try
		{
			// Exercise the real command without starting profile discovery or user settings I/O.
			var viewModel = (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel));
			using var command = (ReactiveCommand<Unit, Unit>)typeof(MainWindowViewModel)
				.GetMethod("CreateRefreshCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewModel, null)!;
			action(viewModel, command);
		}
		finally { RxApp.MainThreadScheduler = originalScheduler; }
	}
}
