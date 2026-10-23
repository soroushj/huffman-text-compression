using System.Diagnostics.CodeAnalysis;

namespace HuffmanTextCompression;

// A node of a Huffman tree, which is also the root of the subtree below it. A leaf holds a character; an internal node
// always has two children. Frequency is the number of occurrences of the leaf's character, or the sum of the children's
// frequencies for an internal node. Trees decoded from a file have no frequencies (Frequency is -1).
class HuffmanTree
{
    public char Character { get; set; }
    public int Frequency { get; }
    public HuffmanTree? Left { get; set; }
    public HuffmanTree? Right { get; set; }

    public HuffmanTree()
    {
        Character = '\0';
        Frequency = -1;
    }

    public HuffmanTree(char character, int frequency)
    {
        Character = character;
        Frequency = frequency;
    }

    public HuffmanTree(int frequency, HuffmanTree left, HuffmanTree right)
    {
        Character = '\0';
        Frequency = frequency;
        Left = left;
        Right = right;
    }

    // In a HuffmanTree, Right == null if and only if Left == null
    [MemberNotNullWhen(false, nameof(Left), nameof(Right))]
    public bool IsLeaf => Left == null;

    // Builds the Huffman tree of the text: starts with one leaf per distinct character, then repeatedly merges the two
    // least frequent trees until only one tree is left
    public static HuffmanTree Build(string text)
    {
        if (text.Length == 0)
        {
            throw new ArgumentException("The text cannot be empty.", nameof(text));
        }

        int[] frequencies = new int[char.MaxValue + 1];

        foreach (char c in text)
        {
            frequencies[c]++;
        }

        HuffmanTreeList list = new();

        for (int i = 0; i <= char.MaxValue; i++)
        {
            if (frequencies[i] != 0)
            {
                list.Add(new HuffmanTree((char)i, frequencies[i]));
            }
        }

        while (!list.HasExactlyOneTree())
        {
            HuffmanTree top1 = list.RemoveTopTree();
            HuffmanTree top2 = list.RemoveTopTree();

            list.Add(new HuffmanTree(top1.Frequency + top2.Frequency, top1, top2));
        }

        return list.RemoveTopTree();
    }
}
