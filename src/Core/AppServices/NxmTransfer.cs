using DivinityModManager.Models.NexusMods;

using System.Net;
using System.Net.Http.Headers;

using Newtonsoft.Json;

namespace DivinityModManager.AppServices;

public sealed record NxmTransferRequest(
	Uri DownloadUri,
	string PartialPath,
	string CompletedPath,
	long MaximumBytes,
	string ETag = null,
	DateTimeOffset? LastModified = null,
	long EstimatedBytes = 0);

public sealed record NxmTransferResult(
	string CompletedPath,
	long SizeBytes,
	string ETag,
	DateTimeOffset? LastModified,
	bool Resumed,
	string Sha256 = "");

public interface INxmTransfer
{
	Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request, IProgress<DownloadProgress> progress,
		CancellationToken cancellationToken);
}

public sealed class NxmTransfer : INxmTransfer, IDisposable
{
	private const int MaximumRedirects = 5;
	private static readonly TimeSpan DefaultBodyIdleTimeout = TimeSpan.FromSeconds(30);
	private readonly HttpClient _client;
	private readonly bool _ownsClient;
	private readonly TimeSpan _bodyIdleTimeout;

	public NxmTransfer(HttpClient client = null, TimeSpan? bodyIdleTimeout = null)
	{
		_bodyIdleTimeout = bodyIdleTimeout ?? DefaultBodyIdleTimeout;
		if (_bodyIdleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(bodyIdleTimeout));
		if (client != null)
		{
			_client = client;
			return;
		}
		_client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
		{
			Timeout = TimeSpan.FromMinutes(30)
		};
		_ownsClient = true;
	}

