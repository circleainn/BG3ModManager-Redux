using DivinityModManager.AppServices;
using DivinityModManager.Models;

using System;
using System.IO;
using System.Linq;

namespace Redux.Core.Tests;

internal sealed class LoadOrderRestorePointTests
{
	public void RoundTripPreservesProfileReasonAndOrder()
	{
		WithTemporaryDirectory(root =>
		{
			var profileUuid = Guid.NewGuid().ToString();
			var saved = LoadOrderRestorePointService.TryCreate(
				root,
				profileUuid,
				"Public",
				"Current",
				"Before game export",
				[
					Entry("First"),
					Entry("Second")
				],
				out var created,
				out var error);

			RegressionAssert.True(saved);
			RegressionAssert.Equal(String.Empty, error);
			var loaded = LoadOrderRestorePointService.Load(root, profileUuid);
			RegressionAssert.Equal(1, loaded.Count);
			RegressionAssert.Equal(created.Id, loaded[0].Id);
			RegressionAssert.Equal("Public", loaded[0].ProfileName);
			RegressionAssert.Equal("Before game export", loaded[0].Reason);
			RegressionAssert.SequenceEqual(
				new[] { "First", "Second" },
				loaded[0].Order.Select(entry => entry.Name));
		});
	}

	public void EmptyExportedOrderCanBeRestored()
	{
		WithTemporaryDirectory(root =>
		{
			var profileUuid = Guid.NewGuid().ToString();
			RegressionAssert.True(LoadOrderRestorePointService.TryCreate(
				root,
				profileUuid,
				"Public",
				"Current",
				"Before game export",
				[],
				out _,
				out _));

			var loaded = LoadOrderRestorePointService.Load(root, profileUuid);
			RegressionAssert.Equal(1, loaded.Count);
			RegressionAssert.Equal(0, loaded[0].ModCount);
		});
	}

	public void RetentionKeepsOnlyTheNewestTwentySnapshots()
	{
		WithTemporaryDirectory(root =>
		{
			var profileUuid = Guid.NewGuid().ToString();
			for (var index = 0; index < LoadOrderRestorePointService.MaximumRestorePointsPerProfile + 3; index++)
			{
				var point = new LoadOrderRestorePoint
				{
					CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-index),
					ProfileUuid = profileUuid,
					ProfileName = "Public",
					Reason = $"Snapshot {index}",
					SourceOrderName = "Current",
					Order = [Entry($"Mod {index}")]
				};
				RegressionAssert.True(LoadOrderRestorePointService.TrySave(root, point, out _));
			}

			var loaded = LoadOrderRestorePointService.Load(root, profileUuid);
			RegressionAssert.Equal(LoadOrderRestorePointService.MaximumRestorePointsPerProfile, loaded.Count);
			RegressionAssert.Equal("Snapshot 0", loaded[0].Reason);
			RegressionAssert.False(loaded.Any(point => point.Reason == "Snapshot 22"));
		});
	}

	public void RestorePointsFromAnotherProfileAreRejected()
	{
		WithTemporaryDirectory(root =>
		{
			var firstProfile = Guid.NewGuid().ToString();
			var secondProfile = Guid.NewGuid().ToString();
			RegressionAssert.True(LoadOrderRestorePointService.TryCreate(
				root,
				firstProfile,
				"Public",
				"Current",
				"Before game export",
				[Entry("First")],
				out _,
				out _));

			RegressionAssert.Equal(0, LoadOrderRestorePointService.Load(root, secondProfile).Count);
		});
	}

	public void InvalidSnapshotsAreIgnoredWithoutLeavingTemporaryFiles()
	{
		WithTemporaryDirectory(root =>
		{
			var profileUuid = Guid.NewGuid().ToString();
			var profileDirectory = Path.Combine(root, profileUuid);
			Directory.CreateDirectory(profileDirectory);
			File.WriteAllText(Path.Combine(profileDirectory, "invalid.json"), "{ not valid json");

			RegressionAssert.True(LoadOrderRestorePointService.TryCreate(
				root,
				profileUuid,
				"Public",
				"Current",
				"Before game export",
				[Entry("First")],
				out _,
				out _));

			RegressionAssert.Equal(1, LoadOrderRestorePointService.Load(root, profileUuid).Count);
			RegressionAssert.False(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any());
		});
	}

	private static DivinityLoadOrderEntry Entry(string name) => new()
	{
		UUID = Guid.NewGuid().ToString(),
		Name = name
	};

	private static void WithTemporaryDirectory(Action<string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxRestorePointTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			action(directory);
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, true);
			}
		}
	}
}
