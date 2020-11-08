using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

namespace Carambolas.Net.Tests
{
    public class MemoryPoolTests
    {
        [Fact]
        public void GetAndReturn()
        {
            var pool = new Carambolas.Net.Memory.Pool();

            long version;
            var m = pool.Get();
            try
            {
                Assert.NotNull(m);
                version = m.Version;
            }
            finally
            {
                pool.Return(m);
            }

            Assert.NotEqual(version, m.Version);

            Assert.Equal(0, m.Capacity);
            Assert.Equal(0, m.Length);
        }

        [Fact]
        public void GetAndReturnAndGetAgain()
        {
            var pool = new Carambolas.Net.Memory.Pool();

            var m1 = pool.Get();
            pool.Return(m1);

            var m2 = pool.Get();
            pool.Return(m2);

            Assert.Same(m1, m2);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(100)]
        public void GetMultiple(int n)
        {
            var pool = new Carambolas.Net.Memory.Pool();

            var m = new Net.Memory[n];
            for (int i = 0; i < n; ++i)
                m[i] = pool.Get();

            for (int i = 0; i < n; ++i)
                for (int j = i + 1; j < n; ++j)
                    Assert.NotSame(m[i], m[j]);
        }
    }

}
