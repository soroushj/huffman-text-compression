using System.Text;

namespace HuffmanTextCompression;

// Maps each character to its Huffman code. The codes are found by walking the tree from the root to each leaf,
// appending a 0 bit for each left branch and a 1 bit for each right branch. The table is indexed by the character
// itself, for constant-time lookup while encoding.
class HuffmanCodeTable
{
    private readonly HuffmanCode[] _codeTable;

    public int NumberOfCodes { get; private set; }

    public HuffmanCodeTable(HuffmanTree tree)
    {
        _codeTable = new HuffmanCode[char.MaxValue + 1];

        // If the text consists entirely of a single character
        if (tree.IsLeaf)
        {
            // Use code 0 for the character
            _codeTable[tree.Character] = new HuffmanCode(0, 1);
            NumberOfCodes = 1;
            return;
        }

        GetAllHuffmanCodes(tree, 0, 0);
    }

    private void GetAllHuffmanCodes(HuffmanTree tree, ushort code, int codeLength)
    {
        if (tree.IsLeaf)
        {
            // Known limitation: codes are stored in 16 bits, so texts with very uneven character frequencies (which
            // produce longer codes) cannot be compressed
            if (codeLength > HuffmanCode.MaxLength)
            {
                throw new NotSupportedException($"The text cannot be compressed because its character frequencies are too uneven (some Huffman codes would be longer than {HuffmanCode.MaxLength} bits).");
            }

            _codeTable[tree.Character] = new HuffmanCode(code, (byte)codeLength);
            NumberOfCodes++;
            return;
        }

        // Put a 0 bit and go left
        GetAllHuffmanCodes(tree.Left, (ushort)(code << 1), codeLength + 1);
        // Put a 1 bit and go right
        GetAllHuffmanCodes(tree.Right, (ushort)((code << 1) | 1), codeLength + 1);
    }

    public HuffmanCode GetHuffmanCode(char character)
    {
        return _codeTable[character];
    }
}

// A Huffman code: the Length least significant bits of Code
readonly record struct HuffmanCode(ushort Code, byte Length)
{
    public const int MaxLength = 16;

    public override string ToString()
    {
        StringBuilder str = new();

        for (ushort mask = (ushort)(1 << (Length - 1)); mask != 0; mask >>= 1)
        {
            str.Append((Code & mask) == 0 ? '0' : '1');
        }

        return str.ToString();
    }
}
