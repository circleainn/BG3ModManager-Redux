// Redux's bounded solid-frame decoder; the upstream native entry points remain available.
#include "lz4wrapper.h"

namespace LSLib { namespace Native {
    public ref class CancellableLZ4 abstract sealed
    {
    public:
        static array<byte>^ Decompress(array<byte>^ input, int expectedSize,
            System::Threading::CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (input->Length == 0 || expectedSize < 0)
                throw gcnew System::IO::InvalidDataException("Invalid LZ4 frame size");
            auto output = gcnew array<byte>(expectedSize);
            pin_ptr<byte> source = &input[0];
            // A valid empty frame still needs a non-null destination pointer.
            auto emptyOutput = expectedSize == 0 ? gcnew array<byte>(1) : output;
            pin_ptr<byte> destination = &emptyOutput[0];
            LZ4F_decompressionContext_t context;
            auto result = LZ4F_createDecompressionContext(&context, LZ4F_VERSION);
            if (LZ4F_isError(result))
                throw gcnew System::IO::InvalidDataException("Cannot create LZ4 decoder");
            try
            {
                size_t consumed = 0, produced = 0;
                do
                {
                    token.ThrowIfCancellationRequested();
                    size_t sourceSize = input->Length - consumed;
                    size_t outputSize = min((size_t)65536, (size_t)expectedSize - produced);
                    result = LZ4F_decompress(context, destination + produced, &outputSize,
                        source + consumed, &sourceSize, nullptr);
                    if (LZ4F_isError(result) || (sourceSize == 0 && outputSize == 0 && result != 0))
                        throw gcnew System::IO::InvalidDataException("Invalid or truncated LZ4 frame");
                    consumed += sourceSize;
                    produced += outputSize;
                } while (result != 0);
                token.ThrowIfCancellationRequested();
                if (consumed != input->Length || produced != expectedSize)
                    throw gcnew System::IO::InvalidDataException("LZ4 frame size does not match the package index");
                return output;
            }
            finally
            {
                LZ4F_freeDecompressionContext(context);
            }
        }
    };
} }