	public async Task<NxmTransferResult> DownloadAsync(NxmTransferRequest request,
		IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (request.DownloadUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Nexus downloads must use HTTPS.");
		if (request.MaximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(request.MaximumBytes));
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.PartialPath))!);
		var existingBytes = File.Exists(request.PartialPath) ? new FileInfo(request.PartialPath).Length : 0;
		var metadataPath = request.PartialPath + ".meta";
		var etag = request.ETag;
		var lastModified = request.LastModified;
		if (existingBytes > 0 && File.Exists(metadataPath))
		{
			try
			{
				var metadata = JsonConvert.DeserializeObject<PartialMetadata>(await File.ReadAllTextAsync(metadataPath, cancellationToken));
				if (metadata?.BodyComplete == true && metadata.ExpectedBytes == existingBytes && existingBytes <= request.MaximumBytes
					&& (String.IsNullOrWhiteSpace(etag) || String.Equals(etag, metadata.ETag, StringComparison.Ordinal))
					&& (lastModified == null || lastModified == metadata.LastModified))
				{
					// The response body was complete before a pause interrupted hashing.
					// Do not request an invalid byte range starting at the end of the file.
					var verified = await FinalizeDownloadAsync(request, cancellationToken);
					return new NxmTransferResult(request.CompletedPath, verified.SizeBytes,
						metadata.ETag, metadata.LastModified, true, verified.Sha256);
				}
				if (String.IsNullOrWhiteSpace(etag) && lastModified == null)
				{
					etag = metadata?.ETag;
					lastModified = metadata?.LastModified;
				}
			}
			catch (Newtonsoft.Json.JsonException) { }
		}
		var current = request.DownloadUri;
		for (var redirects = 0; ; redirects++)
		{
			using var message = new HttpRequestMessage(HttpMethod.Get, current);
			if (existingBytes > 0 && (!String.IsNullOrWhiteSpace(etag) || lastModified != null))
			{
				message.Headers.Range = new RangeHeaderValue(existingBytes, null);
				message.Headers.IfRange = !String.IsNullOrWhiteSpace(etag)
					? new RangeConditionHeaderValue(new EntityTagHeaderValue(etag))
					: new RangeConditionHeaderValue(lastModified!.Value);
			}

			using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			if (IsRedirect(response.StatusCode))
			{
				if (redirects >= MaximumRedirects) throw new HttpRequestException("The Nexus download redirected too many times.");
				var location = response.Headers.Location ?? throw new HttpRequestException("The Nexus redirect did not include a destination.");
				current = location.IsAbsoluteUri ? location : new Uri(current, location);
				if (current.Scheme != Uri.UriSchemeHttps) throw new HttpRequestException("The Nexus download redirected outside HTTPS.");
				continue;
			}

			response.EnsureSuccessStatusCode();
			var contentRange = response.Content.Headers.ContentRange;
			var responseEtag = response.Headers.ETag;
			var responseLastModified = response.Content.Headers.LastModified;
			var validatorMatches = !String.IsNullOrWhiteSpace(etag)
				? responseEtag is { IsWeak: false } && responseEtag.Tag.Equals(etag, StringComparison.Ordinal)
				: lastModified != null && responseLastModified == lastModified;
			var responseLength = response.Content.Headers.ContentLength;
			if (existingBytes == 0 && response.StatusCode == HttpStatusCode.PartialContent)
				throw new InvalidDataException("The Nexus server returned an unexpected partial response.");
			if (responseLength == null)
				throw new InvalidDataException("The Nexus server did not declare the download size.");
			var rangeIsComplete = contentRange?.From == existingBytes && contentRange.To != null
				&& contentRange.Length != null && contentRange.To.Value + 1 == contentRange.Length.Value
				&& (responseLength == null || responseLength == contentRange.To.Value - existingBytes + 1);
			var resumed = existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent
				&& validatorMatches && rangeIsComplete;
			if (existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent && !resumed)
			{
				File.Delete(request.PartialPath);
				if (File.Exists(metadataPath)) File.Delete(metadataPath);
				return await DownloadAsync(request with { ETag = null, LastModified = null }, progress, cancellationToken);
			}
			if (!resumed) existingBytes = 0;
			if (responseLength != null && existingBytes + responseLength > request.MaximumBytes)
				throw new InvalidDataException("The Nexus download exceeds the configured safety limit.");
			var responseMetadata = new PartialMetadata
			{
				ETag = responseEtag is { IsWeak: false } ? responseEtag.Tag : null,
				LastModified = responseLastModified,
				ExpectedBytes = existingBytes + responseLength.Value
			};
			await DivinityModManager.Util.AtomicFileWriter.WriteAllBytesAsync(metadataPath,
				System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(responseMetadata)), cancellationToken: cancellationToken);

			await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
			long finalBytes;
			await using (var output = new FileStream(request.PartialPath, resumed ? FileMode.Append : FileMode.Create,
				FileAccess.Write, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
			{
				var buffer = new byte[65536];
				var total = existingBytes;
				while (true)
				{
					using var idleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					idleCancellation.CancelAfter(_bodyIdleTimeout);
					int read;
					try { read = await input.ReadAsync(buffer, idleCancellation.Token); }
					catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
					{
						throw new HttpRequestException("The Nexus download stopped sending data.");
					}
					if (read == 0) break;
					total += read;
					if (total > request.MaximumBytes) throw new InvalidDataException("The Nexus download exceeds the configured safety limit.");
					await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
					progress?.Report(new DownloadProgress(total, responseLength == null ? null : existingBytes + responseLength));
				}
				await output.FlushAsync(cancellationToken);
				finalBytes = total;
			}
			if (responseLength != null && finalBytes != existingBytes + responseLength.Value)
				throw new InvalidDataException("The Nexus download ended before the declared response length.");
			if (resumed && contentRange?.Length != finalBytes)
				throw new InvalidDataException("The resumed Nexus download did not reach the declared file length.");
			// Persist the validated body boundary before honoring cancellation during
			// hashing. Header metadata alone can still describe an older partial file.
			responseMetadata.BodyComplete = true;
			await DivinityModManager.Util.AtomicFileWriter.WriteAllBytesAsync(metadataPath,
				System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(responseMetadata)), cancellationToken: CancellationToken.None);
			var (size, sha256) = await FinalizeDownloadAsync(request, cancellationToken);
			return new NxmTransferResult(request.CompletedPath, size,
				responseEtag is { IsWeak: false } ? responseEtag.Tag : null, responseLastModified, resumed, sha256);
		}
	}

	internal static async Task<(long SizeBytes, string Sha256)> FinalizeDownloadAsync(
		NxmTransferRequest request, CancellationToken cancellationToken)
	{
		// Keep the resumable file and its validator until verification succeeds.
		// Cancellation during hashing must not leave an untracked completed file
		// that prevents the next transfer from publishing to the same destination.
		long size;
		string sha256;
		await using (var partial = new FileStream(request.PartialPath, FileMode.Open, FileAccess.Read,
			FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			size = partial.Length;
			sha256 = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(partial, cancellationToken))
				.ToLowerInvariant();
		}
		cancellationToken.ThrowIfCancellationRequested();
		if (File.Exists(request.CompletedPath)) throw new IOException("The completed download filename is already in use.");
		File.Move(request.PartialPath, request.CompletedPath);
		var metadataPath = request.PartialPath + ".meta";
		try { if (File.Exists(metadataPath)) File.Delete(metadataPath); }
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// The verified archive has already been committed. A stale sidecar must
			// not turn that success into an unrecoverable occupied-filename retry.
			DivinityApp.Log($"Verified download completed, but its resume metadata could not be removed: {exception.GetType().Name}");
		}
		return (size, sha256);
	}

	public void Dispose()
	{
		if (_ownsClient) _client.Dispose();
	}

	private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is HttpStatusCode.MovedPermanently
		or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect
		or HttpStatusCode.PermanentRedirect;

	private sealed class PartialMetadata
	{
		public string ETag { get; set; }
		public DateTimeOffset? LastModified { get; set; }
		public long ExpectedBytes { get; set; }
		public bool BodyComplete { get; set; }
	}
}
