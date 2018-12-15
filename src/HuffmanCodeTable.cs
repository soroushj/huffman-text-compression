namespace HuffmanTextCompression
{
  class HuffmanCodeTable
  {
    private HuffmanCode[] CodeTable;
    private int _NumberOfCodes;

    public HuffmanCodeTable(HuffmanTree tree)
    {
      CodeTable = new HuffmanCode[0xFFFF + 1];

      // if the text entirely consists of a single character
      if (tree.IsLeaf())
      {
        // use code 0 for the character
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
        CodeTable[tree.Character] = new HuffmanCode(code, codeLength);
        _NumberOfCodes++;
        return;
      }

      // put a 0 bit and go left
      GetAllHuffmanCodes(tree.Left, (ushort)(code << 1), (byte)(codeLength + 1));
      // put a 1 bit and go right
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

  struct HuffmanCode
  {
    public readonly ushort Code;
    public readonly byte Length;

    public HuffmanCode(ushort code, byte length)
    {
      Code = code;
      Length = length;
    }

    public override string ToString()
    {
      string str = "";

      for (ushort mask = (ushort)(1 << (Length - 1)); mask != 0; mask >>= 1)
      {
        str += ((Code & mask) == 0 ? '0' : '1');
      }

      return str;
    }
  }
}
