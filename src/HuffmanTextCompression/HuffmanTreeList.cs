namespace HuffmanTextCompression;

// The priority queue used by HuffmanTree.Build: a linked list of trees, sorted by their frequency
// (node.Tree.Frequency), so the least frequent tree is always at the top. In practice, the built-in PriorityQueue
// should be used instead; this list is written from scratch for demonstration purposes.
class HuffmanTreeList
{
    private class ListNode
    {
        public HuffmanTree Tree;
        public ListNode? Next;

        public ListNode(HuffmanTree tree)
        {
            Tree = tree;
        }
    }

    // Both are null if and only if the list is empty. The last node is tracked so that adding a tree with the maximum
    // frequency (common while building a Huffman tree) takes constant time.
    private ListNode? _first;
    private ListNode? _last;

    // Adds the tree to the list such that the list remains sorted by the frequency of its trees
    public void Add(HuffmanTree tree)
    {
        ListNode newNode = new(tree);

        if (_first == null || _last == null)
        {
            _first = newNode;
            _last = newNode;
        }
        else if (tree.Frequency < _first.Tree.Frequency)
        {
            // Minimum frequency: add newNode at the top of the list
            newNode.Next = _first;
            _first = newNode;
        }
        else if (tree.Frequency >= _last.Tree.Frequency)
        {
            // Maximum frequency: add newNode at the bottom of the list
            _last.Next = newNode;
            _last = newNode;
        }
        else
        {
            // Add newNode before the first node with a higher frequency (which exists, since _last has one)
            for (ListNode curNode = _first; curNode.Next != null; curNode = curNode.Next)
            {
                if (tree.Frequency < curNode.Next.Tree.Frequency)
                {
                    // Add newNode after curNode
                    newNode.Next = curNode.Next;
                    curNode.Next = newNode;
                    break;
                }
            }
        }
    }

    public HuffmanTree RemoveTopTree()
    {
        if (_first == null)
        {
            throw new InvalidOperationException("The list is empty.");
        }

        HuffmanTree top = _first.Tree;
        _first = _first.Next;

        if (_first == null)
        {
            _last = null;
        }

        return top;
    }

    public bool HasExactlyOneTree()
    {
        return _first != null && _first.Next == null;
    }
}
