using System;
using System.Collections.Generic;

using Xunit;

namespace Carambolas.Net.Tests
{    
    public class MemoryTests
    {       
        [Fact]
        public void Disposal()
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();
            var version = m.Version;

            m.Dispose();
            
            Assert.NotEqual(version, m.Version);
            Assert.Equal(0, m.Capacity);
            Assert.Equal(0, m.Length);
        }

        [Fact]
        public void InitialCapacity()
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();

            Assert.Equal(0, m.Capacity);
        }

        [Fact]
        public void InitialLength()
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();

            Assert.Equal(0, m.Length);
        }

        [Theory]
        [InlineData(3, 64)]
        [InlineData(251, 256)]
        [InlineData(1021, 1024)]
        [InlineData(3071, 3072)]
        [InlineData(60001, 65536)]
        public void SetLength(int expectedLength, int expectedCapacity)
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();

            m.Length = expectedLength;

            Assert.Equal(expectedLength, m.Length);
            Assert.Equal(expectedCapacity, m.Capacity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(64)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(4096)]
        [InlineData(65536)]
        public void CopyFromAndTo(int length)
        {            
            var source = new byte[length];
            var destination = new byte[length];
            var expected = new byte[length];

            for (int i = 0; i < length; ++i)
                expected[i] = source[i] = (byte)i;

            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();
            m.CopyFrom(source);
            Assert.Equal(expected, source);

            m.CopyTo(destination);
            Assert.Equal(new ArraySegment<byte>(expected, 0, length) as IEnumerable<byte>, new ArraySegment<byte>(destination, 0, length) as IEnumerable<byte>);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(70001)]
        public void InvalidGetterIndexFails(int index)
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();

            Assert.Throws<IndexOutOfRangeException>(() => { var x = m[index]; });
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(70000)]
        public void InvalidSetterIndexFails(int index)
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();

            Assert.Throws<ArgumentOutOfRangeException>(() => m[index] = 0);
        }

        [Theory]
        [InlineData(0, 1, 2, 3, 4, 64)]
        [InlineData(0, 1, 2, 250, 251, 256)]
        [InlineData(0, 1, 2, 1020, 1021, 1024)]
        [InlineData(0, 1, 2, 3070, 3071, 3072)]
        [InlineData(0, 1, 2, 60001, 60002, 65536)]
        public void ValidGetterSetterIndexes(int i0, int i1, int i2, int i3, int expectedLength, int expectedCapacity)
        {
            var pool = new Carambolas.Net.Memory.Pool();
            var m = pool.Get();
           
            m[i0] = (byte)i0;
            m[i1] = (byte)i1;
            m[i2] = (byte)i2;

            Assert.Equal(3, m.Length);
            Assert.Equal(64, m.Capacity);

            m[i3] = (byte)i3;

            Assert.Equal(expectedLength, m.Length);
            Assert.Equal(expectedCapacity, m.Capacity);

            Assert.Equal((byte)i0, m[i0]);
            Assert.Equal((byte)i1, m[i1]);
            Assert.Equal((byte)i2, m[i2]);
            Assert.Equal((byte)i3, m[i3]);
        }
    }
}
