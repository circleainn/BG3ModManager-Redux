using SixLabors.ImageSharp.Formats.Png;

using System.Collections.Concurrent;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DivinityModManager.Util;

/// <summary>
/// Loads remote artwork through ImageSharp before handing it to WPF. Nexus's CDN
/// currently serves WebP bytes for many URLs whose path ends in .png or .jpeg;
/// BitmapImage trusts neither the response MIME type nor that format on every
/// supported Windows installation and otherwise leaves a blank image surface.
/// </summary>
public static class RemoteImageBehavior
{
	private const int MaximumImageBytes = 12 * 1024 * 1024;
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(20)
	};
	private static readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource>>> Cache =
		new(StringComparer.OrdinalIgnoreCase);

	public static readonly DependencyProperty SourceUriProperty = DependencyProperty.RegisterAttached(
		"SourceUri",
		typeof(Uri),
		typeof(RemoteImageBehavior),
		new PropertyMetadata(null, OnSourceUriChanged));

	public static void SetSourceUri(DependencyObject element, Uri value) => element.SetValue(SourceUriProperty, value);
	public static Uri GetSourceUri(DependencyObject element) => (Uri)element.GetValue(SourceUriProperty);

	private static async void OnSourceUriChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is not Image image) return;
		image.Source = null;
		image.Visibility = Visibility.Collapsed;
		if (args.NewValue is not Uri uri || !uri.IsAbsoluteUri) return;

		try
		{
			var lazy = Cache.GetOrAdd(uri.AbsoluteUri, key =>
				new Lazy<Task<BitmapSource>>(() => DownloadAsync(new Uri(key)), LazyThreadSafetyMode.ExecutionAndPublication));
			var source = await lazy.Value;
			if (!Equals(GetSourceUri(image), uri)) return;
			image.Source = source;
			image.Visibility = Visibility.Visible;
		}
		catch (Exception ex)
		{
			Cache.TryRemove(uri.AbsoluteUri, out _);
			DivinityApp.Log($"Could not load remote mod artwork from '{Redact(uri)}': {ex.Message}");
		}
	}

	private static async Task<BitmapSource> DownloadAsync(Uri uri)
	{
		using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength is > MaximumImageBytes)
			throw new InvalidDataException("The remote image exceeds Redux's size limit.");

		await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
		await using var bounded = new MemoryStream();
		var buffer = new byte[81920];
		var total = 0;
		while (true)
		{
			var read = await responseStream.ReadAsync(buffer).ConfigureAwait(false);
			if (read == 0) break;
			total += read;
			if (total > MaximumImageBytes)
				throw new InvalidDataException("The remote image exceeds Redux's size limit.");
			await bounded.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
		}

		bounded.Position = 0;
		return await DecodeAsync(bounded).ConfigureAwait(false);
	}

	private static async Task<BitmapSource> DecodeAsync(Stream encoded)
	{
		using var decoded = await SixLabors.ImageSharp.Image.LoadAsync(encoded).ConfigureAwait(false);
		await using var png = new MemoryStream();
		await decoded.SaveAsync(png, new PngEncoder()).ConfigureAwait(false);
		png.Position = 0;

		var bitmap = new BitmapImage();
		bitmap.BeginInit();
		bitmap.CacheOption = BitmapCacheOption.OnLoad;
		bitmap.StreamSource = png;
		bitmap.EndInit();
		bitmap.Freeze();
		return bitmap;
	}

	private static string Redact(Uri uri) => $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
}
