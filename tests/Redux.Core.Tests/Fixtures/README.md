`SolidPakWithTrailingData.7z` is a synthetic regression fixture, not a downloaded mod.
It contains two solid LZMA2 blocks in this order:

| Entry | Size | Block | Contents |
| --- | ---: | ---: | --- |
| `000Mod.pak` | 665 bytes | 0 | Valid LSLib v18 package with `Mods/Example0/meta.lsx` |
| `010Readme.txt` | 28 bytes | 0 | `Readme between selected mods` |
| `020Mod.pak` | 665 bytes | 1 | Valid LSLib v18 package with `Mods/Example2/meta.lsx` |
| `999Tail.bin` | 2 MiB | 1 | Eight repetitions of a 256 KiB pseudorandom block |

The PAKs were generated with the repository's `PackageWriterFactory` (no PAK
compression). Module names are `Solid Fixture 0` and `Solid Fixture 2`, author
`Audit`, folders `Example0` and `Example2`, type `Add-on`, and `Version64`
`36028797018963968`. UUIDs are `d18b1759-04a2-47f1-9288-0c7f63eec093` and
`a884cddf-1a0a-4810-adc4-a09139066f9e`. The tail was created with
`new Random(1623).NextBytes(new byte[262144])` and repeated eight times.

Archive creation used 7-Zip with `a -t7z -m0=lzma2 -mx=3 -ms=2f -mqs=off`.
The tests use the embedded archive and require no 7-Zip installation. Its tail
is large enough to distinguish unnecessary compressed reads from normal
decoder buffering without a large installed-mod fixture.
