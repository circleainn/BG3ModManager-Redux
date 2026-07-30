using DivinityModManager.AppServices;
using DivinityModManager.Models;

using System;
using System.IO;

namespace Redux.Core.Tests;

internal sealed class ModAnnotationTests
{
	public void AnnotationsRoundTripWithoutPackageOrProfileData()
	{
		WithTemporaryPath(path =>
		{
			var uuid = Guid.NewGuid().ToString();
			var store = new ReduxModAnnotationStore();
			RegressionAssert.True(ReduxModAnnotationService.TrySet(
				store,
				uuid,
				"Keep below the compatibility patch.",
				out _));
			RegressionAssert.True(ReduxModAnnotationService.TrySave(path, store, out _));

			var loaded = ReduxModAnnotationService.Load(path);
			var annotation = ReduxModAnnotationService.Find(loaded, uuid);
			RegressionAssert.True(annotation != null);
			RegressionAssert.Equal("Keep below the compatibility patch.", annotation!.PrivateNote);

			var json = File.ReadAllText(path);
			RegressionAssert.False(json.Contains("profile", StringComparison.OrdinalIgnoreCase));
			RegressionAssert.False(json.Contains(".pak", StringComparison.OrdinalIgnoreCase));
		});
	}

	public void ClearingTheLastValueRemovesTheAnnotation()
	{
		var uuid = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, "Note", out _));
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, String.Empty, out _));
		RegressionAssert.Equal(0, store.Mods.Count);
	}

	public void OversizedNotesAreRejectedBeforeTheStoreChanges()
	{
		var uuid = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, "Original", out _));

		var updated = ReduxModAnnotationService.TrySet(
			store,
			uuid,
			new string('x', ReduxModAnnotationService.MaximumNoteLength + 1),
			out var error);

		RegressionAssert.False(updated);
		RegressionAssert.Contains(error, "8,000");
		var annotation = ReduxModAnnotationService.Find(store, uuid);
		RegressionAssert.Equal("Original", annotation.PrivateNote);
	}

	private static void WithTemporaryPath(Action<string> action)
	{
		var directory = Path.Combine(Path.GetTempPath(), "ReduxAnnotationTests", Guid.NewGuid().ToString("N"));
		var path = Path.Combine(directory, "mod-annotations.json");
		Directory.CreateDirectory(directory);
		try
		{
			action(path);
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
