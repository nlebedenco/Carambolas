using System;
using System.Collections.Generic;

using Xunit;

using Carambolas.Net.Tests.Attributes;

namespace Carambolas.Net.Tests.Unit
{
    [TestCaseOrderer("Carambolas.Net.Tests.Orderers.PriorityOrderer", "Carambolas.Net.Tests")]
    public class ChannelInboundNodeTreeTests
    {
        private sealed class Node: Net.Channel.Inbound.Node { }
        private static void Allocator(out Node node) => node = new Node();

        [Fact]
        public void IsEmptyFromEmptyTree()
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.True(tree.IsEmpty);
        }

        [Fact]
        public void FirstFromEmptyTree()
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.Null(tree.First);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(65535)]
        public void TryGetFromEmptyTree(ushort seq)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.False(tree.TryGet(seq, out Node node));
            Assert.Null(node);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(65535)]
        public void TryRemoveFromEmptyTree(ushort seq)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.False(tree.TryRemove(seq, out Node node));
            Assert.Null(node);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(65535)]
        public void RemoveAndDisposeBeforeSequenceNumberFromEmptyTree(ushort seq)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            tree.RemoveAndDisposeBefore(seq);
        }

        [Fact]
        public void RemoveAndDisposeAllFromEmptyTree()
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            tree.RemoveAndDisposeAll();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(65535)]
        public void TryAddOne(ushort seq)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.True(tree.TryAdd(new Node() { SequenceNumber = seq }));
            Assert.False(tree.IsEmpty);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        [InlineData(65535)]
        public void TryAddOrGetOne(ushort seq)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            Assert.True(tree.TryAddOrGet(seq, Allocator, out Node node1));
            Assert.NotNull(node1);
            Assert.False(tree.IsEmpty);

            Assert.False(tree.TryAddOrGet(seq, Allocator, out Node node2));
            Assert.False(tree.IsEmpty);

            Assert.Same((object)node1, (object)node2);
        }

        [Theory]
        [InlineData(0, 10)]
        public void TryAddMany(ushort offset, int n)
        {
            var random = new Random();
            var set = new HashSet<ushort>();
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            for (int i = 0; i < n; ++i)
            {
                ushort seq;
                do
                    seq = (ushort)(offset + random.Next(0, Protocol.Ordinal.Window.Size - 1));
                while (set.Contains(seq));

                Assert.True(tree.TryAdd(new Node() { SequenceNumber = seq }));
                Assert.False(tree.IsEmpty);
                set.Add(seq);
            }

            foreach (var seq in set)
            {
                Assert.False(tree.TryAdd(new Node() { SequenceNumber = seq }));
                Assert.False(tree.IsEmpty);
            }
        }

        [Theory]
        [InlineData(0, 10, 10)]
        [InlineData(16384, 10, 10)]
        [InlineData(32767, 10, 10)]
        [InlineData(32768, 10, 10)]
        [InlineData(49152, 10, 10)]
        [InlineData(65535, 10, 10)]
        public void TryAddOrGetMany(ushort offset, int n, int repetitions)
        {
            var random = new Random();

            var dict = new Dictionary<ushort, Node>();
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();

            for (int r = 0; r < repetitions; ++r)
            {
                for (int i = 0; i < n; ++i)
                {
                    ushort seq;
                    do
                        seq = (ushort)(offset + random.Next(0, Protocol.Ordinal.Window.Size - 1));
                    while (dict.ContainsKey(seq));

                    Assert.True(tree.TryAddOrGet(seq, Allocator, out Node node));
                    Assert.NotNull(node);
                    Assert.False(tree.IsEmpty);
                    dict.Add(seq, node);
                }

                foreach (var kv in dict)
                {
                    Assert.False(tree.TryAddOrGet(kv.Key, Allocator, out Node node));
                    Assert.NotNull(node);
                    Assert.False(tree.IsEmpty);
                }
            }
        }

        [Theory]
        [InlineData(0, 10, 10)]
        [InlineData(16384, 10, 10)]
        [InlineData(32767, 10, 10)]
        [InlineData(32768, 10, 10)]
        [InlineData(49152, 10, 10)]
        [InlineData(65535, 10, 10)]
        public void TryAddManyThenRemove(ushort offset, int n, int repetitions)
        {
            var random = new Random();

            var set = new HashSet<ushort>();
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();

            for (int r = 0; r < repetitions; ++r)
            {
                for (int i = 0; i < n; ++i)
                {
                    ushort seq;
                    do
                        seq = (ushort)(offset + random.Next(0, Protocol.Ordinal.Window.Size - 1));
                    while (set.Contains(seq));

                    Assert.True(tree.TryAdd(new Node() { SequenceNumber = seq }));
                    Assert.False(tree.IsEmpty);
                    set.Add(seq);
                }

                foreach (var seq in set)
                {
                    Assert.True(tree.TryRemove(seq, out Node node));
                    Assert.NotNull(node);
                    n--;
                    if (n > 0)
                        Assert.False(tree.IsEmpty);
                }

                Assert.True(tree.IsEmpty);
                set.Clear();
            }
        }

        [Theory]
        [InlineData(0, 10, 3)]
        [InlineData(16384, 10, 7)]
        [InlineData(32767, 10, 11)]
        [InlineData(32768, 10, 2)]
        [InlineData(49152, 10, 1)]
        [InlineData(65535, 10, 9)]
        [InlineData(65535, 10, 0)]
        public void RemoveAndDisposeBefore(ushort offset, int n, int index)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();

            for (int i = 0; i < n; ++i)
            {
                var seq = new Protocol.Ordinal((ushort)(offset + i));
                Assert.True(tree.TryAdd(new Node() { SequenceNumber = seq }));
                Assert.False(tree.IsEmpty);
            }

            var keep = new Protocol.Ordinal((ushort)(offset + index));
            tree.RemoveAndDisposeBefore(keep);
            for (int i = 0; i < n; ++i)
            {
                var seq = (ushort)(offset + i);
                if (seq < keep)
                    Assert.False(tree.TryGet(seq, out Node node));
                else
                    Assert.True(tree.TryGet(seq, out Node node));

                if (index >= n)
                    Assert.True(tree.IsEmpty);
            }
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(16384, 10)]
        [InlineData(32767, 10)]
        [InlineData(32768, 10)]
        [InlineData(49152, 10)]
        [InlineData(65535, 10)]
        public void RemoveAndDisposeAll(ushort offset, int n)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();

            for (int i = 0; i < n; ++i)
            {
                var seq = new Protocol.Ordinal((ushort)(offset + i));
                Assert.True(tree.TryAdd(new Node() { SequenceNumber = seq }));
                Assert.False(tree.IsEmpty);
            }

            tree.RemoveAndDisposeAll();
            Assert.True(tree.IsEmpty);
            Assert.Null(tree.First);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 32768)]
        [InlineData(0, 10)]
        [InlineData(16384, 10)]
        [InlineData(32767, 10)]
        [InlineData(32768, 10)]
        [InlineData(49152, 10)]
        [InlineData(65535, 10)]
        public void Traverse(ushort offset, int n)
        {
            var tree = new Carambolas.Net.Channel.Inbound.Node.Tree<Node>();
            var list = new List<ushort>(n);
            var dict = new Dictionary<ushort, Node>();

            for (int i = 0; i < n; ++i)
            {
                var seq = (ushort)(offset + i);
                list.Add(seq);
                var node = new Node() { SequenceNumber = seq };
                dict.Add(seq, node);
                Assert.True(tree.TryAdd(node));
                Assert.False(tree.IsEmpty);
            }

            int index = 0;
            Assert.True(tree.Traverse((Node node, ref int s) =>
            {
                Assert.Equal(list[index], node.SequenceNumber);
                Assert.Same((object)dict[(ushort)node.SequenceNumber], (object)node);
                index++;
                return true;
            }, ref index));

            Assert.Equal(n, index);
        }
    }
}

