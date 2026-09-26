using DivinityModManager.Models;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.IO;

namespace Redux.Core.Tests;

public sealed class CustomArtworkTests
{
	public void HoverDescriptionsUseBoundedPlainTextFromCachedMetadata()
	{
		var mod = new DivinityModData
		{
			Description = "&lt;p&gt;A local &amp; readable description.&lt;/p&gt;<script>hidden()</script> " + new string('x', 400)
		};

		RegressionAssert.True(mod.Metadata.HasHoverDescription);
		RegressionAssert.Contains(mod.Metadata.HoverDescriptionExcerpt, "A local & readable description.");
		RegressionAssert.False(mod.Metadata.HoverDescriptionExcerpt.Contains("<p>", StringComparison.Ordinal));
		RegressionAssert.False(mod.Metadata.HoverDescriptionExcerpt.Contains("hidden", StringComparison.Ordinal));
		RegressionAssert.True(mod.Metadata.HoverDescriptionExcerpt.Length <= 281);
	}

	public void MissingDescriptionsDoNotCreatePlaceholderHoverText()
	{
		var mod = new DivinityModData { Description = "   " };

		RegressionAssert.False(mod.Metadata.HasHoverDescription);
		RegressionAssert.Equal(String.Empty, mod.Metadata.HoverDescriptionExcerpt);
	}

	public void JpegArtworkIsCopiedAsAPathSafePackageScopedPng()
	{
		var sourcePath = Path.Combine(Path.GetTempPath(), $"redux-artwork-{Guid.NewGuid():N}.jpg");
		using (var image = new Image<Rgba32>(96, 48)) image.SaveAsJpeg(sourcePath);
		var reference = String.Empty;
		try
		{
			RegressionAssert.True(ReduxModArtworkService.TryImport(
				sourcePath, Guid.NewGuid().ToString(), out reference, out _));
			RegressionAssert.True(reference.StartsWith(ReduxModArtworkService.ReferencePrefix, StringComparison.Ordinal));
			RegressionAssert.False(reference.Contains(sourcePath, StringComparison.OrdinalIgnoreCase));
			RegressionAssert.True(ReduxModArtworkService.TryResolvePath(reference, out var storedPath));
			RegressionAssert.True(File.Exists(storedPath));
			RegressionAssert.Equal(".png", Path.GetExtension(storedPath).ToLowerInvariant());
			using var stored = Image.Load(storedPath);
			RegressionAssert.Equal(96, stored.Width);
			RegressionAssert.Equal(48, stored.Height);
		}
		finally
		{
			if (!String.IsNullOrWhiteSpace(reference)) ReduxModArtworkService.TryDelete(reference, out _);
			if (File.Exists(sourcePath)) File.Delete(sourcePath);
		}
	}

	public void CustomArtworkOverridesAndThenFallsBackToProviderArtwork()
	{
		var customPath = Path.Combine(Path.GetTempPath(), "custom-preview.png");
		var providerUri = new Uri("https://static.example.test/provider.png");
		var mod = new DivinityModData
		{
			UUID = Guid.NewGuid().ToString(),
			Name = "Local name",
			CustomPreviewImagePath = customPath,
			HasCustomPreviewImage = true,
			HasMetadata = true,
			OnlineMetadataEnabled = true,
			NexusModsEnabled = true
		};
		mod.NexusModsData.Update(new NexusModsModData
		{
			UUID = mod.UUID,
			ModId = 12345,
			Name = "Provider name",
			PictureUrl = providerUri,
			MetadataOrigin = NexusMetadataOrigin.Manual
		});

		RegressionAssert.True(mod.Metadata.PreviewImageUri.IsFile);
		RegressionAssert.True(mod.Metadata.UsesCustomPreviewImage);
		mod.HasCustomPreviewImage = false;
		RegressionAssert.Equal(providerUri, mod.Metadata.PreviewImageUri);
		RegressionAssert.False(mod.Metadata.UsesCustomPreviewImage);
	}
}
