// Redux cancellation adaptation of LSLib; see LICENSE and README.md in this directory.
﻿using LZ4;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;

namespace LSLib.LS;


public class LZ4DecompressionStream : Stream
{
    private readonly MemoryMappedViewAccessor View;
    private readonly long Offset;
    private readonly int Size;
    private readonly int DecompressedSize;
    private MemoryStream Decompressed;
    private readonly System.Threading.CancellationToken Token = PackageReadCancellation.Current;

    public LZ4DecompressionStream(MemoryMappedViewAccessor view, long offset, int size, int decompressedSize)
    {
        View = view;
        Offset = offset;
        Size = size;
        DecompressedSize = decompressedSize;
    }

    private void DoDecompression()
    {
        var compressed = new byte[Size];
        using var cancellation = new PackageReadCancellation(Token);
        PackageReadCancellation.ReadArray(View, Offset, compressed, Size);

        var decompressed = PackageReadCancellation.DecodeBlock(compressed, DecompressedSize, Token);
        Decompressed = new MemoryStream(decompressed);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Decompressed?.Dispose();
        Decompressed = null;
        base.Dispose(disposing);
    }

    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return false; } }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Decompressed == null)
        {
            DoDecompression();
        }

        return Decompressed.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }


    public override long Position
    {
        get { return Decompressed?.Position ?? 0; }
        set { throw new NotSupportedException(); }
    }

    public override bool CanTimeout { get { return false; } }
    public override bool CanWrite { get { return false; } }
    public override long Length { get { return DecompressedSize; } }
    public override void SetLength(long value) { throw new NotSupportedException(); }
    public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    public override void Flush() { }
}

public static class CompressionHelpers
{
    public static CompressionFlags MakeCompressionFlags(CompressionMethod method, LSCompressionLevel level)
    {
        // Avoid setting compression level bits if there is no compression
        if (method == CompressionMethod.None) return 0;

        return method.ToFlags() | level.ToFlags();
    }

    public static byte[] Decompress(byte[] compressed, int decompressedSize, CompressionFlags compression, bool chunked = false)
    {
        switch (compression.Method())
        {
            case CompressionMethod.None:
                return compressed;

            case CompressionMethod.Zlib:
                {
                    using (var compressedStream = new MemoryStream(compressed))
                    using (var decompressedStream = new MemoryStream())
                    using (var stream = new ZLibStream(compressedStream, CompressionMode.Decompress))
                    {
                        stream.CopyToAsync(decompressedStream, 65536, PackageReadCancellation.Current).GetAwaiter().GetResult();
                        return decompressedStream.ToArray();
                    }
                }

            case CompressionMethod.LZ4:
                if (chunked)
                {
                    var decompressed = Native.CancellableLZ4.Decompress(compressed, decompressedSize, PackageReadCancellation.Current);
                    return decompressed;
                }
                else
                {
                    return PackageReadCancellation.DecodeBlock(compressed, decompressedSize, PackageReadCancellation.Current);
                }

            case CompressionMethod.Zstd:
                {
                    using (var compressedStream = new MemoryStream(compressed))
                    using (var decompressedStream = new MemoryStream())
                    using (var stream = new ZstdSharp.DecompressionStream(compressedStream))
                    {
                        stream.CopyToAsync(decompressedStream, 65536, PackageReadCancellation.Current).GetAwaiter().GetResult();
                        return decompressedStream.ToArray();
                    }
                }

            default:
                throw new InvalidDataException($"No decompressor found for this format: {compression}");
        }
    }

    public static Stream Decompress(MemoryMappedFile file, MemoryMappedViewAccessor view, long sourceOffset,
        int sourceSize, int decompressedSize, CompressionFlags compression)
    {
        // MemoryMappedView considers a size of 0 to mean "entire stream"
        if (sourceSize == 0)
        {
            return new MemoryStream();
        }

        switch (compression.Method())
        {
            case CompressionMethod.None:
                return file.CreateViewStream(sourceOffset, sourceSize, MemoryMappedFileAccess.Read);

            case CompressionMethod.Zlib:
                var sourceStream = file.CreateViewStream(sourceOffset, sourceSize, MemoryMappedFileAccess.Read);
                return new ZLibStream(sourceStream, CompressionMode.Decompress);

            case CompressionMethod.LZ4:
                return new LZ4DecompressionStream(view, sourceOffset, sourceSize, decompressedSize);

            case CompressionMethod.Zstd:
                var zstdStream = file.CreateViewStream(sourceOffset, sourceSize, MemoryMappedFileAccess.Read);
                return new ZstdSharp.DecompressionStream(zstdStream);

            default:
                throw new InvalidDataException($"No decompressor found for this format: {compression}");
        }
    }

    public static byte[] Compress(byte[] uncompressed, CompressionFlags compression)
    {
        return Compress(uncompressed, compression.Method(), compression.Level());
    }

    public static byte[] Compress(byte[] uncompressed, CompressionMethod method, LSCompressionLevel level, bool chunked = false)
    {
        return method switch
        {
            CompressionMethod.None => uncompressed,
            CompressionMethod.Zlib => CompressZlib(uncompressed, level),
            CompressionMethod.LZ4 => CompressLZ4(uncompressed, level, chunked),
            CompressionMethod.Zstd => CompressZstd(uncompressed, level),
            _ => throw new ArgumentException("Invalid compression method specified")
        };
    }

    public static byte[] CompressZlib(byte[] uncompressed, LSCompressionLevel level)
    {
        var zLevel = level switch
        {
            LSCompressionLevel.Fast => CompressionLevel.Fastest,
            LSCompressionLevel.Default => CompressionLevel.Optimal,
            LSCompressionLevel.Max => CompressionLevel.SmallestSize,
            _ => throw new ArgumentException()
        };

        using var outputStream = new MemoryStream();
        using (var compressor = new ZLibStream(outputStream, zLevel, true))
        {
            compressor.Write(uncompressed, 0, uncompressed.Length);
        }


        return outputStream.ToArray();
    }

    public static byte[] CompressLZ4(byte[] uncompressed, LSCompressionLevel compressionLevel, bool chunked = false)
    {
        if (chunked)
        {
            return Native.LZ4FrameCompressor.Compress(uncompressed);
        }
        else if (compressionLevel == LSCompressionLevel.Fast)
        {
            return LZ4Codec.Encode(uncompressed, 0, uncompressed.Length);
        }
        else
        {
            return LZ4Codec.EncodeHC(uncompressed, 0, uncompressed.Length);
        }
    }

    public static byte[] CompressZstd(byte[] uncompressed, LSCompressionLevel level)
    {
        var zLevel = level switch
        {
            LSCompressionLevel.Fast => 3,
            LSCompressionLevel.Default => 9,
            LSCompressionLevel.Max => 22,
            _ => throw new ArgumentException()
        };

        using var outputStream = new MemoryStream();
        using (var compressor = new ZstdSharp.CompressionStream(outputStream, zLevel, 0, true))
        {
            compressor.Write(uncompressed, 0, uncompressed.Length);
        }

        return outputStream.ToArray();
    }
}
