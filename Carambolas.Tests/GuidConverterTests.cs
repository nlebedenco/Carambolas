using System;
using System.Collections.Generic;

using Xunit;

namespace Carambolas.Tests
{
    public class GuidConverterTests
    {
        [Theory]
        [InlineData("ac59e3af-23c4-4df6-9368-aeb820cbe7a1", 0x4df623c4ac59e3afL, 0xa1e7cb20b8ae6893L)]
        [InlineData("0e9aef74-b6b1-48d9-bfb6-7d9db4aeefec", 0x48d9b6b10e9aef74L, 0xecefaeb49d7db6bfL)]
        [InlineData("f5cfd95e-c8b1-44f5-851f-a9b4de317c69", 0x44f5c8b1f5cfd95eL, 0x697c31deb4a91f85L)]
        public void ConvertToPairOfLongs(string s, ulong msb, ulong lsb)
        {            
            var converter = new GuidConverter { Guid = Guid.Parse(s) };
            Assert.True(msb == converter.MSB, $"Wrong MSB. Expected: 0x{msb:x16}. Actual: 0x{converter.MSB:x16}");
            Assert.True(lsb == converter.LSB, $"Wrong LSB. Expected: 0x{lsb:x16}. Actual: 0x{converter.LSB:x16}");
        }

        [Theory]
        [InlineData("ac59e3af-23c4-4df6-9368-aeb820cbe7a1", 0x4df623c4ac59e3afL, 0xa1e7cb20b8ae6893L)]
        [InlineData("0e9aef74-b6b1-48d9-bfb6-7d9db4aeefec", 0x48d9b6b10e9aef74L, 0xecefaeb49d7db6bfL)]
        [InlineData("f5cfd95e-c8b1-44f5-851f-a9b4de317c69", 0x44f5c8b1f5cfd95eL, 0x697c31deb4a91f85L)]
        public void ConvertToGuid(string s, ulong msb, ulong lsb)
        {
            var guid = Guid.Parse(s);
            var converter = new GuidConverter { MSB = msb, LSB = lsb };            
            Assert.Equal(guid, converter.Guid);
        }

        [Fact]
        public void ConvertToPairOfLongsAndBack()
        {
            for (int i = 0; i < 100; ++i)
            {
                var guid = Guid.NewGuid();
                var a = new GuidConverter { Guid = guid };
                var b = new GuidConverter { MSB = a.MSB, LSB = a.LSB };
                Assert.Equal(guid, b.Guid);
            }
        }
    }
}
