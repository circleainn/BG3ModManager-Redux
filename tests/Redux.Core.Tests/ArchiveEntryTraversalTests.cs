using DivinityModManager.AppServices;
using DivinityModManager.Util;

using SharpCompress.Archives;
using SharpCompress.Readers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Redux.Core.Tests;

public sealed class ArchiveEntryTraversalTests
{
	// 7-Zip LZMA2 solid archive: twelve entry-00.pak ... entry-11.pak files, each
	// containing "solid-sevenzip-regression-" repeated 1024 times. No external tool needed at test time.
	private const string SolidFixture = "N3q8ryccAARmjbWiMwEAAAAAAAAjAAAAAAAAAKmqiHTk3/8Ajl0AOZvJzftOYctPS4b6Pk7kQFnSsuAMvMvcmiSYGEEugjnnyxSyrhxGcNax4rq1/802w5+MSe+9v/SUJQ6iwfAQzLt2eFlWwv/ZEFfF+GdqyThrE/LHuydunsBYeQHWVrsIEY1Zm6FXgIWLQeSpTuKyQv62SZRxHz2PJOFwnqcjX+woy4XRlZiKfh+FemEAAAAAAIEzB64P1TAkPpZsKR9IHZj0X2OHfhxBMZRX3TaWpe+IXQp2nRoXMfrow2I4qelT5DVS9dmtQvDBtZjsXzyyKijqwVHa6cdu+6a14oCRHd+OKu6y7ekjFJ5uhxuNoWjGG7mt7+XkUHq8zpXPB1CAphVWBm3payicvWVXMXSg7mILyL+Y4BfQWRa/b0QOC4tOQ0h3Lc+hAnfVlMgAFwaAlgEJgJ0ABwsBAAEjAwEBBV0AEAAADIJWCgHVlI2hAAA=";

	public void SolidArchiveReadsCompressedContentOnceAndPreservesEntries()
	{
		using var baselineSource = new CountingStream(Convert.FromBase64String(SolidFixture));
		using (var baseline = ArchiveFactory.OpenArchive(baselineSource, new ReaderOptions { LeaveStreamOpen = true }))
		{
			RegressionAssert.True(baseline.IsSolid);
			foreach (var entry in baseline.Entries)
			{
				using var stream = entry.OpenEntryStream();
				stream.CopyTo(Stream.Null);
			}
		}

		using var source = new CountingStream(Convert.FromBase64String(SolidFixture));
		using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		var names = new List<string>();
		var expected = Encoding.UTF8.GetBytes(String.Concat(Enumerable.Repeat("solid-sevenzip-regression-", 1024)));
		ArchiveEntryTraversal.ReadSelectedAsync(archive, _ => true, async (entry, stream) =>
		{
			using var contents = new MemoryStream();
			await stream.CopyToAsync(contents);
			RegressionAssert.SequenceEqual(expected, contents.ToArray());
			names.Add(entry.Key!);
		}).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(Enumerable.Range(0, 12).Select(index => $"entry-{index:D2}.pak"), names);
		RegressionAssert.True(source.BytesRead < baselineSource.BytesRead / 2);
	}

	public void CancellationDuringSelectedEntryStopsBeforeLaterEntries()
	{
		using var cancellation = new CancellationTokenSource();
		using var source = new CountingStream(Convert.FromBase64String(SolidFixture));
		using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		var selected = 0;
		RegressionAssert.Throws<OperationCanceledException>(() =>
			ArchiveEntryTraversal.ReadSelectedAsync(archive, _ => true, async (_, stream) =>
			{
				selected++;
				var bytes = new byte[16];
				RegressionAssert.Equal(16, await stream.ReadAsync(bytes, cancellation.Token));
				cancellation.Cancel();
				await stream.CopyToAsync(Stream.Null, cancellation.Token);
			}, cancellation.Token).GetAwaiter().GetResult());
		RegressionAssert.Equal(1, selected);
	}

	public void CancellationWhileSkippingEntryStopsBeforeSelectedEntry()
	{
		using var cancellation = new CancellationTokenSource();
		using var source = new CountingStream(Convert.FromBase64String(SolidFixture));
		using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		var selected = 0;
		// Load the index before arming cancellation, so it fires on compressed content.
		RegressionAssert.True(archive.IsSolid);
		source.CancelOnNextRead = cancellation;
		RegressionAssert.Throws<OperationCanceledException>(() =>
			ArchiveEntryTraversal.ReadSelectedAsync(archive, entry => entry.Key != "entry-00.pak",
				(_, _) => { selected++; return Task.CompletedTask; }, cancellation.Token).GetAwaiter().GetResult());
		RegressionAssert.True(cancellation.IsCancellationRequested);
		RegressionAssert.Equal(0, selected);
		RegressionAssert.Equal(source.BytesReadAtCancellation, source.BytesRead);
	}

