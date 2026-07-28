using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using DivinityModManager.Models;
using DivinityModManager.Util;

namespace Redux.Core.Tests;

internal sealed class ReduxBundleTests
{
	private const string FirstUuid = "11111111-1111-1111-1111-111111111111";
	private const string SecondUuid = "22222222-2222-2222-2222-222222222222";
	private const string CustomIconReference =
		"custom-png:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png";
	private const string CustomIconAsset =
		"assets/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.png";

	public void BundleRoundTripPreservesOrderAndReduxPresentation()
	{
		WithTemporaryBundle(path =>
		{
			var order = CreateOrder();
			var presentation = CreatePresentation();
			var assets = new Dictionary<string, byte[]>
			{
				[CustomIconAsset] = new byte[] { 1, 2, 3, 4 }
			};

			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				order,
				presentation,
				assets,
				out var exportError));
			RegressionAssert.Equal(String.Empty, exportError);
			RegressionAssert.True(ReduxLoadOrderBundleService.TryRead(
				path,
				out var contents,
				out var readError));
			RegressionAssert.Equal(String.Empty, readError);

			RegressionAssert.SequenceEqual(
				new[] { FirstUuid, SecondUuid },
				contents.LoadOrder.Order.Select(entry => entry.UUID));
			RegressionAssert.Equal("Portable Test Order", contents.LoadOrder.Name);
			RegressionAssert.SequenceEqual(
				new[] { "Quest Mods" },
				contents.Presentation.CustomCategoryDisplayOrder);
			RegressionAssert.SequenceEqual(
				new[] { "Quest Mods" },
				contents.Presentation.CategoryAssignments[SecondUuid]);
			RegressionAssert.Equal("Quest-related additions.", contents.Presentation.CustomCategories[0].Description);
			RegressionAssert.Equal("Chapter One", contents.Presentation.Dividers[0].Title);
			RegressionAssert.True(contents.Presentation.Dividers[0].IsCollapsed);
			RegressionAssert.Equal(1, contents.Presentation.Dividers[0].FallbackPosition);
			RegressionAssert.SequenceEqual(
				assets[CustomIconAsset],
				contents.Assets[CustomIconAsset]);
		});
	}

	public void BundleNeverContainsModsettingsLsx()
	{
		WithTemporaryBundle(path =>
		{
			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				CreatePresentation(),
				new Dictionary<string, byte[]>
				{
					[CustomIconAsset] = new byte[] { 1, 2, 3, 4 }
				},
				out _));

			using var archive = ZipFile.OpenRead(path);
			RegressionAssert.False(archive.Entries.Any(entry =>
				String.Equals(entry.FullName, "modsettings.lsx", StringComparison.OrdinalIgnoreCase)));
			RegressionAssert.SequenceEqual(
				new[] { "loadorder.json", "presentation.json", CustomIconAsset },
				archive.Entries.Select(entry => entry.FullName));
		});
	}

	public void MismatchedOrderAndPresentationAreRejected()
	{
		WithTemporaryBundle(path =>
		{
			var presentation = CreatePresentation();
			presentation.OrderedModUuids.Reverse();

			RegressionAssert.False(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				presentation,
				new Dictionary<string, byte[]>
				{
					[CustomIconAsset] = new byte[] { 1, 2, 3, 4 }
				},
				out var error));
			RegressionAssert.Contains(error, "same mod sequence");
			RegressionAssert.False(File.Exists(path));
		});
	}

	public void UnexpectedFilesAreRejectedDuringImport()
	{
		WithTemporaryBundle(path =>
		{
			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				CreatePresentation(),
				new Dictionary<string, byte[]>
				{
					[CustomIconAsset] = new byte[] { 1, 2, 3, 4 }
				},
				out _));

			using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
			{
				var entry = archive.CreateEntry("modsettings.lsx");
				using var writer = new StreamWriter(entry.Open());
				writer.Write("<save />");
			}

			RegressionAssert.False(ReduxLoadOrderBundleService.TryRead(
				path,
				out _,
				out var error));
			RegressionAssert.Contains(error, "unexpected file");
		});
	}

	public void ExistingBundleCanBeAtomicallyReplaced()
	{
		WithTemporaryBundle(path =>
		{
			var firstPresentation = CreatePresentation();
			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				firstPresentation,
				CreateAssets(),
				out _));

			var replacementPresentation = CreatePresentation();
			replacementPresentation.LoadOrderName = "Replacement Order";
			replacementPresentation.Dividers[0].Title = "Replacement Divider";
			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				replacementPresentation,
				CreateAssets(),
				out var exportError));
			RegressionAssert.Equal(String.Empty, exportError);

			RegressionAssert.True(ReduxLoadOrderBundleService.TryRead(
				path,
				out var contents,
				out _));
			RegressionAssert.Equal("Replacement Order", contents.LoadOrder.Name);
			RegressionAssert.Equal("Replacement Divider", contents.Presentation.Dividers[0].Title);
			RegressionAssert.False(File.Exists(path + ".tmp"));
		});
	}

	public void FailedReplacementPreservesTheExistingBundle()
	{
		WithTemporaryBundle(path =>
		{
			RegressionAssert.True(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				CreatePresentation(),
				CreateAssets(),
				out _));

			var invalidReplacement = CreatePresentation();
			invalidReplacement.LoadOrderName = "Invalid Replacement";
			invalidReplacement.OrderedModUuids.Reverse();
			RegressionAssert.False(ReduxLoadOrderBundleService.TryExport(
				path,
				CreateOrder(),
				invalidReplacement,
				CreateAssets(),
				out var exportError));
			RegressionAssert.Contains(exportError, "same mod sequence");

			RegressionAssert.True(ReduxLoadOrderBundleService.TryRead(
				path,
				out var contents,
				out _));
			RegressionAssert.Equal("Portable Test Order", contents.LoadOrder.Name);
			RegressionAssert.False(File.Exists(path + ".tmp"));
		});
	}

	private static DivinityLoadOrder CreateOrder()
	{
		return new DivinityLoadOrder
		{
			Name = "This name is intentionally replaced by presentation metadata",
			Order = new List<DivinityLoadOrderEntry>
			{
				new() { UUID = FirstUuid, Name = "First Mod" },
				new() { UUID = SecondUuid, Name = "Second Mod" }
			}
		};
	}

	private static ReduxLoadOrderPresentation CreatePresentation()
	{
		return new ReduxLoadOrderPresentation
		{
			LoadOrderName = "Portable Test Order",
			CreatorVersion = "0.1.0-alpha.8",
			CreatorInternalVersion = "0.1.0.8",
			OrderedModUuids = new List<string> { FirstUuid, SecondUuid },
			CustomCategories = new List<ReduxLoadOrderCategory>
			{
				new()
				{
					Name = "Quest Mods",
					Color = "#D7A24B",
					IconId = CustomIconReference,
					Description = "Quest-related additions."
				}
			},
			CustomCategoryDisplayOrder = new List<string> { "Quest Mods" },
			CategoryAssignments = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
			{
				[SecondUuid] = new List<string> { "Quest Mods" }
			},
			Dividers = new List<ReduxLoadOrderDivider>
			{
				new()
				{
					Title = "Chapter One",
					Color = "#42A77C",
					IconId = String.Empty,
					IsCollapsed = true,
					FallbackPosition = 1,
					BeforeModUuid = FirstUuid,
					AfterModUuid = SecondUuid
				}
			},
			CustomIconAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				[CustomIconReference] = CustomIconAsset
			}
		};
	}

	private static Dictionary<string, byte[]> CreateAssets()
	{
		return new Dictionary<string, byte[]>
		{
			[CustomIconAsset] = new byte[] { 1, 2, 3, 4 }
		};
	}

	private static void WithTemporaryBundle(Action<string> action)
	{
		var directory = Path.Combine(
			Path.GetTempPath(),
			$"BG3ModManagerRedux.Tests.{Guid.NewGuid():N}");
		var path = Path.Combine(directory, "test.bg3redux");
		Directory.CreateDirectory(directory);
		try
		{
			action(path);
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}
}
