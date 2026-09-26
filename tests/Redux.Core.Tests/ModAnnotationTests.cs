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

	public void BulkNotesUpdateAtomically()
	{
		var first = Guid.NewGuid().ToString();
		var second = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, first, "First", out _));
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, second, "Second", out _));

		RegressionAssert.False(ReduxModAnnotationService.TrySetMany(
			store,
			new[] { first, second },
			new string('x', ReduxModAnnotationService.MaximumNoteLength + 1),
			out _));
		RegressionAssert.Equal("First", ReduxModAnnotationService.Find(store, first).PrivateNote);
		RegressionAssert.Equal("Second", ReduxModAnnotationService.Find(store, second).PrivateNote);

		RegressionAssert.True(ReduxModAnnotationService.TrySetMany(
			store,
			new[] { first, second, first },
			"Shared note",
			out _));
		RegressionAssert.Equal(2, store.Mods.Count);
		RegressionAssert.Equal("Shared note", ReduxModAnnotationService.Find(store, first).PrivateNote);
		RegressionAssert.Equal("Shared note", ReduxModAnnotationService.Find(store, second).PrivateNote);
	}

	public void AliasesPersistIndependentlyFromPrivateNotes()
	{
		WithTemporaryPath(path =>
		{
			var uuid = Guid.NewGuid().ToString();
			var store = new ReduxModAnnotationStore();
			RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, "Keep this note", out _));
			RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(store, uuid, "My clearer mod name", out _));
			RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, String.Empty, out _));
			RegressionAssert.Equal(1, store.Mods.Count);
			RegressionAssert.Equal("My clearer mod name", ReduxModAnnotationService.Find(store, uuid).CustomAlias);

			RegressionAssert.True(ReduxModAnnotationService.TrySave(path, store, out _));
			var loaded = ReduxModAnnotationService.Load(path);
			RegressionAssert.Equal("My clearer mod name", ReduxModAnnotationService.Find(loaded, uuid).CustomAlias);
			RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(loaded, uuid, String.Empty, out _));
			RegressionAssert.Equal(0, loaded.Mods.Count);
		});
	}

	public void OversizedAliasesAreRejectedBeforeTheStoreChanges()
	{
		var uuid = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();
		RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(store, uuid, "Original alias", out _));

		var updated = ReduxModAnnotationService.TrySetAlias(
			store,
			uuid,
			new string('x', ReduxModAnnotationService.MaximumAliasLength + 1),
			out var error);

		RegressionAssert.False(updated);
		RegressionAssert.Contains(error, ReduxModAnnotationService.MaximumAliasLength.ToString());
		RegressionAssert.Equal("Original alias", ReduxModAnnotationService.Find(store, uuid).CustomAlias);
	}

	public void AliasesRemainIndependentForPackagesThatShareProviderMetadata()
	{
		var firstPackageUuid = Guid.NewGuid().ToString();
		var secondPackageUuid = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();

		RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(store, firstPackageUuid, "Core package", out _));
		RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(store, secondPackageUuid, "Optional package", out _));

		RegressionAssert.Equal("Core package", ReduxModAnnotationService.Find(store, firstPackageUuid).CustomAlias);
		RegressionAssert.Equal("Optional package", ReduxModAnnotationService.Find(store, secondPackageUuid).CustomAlias);
	}

	public void CustomArtworkPersistsWithoutReplacingAliasesOrNotes()
	{
		var uuid = Guid.NewGuid().ToString();
		var store = new ReduxModAnnotationStore();
		RegressionAssert.True(ReduxModAnnotationService.TrySet(store, uuid, "Private note", out _));
		RegressionAssert.True(ReduxModAnnotationService.TrySetAlias(store, uuid, "Local alias", out _));
		RegressionAssert.True(ReduxModAnnotationService.TrySetArtwork(
			store, uuid, "custom-artwork:0123456789abcdef-0123456789abcdef0123456789abcdef.png", out _));

		var annotation = ReduxModAnnotationService.Find(store, uuid);
		RegressionAssert.True(annotation.HasCustomPreviewImage);
		RegressionAssert.Equal("Private note", annotation.PrivateNote);
		RegressionAssert.Equal("Local alias", annotation.CustomAlias);
		RegressionAssert.False(ReduxModAnnotationService.TrySetArtwork(
			store, uuid, "custom-artwork:..\\outside.png", out _));

		RegressionAssert.True(ReduxModAnnotationService.TrySetArtwork(store, uuid, String.Empty, out _));
		annotation = ReduxModAnnotationService.Find(store, uuid);
		RegressionAssert.True(annotation != null);
		RegressionAssert.False(annotation.HasCustomPreviewImage);
		RegressionAssert.Equal("Private note", annotation.PrivateNote);
		RegressionAssert.Equal("Local alias", annotation.CustomAlias);
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
