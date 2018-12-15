# Huffman Text Compression

A C# library for text compression, storage, and retrieval using the Huffman code.

## Usage

```csharp
string text = "breathe, breathe in the air";
string filename = "lyrics.huf";

CompressionInfo info = HuffmanCompressor.Encode(text, filename);
Console.WriteLine(info);

string decodedText = HuffmanCompressor.Decode(filename);
Console.WriteLine(decodedText);
```

## File Format

The compressed file consists of three sections: **metadata**, **encoded tree**, and **encoded text**.

The metadata is stored in the first 4 bytes of the file and consists of four numbers:
- `ffv` *(6 bits)* The file format version. This number is intended for backward compatibility and currently has a value of 0.
- `l_tree` *(18 bits)* The size of the encoded tree, in bytes.
- `b_tree` *(4 bits)* The number of the useful bits in the last byte of the encoded tree.
- `b_text` *(4 bits)* The number of the useful bits in the last byte of the encoded text.

The structure of a compressed file looks like this:
```
     6 bits  18 bits  4 bits   4 bits   l_tree bytes
     ------- -------- -------- -------- -------------- --------------
BOF |  ffv  | l_tree | b_tree | b_text | encoded tree | encoded text | EOF
     ------- -------- -------- -------- -------------- --------------
     \________________________________/
                  metadata
```
