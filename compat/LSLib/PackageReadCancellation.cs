using System.IO.MemoryMappedFiles;
using System.Threading;

namespace LSLib.LS;

/// <summary>Flows cancellation through LSLib's synchronous package readers without global shared state.</summary>
public sealed class PackageReadCancellation : IDisposable
{
    private static readonly AsyncLocal<CancellationToken> Ambient = new();
    private readonly CancellationToken previous;
    public static CancellationToken Current => Ambient.Value;

    public PackageReadCancellation(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        previous = Ambient.Value;
        Ambient.Value = token;
    }

    public void Dispose() => Ambient.Value = previous;

    public static void ReadArray(MemoryMappedViewAccessor view, long offset, byte[] buffer, int count)
    {
        for (var position = 0; position < count;)
        {
            Current.ThrowIfCancellationRequested();
            var length = Math.Min(65536, count - position);
            if (view.ReadArray(offset + position, buffer, position, length) != length)
                throw new EndOfStreamException();
            position += length;
        }
        Current.ThrowIfCancellationRequested();
    }

    // LZ4 blocks have no restart points. Decode sequences in bounded pieces instead
    // of asking the legacy codec to expand a whole, potentially multi-GB member.
    public static byte[] DecodeBlock(byte[] input, int outputSize, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var output = GC.AllocateUninitializedArray<byte>(outputSize);
        var source = 0;
        var destination = 0;
        while (source < input.Length)
        {
            token.ThrowIfCancellationRequested();
            var instruction = input[source++];
            var literals = ReadLength(instruction >> 4);
            if (literals > input.Length - source || literals > output.Length - destination)
                throw new InvalidDataException("Invalid LZ4 literal length.");
            CopyLiterals(literals);
            if (source == input.Length) break;
            if (input.Length - source < 2) throw new InvalidDataException("Truncated LZ4 match.");
            var distance = input[source] | (input[source + 1] << 8);
            source += 2;
            if (distance == 0 || distance > destination) throw new InvalidDataException("Invalid LZ4 match offset.");
            var match = checked(ReadLength(instruction & 15) + 4);
            if (match > output.Length - destination) throw new InvalidDataException("Invalid LZ4 match length.");
            // Copy at most the distance each time: LZ4 matches may overlap and
            // repeat the bytes produced by this very match.
            while (match > 0)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(match, Math.Min(distance, 65536));
                output.AsSpan(destination - distance, count).CopyTo(output.AsSpan(destination, count));
                destination += count;
                if (count == distance) distance += count;
                match -= count;
            }
        }
        token.ThrowIfCancellationRequested();
        if (destination != outputSize) throw new InvalidDataException("LZ4 output size does not match the package index.");
        return output;

        int ReadLength(int length)
        {
            if (length != 15) return length;
            byte extension;
            do
            {
                if ((source & 65535) == 0) token.ThrowIfCancellationRequested();
                if (source == input.Length) throw new InvalidDataException("Truncated LZ4 length.");
                extension = input[source++];
                length = checked(length + extension);
            } while (extension == 255);
            return length;
        }

        void CopyLiterals(int remaining)
        {
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(remaining, 65536);
                input.AsSpan(source, count).CopyTo(output.AsSpan(destination, count));
                source += count;
                destination += count;
                remaining -= count;
            }
        }
    }
}
