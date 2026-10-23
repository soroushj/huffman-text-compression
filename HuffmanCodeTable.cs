using System.Text;

namespace HuffmanTextCompression;

class HuffmanCodeTable
{
    private readonly HuffmanCode[] CodeTable;
    private int _NumberOfCodes;

    public HuffmanCodeTable(HuffmanTree tree)
    {
        CodeTable = new HuffmanCode[0xFFFF + 1];

        // If the text consists entirely of a single character
        if (tree.IsLeaf())
        {
            // Use code 0 for the character
            CodeTable[tree.Character] = new HuffmanCode(0, 1);
            _NumberOfCodes = 1;
            return;
        }

        _NumberOfCodes = 0;
        GetAllHuffmanCodes(tree, 0, 0);
    }

    private void GetAllHuffmanCodes(HuffmanTree tree, ushort code, byte codeLength)
    {
        if (tree.IsLeaf())
        {
            // Known limitation: codes are stored in 16 bits, so texts with very uneven character
            // frequencies (which produce longer codes) cannot be compressed
            if (codeLength > HuffmanCode.MaxLength)
            {
                throw new NotSupportedException("The text cannot be compressed: its character frequencies are too uneven, which would require Huffman codes longer than " + HuffmanCode.MaxLength + " bits.");
            }

            CodeTable[tree.Character] = new HuffmanCode(code, codeLength);
            _NumberOfCodes++;
            return;
        }

        // Put a 0 bit and go left
        GetAllHuffmanCodes(tree.Left, (ushort)(code << 1), (byte)(codeLength + 1));
        // Put a 1 bit and go right
        GetAllHuffmanCodes(tree.Right, (ushort)((code << 1) | 1), (byte)(codeLength + 1));
    }

    public HuffmanCode GetHuffmanCode(char character)
    {
        return CodeTable[character];
    }

    public int NumberOfCodes
    {
        get
        {
            return _NumberOfCodes;
        }
    }
}

readonly struct HuffmanCode
{
    public const int MaxLength = 16;

    public readonly ushort Code;
    public readonly byte Length;

    public HuffmanCode(ushort code, byte length)
    {
        Code = code;
        Length = length;
    }

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
