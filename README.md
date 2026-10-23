# Huffman Text Compression

A C# library and command-line tool for text compression, storage, and retrieval using the Huffman code.

I wrote this project for an undergraduate data structures course. It is not meant to be used as a real compression tool or library, and it has known limitations (see the code comments).

## Build and Run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) (Windows, macOS, or Linux). The library and the CLI have no other dependencies; the tests use [xUnit](https://xunit.net), which is downloaded automatically.

The library is in `src/HuffmanTextCompression`, the CLI in `src/HuffmanTextCompression.Cli`, and the tests in `tests/HuffmanTextCompression.Tests`. From the repository root:

```sh
# Build everything
dotnet build

# Run the tests
dotnet test

# Build and run the CLI
dotnet run --project src/HuffmanTextCompression.Cli -- compress LICENSE LICENSE.huff

# Publish the CLI to out/, then run out/huff
dotnet publish src/HuffmanTextCompression.Cli -c Release -o out

# Format the source code
dotnet format
```

## CLI Usage

```
huff compress <input> <output>    Compress a UTF-8 text file
huff decompress <input> <output>  Decompress a compressed file
```

Both commands print the sizes and the compression ratio (uncompressed size / compressed size). The uncompressed size is the size of the text in UTF-8 (without BOM), which is the same as the file size for a typical text file:

```
$ huff compress LICENSE LICENSE.huff
Compressed 'LICENSE' to 'LICENSE.huff'
Characters:        1,081
Uncompressed size: 1,081 bytes
Compressed size:   829 bytes (metadata: 4, tree: 131, text: 694)
Compression ratio: 1.30

$ huff decompress LICENSE.huff LICENSE.copy
Decompressed 'LICENSE.huff' to 'LICENSE.copy'
Characters:        1,081
Uncompressed size: 1,081 bytes
Compressed size:   829 bytes (metadata: 4, tree: 131, text: 694)
Compression ratio: 1.30
```

The exit code is `0` on success, `1` on errors (e.g. a missing or invalid file), and `2` on invalid arguments.

## API Usage

To use the library in another project, reference it with `dotnet add reference <path>/src/HuffmanTextCompression`.

`Encode` reads text from a `TextReader` and writes the compressed data to a `Stream`; `Decode` does the reverse. Both return the compression info. Any reader, writer, or stream works (e.g. a `StreamReader`, `StringWriter`, or `FileStream`), and neither method closes them.

```csharp
string text = "breathe, breathe in the air";

using (StringReader reader = new(text))
using (FileStream file = File.Create("breathe.huff"))
{
    CompressionInfo info = HuffmanCompressor.Encode(reader, file);
    Console.WriteLine(info);
    // Characters:        27
    // Uncompressed size: 27 bytes
    // Compressed size:   38 bytes (metadata: 4, tree: 23, text: 11)
    // Compression ratio: 0.71
}

string decodedText;

using (FileStream file = File.OpenRead("breathe.huff"))
using (StringWriter writer = new())
{
    CompressionInfo info = HuffmanCompressor.Decode(file, writer);
    Console.WriteLine(info); // Same as above
    decodedText = writer.ToString();
}

Console.WriteLine(decodedText == text); // True
```

## File Format

The compressed file consists of three sections: **metadata**, **encoded tree**, and **encoded text**. An empty text is stored as an empty file.

The metadata is stored in the first 4 bytes of the file and consists of four numbers:
- `ffv` (6 bits) - The file format version, for backward compatibility. Currently 0.
- `s_tree` (18 bits) - The size of the encoded tree, in bytes.
- `b_tree` (4 bits) - The number of useful bits in the last byte of the encoded tree.
- `b_text` (4 bits) - The number of useful bits in the last byte of the encoded text.

The structure of a compressed file looks like this:
```
     6 bits  18 bits  4 bits   4 bits   s_tree bytes
     ------- -------- -------- -------- -------------- --------------
BOF |  ffv  | s_tree | b_tree | b_text | encoded tree | encoded text | EOF
     ------- -------- -------- -------- -------------- --------------
     \________________________________/
                  metadata
```
