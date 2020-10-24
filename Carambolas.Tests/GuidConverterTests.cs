using System;
using System.Collections.Generic;

using Xunit;

namespace Carambolas.Tests
{
    public class GuidConverterTests
    {
        [Theory]
        [InlineData("ac59e3af-23c4-4df6-9368-aeb820cbe7a1", 0xac59e3af, 0x23c4, 0x4df6, 0x9368aeb820cbe7a1)]
        [InlineData("0e9aef74-b6b1-48d9-bfb6-7d9db4aeefec", 0x0e9aef74, 0xb6b1, 0x48d9, 0xbfb67d9db4aeefec)]
        [InlineData("f5cfd95e-c8b1-44f5-851f-a9b4de317c69", 0xf5cfd95e, 0xc8b1, 0x44f5, 0x851fa9b4de317c69)]
        public void ConvertToPairOfLongs(string s, uint a, ushort b, ushort c, ulong d)
        {            
            var converter = new Converter.Guid { AsGuid = Guid.Parse(s) };
            var actual = converter.AsTuple;
            Assert.True((a, b, c, d) == actual, $"Wrong MSB. Expected: {a:x8}-{b:x4}-{c:x4}-{d:x8}. Actual: {actual.A:x8}-{actual.B:x4}-{actual.C:x4}-{actual.D:x8}");
        }

        [Theory]
        [InlineData("ac59e3af-23c4-4df6-9368-aeb820cbe7a1", 0xac59e3af, 0x23c4, 0x4df6, 0x9368aeb820cbe7a1)]
        [InlineData("0e9aef74-b6b1-48d9-bfb6-7d9db4aeefec", 0x0e9aef74, 0xb6b1, 0x48d9, 0xbfb67d9db4aeefec)]
        [InlineData("f5cfd95e-c8b1-44f5-851f-a9b4de317c69", 0xf5cfd95e, 0xc8b1, 0x44f5, 0x851fa9b4de317c69)]
        public void ConvertToGuid(string s, uint a, ushort b, ushort c, ulong d)
        {
            var guid = Guid.Parse(s);
            var converter = new Converter.Guid { AsTuple = (a, b, c, d) };            
            Assert.Equal(guid, converter.AsGuid);
        }

        [Fact]
        public void ConvertToPairOfLongsAndBack()
        {
            for (int i = 0; i < 100; ++i)
            {
                var guid = Guid.NewGuid();
                var a = new Converter.Guid { AsGuid = guid };
                var b = new Converter.Guid { AsTuple = a.AsTuple };
                Assert.Equal(guid, b.AsGuid);
            }
        }
    }
}
