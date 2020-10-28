using System;
using System.Collections.Generic;
using Xunit;

namespace Carambolas.Security.Cryptography.NaCl.Tests
{
    public class Curve25519Tests
    {
        [Fact]
        public void ConvertBetweenKeyAndFieldElement()
        {
            var expected = new Key(HexConverter.ToBytes("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"));
            var fe = new Curve25519.FieldElement(in expected);
            var actual = (Key)fe;
            Assert.Equal(expected, actual);
        }


        public static IEnumerable<object[]> SharedKeyTestVectors
        {
            get
            {
                return new List<object[]>
                {
                    // Test Vectors from RFC7748 section 6.1 https://tools.ietf.org/html/rfc7748#section-6.1

                    new object[] // Alice
                    {
                        HexConverter.ToBytes("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"),
                        HexConverter.ToBytes("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f"),
                        HexConverter.ToBytes("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742")
                    },

                    new object[] // Bob
                    {
                        HexConverter.ToBytes("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb"),
                        HexConverter.ToBytes("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a"),
                        HexConverter.ToBytes("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742")
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(SharedKeyTestVectors))]
        public void GenerateSharedKey(byte[] privateKeyBytes, byte[] remoteKeyBytes, byte[] expected)
        {
            var actual = Curve25519.CreateSharedKey(new Key(privateKeyBytes), new Key(remoteKeyBytes));
            Assert.Equal(new Key(expected), actual);
        }

        public static IEnumerable<object[]> PublicKeyTestVectors
        {
            get
            {
                return new List<object[]>
                {
                    // Test Vectors from RFC7748 section 6.1 https://tools.ietf.org/html/rfc7748#section-6.1

                    new object[] // Alice
                    {
                        HexConverter.ToBytes("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a"),
                        HexConverter.ToBytes("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a")
                    },

                    new object[] // Bob
                    {
                        HexConverter.ToBytes("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb"),
                        HexConverter.ToBytes("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f")
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(PublicKeyTestVectors))]
        public void GeneratePublicKey(byte[] privateKeyBytes, byte[] expected)
        {
            var actual = Curve25519.CreatePublicKey(new Key(privateKeyBytes));
            Assert.Equal(new Key(expected), actual);
        }
    }
}
