using System;

using Xunit;

namespace Carambolas.Tests
{
    public class RangeTests
    {
        [Theory]
        [InlineData(0, 1, 0, 1)]
        [InlineData(1, 0, 0, 0)]
        [InlineData(1, 1, 1, 1)]
        public void Creation(int a, int b, int min, int max)
        {
            var range = new Range<int>(a, b);
            Assert.Equal(min, range.Min);
            Assert.Equal(max, range.Max);
        }

        [Theory]
        [InlineData(0, 1, 0, 0)]
        [InlineData(0, 1, 1, 1)]
        [InlineData(0, 9, 0, 0)]
        [InlineData(0, 9, 9, 9)]
        [InlineData(0, 9, 8, 8)]
        [InlineData(0, 9, 1, 1)]
        [InlineData(0, 9, 5, 5)]
        [InlineData(0, 9, 10, 9)]
        [InlineData(0, 9, -1, 0)]
        public void Clamp(int a, int b, int c, int expected)
        {
            var range = new Range<int>(a, b);
            Assert.Equal(expected, range.Clamp(c));
        }

        [Theory]
        [InlineData(0, 1, 0, true)]
        [InlineData(0, 1, 1, true)]
        [InlineData(0, 9, 0, true)]
        [InlineData(0, 9, 9, true)]
        [InlineData(0, 9, 8, true)]
        [InlineData(0, 9, 1, true)]
        [InlineData(0, 9, 5, true)]
        [InlineData(0, 9, 10, false)]
        [InlineData(0, 9, -1, false)]
        public void Contains(int a, int b, int c, bool expected)
        {
            var range = new Range<int>(a, b);
            Assert.Equal(expected, range.Contains(c));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(1, 1)]
        [InlineData(0, 9)]
        [InlineData(-1, 7)]
        public void EqualOperator(int a, int b)
        {
            var r0 = new Range<int>(a, b);
            var r1 = new Range<int>(a, b);
            Assert.True(r0 == r1);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(1, 1)]
        [InlineData(0, 9)]
        [InlineData(-1, 7)]
        public void NotEqualOperator(int a, int b)
        {
            var r0 = new Range<int>(a, b);
            var r1 = new Range<int>(a, b + 1);
            Assert.True(r0 != r1);
        }
    }
}
