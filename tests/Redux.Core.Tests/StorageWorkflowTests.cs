using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using DivinityModManager.AppServices;
using DivinityModManager.Models;
using DivinityModManager.Views;

namespace Redux.Core.Tests;

internal sealed class StorageWorkflowTests
{
	public void ManagedDownloadsLocationKeepsEachQueueInItsOwnFolder()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-download-location-" + Guid.NewGuid().ToString("N"));
		var defaultPath = Path.Combine(root, "default");
		var customPath = Path.Combine(root, "custom");
		Directory.CreateDirectory(defaultPath);
		File.WriteAllText(Path.Combine(defaultPath, "downloads.json"), "original queue");
		try
		{
			RegressionAssert.True(ManagedDownloadsLocation.TryPrepare(customPath, defaultPath,
				out var selected, out var error));
			RegressionAssert.Equal(String.Empty, error);
			RegressionAssert.Equal(Path.GetFullPath(customPath), selected);
			RegressionAssert.True(Directory.Exists(customPath));
			RegressionAssert.False(File.Exists(Path.Combine(customPath, "downloads.json")));
			RegressionAssert.Equal("original queue", File.ReadAllText(Path.Combine(defaultPath, "downloads.json")));
			RegressionAssert.True(ManagedDownloadsLocation.TryPrepare(String.Empty, defaultPath,
				out var restored, out _));
			RegressionAssert.Equal(Path.GetFullPath(defaultPath), restored);
			var fileInsteadOfFolder = Path.Combine(root, "not-a-folder");
			File.WriteAllText(fileInsteadOfFolder, "leave alone");
			RegressionAssert.False(ManagedDownloadsLocation.TryPrepare(fileInsteadOfFolder, defaultPath,
				out _, out _));
			RegressionAssert.Equal("leave alone", File.ReadAllText(fileInsteadOfFolder));
			RegressionAssert.False(ManagedDownloadsLocation.TryPrepare("relative-downloads", defaultPath,
				out _, out _));
		}
		finally { Directory.Delete(root, true); }
	}

	public void BackupQuotaPrunesOldestAndLeavesOtherFilesAlone()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-retention-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var old = Path.Combine(root, "old.pak"); var recent = Path.Combine(root, "recent.pak");
			File.WriteAllBytes(old, new byte[8]); File.SetCreationTimeUtc(old, DateTime.UtcNow.AddDays(-2));
			File.WriteAllBytes(recent, new byte[8]);
			File.WriteAllText(Path.Combine(root, "notes.txt"), "keep");
			Directory.CreateDirectory(Path.Combine(root, "nested"));
			File.WriteAllText(Path.Combine(root, "nested", "installed.pak"), "keep");
			var result = ModBackupRetention.Prune(root, 8);
			RegressionAssert.Equal(1, result.Deleted);
			RegressionAssert.False(File.Exists(old)); RegressionAssert.True(File.Exists(recent));
			ModBackupRetention.Prune(root, 0);
			RegressionAssert.False(File.Exists(recent));
			RegressionAssert.True(File.Exists(Path.Combine(root, "notes.txt")));
			RegressionAssert.True(File.Exists(Path.Combine(root, "nested", "installed.pak")));
		}
		finally { Directory.Delete(root, true); }
	}

	public void ReviewedCleanupDoesNotDeleteNewOrChangedBackups()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-retention-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var path = Path.Combine(root, "old.pak"); File.WriteAllBytes(path, new byte[8]);
			var reviewed = ModBackupRetention.Snapshot(root);
			File.WriteAllBytes(path, new byte[16]);
			File.WriteAllBytes(Path.Combine(root, "new.pak"), new byte[4]);
			RegressionAssert.Equal(0, ModBackupRetention.DeleteReviewed(root, reviewed).Deleted);
			RegressionAssert.Equal(2, ModBackupRetention.Snapshot(root).Count);
		}
		finally { Directory.Delete(root, true); }
	}

	public void FailedReplacementDoesNotReachRetention()
	{
		var root = Path.Combine(Path.GetTempPath(), "redux-retention-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var path = Path.Combine(root, "recovery.pak"); File.WriteAllBytes(path, new byte[8]);
			try
			{
				ModBackupRetention.CommitReplacement(() =>
				{
					DivinityModManager.Util.AtomicFileWriter.CopyFile(Path.Combine(root, "missing.pak"), Path.Combine(root, "destination.pak"));
					ModBackupRetention.Prune(root, 0);
				});
			}
			catch (FileNotFoundException) { }
			RegressionAssert.True(File.Exists(path));
		}
		finally { Directory.Delete(root, true); }
	}

	public void DeferredCollapseCompletionCanBeCancelledOrFlushed()
	{
		var type = typeof(HorizontalModLayout).GetNestedType("VisualDividerAnimation", System.Reflection.BindingFlags.NonPublic)!;
		foreach (var complete in new[] { false, true })
		{
			var animation = Activator.CreateInstance(type, [1, (Action<double>)(_ => { }), (Action<bool>)(_ => { }), true])!;
			type.GetMethod("Complete")!.Invoke(animation, null);
			var calls = new System.Collections.Generic.List<bool>();
			type.GetMethod("DeferCompletion")!.Invoke(animation, [(Action<bool>)(value => calls.Add(value))]);
			type.GetMethod(complete ? "Complete" : "Cancel")!.Invoke(animation, null);
			System.Windows.Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
			RegressionAssert.SequenceEqual([complete], calls);
		}
	}

	public void OverrideSortingPreservesSearchAndUnderlyingOrder()
	{
		var layout = new HorizontalModLayout();
		var first = new DivinityModData { UUID = "1", Name = "Matching Zebra", CustomAlias = "Matching Zebra", HasCustomAlias = true };
		var second = new DivinityModData { UUID = "2", Name = "Unrelated" };
		var third = new DivinityModData { UUID = "3", Name = "Matching Alpha", CustomAlias = "Matching Alpha", HasCustomAlias = true };
		var source = new ObservableCollection<DivinityModData> { first, second, third };
		layout.ForceLoadedModsView.ItemsSource = source;
		((TextBox)layout.FindName("OverrideModsFilterTextBox")).Text = "Matching";
		layout.Sort("Name", ListSortDirection.Ascending, layout.ForceLoadedModsView);
		RegressionAssert.SequenceEqual([third, first], layout.ForceLoadedModsView.Items.Cast<DivinityModData>());
		RegressionAssert.SequenceEqual([first, second, third], source);
	}
}
