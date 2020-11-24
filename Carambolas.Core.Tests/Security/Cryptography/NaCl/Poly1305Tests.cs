using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Xunit;

namespace Carambolas.Security.Cryptography.NaCl.Tests
{
    public class Poly1305Tests
    {
        public static IEnumerable<object[]> RandomData(int n)
        {
            var list = new List<object[]>(n);
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < n; ++i)
                {
                    var keybytes = new byte[Key.Size];
                    var word = new byte[2];
                    rng.GetBytes(word);
                    var data = new byte[BitConverter.ToUInt16(word)];

                    rng.GetBytes(keybytes);
                    rng.GetBytes(data);

                    list.Add(new object[] { data, keybytes });
                }
            }
            return list;
        }

        [Theory]
        [MemberData(nameof(RandomData), parameters: 4)]
        public void SignAndVerify(byte[] data, byte[] keybytes)
        {
            var key = new Key(keybytes);
            Poly1305.Sign(data, 0, data.Length, in key, out Mac mac);
            Assert.True(Poly1305.Verify(data, 0, data.Length, key, in mac));
        }

        [Fact]
        public void VerifyFails()
        {
            var key = new Key(1, 0, 0, 0, 0, 0, 0, 0);

            Assert.False(Poly1305.Verify(new byte[] { 1 }, 0, 1, in key, default));
        }

        public static IEnumerable<object[]> TestVectors
        {
            get
            {
                return new List<object[]>
                {
                    // Tests against the test vectors in Section 2.5.2 of RFC 7539. https://tools.ietf.org/html/rfc7539#section-2.5.2
                    new object[]
                    {
                        "Vectors in RFC 7539 section 2.5.2",

                        Encoding.UTF8.GetBytes("Cryptographic Forum Research Group"),

                        HexConverter.ToBytes("85d6be7857556d337f4452fe42d506a8"
                                            + "0103808afb0db2fd4abff6af4149f51b"),

                        HexConverter.ToBytes("a8061dc1305136c6c22b8baf0c0127a9")
                    },

                    // Tests against the test vector 1 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 1 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("00000000000000000000000000000000"
                                            + "00000000000000000000000000000000"
                                            + "00000000000000000000000000000000"
                                            + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("00000000000000000000000000000000"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("00000000000000000000000000000000")
                    },

                    // Tests against the test vector 2 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 2 in RFC 7539 Appendix A.3",

                        Encoding.UTF8.GetBytes("Any submission to the IETF intended by the Contributor for publication as all or part of an IETF Internet-Draft or RFC and any statement made within the context of an IETF activity is considered an \"IETF Contribution\". Such statements include oral statements in IETF sessions, as well as written and electronic communications made at any time or place, which are addressed to"),

                        HexConverter.ToBytes("00000000000000000000000000000000"
                                           + "36e5f6b5c5e06070f0efca96227a863e"),

                        HexConverter.ToBytes("36e5f6b5c5e06070f0efca96227a863e")
                    },

                    // Tests against the test vector 3 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 3 in RFC 7539 Appendix A.3",

                        Encoding.UTF8.GetBytes("Any submission to the IETF intended by the Contributor for publication as all or part of an IETF Internet-Draft or RFC and any statement made within the context of an IETF activity is considered an \"IETF Contribution\". Such statements include oral statements in IETF sessions, as well as written and electronic communications made at any time or place, which are addressed to"),

                        HexConverter.ToBytes("36e5f6b5c5e06070f0efca96227a863e"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("f3477e7cd95417af89a6b8794c310cf0")
                    },

                    // Tests against the test vector 4 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 4 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("2754776173206272696c6c69672c2061"
                                           + "6e642074686520736c6974687920746f"
                                           + "7665730a446964206779726520616e64"
                                           + "2067696d626c6520696e207468652077"
                                           + "6162653a0a416c6c206d696d73792077"
                                           + "6572652074686520626f726f676f7665"
                                           + "732c0a416e6420746865206d6f6d6520"
                                           + "7261746873206f757467726162652e"),

                        HexConverter.ToBytes("1c9240a5eb55d38af333888604f6b5f0"
                                            + "473917c1402b80099dca5cbc207075c0"),

                        HexConverter.ToBytes("4541669a7eaaee61e708dc7cbcc5eb62")
                    },

                    // Tests against the test vector 5 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 5 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("ffffffffffffffffffffffffffffffff"),

                        HexConverter.ToBytes("02000000000000000000000000000000"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("03000000000000000000000000000000")
                    },

                    // Tests against the test vector 6 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 6 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("02000000000000000000000000000000"),

                        HexConverter.ToBytes("02000000000000000000000000000000"
                                           + "ffffffffffffffffffffffffffffffff"),

                        HexConverter.ToBytes("03000000000000000000000000000000")
                    },

                    // Tests against the test vector 7 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 7 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("ffffffffffffffffffffffffffffffff"
                                           + "f0ffffffffffffffffffffffffffffff"
                                           + "11000000000000000000000000000000"),

                        HexConverter.ToBytes("01000000000000000000000000000000"
                                            + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("05000000000000000000000000000000")
                    },

                    // Tests against the test vector 8 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 8 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("ffffffffffffffffffffffffffffffff"
                                           + "fbfefefefefefefefefefefefefefefe"
                                           + "01010101010101010101010101010101"),

                        HexConverter.ToBytes("01000000000000000000000000000000"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("00000000000000000000000000000000")
                    },

                    // Tests against the test vector 9 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 9 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("fdffffffffffffffffffffffffffffff"),

                        HexConverter.ToBytes("02000000000000000000000000000000"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("faffffffffffffffffffffffffffffff")
                    },

                    // Tests against the test vector 10 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 10 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("e33594d7505e43b90000000000000000"
                                           + "3394d7505e4379cd0100000000000000"
                                           + "00000000000000000000000000000000"
                                           + "01000000000000000000000000000000"),

                        HexConverter.ToBytes("01000000000000000400000000000000"
                                           + "00000000000000000000000000000000"),


                        HexConverter.ToBytes("14000000000000005500000000000000")
                    },

                    // Tests against the test vector 11 in Appendix A.3 of RFC 7539. https://tools.ietf.org/html/rfc7539#appendix-A.3
                    new object[]
                    {
                        "Vector 11 in RFC 7539 Appendix A.3",

                        HexConverter.ToBytes("e33594d7505e43b90000000000000000"
                                           + "3394d7505e4379cd0100000000000000"
                                           + "00000000000000000000000000000000"),

                        HexConverter.ToBytes("01000000000000000400000000000000"
                                           + "00000000000000000000000000000000"),


                        HexConverter.ToBytes("13000000000000000000000000000000")
                    }
                };
            }
        }

