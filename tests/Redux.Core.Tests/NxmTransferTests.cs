using DivinityModManager.AppServices;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

internal sealed class NxmTransferTests
{
	public void CancelledFinalVerificationPreservesPartialAndCanBeRetried()
	{
		using var fixture = new TransferFixture("hello world");
		File.WriteAllText(fixture.Partial + ".meta", "{\"ETag\":\"\\\"persisted\\\"\",\"ExpectedBytes\":11,\"BodyComplete\":true}");
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		RegressionAssert.Throws<OperationCanceledException>(() => NxmTransfer.FinalizeDownloadAsync(
			fixture.Request(null!), cancellation.Token).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.Completed));
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Partial));
		RegressionAssert.True(File.Exists(fixture.Partial + ".meta"));

		using var transfer = new NxmTransfer(new HttpClient(new StubHandler(_ =>
			throw new InvalidOperationException("A fully received partial must not request a range beyond the end of the remote file."))));
		var result = transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.True(result.Resumed);
		RegressionAssert.Equal(11L, result.SizeBytes);
		RegressionAssert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", result.Sha256);
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Completed));
		RegressionAssert.False(File.Exists(fixture.Partial));
		RegressionAssert.False(File.Exists(fixture.Partial + ".meta"));
	}

	public void FailedFinalVerificationDoesNotPublishUnverifiedDownload()
	{
		using var fixture = new TransferFixture("verified later");
		File.WriteAllText(fixture.Partial + ".meta", "resume validator");
		// This handle permits rename but prevents hashing. Promoting the file
		// before verification would strand it under the completed filename.
		using (var held = new FileStream(fixture.Partial, FileMode.Open, FileAccess.Write, FileShare.Delete))
		{
			RegressionAssert.Throws<IOException>(() => NxmTransfer.FinalizeDownloadAsync(
				fixture.Request(null!), CancellationToken.None).GetAwaiter().GetResult());
			RegressionAssert.False(File.Exists(fixture.Completed));
			RegressionAssert.True(File.Exists(fixture.Partial));
			RegressionAssert.True(File.Exists(fixture.Partial + ".meta"));
		}
		RegressionAssert.Equal("verified later", File.ReadAllText(fixture.Partial));
	}

	public void ApproximateNexusSizeDoesNotCompleteAnUnfinishedPartial()
	{
		using var fixture = new TransferFixture("hello");
		File.WriteAllText(fixture.Partial + ".meta", "{\"ETag\":\"\\\"persisted\\\"\",\"ExpectedBytes\":11}");
		var handler = new StubHandler(request =>
		{
			RegressionAssert.Equal(5L, request.Headers.Range!.Ranges.First().From!.Value);
			var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent(" world") };
			response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, 10, 11);
			response.Headers.ETag = new EntityTagHeaderValue("\"persisted\"");
			return response;
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));
		var result = transfer.DownloadAsync(fixture.Request(null!) with { EstimatedBytes = 5 },
			null!, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.True(result.Resumed);
		RegressionAssert.Equal(11L, result.SizeBytes);
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Completed));
	}

	public void HeaderLengthAloneDoesNotPublishAnOlderPartial()
	{
		using var fixture = new TransferFixture("stale");
		File.WriteAllText(fixture.Partial + ".meta", "{\"ETag\":\"\\\"new-header\\\"\",\"ExpectedBytes\":5,\"BodyComplete\":false}");
		var requests = 0;
		using var transfer = new NxmTransfer(new HttpClient(new StubHandler(_ =>
		{
			requests++;
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fresh") };
		})));
		transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.Equal(1, requests);
		RegressionAssert.Equal("fresh", File.ReadAllText(fixture.Completed));
	}

	public void ResumeMetadataCleanupFailureDoesNotRejectVerifiedDownload()
	{
		using var fixture = new TransferFixture("hello world");
		File.WriteAllText(fixture.Partial + ".meta", "resume metadata");
		using var held = new FileStream(fixture.Partial + ".meta", FileMode.Open, FileAccess.Read, FileShare.Read);
		var result = NxmTransfer.FinalizeDownloadAsync(fixture.Request(null!), CancellationToken.None).GetAwaiter().GetResult();
		RegressionAssert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", result.Sha256);
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Completed));
		RegressionAssert.True(File.Exists(fixture.Partial + ".meta"));
	}

	public void MatchingRangeResponseResumesPartialFile()
	{
		using var fixture = new TransferFixture("hello ");
		var handler = new StubHandler(request =>
		{
			RegressionAssert.Equal(6L, request.Headers.Range!.Ranges.First().From!.Value);
			var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("world") };
			response.Content.Headers.ContentRange = new ContentRangeHeaderValue(6, 10, 11);
			response.Headers.ETag = new EntityTagHeaderValue("\"same\"");
			return response;
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		var result = transfer.DownloadAsync(fixture.Request("\"same\""), null!, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.True(result.Resumed);
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Completed));
		RegressionAssert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", result.Sha256);
	}

	public void FullResponseRestartsInsteadOfAppendingPartialFile()
	{
		using var fixture = new TransferFixture("stale");
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fresh") });
		using var transfer = new NxmTransfer(new HttpClient(handler));

		var result = transfer.DownloadAsync(fixture.Request("\"old\""), null!, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(result.Resumed);
		RegressionAssert.Equal("fresh", File.ReadAllText(fixture.Completed));
	}

	public void SidecarValidatorEnablesResumeAfterRestart()
	{
		using var fixture = new TransferFixture("hello ");
		File.WriteAllText(fixture.Partial + ".meta", "{\"ETag\":\"\\\"persisted\\\"\"}");
		var handler = new StubHandler(request =>
		{
			RegressionAssert.Equal("\"persisted\"", request.Headers.IfRange!.EntityTag!.Tag);
			var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("world") };
			response.Content.Headers.ContentRange = new ContentRangeHeaderValue(6, 10, 11);
			response.Headers.ETag = new EntityTagHeaderValue("\"persisted\"");
			return response;
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		var result = transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.True(result.Resumed);
		RegressionAssert.Equal("hello world", File.ReadAllText(fixture.Completed));
		RegressionAssert.False(File.Exists(fixture.Partial + ".meta"));
	}

	public void MismatchedResumeValidatorRestartsFromZero()
	{
		using var fixture = new TransferFixture("stale ");
		var requestCount = 0;
		var handler = new StubHandler(request =>
		{
			requestCount++;
			if (requestCount == 1)
			{
				var partial = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("tail") };
				partial.Content.Headers.ContentRange = new ContentRangeHeaderValue(6, 9, 10);
				partial.Headers.ETag = new EntityTagHeaderValue("\"different\"");
				return partial;
			}
			RegressionAssert.Equal(null, request.Headers.Range);
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("fresh") };
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		var result = transfer.DownloadAsync(fixture.Request("\"expected\""), null!, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.False(result.Resumed);
		RegressionAssert.Equal(2, requestCount);
		RegressionAssert.Equal("fresh", File.ReadAllText(fixture.Completed));
	}

	public void ShortResponseIsNotPublishedAsComplete()
	{
		using var fixture = new TransferFixture(String.Empty);
		var handler = new StubHandler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("short") };
			response.Content.Headers.ContentLength = 20;
			return response;
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		RegressionAssert.Throws<InvalidDataException>(() =>
			transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.Completed));
	}

	public void ApproximateMetadataSizeDoesNotRejectCompleteResponse()
	{
		using var fixture = new TransferFixture(String.Empty);
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("valid") });
		using var transfer = new NxmTransfer(new HttpClient(handler));

		var request = fixture.Request(null!) with { EstimatedBytes = 4 };
		var result = transfer.DownloadAsync(request, null!, CancellationToken.None).GetAwaiter().GetResult();

		RegressionAssert.Equal(5L, result.SizeBytes);
		RegressionAssert.Equal("valid", File.ReadAllText(fixture.Completed));
	}

	public void UnsolicitedPartialResponseIsNotPublished()
	{
		using var fixture = new TransferFixture(String.Empty);
		var handler = new StubHandler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("wrong") };
			response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 4, 5);
			return response;
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		RegressionAssert.Throws<InvalidDataException>(() =>
			transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.Completed));
	}

	public void ResponseWithoutADeclaredLengthIsNotPublished()
	{
		using var fixture = new TransferFixture(String.Empty);
		var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new UnknownLengthContent("unknown")
		});
		using var transfer = new NxmTransfer(new HttpClient(handler));

		RegressionAssert.Throws<InvalidDataException>(() =>
			transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.Completed));
	}

	public void StalledResponseBodyTimesOutWithoutPublishing()
	{
		using var fixture = new TransferFixture(String.Empty);
		var handler = new StubHandler(_ =>
		{
			var content = new StreamContent(new BlockingReadStream());
			content.Headers.ContentLength = 1;
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
		});
		using var transfer = new NxmTransfer(new HttpClient(handler), TimeSpan.FromMilliseconds(25));

		RegressionAssert.Throws<HttpRequestException>(() =>
			transfer.DownloadAsync(fixture.Request(null!), null!, CancellationToken.None).GetAwaiter().GetResult());
		RegressionAssert.False(File.Exists(fixture.Completed));
	}

	private sealed class TransferFixture : IDisposable
	{
		private readonly string _root = Path.Combine(Path.GetTempPath(), "ReduxNxmTransferTests", Guid.NewGuid().ToString("N"));
		public string Partial { get; }
		public string Completed { get; }
		public TransferFixture(string partial)
		{
			Directory.CreateDirectory(_root);
			Partial = Path.Combine(_root, "file.zip.part");
			Completed = Path.Combine(_root, "file.zip");
			File.WriteAllText(Partial, partial);
		}
		public NxmTransferRequest Request(string etag) => new(new Uri("https://example.test/file"), Partial, Completed, 1024, etag);
		public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
		public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(_response(request));
	}

	private sealed class UnknownLengthContent : HttpContent
	{
		private readonly byte[] _bytes;
		public UnknownLengthContent(string value) => _bytes = Encoding.UTF8.GetBytes(value);
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();
		protected override bool TryComputeLength(out long length)
		{
			length = 0;
			return false;
		}
	}

	private sealed class BlockingReadStream : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			await Task.Delay(Timeout.Infinite, cancellationToken);
			return 0;
		}
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
