namespace HuffmanTextCompression;

// The priority queue used by HuffmanTree.GenerateHuffmanTree: a linked list of trees, sorted by their frequency
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

    private ListNode? _first;

    // Adds the tree to the list such that the list remains sorted by the frequency of its trees
    public void Add(HuffmanTree tree)
    {
        ListNode newNode = new(tree);

        if (_first == null)
        {
            _first = newNode;
        }
        else if (tree.Frequency < _first.Tree.Frequency)
        {
            // Minimum frequency: add newNode at the top of the list
            newNode.Next = _first;
            _first = newNode;
        }
        else
        {
            ListNode curNode;

            for (curNode = _first; curNode.Next != null; curNode = curNode.Next)
            {
                if (tree.Frequency < curNode.Next.Tree.Frequency)
                {
                    // Add newNode after curNode
                    newNode.Next = curNode.Next;
                    curNode.Next = newNode;
                    break;
                }
            }

            // If newNode was not added to the list (maximum frequency)
            if (newNode.Next == null)
            {
                // Add newNode at the bottom of the list (curNode is now the last node)
                curNode.Next = newNode;
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
        return top;
    }

    public bool HasExactlyOneTree()
    {
        return _first != null && _first.Next == null;
    }
}