#pragma warning disable xUnit1026
        [Theory]
        [MemberData(nameof(TestVectors))]
        public void SignTestVectors(string name, byte[] data, byte[] keybytes, byte[] expected)
        {
            var key = new Key(keybytes);
            Poly1305.Sign(data, 0, data.Length, in key, out Mac actual);
            Assert.Equal(new Mac(expected), actual);
        }
#pragma warning restore xUnit1026  
        
    }

    public class Poly1305AEADTests
    {
        [Fact]
        public void EncryptSignVerifyAndDecrypt()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keybytes = new byte[Key.Size];
                rng.GetBytes(keybytes);

                for (var i = 0; i < 100; i++)
                {
                    var plaintext = new byte[100];
                    rng.GetBytes(plaintext);

                    var aad = new byte[16];
                    rng.GetBytes(aad);

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = 1 };


                    var ciphertext = new byte[plaintext.Length];
                    var nonce = new Nonce(noncebytes);

                    chacha.Encrypt(plaintext, 0, ciphertext, 0, plaintext.Length, in nonce);

                    var mackey = chacha.CreateKey(nonce);
                    Poly1305.AEAD.Sign(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, out Mac mac);

                    Assert.True(Poly1305.AEAD.Verify(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, in mac));

                    var actualtext = new byte[plaintext.Length];
                    chacha.Decrypt(ciphertext, 0, actualtext, 0, ciphertext.Length, in nonce);

                    Assert.Equal(plaintext, actualtext);
                }
            }
        }

        [Fact]
        public void EncryptSignVerifyAndDecryptLargeData()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keybytes = new byte[Key.Size];
                rng.GetBytes(keybytes);

                for (var length = 16; length <= (1 << 24); length += 5 * length / 11)
                {
                    var plaintext = new byte[length];
                    rng.GetBytes(plaintext);

                    var aad = new byte[16];
                    rng.GetBytes(aad);

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = 1 };

                    var ciphertext = new byte[plaintext.Length];
                    var nonce = new Nonce(noncebytes);

                    chacha.Encrypt(plaintext, 0, ciphertext, 0, plaintext.Length, in nonce);

                    var mackey = chacha.CreateKey(nonce);
                    Poly1305.AEAD.Sign(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, out Mac mac);

                    Assert.True(Poly1305.AEAD.Verify(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, in mac));

                    var actualtext = new byte[plaintext.Length];
                    chacha.Decrypt(ciphertext, 0, actualtext, 0, ciphertext.Length, in nonce);

                    Assert.Equal(plaintext, actualtext);
                }
            }           
        }

        [Fact]
        public void EncryptSignModifyCipherTextAndVerifyFails()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keybytes = new byte[Key.Size];
                rng.GetBytes(keybytes);

                var word = new byte[2];

                for (var i = 0; i < 100; i++)
                {
                    var plaintext = new byte[100];
                    rng.GetBytes(plaintext);

                    var aad = new byte[16];
                    rng.GetBytes(aad);

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = 1 };

                    var ciphertext = new byte[plaintext.Length];
                    var nonce = new Nonce(noncebytes);

                    chacha.Encrypt(plaintext, 0, ciphertext, 0, plaintext.Length, in nonce);

                    var mackey = chacha.CreateKey(nonce);
                    Poly1305.AEAD.Sign(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, out Mac mac);

                    rng.GetBytes(word);
                    var j = BitConverter.ToUInt16(word) % ciphertext.Length;
                    ciphertext[j]++;

                    Assert.False(Poly1305.AEAD.Verify(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, in mac));
                }
            }
        }

        [Fact]
        public void EncryptSignModifyAssociatedDataAndVerifyFails()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keybytes = new byte[Key.Size];
                rng.GetBytes(keybytes);

                var word = new byte[2];

                for (var i = 0; i < 100; i++)
                {
                    var plaintext = new byte[100];
                    rng.GetBytes(plaintext);

                    var aad = new byte[16];
                    rng.GetBytes(aad);

                    var noncebytes = new byte[Nonce.Size];
                    rng.GetBytes(noncebytes);

                    var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = 1 };


                    var ciphertext = new byte[plaintext.Length];
                    var nonce = new Nonce(noncebytes);

                    chacha.Encrypt(plaintext, 0, ciphertext, 0, plaintext.Length, in nonce);

                    var mackey = chacha.CreateKey(nonce);
                    Poly1305.AEAD.Sign(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, out Mac mac);

                    rng.GetBytes(word);
                    var j = BitConverter.ToUInt16(word) % aad.Length;
                    aad[j]++;

                    Assert.False(Poly1305.AEAD.Verify(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, in mac));
                }
            }
        }

        public static IEnumerable<object[]> RFC8439AeadTestVectors
        {
            get
            {
                return new List<object[]>
                {
                    // Section 2.8.2 
                    // Example and Test Vector for AEAD_CHACHA20_POLY1305 https://tools.ietf.org/html/rfc8439#section-2.8.2

                    new object[]
                    {
                        "RFC8439 Section 2.8.2",
                        HexConverter.ToBytes("4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a204966204920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f7220746865206675747572652c2073756e73637265656e20776f756c642062652069742e"),
                        HexConverter.ToBytes("50515253c0c1c2c3c4c5c6c7"),
                        HexConverter.ToBytes("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f"),
                        HexConverter.ToBytes("070000004041424344454647"),
                        HexConverter.ToBytes("d31a8d34648e60db7b86afbc53ef7ec2a4aded51296e08fea9e2b5a736ee62d63dbea45e8ca9671282fafb69da92728b1a71de0a9e060b2905d6a5b67ecd3b3692ddbd7f2d778b8c9803aee328091b58fab324e4fad675945585808b4831d7bc3ff4def08e4b7a9de576d26586cec64b6116"),
                        HexConverter.ToBytes("1ae10b594f09e26a7e902ecbd0600691")
                    },

                    new object[]
                    {
                        "RFC8439 Appendix A.5",
                        HexConverter.ToBytes("496e7465726e65742d4472616674732061726520647261667420646f63756d656e74732076616c696420666f722061206d6178696d756d206f6620736978206d6f6e74687320616e64206d617920626520757064617465642c207265706c616365642c206f72206f62736f6c65746564206279206f7468657220646f63756d656e747320617420616e792074696d652e20497420697320696e617070726f70726961746520746f2075736520496e7465726e65742d447261667473206173207265666572656e6365206d6174657269616c206f7220746f2063697465207468656d206f74686572207468616e206173202fe2809c776f726b20696e2070726f67726573732e2fe2809d"),
                        HexConverter.ToBytes("f33388860000000000004e91"),
                        HexConverter.ToBytes("1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0"),
                        HexConverter.ToBytes("000000000102030405060708"),
                        HexConverter.ToBytes("64a0861575861af460f062c79be643bd5e805cfd345cf389f108670ac76c8cb24c6cfc18755d43eea09ee94e382d26b0bdb7b73c321b0100d4f03b7f355894cf332f830e710b97ce98c8a84abd0b948114ad176e008d33bd60f982b1ff37c8559797a06ef4f0ef61c186324e2b3506383606907b6a7c02b0f9f6157b53c867e4b9166c767b804d46a59b5216cde7a4e99040c5a40433225ee282a1b0a06c523eaf4534d7f83fa1155b0047718cbc546a0d072b04b3564eea1b422273f548271a0bb2316053fa76991955ebd63159434ecebb4e466dae5a1073a6727627097a1049e617d91d361094fa68f0ff77987130305beaba2eda04df997b714d6c6f2c29a6ad5cb4022b02709b"),
                        HexConverter.ToBytes("eead9d67890cbb22392336fea1851f38")
                    },

                    new object[]
                    {
                        "RFC7634 Appendix A",
                        HexConverter.ToBytes("45000054a6f200004001e778c6336405c000020508005b7a3a080000553bec100007362708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363701020204"),
                        HexConverter.ToBytes("0102030400000005"),
                        HexConverter.ToBytes("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f"),
                        HexConverter.ToBytes("a0a1a2a31011121314151617"),
                        HexConverter.ToBytes("24039428b97f417e3c13753a4f05087b67c352e6a7fab1b982d466ef407ae5c614ee8099d52844eb61aa95dfab4c02f72aa71e7c4c4f64c9befe2facc638e8f3cbec163fac469b502773f6fb94e664da9165b82829f641e0"),
                        HexConverter.ToBytes("76aaa8266b7fb0f7b11b369907e1ad43")
                    },

                    new object[]
                    {
                        "RFC7634 Appendix B",
                        HexConverter.ToBytes("0000000c000040010000000a00"),
                        HexConverter.ToBytes("c0c1c2c3c4c5c6c7d0d1d2d3d4d5d6d72e202500000000090000004529000029"),
                        HexConverter.ToBytes("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f"),
                        HexConverter.ToBytes("a0a1a2a31011121314151617"),
                        HexConverter.ToBytes("610394701f8d017f7c12924889"),
                        HexConverter.ToBytes("6b71bfe25236efd7cdc67066906315b2")
                    }
                };
            }
        }

        #pragma warning disable xUnit1026
        [Theory]
        [MemberData(nameof(RFC8439AeadTestVectors))]
        // string plaintext, string aad, string key, string nonce, string ciphertext, string tag
        public void EncryptSignVerifyAndDecryptTestVectors(string name, byte[] plaintext, byte[] aad, byte[] keybytes, byte[] noncebytes, byte[]ciphertext, byte[] macbytes)
        {
            var chacha = new ChaCha20 { Key = new Key(keybytes), Counter = 1 };
            var nonce = new Nonce(noncebytes);

            chacha.Encrypt(plaintext, 0, ciphertext, 0, plaintext.Length, in nonce);

            var mackey = chacha.CreateKey(nonce);
            Poly1305.AEAD.Sign(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, out Mac mac);

            Assert.True(Poly1305.AEAD.Verify(new ArraySegment<byte>(aad), new ArraySegment<byte>(ciphertext), in mackey, in mac));

            var actualtext = new byte[plaintext.Length];
            chacha.Decrypt(ciphertext, 0, actualtext, 0, ciphertext.Length, in nonce);

            Assert.Equal(plaintext, actualtext);
        }
        #pragma warning restore xUnit1026
    }
}
