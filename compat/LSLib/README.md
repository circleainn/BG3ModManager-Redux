# Redux package decoding adaptations

`Compression.cs` and `PackageReader.cs` are MIT-licensed adaptations of the files
at the same names under `External/lslib/LSLib/LS`, pinned at submodule commit
`2b88c0ff3026c5a56a780449629c704a9b5d9573`. The upstream license is included here.
`Directory.Build.targets` substitutes these sources only in Redux's LSLib build;
it does not edit the submodule. Rebase these two adaptations when updating LSLib.

The changes route mapped input reads and raw LZ4 block expansion through bounded
64 KiB copies with cancellation checks, release decompressed buffers on disposal,
and pass cancellation into the native solid-frame decoder. The new native decoder
uses the vendored LZ4 frame API with at most 64 KiB of output per call and always
releases its context, including cancellation and invalid input. It validates the
declared output size. Existing upstream native entry points are unchanged.

`PackageReadCancellation` uses an async-local scope so parallel loads do not share
cancellation state. Import/preflight and extraction keep that scope alive across
metadata and member reads. This does not change editor-package compression or
make memory allocation and operating-system I/O preemptible.

Regression fixtures compare block output with both upstream encoders across
literal/repeating/random inputs, exercise a real solid v18 PAK, reject malformed
frames, and cancel large raw-block and frame decoding. Multipart fixtures exercise
complete-set staging, replacement, rollback, restart recovery, retention, and
Download Manager preparation separately from the unavailable Vivid download.
