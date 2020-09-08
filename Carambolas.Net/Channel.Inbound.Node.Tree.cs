using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas.Net
{
    internal partial struct Channel
    {
        public partial struct Inbound
        {
            public abstract partial class Node
            {
                /// <summary>
                /// Red-black tree based on Cormen et al, "Introduction to Algorithms", 3rd ed. chapter 13
                /// <para/>
                /// Note that becase of the particular way <see cref="Protocol.Ordinal"/> defines its comparison operators, special care 
                /// must be taken to never add any two nodes that are more than <see cref="Protocol.Ordinal.Window.Size"/> apart.
                /// Otherwise, certain sequences of operations (add and remove) may produce invalid trees with unreachable nodes.
                /// For instance if we add the sequence 14623, 8125, 14265, 58127, 37329, 39852, 14184, 6452, 46451, 3007 and then try
                /// to remove the nodes in the same order we'll find out that node 58127 can't be found anymore. This is due to the fact 
                /// that after nodes 14623, 8125 and 14625 are removed node 37329 becomes the root and now node 58127 that was initially 
                /// put on the left-side subtree (because it is lower than 14623, 8125 and 14265) is not lower than 37329 and the search 
                /// always fails.
                /// </summary>
                public struct Tree<T> where T : Node
                {
                    public delegate void Allocator(out T message);
                    public delegate bool Visitor<S>(T message, ref S state);

                    private sealed class Sentinel: Node { }

                    private Node nil;
                    private Node root;

                    private T first;
                    public T First => first;

                    public bool IsEmpty => root == nil;

                    public bool TryGet(Protocol.Ordinal seq, out T node)
                    {
                        var z = root;
                        while (z != nil)
                        {
                            if (z.SequenceNumber == seq)
                            {
                                node = z as T;
                                return true;
                            }

                            z = (seq < z.SequenceNumber) ? z.left : z.right;
                        }

                        node = null;
                        return false;
                    }

                    public bool TryAdd(T message)
                    {
                        if (nil == null)
                            root = nil = new Sentinel();

                        var seq = message.SequenceNumber;
                        var y = nil;
                        var x = root;

                        while (x != nil)
                        {
                            y = x;
                            if (seq == x.SequenceNumber)
                                return false;

                            x = (seq < x.SequenceNumber) ? x.left : x.right;
                        }

                        Add(x, y, message);
                        if (first == null || message.SequenceNumber < first.SequenceNumber)
                            first = message;

                        return true;
                    }

                    public bool TryAddOrGet(Protocol.Ordinal seq, Allocator allocate, out T node)
                    {
                        if (nil == null)
                            root = nil = new Sentinel();

                        var y = nil;
                        var x = root;

                        while (x != nil)
                        {
                            y = x;
                            if (seq == x.SequenceNumber)
                            {
                                node = x as T;
                                return false;
                            }

                            x = (seq < x.SequenceNumber) ? x.left : x.right;
                        }

                        allocate(out node);
                        node.SequenceNumber = seq;

                        Add(x, y, node);
                        if (first == null || node.SequenceNumber < first.SequenceNumber)
                            first = node;

                        return true;
                    }

                    private void Add(Node x, Node y, Node z)
                    {
                        z.parent = y;
                        if (y == nil)
                            root = z;
                        else if (z.SequenceNumber < y.SequenceNumber)
                            y.left = z;
                        else
                            y.right = z;

                        z.left = nil;
                        z.right = nil;
                        z.red = true;

                        // Fixup
                        while (z.parent.red)
                        {
                            if (z.parent == z.parent.parent.left)
                            {
                                y = z.parent.parent.right;
                                if (y.red)
                                {
                                    y.red = false;

                                    z.parent.red = false;
                                    z.parent.parent.red = true;
                                    z = z.parent.parent;
                                }
                                else
                                {
                                    if (z == z.parent.right)
                                    {
                                        z = z.parent;
                                        RotateLeft(z);
                                    }

                                    z.parent.red = false;
                                    z.parent.parent.red = true;
                                    RotateRight(z.parent.parent);
                                }
                            }
                            else
                            {
                                y = z.parent.parent.left;
                                if (y.red)
                                {
                                    y.red = false;

                                    z.parent.red = false;
                                    z.parent.parent.red = true;
                                    z = z.parent.parent;
                                }
                                else
                                {
                                    if (z == z.parent.left)
                                    {
                                        z = z.parent;
                                        RotateRight(z);
                                    }

                                    z.parent.red = false;
                                    z.parent.parent.red = true;
                                    RotateLeft(z.parent.parent);
                                }
                            }
                        }

                        root.red = false;
                    }

                    /// <summary>
                    /// Return true if message identified by seq was found and removed; otherwise, false.
                    /// Message is removed but not disposed.
                    /// </summary>
                    public bool TryRemove(Protocol.Ordinal seq, out T node)
                    {
                        if (!TryGet(seq, out node))
                            return false;

                        if (node == first)
                            first = first.right as T ?? first.parent as T;

                        Remove(node);
                        return true;
                    }

                    /// <summary>
                    /// Remove all messages below <paramref name="seq"/>. May be called on an empty tree. Removed messages are disposed.
                    /// </summary>
                    public void RemoveAndDisposeBefore(Protocol.Ordinal seq)
                    {
                        var message = first;
                        while (message != null && message.SequenceNumber < seq)
                        {
                            var next = message.right as T ?? message.parent as T;
                            Remove(message);
                            message.Dispose();
                            message = next;
                        }
                        first = message;
                    }

                    public bool Traverse<S>(Visitor<S> visit, ref S state) => Traverse(root, visit, ref state);

                    private bool Traverse<S>(Node node, Visitor<S> visit, ref S state) => node == nil
                        || (Traverse(node.left, visit, ref state) && visit(node as T, ref state) && Traverse(node.right, visit, ref state));

                    // From the "Instructor’s Manual by Thomas H. Cormen"
                    //
                    // Conceptually, deleting a node z from a binary search tree T has three cases:
                    // 1. If z has no children, just remove it.
                    // 2. If z has just one child, then make that child take z’s position in the tree, dragging the child’s subtree along.
                    // 3. If z has two children, then find z’s successor y and replace z by y in the tree.
                    //    y must be in z’s right subtree and have no left child. The rest of z’s original
                    //    right subtree becomes y’s new right subtree, and z’s left subtree becomes y’s
                    //    new left subtree. 
                    //
                    //    This case is a little tricky because the exact sequence of steps taken depends on
                    //    whether y is z’s right child.
                    //    The code organizes the cases a bit differently. Since it will move subtrees around
                    //    within the binary search tree, it uses a subroutine, TRANSPLANT, to replace one
                    //    subtree as the child of its parent by another subtree.
                    private void Remove(Node z)
                    {
                        Node x;
                        var red = z.red;

                        if (z.left == nil)                   // z has no left child
                        {
                            x = z.right;
                            Transplant(z, z.right);
                        }
                        else if (z.right == nil)             // z has just just the left child
                        {
                            x = z.left;
                            Transplant(z, z.left);
                        }
                        else                                 // z has 2 children
                        {
                            // Successor must be the node with the lowest key that is larger than the removed node's key.
                            var y = z.right;
                            while (y.left != nil)
                                y = y.left;

                            red = y.red;
                            x = y.right;
                            if (y.parent == z)
                                x.parent = y;
                            else
                            {
                                Transplant(y, y.right);
                                y.right = z.right;
                                y.right.parent = y;
                            }

                            Transplant(z, y);
                            y.left = z.left;
                            y.left.parent = y;
                            y.red = z.red;
                        }

                        // Fix up from x if color removed was red.
                        if (!red)
                        {
                            while (x != root && !x.red)
                            {
                                if (x == x.parent.left)
                                {
                                    var w = x.parent.right;
                                    if (w.red)
                                    {
                                        w.red = false;
                                        x.parent.red = true;
                                        RotateLeft(x.parent);
                                        w = x.parent.right;
                                    }

                                    if (!w.left.red && !w.right.red)
                                    {
                                        w.red = true;
                                        x = x.parent;
                                    }
                                    else
                                    {
                                        if (!w.right.red)
                                        {
                                            w.left.red = false;
                                            w.red = true;
                                            RotateRight(w);
                                            w = x.parent.right;
                                        }

                                        w.red = x.parent.red;
                                        x.parent.red = false;
                                        w.right.red = false;
                                        RotateLeft(x.parent);
                                        x = root;
                                    }
                                }
                                else
                                {
                                    var w = x.parent.left;
                                    if (w.red)
                                    {
                                        w.red = false;
                                        x.parent.red = true;
                                        RotateRight(x.parent);
                                        w = x.parent.left;
                                    }

                                    if (!w.right.red && !w.left.red)
                                    {
                                        w.red = true;
                                        x = x.parent;
                                    }
                                    else
                                    {
                                        if (!w.left.red)
                                        {
                                            w.right.red = false;
                                            w.red = true;
                                            RotateLeft(w);
                                            w = x.parent.left;
                                        }

                                        w.red = x.parent.red;
                                        x.parent.red = false;
                                        w.left.red = false;
                                        RotateRight(x.parent);
                                        x = root;
                                    }
                                }
                            }

                            x.red = false;
                        }

                        z.parent = default;
                        z.left = default;
                        z.right = default;
                        z.red = default;
                    }

                    private void Transplant(Node u, Node v)
                    {
                        if (u.parent == nil)
                            root = v;
                        else if (u == u.parent.left)
                            u.parent.left = v;
                        else
                            u.parent.right = v;

                        v.parent = u.parent;
                    }

                    private void RotateLeft(Node x)
                    {
                        var y = x.right;
                        x.right = y.left;

                        if (y.left != nil)
                            y.left.parent = x;

                        y.parent = x.parent;

                        if (x.parent == nil)
                            root = y;
                        else if (x == x.parent.left)
                            x.parent.left = y;
                        else
                            x.parent.right = y;

                        y.left = x;
                        x.parent = y;
                    }

                    private void RotateRight(Node x)
                    {
                        var y = x.left;
                        x.left = y.right;

                        if (y.left != nil)
                            y.right.parent = x;

                        y.parent = x.parent;

                        if (x.parent == nil)
                            root = y;
                        else if (x == y.parent.right)
                            x.parent.right = y;
                        else
                            x.parent.left = y;

                        y.right = x;
                        x.parent = y;
                    }

                    /// <summary>
                    /// Dispose all messages and clear.
                    /// </summary>
                    public void RemoveAndDisposeAll()
                    {
                        Dispose(root);
                        root = nil;
                        first = null;
                    }

                    private void Dispose(Node node)
                    {
                        if (node == nil)
                            return;

                        var left = node.left;
                        var right = node.right;

                        Dispose(left);
                        node.Dispose();
                        Dispose(right);
                    }
                }
            }
        }
    }
}
