using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DivinityModManager.AppServices;

/// <summary>Reads solid 7z data once while retaining direct entry access for other archives.</summary>
public static class ArchiveEntryTraversal
{
	public static async Task ReadSelectedAsync(
		IArchive archive,
		Func<IEntry, bool> select,
		Func<IEntry, Stream, Task> readEntry,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (archive.Type == ArchiveType.SevenZip && archive.IsSolid && archive is IAsyncArchive asyncArchive)
		{
			// The solid reader yields the same entry objects as the archive index. Use identity,
			// not names or sort positions, so duplicate names and separate solid blocks stay distinct.
			var remaining = new HashSet<IEntry>(ReferenceEqualityComparer.Instance);
			foreach (var entry in archive.Entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!entry.IsDirectory && select(entry)) remaining.Add(entry);
			}
			cancellationToken.ThrowIfCancellationRequested();
			if (remaining.Count == 0) return;

			await using var reader = await asyncArchive.ExtractAllEntriesAsync();
			var readerControl = (IReader)reader;
			// EntryStream disposal otherwise drains a partially read solid entry after cancellation.
			using var cancellation = cancellationToken.Register(readerControl.Cancel);
			while (await reader.MoveToNextEntryAsync(cancellationToken))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (reader.Entry.IsDirectory) continue;
				await using var stream = await reader.OpenEntryStreamAsync(cancellationToken);
				try
				{
					if (remaining.Remove(reader.Entry)) await readEntry(reader.Entry, stream);
					// Read necessary prefixes and finish selected entries, but never decompress an unused tail.
					await stream.CopyToAsync(Stream.Null, cancellationToken);
					if (remaining.Count == 0)
					{
						readerControl.Cancel();
						break;
					}
				}
				catch
				{
					// Stop before stream disposal: a callback failure must not drain the rest of its entry.
					readerControl.Cancel();
					throw;
				}
			}
			if (remaining.Count > 0)
				throw new InvalidDataException("The archive reader did not return every selected entry.");
		}
		else
		{
			foreach (var entry in archive.Entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (entry.IsDirectory || !select(entry)) continue;
				await using var stream = entry.OpenEntryStream();
				await readEntry(entry, stream);
			}
		}
		cancellationToken.ThrowIfCancellationRequested();
	}
}
