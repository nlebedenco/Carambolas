using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Xunit;

namespace Carambolas.Text.Tests
{
    public class StringBuilderBufferTests
    {
        [Fact]
        public void DefaultCapacity()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Equal(0, sb.Capacity);
            }
        }

        [Fact]
        public void DefaultLength()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Equal(0, sb.Length);
            }
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-10)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void CustomCapacity(int capacity)
        {
            using (var sb = new StringBuilder.Buffer(capacity))
            {
                var actual = sb.Capacity;
                Assert.True(actual >= capacity, $"Expected: >= {capacity}; Actual: {actual}");
            }
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-10)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void InitialLength(int capacity)
        {
            using (var sb = new StringBuilder.Buffer(capacity))
            {
                Assert.Equal(0, sb.Length);
            }
        }

        [Fact]
        public static void ItemGetSet()
        {
            string s = "Hello";
            var sb = new StringBuilder.Buffer();
            try
            {
                sb.Append(s);
                for (int i = 0; i < s.Length; i++)
                {
                    Assert.Equal(s[i], sb[i]);

                    char c = (char)(i + '0');
                    sb[i] = c;
                    Assert.Equal(c, sb[i]);
                }
                Assert.Equal("01234", sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }

        [Fact]
        public static void ItemGetSet_InvalidIndex()
        {
            string s = "Hello";
            var sb = new StringBuilder.Buffer();
            try
            {
                sb.Append(s);

                Assert.Throws<IndexOutOfRangeException>(() => sb[-1]);  // Index < 0
                Assert.Throws<IndexOutOfRangeException>(() => sb[5]);   // Index >= string.Length

                Assert.Throws<ArgumentOutOfRangeException>("index", () => sb[-1] = 'a');  // Index < 0
                Assert.Throws<ArgumentOutOfRangeException>("index", () => sb[5] = 'a');   // Index >= string.Length
            }
            finally
            {
                sb.Dispose();
            }
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-10)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void EnsureCapacity(int capacity)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.EnsureCapacity(capacity);
                var actual = sb.Capacity;
                Assert.True(actual >= capacity, $"Expected: >= {capacity}; Actual: {actual}");
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void CopyToCharArray(string s)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var destination = new char[s.Length];
                sb.Append(s);
                sb.CopyTo(0, destination, 0, s.Length);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void TryCopyTo(string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var destination = new char[expected.Length];
                sb.Append(expected);
                Assert.True(sb.TryCopyTo(destination));
                Assert.Equal(expected, destination);
            }
        }

        [Theory]
        [InlineData("Hello", 0, new char[] { '\0', '\0', '\0', '\0', '\0' }, 5, new char[] { 'H', 'e', 'l', 'l', 'o' })]
        [InlineData("Hello", 0, new char[] { '\0', '\0', '\0', '\0' }, 4, new char[] { 'H', 'e', 'l', 'l' })]
        [InlineData("Hello", 1, new char[] { '\0', '\0', '\0', '\0', '\0' }, 4, new char[] { 'e', 'l', 'l', 'o', '\0' })]
        public void CopyToSpan(string value, int sourceIndex, char[] destination, int count, char[] expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(value);
                sb.CopyTo(sourceIndex, destination, count);
                Assert.Equal(expected, destination);
            }
        }

        [Fact]
        public void EmptyCopyTo()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var destination = new char[1] { 'A' };
                sb.CopyTo(0, destination, 0, 0);
                Assert.Equal('A', destination[0]);
            }
        }

        [Fact]
        public void EmptyTryCopyTo()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Span<char> destination = stackalloc char[1] { 'A' };
                sb.TryCopyTo(destination);
                Assert.Equal('A', destination[0]);
            }
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-10)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        public void GetSpan(int n)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var span = sb.GetSpan(n);
                Assert.Equal(Math.Max(0, n), span.Length);
                Assert.Equal(Math.Max(0, n), sb.Length);

                var actual = sb.Capacity;
                Assert.True(actual >= n, $"Capacity Expected: >= {n}; Actual: {actual}");
            }
        }

        [Fact]
        public void EmptyToString()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Equal("", sb.ToString());
            }
        }

        [Fact]
        public void EmptyToSubtring()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Equal("", sb.ToString(0, 0));
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void BufferToString(string s)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var destination = new char[s.Length];
                sb.Append(s);
                Assert.Equal(s, sb.ToString());
            }
        }

        [Theory]
        [InlineData("", 0, 0)]
        [InlineData("The quick brown fox jumps over the lazy dog", 0, 10)]
        [InlineData("The quick brown fox jumps over the lazy dog", 10, 0)]
        [InlineData("The quick brown fox jumps over the lazy dog", 10, 2)]
        public void BufferToSubstring(string s, int startIndex, int length)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var destination = new char[s.Length];
                sb.Append(s);
                Assert.Equal(s.Substring(startIndex, length), sb.ToString(startIndex, length));
            }
        }

        [Fact]
        public void EmptyArraySegment()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var segment = sb.AsArraySegment();
                var list = segment as IReadOnlyList<char>;
                Assert.NotNull(segment.Array);
                Assert.Equal(0, list.Count);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void ArraySegment(string s)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(s);
                var segment = sb.AsArraySegment();
                var list = segment as IReadOnlyList<char>;
                Assert.NotNull(segment.Array);
                Assert.Equal(sb.Length, list.Count);
            }
        }

        [Theory]
        [InlineData('A')]
        public void AppendChar(char expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(expected);
                Assert.Equal(1, sb.Length);
                var segment = sb.AsArraySegment();
                Assert.Single(segment);
                Assert.Equal(new char[] { expected }, segment as IEnumerable<char>);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void AppendString(string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(expected);
                Assert.Equal(expected.Length, sb.Length);
                var segment = sb.AsArraySegment();
                Assert.Equal(expected.Length, segment.Count);
                Assert.Equal(expected, segment as IEnumerable<char>);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello")]
        public void AppendSpan(string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var span = expected.AsSpan();
                sb.Append(span);
                Assert.Equal(span.Length, sb.Length);
                var segment = sb.AsArraySegment();
                Assert.Equal(expected.Length, segment.Count);
                Assert.Equal(expected, segment as IEnumerable<char>);
            }
        }

        [Theory]
        [InlineData('A', 0)]
        [InlineData('A', 1)]
        [InlineData('A', 10)]
        [InlineData('A', 100)]
        [InlineData('A', 1000)]
        public void AppendCharFill(char c, int count)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = new string(c, count);
                sb.Append(c, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(byte.MaxValue)]
        public void AppendUInt8(byte value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(ushort.MaxValue)]
        public void AppendUInt16(ushort value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        [InlineData(uint.MaxValue)]
        public void AppendUInt32(uint value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        [InlineData(ulong.MaxValue)]
        public void AppendUInt64(ulong value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(sbyte.MaxValue)]
        [InlineData(sbyte.MinValue)]
        public void AppendInt8(sbyte value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void AppendInt16(short value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void AppendInt32(int value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        [InlineData(10000000)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void AppendInt64(long value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(-0.0, "-0")]
        [InlineData(12345.0, "12345")]
        [InlineData(1.2345e27, "1.2345E+27")]
        [InlineData(1e21, "1E+21")]
        [InlineData(1e20, "1E+20")]
        [InlineData(111111111111111111111.0, "1.1111111111111111E+20")]
        [InlineData(1111111111111111111111.0, "1.1111111111111111E+21")]
        [InlineData(11111111111111111111111.0, "1.1111111111111111E+22")]
        [InlineData(-0.00001, "-1E-05")]
        [InlineData(-0.000001, "-1E-06")]
        [InlineData(-0.0000001, "-1E-07")]
        [InlineData(0.1, "0.1")]
        [InlineData(0.01, "0.01")]
        [InlineData(1.0, "1")]
        [InlineData(10.0, "10")]
        [InlineData(1100.0, "1100")]
        [InlineData(1122.0, "1122")]
        [InlineData(10000.0, "10000")]
        [InlineData(11100.0, "11100")]
        [InlineData(100000.0, "100000")]
        [InlineData(0.000001, "1E-06")]
        [InlineData(0.0000001, "1E-07")]
        [InlineData(double.PositiveInfinity, "Infinity")]
        [InlineData(double.NegativeInfinity, "-Infinity")]
        [InlineData(double.NaN, "NaN")]
        [InlineData(3.5844466002796428e+298, "3.5844466002796428E+298")]
        [InlineData(-0.0005401035826582183, "-0.0005401035826582183")]
        [InlineData(0.0005401035826582183, "0.0005401035826582183")]
        [InlineData(5401.035826582183, "5401.035826582183")]
        [InlineData(-5401.035826582183, "-5401.035826582183")]
        [InlineData(-0.0015677654444036897, "-0.0015677654444036897")]
        [InlineData(-3.3200274383931173e-4, "-0.00033200274383931173")]
        public void AppendDouble(double value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(value, CultureInfo.InvariantCulture, default, default);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AppendBool(bool value)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var expected = value.ToString();
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        public static readonly IEnumerable<object[]> AppendStringAndFloatTestData = new object[][]
        {
            new object[] { "Hello", 0.0f, "Hello0" },
            new object[] { "Hello", 1.23f, "Hello1.23" },
            new object[] { "Hello", 0.999999f, "Hello0.999999" },
            new object[] { "", -4.56f, "-4.56" }
        };

        [Theory]
        [InlineData("Hello", new char[] { 'a' }, "Helloa")]
        [InlineData("Hello", new char[] { 'b', 'c', 'd' }, "Hellobcd")]
        [InlineData("Hello", new char[] { 'b', '\0', 'd' }, "Hellob\0d")]
        [InlineData("", new char[] { 'e', 'f', 'g' }, "efg")]
        [InlineData("Hello", new char[0], "Hello")]
        public static void AppendStringAndSpan(string original, char[] value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(new ReadOnlySpan<char>(value));
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [MemberData(nameof(AppendStringAndFloatTestData))]
        public void AppendStringAndFloat(string original, float value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        public static readonly IEnumerable<object[]> AppendStringAndDoubleTestData = new object[][]
        {
            new object[] { "Hello", 0.0, "Hello0" },
            new object[] { "Hello", 1.23, "Hello1.23" },
            new object[] { "Hello", 0.99999999999999, "Hello0.99999999999999" },
            new object[] { "", -4.56, "-4.56" }
        };

        [Theory]
        [MemberData(nameof(AppendStringAndDoubleTestData))]
        public void AppendStringAndDouble(string original, double value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        public static readonly IEnumerable<object[]> AppendStringAndDecimalTestData = new object[][]
        {
            new object[] { "Hello", 0M, "Hello0" },
            new object[] { "Hello", 1.23M, "Hello1.23" },
            new object[] { "", -4.56M, "-4.56" }
        };

        [Theory]
        [MemberData(nameof(AppendStringAndDecimalTestData))]
        public void AppendStringAndDecimal(string original, decimal value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData("Hello", "abc", "Helloabc")]
        [InlineData("Hello", "def", "Hellodef")]
        [InlineData("", "g", "g")]
        [InlineData("Hello", "", "Hello")]
        [InlineData("Hello", null, "Hello")]
        public static void AppendStringAndObject(string original, object value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData("Hello", '\0', 1, "Hello\0")]
        [InlineData("Hello", 'a', 1, "Helloa")]
        [InlineData("", 'b', 1, "b")]
        [InlineData("Hello", 'c', 2, "Hellocc")]
        [InlineData("Hello", '\0', 0, "Hello")]
        public static void AppendStringAndChar(string original, char value, int count, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData("Hello", "abc", "Helloabc")]
        [InlineData("", "g", "g")]
        [InlineData("Hello", "", "Hello")]
        public static void AppendStringAndString(string original, string value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData("Hello", "abc", 0, 3, "Helloabc")]
        [InlineData("Hello", "def", 1, 2, "Helloef")]
        [InlineData("Hello", "def", 2, 1, "Hellof")]
        [InlineData("", "g", 0, 1, "g")]
        [InlineData("Hello", "g", 1, 0, "Hello")]
        [InlineData("Hello", "g", 0, 0, "Hello")]
        [InlineData("Hello", "", 0, 0, "Hello")]
        [InlineData("Hello", null, 0, 0, "Hello")]
        public static void AppendStringAndSubstring(string original, string value, int startIndex, int count, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Append(value, startIndex, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public static void AppendNullStringWithStartIndexAndCountOtherThanZero_ThrowsArgumentNullException()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Throws<ArgumentNullException>("value", () => sb.Append((string)null, 1, 1));
            }
        }

        [Theory]
        [InlineData("", -1, 0)]
        [InlineData("", 0, -1)]
        [InlineData("Hello", 5, 1)]
        [InlineData("Hello", 4, 2)]
        public static void AppendStringWithInvalidIndexPlusCount_ThrowsArgumentOutOfRangeException(string value, int startIndex, int count)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                Assert.Throws<ArgumentOutOfRangeException>("start", () => sb.Append(value, startIndex, count));
            }
        }

        public static IEnumerable<object[]> AppendFormatTestData = new object[][]
        {
            new object[] { "", null, "", new object[0], "" },
            new object[] { "", null, ", ", new object[0], ", " },

            new object[] { "Hello", null, ", Foo {0  }", new object[] { "Bar" }, "Hello, Foo Bar" }, // Ignores whitespace
            
            new object[] { "Hello", null, ", Foo {0}", new object[] { "Bar" }, "Hello, Foo Bar" },
            new object[] { "Hello", null, ", Foo {0} Baz {1}", new object[] { "Bar", "Foo" }, "Hello, Foo Bar Baz Foo" },
            new object[] { "Hello", null, ", Foo {0} Baz {1} Bar {2}", new object[] { "Bar", "Foo", "Baz" }, "Hello, Foo Bar Baz Foo Bar Baz" },
            new object[] { "Hello", null, ", Foo {0} Baz {1} Bar {2} Foo {3}", new object[] { "Bar", "Foo", "Baz", "Bar" }, "Hello, Foo Bar Baz Foo Bar Baz Foo Bar" },
            
            // Length is positive
            new object[] { "Hello", null, ", Foo {0,2}", new object[] { "Bar" }, "Hello, Foo Bar" }, // MiValue's length > minimum length (so don't prepend whitespace)
            new object[] { "Hello", null, ", Foo {0,3}", new object[] { "B" }, "Hello, Foo   B" }, // Value's length < minimum length (so prepend whitespace)
            new object[] { "Hello", null, ", Foo {0,     3}", new object[] { "B" }, "Hello, Foo   B" }, // Same as above, but verify AppendFormat ignores whitespace
            new object[] { "Hello", null, ", Foo {0,0}", new object[] { "Bar" }, "Hello, Foo Bar" }, // Minimum length is 0
            
            // Length is negative
            new object[] { "Hello", null, ", Foo {0,-2}", new object[] { "Bar" }, "Hello, Foo Bar" }, // Value's length > |minimum length| (so don't prepend whitespace)
            new object[] { "Hello", null, ", Foo {0,-3}", new object[] { "B" }, "Hello, Foo B  " }, // Value's length < |minimum length| (so append whitespace)
            new object[] { "Hello", null, ", Foo {0,     -3}", new object[] { "B" }, "Hello, Foo B  " }, // Same as above, but verify AppendFormat ignores whitespace
            new object[] { "Hello", null, ", Foo {0,0}", new object[] { "Bar" }, "Hello, Foo Bar" }, // Minimum length is 0
            
            new object[] { "Hello", null, ", Foo {0:D6}", new object[] { 1 }, "Hello, Foo 000001" }, // Custom format
            new object[] { "Hello", null, ", Foo {0     :D6}", new object[] { 1 }, "Hello, Foo 000001" }, // Custom format with ignored whitespace
            new object[] { "Hello", null, ", Foo {0:}", new object[] { 1 }, "Hello, Foo 1" }, // Missing custom format
            
            new object[] { "Hello", null, ", Foo {0,9:D6}", new object[] { 1 }, "Hello, Foo    000001" }, // Positive minimum length and custom format
            new object[] { "Hello", null, ", Foo {0,-9:D6}", new object[] { 1 }, "Hello, Foo 000001   " }, // Negative length and custom format
            
            new object[] { "Hello", null, ", Foo {{{0}", new object[] { 1 }, "Hello, Foo {1" }, // Escaped open curly braces
            new object[] { "Hello", null, ", Foo }}{0}", new object[] { 1 }, "Hello, Foo }1" }, // Escaped closed curly braces
            new object[] { "Hello", null, ", Foo {0} {{0}}", new object[] { 1 }, "Hello, Foo 1 {0}" }, // Escaped placeholder
            
            
            new object[] { "Hello", null, ", Foo {0}", new object[] { null }, "Hello, Foo " }, // Values has null only
            new object[] { "Hello", null, ", Foo {0} {1} {2}", new object[] { "Bar", null, "Baz" }, "Hello, Foo Bar  Baz" }, // Values has null

            new object[] { "Hello", CultureInfo.InvariantCulture, ", Foo {0,9:D6}", new object[] { 1 }, "Hello, Foo    000001" }, // Positive minimum length, custom format and custom format provider

            new object[] { "", new CustomFormatter(), "{0}", new object[] { 1.2 }, "abc" }, // Custom format provider
            new object[] { "", new CustomFormatter(), "{0:0}", new object[] { 1.2 }, "abc" } // Custom format provider
        };

        public class CustomFormatter: ICustomFormatter, IFormatProvider
        {
            public string Format(string format, object arg, IFormatProvider formatProvider) => "abc";
            public object GetFormat(Type formatType) => this;
        }

        [Theory]
        [MemberData(nameof(AppendFormatTestData))]
        public static void AppendFormat(string original, IFormatProvider provider, string format, object[] values, string expected)
        {
            // StringBuilder has overloads for providing a format without specifying a format provider in which case it defaults to
            // CultureInfo.CurrentCulture but StringBuilder.Buffer does not have these overloads and everything must be specified.
            // We don't simply change the test data after all because the null provider entry is still useful for the StringBuilder test.
            if (provider == null)
                provider = CultureInfo.CurrentCulture;

            if (values != null)
            {
                if (values.Length == 1)
                {
                    using (var sb = new StringBuilder.Buffer())
                    {
                        sb.Append(original);
                        sb.AppendFormat(provider, format, values[0]);
                        Assert.Equal(expected, sb.ToString());
                    }
                }
                else if (values.Length == 2)
                {
                    using (var sb = new StringBuilder.Buffer())
                    {
                        sb.Append(original);
                        sb.AppendFormat(provider, format, values[0], values[1]);
                        Assert.Equal(expected, sb.ToString());
                    }
                }
                else if (values.Length == 3)
                {
                    using (var sb = new StringBuilder.Buffer())
                    {
                        sb.Append(original);
                        sb.AppendFormat(provider, format, values[0], values[1], values[2]);
                        Assert.Equal(expected, sb.ToString());
                    }
                }
                else if (values.Length == 4)
                {
                    using (var sb = new StringBuilder.Buffer())
                    {
                        sb.Append(original);
                        sb.AppendFormat(provider, format, values[0], values[1], values[2], values[3]);
                        Assert.Equal(expected, sb.ToString());
                    }
                }
            }

            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.AppendFormat(provider, format, values);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public static void AppendFormat_Invalid()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append("Hello");

                var obj1 = new object();
                var obj2 = new object();
                var obj3 = new object();
                var obj4 = new object();

                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(CultureInfo.CurrentCulture, null, obj1)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(CultureInfo.CurrentCulture, null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(CultureInfo.CurrentCulture, null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(CultureInfo.CurrentCulture, null, obj1, obj2, obj3, obj4)); // Format is null
                Assert.Throws<ArgumentNullException>("args", () => sb.AppendFormat(CultureInfo.CurrentCulture, "", (object[])null)); // Args is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(CultureInfo.CurrentCulture, null, (object[])null)); // Both format and args are null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, null, obj1)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, null, obj1, obj2)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, null, obj1, obj2, obj3, obj4)); // Format is null
                Assert.Throws<ArgumentNullException>("args", () => sb.AppendFormat(null, "", (object[])null)); // Args is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, null, (object[])null)); // Both format and args are null

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{-1}", obj1)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{-1}", obj1, obj2)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{-1}", obj1, obj2, obj3)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{-1}", obj1, obj2, obj3, obj4)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{-1}", obj1)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{-1}", obj1, obj2)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{-1}", obj1, obj2, obj3)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{-1}", obj1, obj2, obj3, obj4)); // Format has value < 0

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{1}", obj1)); // Format has value >= 1
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{2}", obj1, obj2)); // Format has value >= 2
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{3}", obj1, obj2, obj3)); // Format has value >= 3
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{4}", obj1, obj2, obj3, obj4)); // Format has value >= 4
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{1}", obj1)); // Format has value >= 1
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{2}", obj1, obj2)); // Format has value >= 2
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{3}", obj1, obj2, obj3)); // Format has value >= 3
                Assert.Throws<FormatException>(() => sb.AppendFormat(null, "{4}", obj1, obj2, obj3, obj4)); // Format has value >= 4

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{", "")); // Format has unescaped {
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{a", "")); // Format has unescaped {

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "}", "")); // Format has unescaped }
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "}a", "")); // Format has unescaped }
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}", "")); // Format has unescaped }

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{\0", "")); // Format has invalid character after {
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{a", "")); // Format has invalid character after {

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0     ", "")); // Format with index and spaces is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{1000000", new string[10])); // Format index is too long
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{10000000}", new string[10])); // Format index is too long

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,", "")); // Format with comma is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,   ", "")); // Format with comma and spaces is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,-", "")); // Format with comma and minus sign is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,-\0", "")); // Format has invalid character after minus sign
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,-a", "")); // Format has invalid character after minus sign

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,1000000", new string[10])); // Format length is too long
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0,10000000}", new string[10])); // Format length is too long

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:", new string[10])); // Format with colon is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:    ", new string[10])); // Format with colon and spaces is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{", new string[10])); // Format with custom format contains unescaped {
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{}", new string[10])); // Format with custom format contains unescaped {
            }
        }

        [Fact]
        public static void AppendFormat_NoEscapedBracesInCustomFormatSpecifier()
        {
            // Tests new rule which does not allow escaped braces in the custom format specifier
            using (var sb = new StringBuilder.Buffer())
            {
                var args = new int[] { 0, 1 };

                sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}}{1:}}}", args);

                // Old .NET behavior: first two closing braces would be escaped and passed in as the custom format specifier, thus result = "}"
                // New .NET behavior: first closing brace closes the argument hole and next two are escaped as part of the format, thus result = "0}"
                Assert.Equal("0}1}", sb.ToString());
                // Previously this would be allowed and escaped brace would be passed into the custom format, now this is unsupported
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{{}{1}", args)); // Format with custom format contains {
            }

            // Tests new rule which does not allow escaped braces in the custom format specifier
            using (var sb = new StringBuilder.Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}}", 0);

                // Old .NET behavior: first two closing braces would be escaped and passed in as the custom format specifier, thus result = "}"
                // New .NET behavior: first closing brace closes the argument hole and next two are escaped as part of the format, thus result = "0}"
                Assert.Equal("0}", sb.ToString());
                // Previously this would be allowed and escaped brace would be passed into the custom format, now this is unsupported
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{{}", 0)); // Format with custom format contains {
            }

            // Tests new rule which does not allow escaped braces in the custom format specifier
            using (var sb = new StringBuilder.Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}}{1:}}}", 0, 1);

                // Old .NET behavior: first two closing braces would be escaped and passed in as the custom format specifier, thus result = "}"
                // New .NET behavior: first closing brace closes the argument hole and next two are escaped as part of the format, thus result = "0}"
                Assert.Equal("0}1}", sb.ToString());
                // Previously this would be allowed and escaped brace would be passed into the custom format, now this is unsupported
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{{}{1}", 0, 1)); // Format with custom format contains {
            }

            // Tests new rule which does not allow escaped braces in the custom format specifier
            using (var sb = new StringBuilder.Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}}{1:}}}{2:}}}", 0, 1, 2);

                // Old .NET behavior: first two closing braces would be escaped and passed in as the custom format specifier, thus result = "}"
                // New .NET behavior: first closing brace closes the argument hole and next two are escaped as part of the format, thus result = "0}"
                Assert.Equal("0}1}2}", sb.ToString());
                // Previously this would be allowed and escaped brace would be passed into the custom format, now this is unsupported
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{{}{1}{2}", 0, 1, 2)); // Format with custom format contains {
            }

            // Tests new rule which does not allow escaped braces in the custom format specifier
            using (var sb = new StringBuilder.Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, "{0:}}}{1:}}}{2:}}}{3:}}}", 0, 1, 2, 3);

                // Old .NET behavior: first two closing braces would be escaped and passed in as the custom format specifier, thus result = "}"
                // New .NET behavior: first closing brace closes the argument hole and next two are escaped as part of the format, thus result = "0}"
                Assert.Equal("0}1}2}3}", sb.ToString());
                // Previously this would be allowed and escaped brace would be passed into the custom format, now this is unsupported
                Assert.Throws<FormatException>(() => sb.AppendFormat(CultureInfo.CurrentCulture, "{0:{{}{1}{2}{3}", 0, 1, 2, 3)); // Format with custom format contains {
            }
        }


        [Theory]
        [InlineData(1)]
        [InlineData(10000)]
        public static void ClearThenAppendAndInsertBeforeClearManyTimes_CapacityStaysWithinRange(int times)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var originalCapacity = sb.Capacity;
                var s = new string(' ', 10);
                int oldLength = 0;
                for (int i = 0; i < times; i++)
                {
                    sb.Append(s);
                    sb.Append(s);
                    sb.Append(s);
                    sb.Insert(0, s);
                    sb.Insert(0, s);
                    oldLength = sb.Length;

                    sb.Clear();
                }

                Assert.True(sb.Capacity >= originalCapacity, $"Expected: >= {originalCapacity}. Actual: {sb.Capacity}");
            }
        }

        [Theory]
        [InlineData("Hello", 0, new char[] { '\0' }, "\0Hello")]
        [InlineData("Hello", 3, new char[] { 'a', 'b', 'c' }, "Helabclo")]
        [InlineData("Hello", 5, new char[] { 'd', 'e', 'f' }, "Hellodef")]
        [InlineData("Hello", 0, new char[0], "Hello")]
        public static void Insert_CharSpan(string original, int index, char[] value, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(original);
                sb.Insert(index, new ReadOnlySpan<char>(value));
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public static void Insert_CharSpan_Invalid()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append("Hello");

                Assert.Throws<ArgumentOutOfRangeException>("index", () => sb.Insert(-1, new ReadOnlySpan<char>(new char[0]))); // Index < 0
                Assert.Throws<ArgumentOutOfRangeException>("index", () => sb.Insert(sb.Length + 1, new ReadOnlySpan<char>(new char[0]))); // Index > builder.Length
                Assert.Throws<ArgumentOutOfRangeException>("count", () => sb.Insert(sb.Length, new ReadOnlySpan<char>(new char[1]), -1)); // count < 0
            }
        }

        [Theory]
        [InlineData("", 0, 0, "")]
        [InlineData("Hello", 0, 5, "")]
        [InlineData("Hello", 1, 3, "Ho")]
        [InlineData("Hello", 1, 4, "H")]
        [InlineData("Hello", 1, 0, "Hello")]
        [InlineData("Hello", 5, 0, "Hello")]
        public static void Remove(string value, int startIndex, int length, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(value);
                sb.Remove(startIndex, length);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(30, 1, 29)]
        [InlineData(30, 0, 29)]
        [InlineData(30, 20, 10)]
        [InlineData(30, 0, 15)]
        [InlineData(300, 0, 15)]
        [InlineData(3000, 0, 15)]
        public static void RemoveWithArbitrarilyLargeString(int n, int startIndex, int count)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var source = new char[n];
                for (int i = 0; i < n; ++i)
                    source[i] = (char)((i % 96) + ' ');
                var original = new string(source);
                var expected = original.Remove(startIndex, count);
                sb.Append(original);
                sb.Remove(startIndex, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public static void Remove_Invalid()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append("Hello");
                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Remove(-1, 0)); // Start index < 0
                Assert.Throws<ArgumentOutOfRangeException>("length", () => sb.Remove(0, -1)); // Length < 0
                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Remove(6, 0)); // Start index + length > 0
                Assert.Throws<ArgumentException>("length", () => sb.Remove(5, 1)); // Start index + length > 0
                Assert.Throws<ArgumentException>("length", () => sb.Remove(4, 2)); // Start index + length > 0
            }
        }

        [Theory]
        [InlineData("", 'a', '!', 0, 0, "")]
        [InlineData("aaaabbbbccccdddd", 'a', '!', 0, 16, "!!!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'a', '!', 0, 4, "!!!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'a', '!', 2, 3, "aa!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'a', '!', 4, 1, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'b', '!', 0, 0, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'a', '!', 16, 0, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", 'e', '!', 0, 16, "aaaabbbbccccdddd")]
        public static void ReplaceChar(string value, char oldChar, char newChar, int startIndex, int count, string expected)
        {
            if (startIndex == 0 && count == value.Length)
            {
                using (var sb = new StringBuilder.Buffer())
                {
                    // Use Replace(char, char)
                    sb.Append(value);
                    sb.Replace(oldChar, newChar);
                    Assert.Equal(expected, sb.ToString());
                }
            }

            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(value);
                sb.Replace(oldChar, newChar, startIndex, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData("", "a", "!", 0, 0, "")]
        [InlineData("aaaabbbbccccdddd", "a", "!", 0, 16, "!!!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "a", "!", 2, 3, "aa!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "a", "!", 4, 1, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aab", "!", 2, 2, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aab", "!", 2, 3, "aa!bbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aa", "!", 0, 16, "!!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aa", "$!", 0, 16, "$!$!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aa", "$!$", 0, 16, "$!$$!$bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aaaa", "!", 0, 16, "!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aaaa", "$!", 0, 16, "$!bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "a", "", 0, 16, "bbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "b", null, 0, 16, "aaaaccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aaaabbbbccccdddd", "", 0, 16, "")]
        [InlineData("aaaabbbbccccdddd", "aaaabbbbccccdddd", "", 16, 0, "aaaabbbbccccdddd")]
        [InlineData("aaaabbbbccccdddd", "aaaabbbbccccdddde", "", 0, 16, "aaaabbbbccccdddd")]
        [InlineData("aaaaaaaaaaaaaaaa", "a", "b", 0, 16, "bbbbbbbbbbbbbbbb")]
        public static void ReplaceString(string value, string oldValue, string newValue, int startIndex, int count, string expected)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(value);
                sb.Replace(oldValue, newValue, startIndex, count);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Theory]
        [InlineData(30)]
        [InlineData(300)]
        [InlineData(3000)]
        public static void ReplaceCharWithArbitrarilyLargeString(int n)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var source = new char[n];
                var oldChar = 'A';
                var newChar = '%';
                for (int i = 0; i < n; ++i)
                    source[i] = (char)((i % 26) + 'A');
                var original = new string(source);
                var expected = original.Replace(oldChar, newChar);
                sb.Append(original);
                sb.Replace(oldChar, newChar);
                Assert.Equal(expected, sb.ToString());
            }
        }

        [Fact]
        public static void ReplaceChar_Invalid()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append("Hello");
                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Replace('a', 'b', -1, 0)); // Start index < 0
                Assert.Throws<ArgumentOutOfRangeException>("count", () => sb.Replace('a', 'b', 0, -1)); // Count < 0

                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Replace('a', 'b', 6, 0)); // Count + start index > builder.Length
                Assert.Throws<ArgumentException>("count", () => sb.Replace('a', 'b', 5, 1)); // Count + start index > builder.Length
                Assert.Throws<ArgumentException>("count", () => sb.Replace('a', 'b', 4, 2)); // Count + start index > builder.Length
            }
        }

        [Fact]
        public static void ReplaceWhole()
        {
            using (var sb = new StringBuilder.Buffer())
            {
                var s = "Hello";
                sb.Append(s);
                sb.Replace(s, ReadOnlySpan<char>.Empty, 0, sb.Length);

                Assert.Equal(0, sb.Length);
                Assert.Same(string.Empty, sb.ToString());
            }
        }

        [Theory]
        [InlineData(30, 20)]
        [InlineData(300, 512)]
        [InlineData(3000, 2048)]
        public static void ReplaceArbitrarilyLongString(int n, int m)
        {
            var a = new string('a', n);
            var b = new string('b', m);

            using (var sb = new StringBuilder.Buffer())
            {
                sb.Append(a);
                sb.Append(b);
                sb.Append(a);
                sb.Append(b);
                sb.Append(a);

                sb.Replace(b, "", 0, sb.Length);

                Assert.Equal(a + a + a, sb.ToString());
            }
        }

        private static readonly string[] StressTestData = new string[]
        {
            "_abcdefghijklmnopqrstuvwxyz01234567890__Abcdefghijklmnopqrstuvwxyz01234567890__ABcdefghijklmnopqrstuvwxyz01_",
            "_abcdefghijklmnopqrstuvwxyz01234567890__Abcdefghijklmnopqrstuvwxyz01234567890__ABcdefghijklmnopqrstuvwxyz0123_",
            "_abcdefghijklmnopqrstuvwxyz01234567890__Abcdefghijklmnopqrstuvwxyz01234567890__ABcdefghijklmnopqrstuvwxyz012345_",
            "_abcdefghijklmnopqrstuvwxyz01234567890__Abcdefghijklmnopqrstuvwxyz01234567890__ABcdefghijklmnopqrstuvwxyz012345678_",
            "_abcdefghijklmnopqrstuvwxyz01234567890__Abcdefghijklmnopqrstuvwxyz01234567890__ABcdefghijklmnopqrstuvwxyz01234567890_"
        };

        [Fact]
        public static void StressTest()
        {


            // Test on a variety of lengths
            for (int i = 0; i < 200; i++)
            {
                var expected = "";
                using (var sb = new StringBuilder.Buffer())
                {
                    for (int j = 0; j < i; j++)
                    {
                        // Make some unique strings that are at least 500 bytes long and append the same data in the builder buffer.
                        expected += j;
                        sb.Append(j);
                        for (int k = 0; k < StressTestData.Length; ++k)
                        {
                            expected += StressTestData[k];
                            sb.Append(StressTestData[k]);
                        }
                    }

                    // The strings formed by concatenating the chunks should be the same as the value in the StringBuilder.
                    Assert.Equal(expected, sb.ToString());
                }
            }
        }


        public static readonly IEnumerable<object[]> DisposeTestActions = new object[][]
        {
            new object[] { new Action(DisposeAndToArraySegment) },
            new object[] { new Action(DisposeAndEnsureCapacity) },
            new object[] { new Action(DisposeAndCopyTo) },
            new object[] { new Action(DisposeAndTryCopyTo) },
            new object[] { new Action(DisposeAndGetSpan) },
            new object[] { new Action(DisposeAndToString) },
            new object[] { new Action(DisposeAndToSubtring) },
            new object[] { new Action(DisposeAndAppendChar) },
            new object[] { new Action(DisposeAndAppendString) },
            new object[] { new Action(DisposeAndAppendSpan) },
            new object[] { new Action(DisposeAndAppendCharFill) },
            new object[] { new Action(DisposeAndAppendGeneric) },
            new object[] { new Action(DisposeAndAppendGenericWithFormat) },
            new object[] { new Action(DisposeAndInsert) },
            new object[] { new Action(DisposeAndReplaceChar) },
            new object[] { new Action(DisposeAndReplaceSpan) },
            new object[] { new Action(DisposeAndRemove) }
        };

        [Fact]
        public void DisposeAgain()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Dispose();
            sb.Dispose();
        }

        [Theory]
        [MemberData(nameof(DisposeTestActions))]
        public static void DisposeTests_ThrowsObjectDisposedException(Action action) => Assert.Throws<ObjectDisposedException>(action);

        private static void DisposeAndToArraySegment()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            var segment = sb.AsArraySegment();
        }

        private static void DisposeAndEnsureCapacity()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.EnsureCapacity(10);
        }

        private static void DisposeAndCopyTo()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s);
            sb.Dispose();

            var destination = new char[s.Length];
            sb.CopyTo(0, destination, 0, s.Length);
        }

        private static void DisposeAndTryCopyTo()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s);
            sb.Dispose();

            Span<char> destination = stackalloc char[s.Length];
            sb.TryCopyTo(destination);
        }

        private static void DisposeAndGetSpan()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();

            var span = sb.GetSpan(10);
        }

        private static void DisposeAndToString()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s);
            sb.Dispose();
            sb.ToString();
        }

        private static void DisposeAndToSubtring()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s);
            sb.Dispose();
            sb.ToString(0, 10);
        }

        private static void DisposeAndAppendChar()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append('A');
        }

        private static void DisposeAndAppendString()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append("Hello");
        }

        private static void DisposeAndAppendSpan()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append(s.AsSpan());
        }

        private static void DisposeAndAppendCharFill()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append('A', 10);
        }

        private static void DisposeAndAppendGeneric()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append(10);
        }

        private static void DisposeAndAppendGenericWithFormat()
        {
            var sb = new StringBuilder.Buffer();
            sb.Dispose();
            sb.Append(10, CultureInfo.CurrentCulture, "X2", default);
        }

        private static void DisposeAndInsert()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s.AsSpan());
            sb.Dispose();
            sb.Insert(1, "***".AsSpan());
        }

        private static void DisposeAndReplaceChar()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s.AsSpan());
            sb.Dispose();
            sb.Replace('o', 'O', 0, s.Length);
        }

        private static void DisposeAndReplaceSpan()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s.AsSpan());
            sb.Dispose();
            sb.Replace("he".AsSpan(), "HE".AsSpan(), 0, s.Length);
        }

        private static void DisposeAndRemove()
        {
            var s = "Hello";
            var sb = new StringBuilder.Buffer();
            sb.Append(s.AsSpan());
            sb.Dispose();
            sb.Remove(s.Length / 2, s.Length - (s.Length / 2));
        }
    }

    public class StringBuilderTests
    {
        [Fact]
        public static void AppendFormat_Invalid()
        {
            using (var sb = new StringBuilder())
            {
                sb.Append("Hello");

                IFormatProvider formatter = null;
                var obj1 = new object();
                var obj2 = new object();
                var obj3 = new object();
                var obj4 = new object();

                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, obj1)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, obj1, obj2, obj3, obj4)); // Format is null
                Assert.Throws<ArgumentNullException>("args", () => sb.AppendFormat("", (object[])null)); // Args is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(null, (object[])null)); // Both format and args are null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(formatter, null, obj1)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(formatter, null, obj1, obj2)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(formatter, null, obj1, obj2, obj3)); // Format is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(formatter, null, obj1, obj2, obj3, obj4)); // Format is null
                Assert.Throws<ArgumentNullException>("args", () => sb.AppendFormat(formatter, "", (object[])null)); // Args is null
                Assert.Throws<ArgumentNullException>("format", () => sb.AppendFormat(formatter, null, (object[])null)); // Both format and args are null

                Assert.Throws<FormatException>(() => sb.AppendFormat("{-1}", obj1)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat("{-1}", obj1, obj2)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat("{-1}", obj1, obj2, obj3)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat("{-1}", obj1, obj2, obj3, obj4)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{-1}", obj1)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{-1}", obj1, obj2)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{-1}", obj1, obj2, obj3)); // Format has value < 0
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{-1}", obj1, obj2, obj3, obj4)); // Format has value < 0

                Assert.Throws<FormatException>(() => sb.AppendFormat("{1}", obj1)); // Format has value >= 1
                Assert.Throws<FormatException>(() => sb.AppendFormat("{2}", obj1, obj2)); // Format has value >= 2
                Assert.Throws<FormatException>(() => sb.AppendFormat("{3}", obj1, obj2, obj3)); // Format has value >= 3
                Assert.Throws<FormatException>(() => sb.AppendFormat("{4}", obj1, obj2, obj3, obj4)); // Format has value >= 4
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{1}", obj1)); // Format has value >= 1
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{2}", obj1, obj2)); // Format has value >= 2
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{3}", obj1, obj2, obj3)); // Format has value >= 3
                Assert.Throws<FormatException>(() => sb.AppendFormat(formatter, "{4}", obj1, obj2, obj3, obj4)); // Format has value >= 4

                Assert.Throws<FormatException>(() => sb.AppendFormat("{", "")); // Format has unescaped {
                Assert.Throws<FormatException>(() => sb.AppendFormat("{a", "")); // Format has unescaped {

                Assert.Throws<FormatException>(() => sb.AppendFormat("}", "")); // Format has unescaped }
                Assert.Throws<FormatException>(() => sb.AppendFormat("}a", "")); // Format has unescaped }
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0:}}", "")); // Format has unescaped }

                Assert.Throws<FormatException>(() => sb.AppendFormat("{\0", "")); // Format has invalid character after {
                Assert.Throws<FormatException>(() => sb.AppendFormat("{a", "")); // Format has invalid character after {

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0     ", "")); // Format with index and spaces is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat("{1000000", new string[10])); // Format index is too long
                Assert.Throws<FormatException>(() => sb.AppendFormat("{10000000}", new string[10])); // Format index is too long

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,", "")); // Format with comma is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,   ", "")); // Format with comma and spaces is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,-", "")); // Format with comma and minus sign is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,-\0", "")); // Format has invalid character after minus sign
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,-a", "")); // Format has invalid character after minus sign

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,1000000", new string[10])); // Format length is too long
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0,10000000}", new string[10])); // Format length is too long

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0:", new string[10])); // Format with colon is not closed
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0:    ", new string[10])); // Format with colon and spaces is not closed

                Assert.Throws<FormatException>(() => sb.AppendFormat("{0:{", new string[10])); // Format with custom format contains unescaped {
                Assert.Throws<FormatException>(() => sb.AppendFormat("{0:{}", new string[10])); // Format with custom format contains unescaped {
            }
        }


        [Fact]
        public static void ReplaceString_Invalid()
        {
            using (var sb = new StringBuilder())
            {
                sb.Append("Hello");

                Assert.Throws<ArgumentNullException>("oldValue", () => sb.Replace(null, "")); // Old value is null
                Assert.Throws<ArgumentNullException>("oldValue", () => sb.Replace(null, "a", 0, 0)); // Old value is null

                Assert.Throws<ArgumentException>("oldValue", () => sb.Replace("", "a")); // Old value is empty
                Assert.Throws<ArgumentException>("oldValue", () => sb.Replace("", "a", 0, 0)); // Old value is empty

                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Replace("a", "b", -1, 0)); // Start index < 0
                Assert.Throws<ArgumentOutOfRangeException>("count", () => sb.Replace("a", "b", 0, -1)); // Count < 0

                Assert.Throws<ArgumentOutOfRangeException>("startIndex", () => sb.Replace("a", "b", 6, 0)); // Count + start index > builder.Length
                Assert.Throws<ArgumentException>("count", () => sb.Replace("a", "b", 5, 1)); // Count + start index > builder.Length
                Assert.Throws<ArgumentException>("count", () => sb.Replace("a", "b", 4, 2)); // Count + start index > builder.Length
            }
        }

        private class ClassWithDefaultToString { }
        private class AnotherClassWithDefaultToString { }

        public static readonly IEnumerable<object[]> ConcatParamsTestData = new object[][]
        {
            new object[] { new object[] { "Hello", " ", "World" } },
            new object[] { new object[] { " Hello", "$!$!$", " World " } },
            new object[] { new object[] { 1, 2, 3, 4 } },
            new object[] { new object[] { new ClassWithDefaultToString(), new ClassWithDefaultToString() } }
        };

        [Theory]
        [MemberData(nameof(ConcatParamsTestData))]
        public static void ConcatParams(object[] args)
        {
            Assert.Equal(string.Join("", args), StringBuilder.Concat(args));
        }

        [Fact]
        public static void ConcatVarargs()
        {
            var a = new ClassWithDefaultToString();
            var b = new AnotherClassWithDefaultToString();

            Assert.Equal(string.Join("", a), StringBuilder.Concat(a));
            Assert.Equal(string.Join("", a, b), StringBuilder.Concat(a, b));
            Assert.Equal(string.Join("", a, b, a), StringBuilder.Concat(a, b, a));
            Assert.Equal(string.Join("", a, b, a), StringBuilder.Concat(a, b, a));
            Assert.Equal(string.Join("", a, b, a, b), StringBuilder.Concat(a, b, a, b));
            Assert.Equal(string.Join("", a, b, a, b, a, a, b, b), StringBuilder.Concat(a, b, a, b, a, a, b, b));
        }

        public static readonly IEnumerable<object[]> ConcatListTestData = new object[][]
        {
            new object[] { new List<string>{ "Hello", " ", "World" } },
            new object[] { new List<string>{ " Hello", "$!$!$", " World " } },
        };

        [Theory]
        [MemberData(nameof(ConcatListTestData))]
        public static void ConcatList(List<string> list)
        {
            Assert.Equal(string.Join("", list), StringBuilder.Concat(list));
        }
    }
}
