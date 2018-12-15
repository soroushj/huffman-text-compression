namespace HuffmanTextCompression
{
  // each node of this linked list keeps a HuffmanTree
  // the list is sorted by the frequency of the trees (node.Tree.Frequency)
  class HuffmanTreeList
  {
    private class ListNode
    {
      public HuffmanTree Tree;
      public ListNode Next;

      public ListNode(HuffmanTree tree)
      {
        Tree = tree;
      }
    }

    private ListNode First;

    // adds the tree to the list such that the list remains sorted by the frequency of its trees
    public void Add(HuffmanTree tree)
    {
      ListNode newNode = new ListNode(tree);

      if (First == null)
      {
        First = newNode;
      }
      else if (tree.Frequency < First.Tree.Frequency)
      {
        // min frequency
        // add newNode at the top of the list
        newNode.Next = First;
        First = newNode;
      }
      else
      {
        ListNode curNode;

        for (curNode = First; curNode.Next != null; curNode = curNode.Next)
        {
          if (tree.Frequency < curNode.Next.Tree.Frequency)
          {
            // add newNode after curNode
            newNode.Next = curNode.Next;
            curNode.Next = newNode;
            break;
          }
        }

        // if newNode was not added to the list (max frequency)
        if (newNode.Next == null)
        {
          // add newNode at the bottom of the list
          // curNode is now the last node
          curNode.Next = newNode;
        }
      }
    }

    public HuffmanTree RemoveTopTree()
    {
      if (First == null)
      {
        return null;
      }

      HuffmanTree top = First.Tree;
      First = First.Next;
      return top;
    }

    public bool HasExactlyOneTree()
    {
      return First != null && First.Next == null;
    }
  }
}
