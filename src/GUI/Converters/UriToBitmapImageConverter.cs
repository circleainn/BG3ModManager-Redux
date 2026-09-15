using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DivinityModManager.Converters;

internal class UriToBitmapImageConverter : IValueConverter
{
	private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly object ImageCacheLock = new();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is Uri uri)
		{
			try
			{
				var cacheKey = uri.AbsoluteUri;
				lock (ImageCacheLock)
				{
					if (ImageCache.TryGetValue(cacheKey, out var cachedBitmap))
					{
						return cachedBitmap;
					}
				}

				var bitmap = new BitmapImage();
				void RemoveFailedImage(object _, System.Windows.Media.ExceptionEventArgs args)
				{
					lock (ImageCacheLock)
					{
						if (ImageCache.TryGetValue(cacheKey, out var failed) && ReferenceEquals(failed, bitmap))
							ImageCache.Remove(cacheKey);
					}
					DivinityApp.Log($"Could not load a remote mod image from '{RedactRemoteImageUri(uri)}': {args.ErrorException?.Message}");
				}
				bitmap.DownloadFailed += RemoveFailedImage;
				bitmap.DecodeFailed += RemoveFailedImage;
				bitmap.BeginInit();
				bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache;
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.UriSource = uri;
				bitmap.EndInit();
				lock (ImageCacheLock)
				{
					ImageCache[cacheKey] = bitmap;
				}
				return bitmap;
			}
			catch (Exception ex)
			{
				DivinityApp.Log($"Failed to create BitmapImage from '{uri}':\n{ex}");
			}
		}
		return null;
	}

	private static string RedactRemoteImageUri(Uri uri)
	{
		if (uri == null || !uri.IsAbsoluteUri) return "remote image";
		return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return "";
	}
}
