using System;
using System.Collections.Generic;

using Xunit;
using Carambolas.Net.Tests.Attributes;

namespace Carambolas.Net.Tests
{
    public class TickCounterTests
    {
        [Theory]
        [InlineData(0, 0.0)]
        [InlineData(1, 0.0000001)]
        [InlineData(10, 0.000001)]
        [InlineData(99, 0.0000099)]
        [InlineData(1000000, 0.1)]
        public void TicksToSeconds(long ticks, double expected)
        {
            Assert.Equal(expected, TickCounter.TicksToSeconds(ticks), 6);
        }

        [Theory]
        [InlineData(0, 0.0)]
        [InlineData(1, 0.0001)]
        [InlineData(10, 0.001)]
        [InlineData(99, 0.0099)]
        [InlineData(1000000, 100)]
        public void TicksToMilliseconds(long ticks, double expected)
        {
            Assert.Equal(expected, TickCounter.TicksToMilliseconds(ticks), 6);
        }
    }
}
