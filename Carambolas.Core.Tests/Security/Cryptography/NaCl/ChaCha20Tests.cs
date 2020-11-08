using System;
using System.Collections.Generic;
using System.Security.Cryptography;

using Xunit;

namespace Carambolas.Security.Cryptography.NaCl.Tests
{
    public class ChaCha20Tests
    {
        public static IEnumerable<object[]> RandomBlocks(int n, int m)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var list = new List<object[]>(n);
                for (int i = 0; i < n; ++i)
                {
                    var data = new byte[ChaCha20.BlockSize * m];
                    var keybytes = new byte[Key.Size];
                    var noncebytes = new byte[Nonce.Size];

                    rng.GetBytes(data);
                    rng.GetBytes(keybytes);
                    rng.GetBytes(noncebytes);

                    list.Add(new object[] { keybytes, data, noncebytes });
                }

                return list;
            }
        }

        [Theory]
        [MemberData(nameof(RandomBlocks), 4, 1)]
        public void EncryptDecrypt1Block(byte[] keybytes, byte[] data, byte[] noncebytes)
        {
            var key = new Key(keybytes);
            var nonce = new Nonce(noncebytes);

            var cipher = new ChaCha20 { Key = key };

            var ciphertext = new byte[data.Length];
            var plaintext = new byte[data.Length];

            cipher.Encrypt(data, 0, ciphertext, 0, data.Length, in nonce);
            cipher.Decrypt(ciphertext, 0, plaintext, 0, ciphertext.Length, in nonce);

            Assert.Equal(data, plaintext);
        }

        [Theory]
        [MemberData(nameof(RandomBlocks), 4, 64)]
        public void EncryptDecryptNBlocks(byte[] keybytes, byte[] data, byte[] noncebytes)
        {
            var key = new Key(keybytes);
            var nonce = new Nonce(noncebytes);

            var cipher = new ChaCha20 { Key = key };

            var ciphertext = new byte[data.Length];
            var plaintext = new byte[data.Length];

            cipher.Encrypt(data, 0, ciphertext, 0, data.Length, in nonce);
            cipher.Decrypt(ciphertext, 0, plaintext, 0, ciphertext.Length, in nonce);

            Assert.Equal(data, plaintext);
        }

        [Fact]
        public void EncryptDecryptLargeDataSameInstance()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keybytes = new byte[Key.Size];
                rng.GetBytes(keybytes);

                var cipher = new ChaCha20 { Key = new Key(keybytes) };

                for (var length = 16; length <= (1 << 24); length += 5 * length / 11)
                {
                    var ciphertext = new byte[length];

                    var plaintext = new byte[length];
                    rng.GetBytes(plaintext);

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var nonce = new Nonce(noncebytes);

                    cipher.Encrypt(plaintext, 0, ciphertext, 0, length, in nonce);

                    var decrypted = new byte[length];
                    cipher.Decrypt(ciphertext, 0, decrypted, 0, length, in nonce);

                    Assert.Equal(plaintext, decrypted);
                }
            }
        }

        [Fact]
        public void EncryptDecryptLargeDataDifferentInstances()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                for (var length = 16; length <= (1 << 24); length += 5 * length / 11)
                {
                    var ciphertext = new byte[length];

                    var plaintext = new byte[length];
                    rng.GetBytes(plaintext);

                    var keybytes = new byte[Key.Size];
                    rng.GetBytes(keybytes);

                    var cipher = new ChaCha20 { Key = new Key(keybytes) };

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var nonce = new Nonce(noncebytes);

                    cipher.Encrypt(plaintext, 0, ciphertext, 0, length, in nonce);

                    var decrypted = new byte[length];
                    cipher.Decrypt(ciphertext, 0, decrypted, 0, length, in nonce);

                    Assert.Equal(plaintext, decrypted);
                }
            }
        }

        [Theory]
        // https://tools.ietf.org/html/rfc8439#section-2.1.1
        [InlineData(new uint[] { 0x11111111, 0x01020304, 0x9b8d6f43, 0x01234567 }, 
                    0, 1, 2, 3,
                    new uint[] { 0xea2a92f4, 0xcb1cf8ce, 0x4581472e, 0x5881c4bb })]
        // https://tools.ietf.org/html/rfc8439#section-2.2.1
        [InlineData(new uint[] { 0x879531e0, 0xc5ecf37d, 0x516461b1, 0xc9a62f8a, 0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0x2a5f714c, 0x53372767, 0xb00a5631, 0x974c541a, 0x359e9963, 0x5c971061, 0x3d631689, 0x2098d9d6, 0x91dbd320 },
                    2, 7, 8, 13,
                    new uint[] { 0x879531e0, 0xc5ecf37d, 0xbdb886dc, 0xc9a62f8a, 0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0xcfacafd2, 0xe46bea80, 0xb00a5631, 0x974c541a, 0x359e9963, 0x5c971061, 0xccc07c79, 0x2098d9d6, 0x91dbd320 })]
        public void QuarterRound(uint[] x, int a, int b, int c, int d, uint[] expected)
        {
            ChaCha20.QuarterRound(ref x[a], ref x[b], ref x[c], ref x[d]);
            Assert.Equal(expected, x);
        }

        [Fact]
        public void BlockTestVector()
        {
            // https://tools.ietf.org/html/rfc8439#section-2.3.2

            var keybytes = HexConverter.ToBytes("000102030405060708090a0b0c0d0e0f"
                                              + "101112131415161718191a1b1c1d1e1f");

            var noncebytes = HexConverter.ToBytes("000000090000004a00000000");

            var expected = HexConverter.ToBytes("10f1e7e4d13b5915500fdd1fa32071c4"
                                              + "c7d1f4c733c068030422aa9ac3d46c4e"
                                              + "d2826446079faa0914c2d705d98b02a2"
                                              + "b5129cd1de164eb9cbd083e8a2503c4e");
            var counter = 1u;

            var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = counter };
            var actual = new byte[ChaCha20.BlockSize];
            chacha.Process(actual, counter, new Nonce(noncebytes));

            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> RFC8439TestVectors
        {
            get
            {                
                return new List<object[]>
                {
                    // Tests against the test vectors in Section 2.3.2 of RFC 8439. https://tools.ietf.org/html/rfc8439#section-2.3.2

                    new object[]
                    {
                        "RFC8439 Test Vector #1", 1,
                        HexConverter.ToBytes("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"),                        
                        HexConverter.ToBytes("6e2e359a2568f98041ba0728dd0d6981e97e7aec1d4360c20a27afccfd9fae0bf91b65c5524733ab8f593dabcd"
                                           + "62b3571639d624e65152ab8f530c359f0861d807ca0dbf500d6a6156a38e088a22b65e52bc514d16ccf8"
                                           + "06818ce91ab77937365af90bbf74a35be6b40b8eedf2785e42874d"),
                        HexConverter.ToBytes("000000000000004a00000000"),
                        HexConverter.ToBytes("4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a20496620"
                                           + "4920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f722074686520667574"
                                           + "7572652c2073756e73637265656e20776f756c642062652069742e")
                    },

                    new object[]
                    {
                        "RFC8439 Test Vector #2", 0,
                        HexConverter.ToBytes("0000000000000000000000000000000000000000000000000000000000000000"),
                        HexConverter.ToBytes("76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8"
                                           + "d84a376a43b8f41518a11cc387b669b2ee6586"),
                        HexConverter.ToBytes("000000000000000000000000"),
                        new byte[64]
                    },

                    new object[] 
                    {
                        "RFC8439 Test Vector #3", 1,
                        HexConverter.ToBytes("0000000000000000000000000000000000000000000000000000000000000001"),
                        HexConverter.ToBytes("a3fbf07df3fa2fde4f376ca23e82737041605d9f4f4f57bd8cff2c1d4b7955ec2a97948bd3722915c8f3d337f7"
                                           + "d370050e9e96d647b7c39f56e031ca5eb6250d4042e02785ececfa4b4bb5e8ead0440e20b6e8db09d881"
                                           + "a7c6132f420e52795042bdfa7773d8a9051447b3291ce1411c680465552aa6c405b7764d5e87bea85ad0"
                                           + "0f8449ed8f72d0d662ab052691ca66424bc86d2df80ea41f43abf937d3259dc4b2d0dfb48a6c9139ddd7"
                                           + "f76966e928e635553ba76c5c879d7b35d49eb2e62b0871cdac638939e25e8a1e0ef9d5280fa8ca328b35"
                                           + "1c3c765989cbcf3daa8b6ccc3aaf9f3979c92b3720fc88dc95ed84a1be059c6499b9fda236e7e818b04b"
                                           + "0bc39c1e876b193bfe5569753f88128cc08aaa9b63d1a16f80ef2554d7189c411f5869ca52c5b83fa36f"
                                           + "f216b9c1d30062bebcfd2dc5bce0911934fda79a86f6e698ced759c3ff9b6477338f3da4f9cd8514ea99"
                                           + "82ccafb341b2384dd902f3d1ab7ac61dd29c6f21ba5b862f3730e37cfdc4fd806c22f221"),
                        HexConverter.ToBytes("000000000000000000000002"),
                        HexConverter.ToBytes("416e79207375626d697373696f6e20746f20746865204945544620696e74656e6465642062792074686520436f"
                                            + "6e7472696275746f7220666f72207075626c69636174696f6e20617320616c6c206f722070617274206f"
                                            + "6620616e204945544620496e7465726e65742d4472616674206f722052464320616e6420616e79207374"
                                            + "6174656d656e74206d6164652077697468696e2074686520636f6e74657874206f6620616e2049455446"
                                            + "20616374697669747920697320636f6e7369646572656420616e20224945544620436f6e747269627574"
                                            + "696f6e222e20537563682073746174656d656e747320696e636c756465206f72616c2073746174656d65"
                                            + "6e747320696e20494554462073657373696f6e732c2061732077656c6c206173207772697474656e2061"
                                            + "6e6420656c656374726f6e696320636f6d6d756e69636174696f6e73206d61646520617420616e792074"
                                            + "696d65206f7220706c6163652c207768696368206172652061646472657373656420746f")
                        },

                    new object[]
                    {
                        "RFC8439 Test Vector #4", 42,
                        HexConverter.ToBytes("1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0"),
                        HexConverter.ToBytes("62e6347f95ed87a45ffae7426f27a1df5fb69110044c0d73118effa95b01e5cf166d3df2d721caf9b21e5fb14c616871fd84c54f9d65b283196c7fe4f60553ebf39c6402c42234e32a356b3e764312a61a5532055716ead6962568f87d3f3f7704c6a8d1bcd1bf4d50d6154b6da731b187b58dfd728afa36757a797ac188d1"),
                        HexConverter.ToBytes("000000000000000000000002"),
                        HexConverter.ToBytes("2754776173206272696c6c69672c20616e642074686520736c6974687920746f7665730a446964206779726520616e642067696d626c6520696e2074686520776162653a0a416c6c206d696d737920776572652074686520626f726f676f7665732c0a416e6420746865206d6f6d65207261746873206f757467726162652e")
                    },

                    // Tests against the test vectors in Section 2.6.2 of RFC 8439. https://tools.ietf.org/html/rfc8439#section-2.6.2

                    new object[]
                    {
                        "RFC8439 Test Vector #5", 0,
                        HexConverter.ToBytes("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f"),                       
                        HexConverter.ToBytes("8ad5a08b905f81cc815040274ab29471a833b637e3fd0da508dbb8e2fdd1a646"),
                        HexConverter.ToBytes("000000000001020304050607"),
                        new byte[32]
                    },

                    new object[]
                    {
                        "RFC8439 Test Vector #6", 0,
                        HexConverter.ToBytes("0000000000000000000000000000000000000000000000000000000000000000"),                       
                        HexConverter.ToBytes("76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7"),
                        HexConverter.ToBytes("000000000000000000000000"),
                        new byte[32]
                    },

                    new object[]
                    {
                        "RFC8439 Test Vector #7", 0,
                        HexConverter.ToBytes("0000000000000000000000000000000000000000000000000000000000000001"),                                             
                        HexConverter.ToBytes("ecfa254f845f647473d3cb140da9e87606cb33066c447b87bc2666dde3fbb739"),
                        HexConverter.ToBytes("000000000000000000000002"),
                        new byte[32]
                    },

                    new object[]
                    {
                       "RFC8439 Test Vector #8", 0,
                       HexConverter.ToBytes("1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0"),                       
                       HexConverter.ToBytes("965e3bc6f9ec7ed9560808f4d229f94b137ff275ca9b3fcbdd59deaad23310ae"),
                       HexConverter.ToBytes("000000000000000000000002"),
                       new byte[32]
                    }
                };
            }
        }

        #pragma warning disable xUnit1026
        [Theory]
        [MemberData(nameof(RFC8439TestVectors))]
        public void DecryptWithTestVectors(string name, uint counter, byte[] keybytes, byte[] ciphertext, byte[] noncebytes, byte[] expected)
        {
            var key = new Key(keybytes);
            var nonce = new Nonce(noncebytes);

            var cipher = new ChaCha20 { Key = key, Counter = counter };
            var plaintext = new byte[ciphertext.Length];

            cipher.Decrypt(ciphertext, 0, plaintext, 0, ciphertext.Length, in nonce);
            Assert.Equal(expected, plaintext);
        }
        #pragma warning restore xUnit1026

        [Fact]
        public void BlockTestVectorTC8()
        {
            // TC8: key: 'All your base are belong to us!, IV: 'IETF2013'
            // Test vector TC8 from RFC draft by J. Strombergson
            // https://tools.ietf.org/html/draft-strombergson-chacha-test-vectors-01

            var keybytes = new byte[Key.Size]
            {
                0xC4, 0x6E, 0xC1, 0xB1, 0x8C, 0xE8, 0xA8, 0x78,
                0x72, 0x5A, 0x37, 0xE7, 0x80, 0xDF, 0xB7, 0x35,
                0x1F, 0x68, 0xED, 0x2E, 0x19, 0x4C, 0x79, 0xFB,
                0xC6, 0xAE, 0xBE, 0xE1, 0xA6, 0x67, 0x97, 0x5D
            };

            // The first 4 bytes are set to zero and a large counter
            // is used; this makes the RFC 8439 version of ChaCha20
            // compatible with the original specification by D. J. Bernstein.
            var noncebytes = new byte[Nonce.Size] { 0x00, 0x00, 0x00, 0x00, 0x1A, 0xDA, 0x31, 0xD5, 0xCF, 0x68, 0x82, 0x21 };
            var nonce = new Nonce(noncebytes);
            // Act
            var cipher = new ChaCha20 { Key = new Key(keybytes) };
            var block0 = new byte[ChaCha20.BlockSize];
            var block1 = new byte[ChaCha20.BlockSize];

            cipher.Process(block0, 0, in nonce);
            cipher.Process(block1, 1, in nonce);

            // Assert
            var expected = new byte[128]
            {
                0xF6, 0x3A, 0x89, 0xB7, 0x5C, 0x22, 0x71, 0xF9,
                0x36, 0x88, 0x16, 0x54, 0x2B, 0xA5, 0x2F, 0x06,
                0xED, 0x49, 0x24, 0x17, 0x92, 0x30, 0x2B, 0x00,
                0xB5, 0xE8, 0xF8, 0x0A, 0xE9, 0xA4, 0x73, 0xAF,
                0xC2, 0x5B, 0x21, 0x8F, 0x51, 0x9A, 0xF0, 0xFD,
                0xD4, 0x06, 0x36, 0x2E, 0x8D, 0x69, 0xDE, 0x7F,
                0x54, 0xC6, 0x04, 0xA6, 0xE0, 0x0F, 0x35, 0x3F,
                0x11, 0x0F, 0x77, 0x1B, 0xDC, 0xA8, 0xAB, 0x92,

                0xE5, 0xFB, 0xC3, 0x4E, 0x60, 0xA1, 0xD9, 0xA9,
                0xDB, 0x17, 0x34, 0x5B, 0x0A, 0x40, 0x27, 0x36,
                0x85, 0x3B, 0xF9, 0x10, 0xB0, 0x60, 0xBD, 0xF1,
                0xF8, 0x97, 0xB6, 0x29, 0x0F, 0x01, 0xD1, 0x38,
                0xAE, 0x2C, 0x4C, 0x90, 0x22, 0x5B, 0xA9, 0xEA,
                0x14, 0xD5, 0x18, 0xF5, 0x59, 0x29, 0xDE, 0xA0,
                0x98, 0xCA, 0x7A, 0x6C, 0xCF, 0xE6, 0x12, 0x27,
                0x05, 0x3C, 0x84, 0xE4, 0x9A, 0x4A, 0x33, 0x32
            };

            Assert.Equal(new ArraySegment<byte>(expected, 0, ChaCha20.BlockSize) as IEnumerable<byte>, block0);
            Assert.Equal(new ArraySegment<byte>(expected, ChaCha20.BlockSize, ChaCha20.BlockSize) as IEnumerable<byte>, block1);
        }
    }
}