	public void SolidTraversalStopsAfterLastSelectedEntryAcrossBlocks()
	{
		var fixture = ReadPakFixture();
		var expected = new Dictionary<string, byte[]>();
		using var baselineSource = new CountingStream(fixture);
		using (var archive = ArchiveFactory.OpenArchive(baselineSource, new ReaderOptions { LeaveStreamOpen = true }))
		{
			using var reader = archive.ExtractAllEntries();
			while (reader.MoveToNextEntry())
			{
				using var stream = reader.OpenEntryStream();
				using var contents = new MemoryStream();
				stream.CopyTo(contents);
				if (reader.Entry.Key!.EndsWith(".pak", StringComparison.Ordinal))
					expected.Add(reader.Entry.Key, contents.ToArray());
			}
		}

		using var source = new CountingStream(fixture);
		using var selectedArchive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		var names = new List<string>();
		ArchiveEntryTraversal.ReadSelectedAsync(selectedArchive,
			entry => entry.Key!.EndsWith(".pak", StringComparison.Ordinal), async (entry, stream) =>
			{
				using var contents = new MemoryStream();
				await stream.CopyToAsync(contents);
				RegressionAssert.SequenceEqual(expected[entry.Key!], contents.ToArray());
				names.Add(entry.Key!);
			}).GetAwaiter().GetResult();
		RegressionAssert.SequenceEqual(new[] { "000Mod.pak", "020Mod.pak" }, names);
		RegressionAssert.True(source.BytesRead + 64 * 1024 < baselineSource.BytesRead);
	}

	public void FailedSolidCallbackDoesNotDrainEntryDuringDisposal()
	{
		using var source = new CountingStream(Convert.FromBase64String(SolidFixture));
		using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		var expected = new IOException("Callback failure");
		long bytesAtFailure = 0;
		var visited = 0;
		var actual = RegressionAssert.Throws<IOException>(() =>
			ArchiveEntryTraversal.ReadSelectedAsync(archive, _ => true, (_, _) =>
			{
				visited++;
				bytesAtFailure = source.BytesRead;
				return Task.FromException(expected);
			}).GetAwaiter().GetResult());
		RegressionAssert.True(ReferenceEquals(expected, actual));
		RegressionAssert.Equal(1, visited);
		RegressionAssert.Equal(bytesAtFailure, source.BytesRead);
	}

	public void NoSelectedSolidEntriesNeverOpensContentStream()
	{
		using var source = new CountingStream(ReadPakFixture());
		using var archive = ArchiveFactory.OpenArchive(source, new ReaderOptions { LeaveStreamOpen = true });
		RegressionAssert.True(archive.IsSolid);
		var indexBytes = source.BytesRead;
		ArchiveEntryTraversal.ReadSelectedAsync(archive, _ => false,
			(_, _) => throw new InvalidOperationException("No entry should be selected")).GetAwaiter().GetResult();
		RegressionAssert.Equal(indexBytes, source.BytesRead);
	}

	public void SolidArchivePreflightReadsActualPaksAcrossBlocks()
	{
		var root = Path.Combine(Path.GetTempPath(), "ReduxSolidPakPreflightTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			var path = Path.Combine(root, "SolidMods.7z");
			var original = ReadPakFixture();
			File.WriteAllBytes(path, original);
			var report = ArchivePackagePreflightService.AnalyzeAsync(path, []).GetAwaiter().GetResult();
			RegressionAssert.Equal(4, report.EntryCount);
			RegressionAssert.Equal(2, report.Packages.Count);
			RegressionAssert.True(report.Packages.All(package => package.IsReadable));
			RegressionAssert.SequenceEqual(new[] { "Solid Fixture 0", "Solid Fixture 2" }, report.Packages.Select(package => package.Mod.Name));
			RegressionAssert.SequenceEqual(new[] { "000Mod.pak", "020Mod.pak" }, report.Packages.Select(package => package.PackagePath.Split("::")[1]));
			RegressionAssert.SequenceEqual(original, File.ReadAllBytes(path));
		}
		finally { Directory.Delete(root, true); }
	}

	private static byte[] ReadPakFixture()
	{
		// Two solid blocks: PAK + readme, then PAK + 2 MiB of irrelevant trailing data.
		using var resource = typeof(ArchiveEntryTraversalTests).Assembly.GetManifestResourceStream(
			"Redux.Core.Tests.Fixtures.SolidPakWithTrailingData.7z")!;
		using var contents = new MemoryStream();
		resource.CopyTo(contents);
		return contents.ToArray();
	}

	private sealed class CountingStream(byte[] bytes) : Stream
	{
		private readonly MemoryStream _inner = new(bytes);
		public long BytesRead { get; private set; }
		public long BytesReadAtCancellation { get; private set; }
		public CancellationTokenSource? CancelOnNextRead { get; set; }
		private int Count(int count)
		{
			BytesRead += count;
			if (CancelOnNextRead is { } cancellation)
			{
				CancelOnNextRead = null;
				BytesReadAtCancellation = BytesRead;
				cancellation.Cancel();
			}
			return count;
		}
		public override int Read(byte[] buffer, int offset, int count) => Count(_inner.Read(buffer, offset, count));
		public override int Read(Span<byte> buffer) => Count(_inner.Read(buffer));
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
			Count(await _inner.ReadAsync(buffer, offset, count, token));
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) =>
			Count(await _inner.ReadAsync(buffer, token));
		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;
		public override long Length => _inner.Length;
		public override long Position { get => _inner.Position; set => _inner.Position = value; }
		public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
		public override void Flush() { }
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
	}
}
