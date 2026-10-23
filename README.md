# Huffman Text Compression

A C# library and command-line tool for text compression, storage, and retrieval using the Huffman code.

I wrote this project for an undergraduate data structures course. It is not meant to be used as a real compression tool or library, and it has known limitations (see the code comments).

## Build and Run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) (Windows, macOS, or Linux). No other dependencies.

```sh
dotnet build                                   # build
dotnet run -- compress input.txt output.huf    # build and run
dotnet publish -c Release -o out               # publish to out/, then run out/HuffmanTextCompression
dotnet format                                  # format the source code
```

## CLI Usage

```
HuffmanTextCompression compress <input> <output>
HuffmanTextCompression decompress <input> <output>
```

`compress` reads a UTF-8 text file and writes the compressed file; `decompress` does the reverse. Both print size info:

```
$ HuffmanTextCompression compress LICENSE LICENSE.huf
1,081 char(s) total
829 byte(s) total
4 byte(s) for metadata
131 byte(s) for tree
694 byte(s) for text
1,081 -> 829 byte(s) (76.7% of input)
```

The exit code is `0` on success, `1` on errors (e.g. a missing or invalid file), and `2` on invalid arguments.

## API Usage

```csharp
string text = "breathe, breathe in the air";
string filename = "lyrics.huf";

CompressionInfo info = HuffmanCompressor.Encode(text, filename);
Console.WriteLine(info);

string decodedText = HuffmanCompressor.Decode(filename);
Console.WriteLine(decodedText);
```

## File Format

The compressed file consists of three sections: **metadata**, **encoded tree**, and **encoded text**. An empty text is stored as an empty file.

The metadata is stored in the first 4 bytes of the file and consists of four numbers:
- `ffv` (6 bits) - The file format version, for backward compatibility. Currently 0.
- `l_tree` (18 bits) - The size of the encoded tree, in bytes.
- `b_tree` (4 bits) - The number of useful bits in the last byte of the encoded tree.
- `b_text` (4 bits) - The number of useful bits in the last byte of the encoded text.

The structure of a compressed file looks like this:
```
     6 bits  18 bits  4 bits   4 bits   l_tree bytes
     ------- -------- -------- -------- -------------- --------------
BOF |  ffv  | l_tree | b_tree | b_text | encoded tree | encoded text | EOF
     ------- -------- -------- -------- -------------- --------------
     \________________________________/
                  metadata
```
