using System;
using System.Collections.Generic;
using System.Text;

namespace Carambolas.Security.Cryptography.NaCl
{
    public partial class Curve25519
    {
        internal readonly ref struct FieldElement
        {
            public const int Size = 10;

            private readonly int v0;
            private readonly int v1;
            private readonly int v2;
            private readonly int v3;
            private readonly int v4;
            private readonly int v5;
            private readonly int v6;
            private readonly int v7;
            private readonly int v8;
            private readonly int v9;

            public FieldElement(int a0)
            {
                v0 = a0;
                v1 = 0;
                v2 = 0;
                v3 = 0;
                v4 = 0;
                v5 = 0;
                v6 = 0;
                v7 = 0;
                v8 = 0;
                v9 = 0;
            }

            public FieldElement(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8, int a9)
            {
                v0 = a0;
                v1 = a1;
                v2 = a2;
                v3 = a3;
                v4 = a4;
                v5 = a5;
                v6 = a6;
                v7 = a7;
                v8 = a8;
                v9 = a9;
            }

            public FieldElement(in Key key)
            {
                var (t0, t1, t2, t3, t4, t5, t6, t7) = key;

                var h0 = (long)t0;
                var h1 = (long)(t1 & 0x00FFFFFF) << 6;
                var h2 = (long)((t1 & 0xFF000000) >> 19 | (t2 & 0x0000FFFF) << 13);
                var h3 = (long)((t2 & 0xFFFF0000) >> 13 | (t3 & 0x000000FF) << 19);
                var h4 = (long)((t3 & 0xFFFFFF00) >> 6);
                var h5 = (long)t4;
                var h6 = (long)(t5 & 0x00FFFFFF) << 7;
                var h7 = (long)((t5 & 0xFF000000) >> 19 | (t6 & 0x0000FFFF) << 13);
                var h8 = (long)((t6 & 0xFFFF0000) >> 12 | (t7 & 0x000000FF) << 20);
                var h9 = (long)(((t7 & 0xFFFFFF00) >> 8) & 8388607) << 2;

                var carry9 = (h9 + (long)(1 << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 << 25;
                var carry1 = (h1 + (long)(1 << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 << 25;
                var carry3 = (h3 + (long)(1 << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 << 25;
                var carry5 = (h5 + (long)(1 << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 << 25;
                var carry7 = (h7 + (long)(1 << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 << 25;

                var carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;
                var carry2 = (h2 + (long)(1 << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 << 26;
                var carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;
                var carry6 = (h6 + (long)(1 << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 << 26;
                var carry8 = (h8 + (long)(1 << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 << 26;

                v0 = (int)h0;
                v1 = (int)h1;
                v2 = (int)h2;
                v3 = (int)h3;
                v4 = (int)h4;
                v5 = (int)h5;
                v6 = (int)h6;
                v7 = (int)h7;
                v8 = (int)h8;
                v9 = (int)h9;
            }

            public static explicit operator Key(in FieldElement f)
            {
                int h0 = f.v0;
                int h1 = f.v1;
                int h2 = f.v2;
                int h3 = f.v3;
                int h4 = f.v4;
                int h5 = f.v5;
                int h6 = f.v6;
                int h7 = f.v7;
                int h8 = f.v8;
                int h9 = f.v9;

                int q;
                int carry0, carry1, carry2, carry3, carry4, carry5, carry6, carry7, carry8, carry9;

                q = (19 * h9 + ((int)1 << 24)) >> 25;
                q = (h0 + q) >> 26;
                q = (h1 + q) >> 25;
                q = (h2 + q) >> 26;
                q = (h3 + q) >> 25;
                q = (h4 + q) >> 26;
                q = (h5 + q) >> 25;
                q = (h6 + q) >> 26;
                q = (h7 + q) >> 25;
                q = (h8 + q) >> 26;
                q = (h9 + q) >> 25;

                // Goal: Output h-(2^255-19)q, which is between 0 and 2^255-20.
                h0 += 19 * q;

                // Goal: Output h-2^255 q, which is between 0 and 2^255-20.
                carry0 = h0 >> 26;
                h1 += carry0;
                h0 -= carry0 * (1 << 26);
                carry1 = h1 >> 25;
                h2 += carry1;
                h1 -= carry1 * (1 << 25);
                carry2 = h2 >> 26;
                h3 += carry2;
                h2 -= carry2 * (1 << 26);
                carry3 = h3 >> 25;
                h4 += carry3;
                h3 -= carry3 * (1 << 25);
                carry4 = h4 >> 26;
                h5 += carry4;
                h4 -= carry4 * (1 << 26);
                carry5 = h5 >> 25;
                h6 += carry5;
                h5 -= carry5 * (1 << 25);
                carry6 = h6 >> 26;
                h7 += carry6;
                h6 -= carry6 * (1 << 26);
                carry7 = h7 >> 25;
                h8 += carry7;
                h7 -= carry7 * (1 << 25);
                carry8 = h8 >> 26;
                h9 += carry8;
                h8 -= carry8 * (1 << 26);
                carry9 = h9 >> 25;
                h9 -= carry9 * (1 << 25);

                return new Key(
                    (uint)h0                        | ((((uint)h1 * (1 << 2)) & 0x000000FF) << 24),

                    (((uint)h1 >>  6) & 0x00FFFFFF) | ((((uint)h2 * (1 << 3)) & 0x000000FF) << 16) | (((uint)h2 << 19) & 0xFF000000),

                    (((uint)h2 >> 13) & 0x0000FFFF) | ((((uint)h3 * (1 << 5)) & 0x000000FF) <<  8) | (((uint)h3 << 13) & 0xFFFF0000),

                    (((uint)h3 >> 19) & 0x000000FF) | ((((uint)h4 * (1 << 6)) & 0x000000FF)      ) | (((uint)h4 <<  6) & 0xFFFFFF00),

                    (uint)h5                        | ((((uint)h6 * (1 << 1)) & 0x000000FF) << 24),

                    (((uint)h6 >>  7) & 0x00FFFFFF) | ((((uint)h7 * (1 << 3)) & 0x000000FF) << 16) | (((uint)h7 << 19) & 0xFF000000),

                    (((uint)h7 >> 13) & 0x0000FFFF) | ((((uint)h8 * (1 << 4)) & 0x000000FF) <<  8) | (((uint)h8 << 12) & 0xFFFF0000),

                    (((uint)h8 >> 20) & 0x000000FF) | ((((uint)h9 * (1 << 6)) & 0x000000FF)      ) | (((uint)h9 << 6) & 0xFFFFFF00)
                );                
            }

            public static void ConditionalSwap(ref FieldElement f, ref FieldElement g, int b) 
            {
                var mask = -b;

                var f0 = f.v0;
                var f1 = f.v1;
                var f2 = f.v2;
                var f3 = f.v3;
                var f4 = f.v4;
                var f5 = f.v5;
                var f6 = f.v6;
                var f7 = f.v7;
                var f8 = f.v8;
                var f9 = f.v9;

                var g0 = g.v0;
                var g1 = g.v1;
                var g2 = g.v2;
                var g3 = g.v3;
                var g4 = g.v4;
                var g5 = g.v5;
                var g6 = g.v6;
                var g7 = g.v7;
                var g8 = g.v8;
                var g9 = g.v9;

                var x0 = f0 ^ g0;
                var x1 = f1 ^ g1;
                var x2 = f2 ^ g2;
                var x3 = f3 ^ g3;
                var x4 = f4 ^ g4;
                var x5 = f5 ^ g5;
                var x6 = f6 ^ g6;
                var x7 = f7 ^ g7;
                var x8 = f8 ^ g8;
                var x9 = f9 ^ g9;

                x0 &= mask;
                x1 &= mask;
                x2 &= mask;
                x3 &= mask;
                x4 &= mask;
                x5 &= mask;
                x6 &= mask;
                x7 &= mask;
                x8 &= mask;
                x9 &= mask;

                f = new FieldElement(f0 ^ x0, f1 ^ x1, f2 ^ x2, f3 ^ x3, f4 ^ x4, f5 ^ x5, f6 ^ x6, f7 ^ x7, f8 ^ x8, f9 ^ x9);
                g = new FieldElement(g0 ^ x0, g1 ^ x1, g2 ^ x2, g3 ^ x3, g4 ^ x4, g5 ^ x5, g6 ^ x6, g7 ^ x7, g8 ^ x8, g9 ^ x9);
            }

            public static FieldElement operator -(in FieldElement f, in FieldElement g) => new FieldElement(f.v0 - g.v0, f.v1 - g.v1, f.v2 - g.v2, f.v3 - g.v3, f.v4 - g.v4, f.v5 - g.v5, f.v6 - g.v6, f.v7 - g.v7, f.v8 - g.v8, f.v9 - g.v9);

            public static FieldElement operator +(in FieldElement f, in FieldElement g) => new FieldElement(f.v0 + g.v0, f.v1 + g.v1, f.v2 + g.v2, f.v3 + g.v3, f.v4 + g.v4, f.v5 + g.v5, f.v6 + g.v6, f.v7 + g.v7, f.v8 + g.v8, f.v9 + g.v9);

            public static FieldElement operator *(in FieldElement f, in FieldElement g)
            {
                var f0 = f.v0;
                var f1 = f.v1;
                var f2 = f.v2;
                var f3 = f.v3;
                var f4 = f.v4;
                var f5 = f.v5;
                var f6 = f.v6;
                var f7 = f.v7;
                var f8 = f.v8;
                var f9 = f.v9;

                var g0 = g.v0;
                var g1 = g.v1;
                var g2 = g.v2;
                var g3 = g.v3;
                var g4 = g.v4;
                var g5 = g.v5;
                var g6 = g.v6;
                var g7 = g.v7;
                var g8 = g.v8;
                var g9 = g.v9;

                var g1_19 = 19 * g1; /* 1.959375*2^29 */
                var g2_19 = 19 * g2; /* 1.959375*2^30; still ok */
                var g3_19 = 19 * g3;
                var g4_19 = 19 * g4;
                var g5_19 = 19 * g5;
                var g6_19 = 19 * g6;
                var g7_19 = 19 * g7;
                var g8_19 = 19 * g8;
                var g9_19 = 19 * g9;
                var f1_2 = 2 * f1;
                var f3_2 = 2 * f3;
                var f5_2 = 2 * f5;
                var f7_2 = 2 * f7;
                var f9_2 = 2 * f9;

                var f0g0 = f0 * (long)g0;
                var f0g1 = f0 * (long)g1;
                var f0g2 = f0 * (long)g2;
                var f0g3 = f0 * (long)g3;
                var f0g4 = f0 * (long)g4;
                var f0g5 = f0 * (long)g5;
                var f0g6 = f0 * (long)g6;
                var f0g7 = f0 * (long)g7;
                var f0g8 = f0 * (long)g8;
                var f0g9 = f0 * (long)g9;
                var f1g0 = f1 * (long)g0;
                var f1g1_2 = f1_2 * (long)g1;
                var f1g2 = f1 * (long)g2;
                var f1g3_2 = f1_2 * (long)g3;
                var f1g4 = f1 * (long)g4;
                var f1g5_2 = f1_2 * (long)g5;
                var f1g6 = f1 * (long)g6;
                var f1g7_2 = f1_2 * (long)g7;
                var f1g8 = f1 * (long)g8;
                var f1g9_38 = f1_2 * (long)g9_19;
                var f2g0 = f2 * (long)g0;
                var f2g1 = f2 * (long)g1;
                var f2g2 = f2 * (long)g2;
                var f2g3 = f2 * (long)g3;
                var f2g4 = f2 * (long)g4;
                var f2g5 = f2 * (long)g5;
                var f2g6 = f2 * (long)g6;
                var f2g7 = f2 * (long)g7;
                var f2g8_19 = f2 * (long)g8_19;
                var f2g9_19 = f2 * (long)g9_19;
                var f3g0 = f3 * (long)g0;
                var f3g1_2 = f3_2 * (long)g1;
                var f3g2 = f3 * (long)g2;
                var f3g3_2 = f3_2 * (long)g3;
                var f3g4 = f3 * (long)g4;
                var f3g5_2 = f3_2 * (long)g5;
                var f3g6 = f3 * (long)g6;
                var f3g7_38 = f3_2 * (long)g7_19;
                var f3g8_19 = f3 * (long)g8_19;
                var f3g9_38 = f3_2 * (long)g9_19;
                var f4g0 = f4 * (long)g0;
                var f4g1 = f4 * (long)g1;
                var f4g2 = f4 * (long)g2;
                var f4g3 = f4 * (long)g3;
                var f4g4 = f4 * (long)g4;
                var f4g5 = f4 * (long)g5;
                var f4g6_19 = f4 * (long)g6_19;
                var f4g7_19 = f4 * (long)g7_19;
                var f4g8_19 = f4 * (long)g8_19;
                var f4g9_19 = f4 * (long)g9_19;
                var f5g0 = f5 * (long)g0;
                var f5g1_2 = f5_2 * (long)g1;
                var f5g2 = f5 * (long)g2;
                var f5g3_2 = f5_2 * (long)g3;
                var f5g4 = f5 * (long)g4;
                var f5g5_38 = f5_2 * (long)g5_19;
                var f5g6_19 = f5 * (long)g6_19;
                var f5g7_38 = f5_2 * (long)g7_19;
                var f5g8_19 = f5 * (long)g8_19;
                var f5g9_38 = f5_2 * (long)g9_19;
                var f6g0 = f6 * (long)g0;
                var f6g1 = f6 * (long)g1;
                var f6g2 = f6 * (long)g2;
                var f6g3 = f6 * (long)g3;
                var f6g4_19 = f6 * (long)g4_19;
                var f6g5_19 = f6 * (long)g5_19;
                var f6g6_19 = f6 * (long)g6_19;
                var f6g7_19 = f6 * (long)g7_19;
                var f6g8_19 = f6 * (long)g8_19;
                var f6g9_19 = f6 * (long)g9_19;
                var f7g0 = f7 * (long)g0;
                var f7g1_2 = f7_2 * (long)g1;
                var f7g2 = f7 * (long)g2;
                var f7g3_38 = f7_2 * (long)g3_19;
                var f7g4_19 = f7 * (long)g4_19;
                var f7g5_38 = f7_2 * (long)g5_19;
                var f7g6_19 = f7 * (long)g6_19;
                var f7g7_38 = f7_2 * (long)g7_19;
                var f7g8_19 = f7 * (long)g8_19;
                var f7g9_38 = f7_2 * (long)g9_19;
                var f8g0 = f8 * (long)g0;
                var f8g1 = f8 * (long)g1;
                var f8g2_19 = f8 * (long)g2_19;
                var f8g3_19 = f8 * (long)g3_19;
                var f8g4_19 = f8 * (long)g4_19;
                var f8g5_19 = f8 * (long)g5_19;
                var f8g6_19 = f8 * (long)g6_19;
                var f8g7_19 = f8 * (long)g7_19;
                var f8g8_19 = f8 * (long)g8_19;
                var f8g9_19 = f8 * (long)g9_19;
                var f9g0 = f9 * (long)g0;
                var f9g1_38 = f9_2 * (long)g1_19;
                var f9g2_19 = f9 * (long)g2_19;
                var f9g3_38 = f9_2 * (long)g3_19;
                var f9g4_19 = f9 * (long)g4_19;
                var f9g5_38 = f9_2 * (long)g5_19;
                var f9g6_19 = f9 * (long)g6_19;
                var f9g7_38 = f9_2 * (long)g7_19;
                var f9g8_19 = f9 * (long)g8_19;
                var f9g9_38 = f9_2 * (long)g9_19;

                var h0 = f0g0 + f1g9_38 + f2g8_19 + f3g7_38 + f4g6_19 + f5g5_38 + f6g4_19 + f7g3_38 + f8g2_19 + f9g1_38;
                var h1 = f0g1 + f1g0 + f2g9_19 + f3g8_19 + f4g7_19 + f5g6_19 + f6g5_19 + f7g4_19 + f8g3_19 + f9g2_19;
                var h2 = f0g2 + f1g1_2 + f2g0 + f3g9_38 + f4g8_19 + f5g7_38 + f6g6_19 + f7g5_38 + f8g4_19 + f9g3_38;
                var h3 = f0g3 + f1g2 + f2g1 + f3g0 + f4g9_19 + f5g8_19 + f6g7_19 + f7g6_19 + f8g5_19 + f9g4_19;
                var h4 = f0g4 + f1g3_2 + f2g2 + f3g1_2 + f4g0 + f5g9_38 + f6g8_19 + f7g7_38 + f8g6_19 + f9g5_38;
                var h5 = f0g5 + f1g4 + f2g3 + f3g2 + f4g1 + f5g0 + f6g9_19 + f7g8_19 + f8g7_19 + f9g6_19;
                var h6 = f0g6 + f1g5_2 + f2g4 + f3g3_2 + f4g2 + f5g1_2 + f6g0 + f7g9_38 + f8g8_19 + f9g7_38;
                var h7 = f0g7 + f1g6 + f2g5 + f3g4 + f4g3 + f5g2 + f6g1 + f7g0 + f8g9_19 + f9g8_19;
                var h8 = f0g8 + f1g7_2 + f2g6 + f3g5_2 + f4g4 + f5g3_2 + f6g2 + f7g1_2 + f8g0 + f9g9_38;
                var h9 = f0g9 + f1g8 + f2g7 + f3g6 + f4g5 + f5g4 + f6g3 + f7g2 + f8g1 + f9g0;

                // |h0| <= (1.65*1.65*2^52*(1+19+19+19+19)+1.65*1.65*2^50*(38+38+38+38+38))
                //      i.e. |h0| <= 1.4*2^60; narrower ranges for h2, h4, h6, h8
                // |h1| <= (1.65*1.65*2^51*(1+1+19+19+19+19+19+19+19+19))
                //      i.e. |h1| <= 1.7*2^59; narrower ranges for h3, h5, h7, h9

                var carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 * (1 << 26);
                var carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 * (1 << 26);
                // |h0| <= 2^25
                // |h4| <= 2^25
                // |h1| <= 1.71*2^59
                // |h5| <= 1.71*2^59

                var carry1 = (h1 + (long)(1 << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 * (1 << 25);
                var carry5 = (h5 + (long)(1 << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 * (1 << 25);

                // |h1| <= 2^24; from now on fits into int32
                // |h5| <= 2^24; from now on fits into int32
                // |h2| <= 1.41*2^60
                // |h6| <= 1.41*2^60

                var carry2 = (h2 + (long)(1 << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 * (1 << 26);
                var carry6 = (h6 + (long)(1 << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 * (1 << 26);

                // |h2| <= 2^25; from now on fits into int32 unchanged
                // |h6| <= 2^25; from now on fits into int32 unchanged
                // |h3| <= 1.71*2^59
                // |h7| <= 1.71*2^59

                var carry3 = (h3 + (long)(1 << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 * (1 << 25);
                var carry7 = (h7 + (long)(1 << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 * (1 << 25);

                // |h3| <= 2^24; from now on fits into int32 unchanged
                // |h7| <= 2^24; from now on fits into int32 unchanged
                // |h4| <= 1.72*2^34
                // |h8| <= 1.41*2^60

                carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 * (1 << 26);
                var carry8 = (h8 + (long)(1 << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 * (1 << 26);

                // |h4| <= 2^25; from now on fits into int32 unchanged
                // |h8| <= 2^25; from now on fits into int32 unchanged
                // |h5| <= 1.01*2^24
                // |h9| <= 1.71*2^59

                var carry9 = (h9 + (long)(1 << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 * (1 << 25);

                // |h9| <= 2^24; from now on fits into int32 unchanged
                // |h0| <= 1.1*2^39

                carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 * (1 << 26);

                // |h0| <= 2^25; from now on fits into int32 unchanged
                // |h1| <= 1.01*2^24

                return new FieldElement((int)h0, (int)h1, (int)h2, (int)h3, (int)h4, (int)h5, (int)h6, (int)h7, (int)h8, (int)h9);
            }

            public static FieldElement Square(in FieldElement f)
            {
                var f0 = f.v0;
                var f1 = f.v1;
                var f2 = f.v2;
                var f3 = f.v3;
                var f4 = f.v4;
                var f5 = f.v5;
                var f6 = f.v6;
                var f7 = f.v7;
                var f8 = f.v8;
                var f9 = f.v9;

                var f0_2 = 2 * f0;
                var f1_2 = 2 * f1;
                var f2_2 = 2 * f2;
                var f3_2 = 2 * f3;
                var f4_2 = 2 * f4;
                var f5_2 = 2 * f5;
                var f6_2 = 2 * f6;
                var f7_2 = 2 * f7;
                var f5_38 = 38 * f5; // 1.959375*2^30
                var f6_19 = 19 * f6; // 1.959375*2^30
                var f7_38 = 38 * f7; // 1.959375*2^30
                var f8_19 = 19 * f8; // 1.959375*2^30
                var f9_38 = 38 * f9; // 1.959375*2^30

                var f0f0 = f0 * (long)f0;
                var f0f1_2 = f0_2 * (long)f1;
                var f0f2_2 = f0_2 * (long)f2;
                var f0f3_2 = f0_2 * (long)f3;
                var f0f4_2 = f0_2 * (long)f4;
                var f0f5_2 = f0_2 * (long)f5;
                var f0f6_2 = f0_2 * (long)f6;
                var f0f7_2 = f0_2 * (long)f7;
                var f0f8_2 = f0_2 * (long)f8;
                var f0f9_2 = f0_2 * (long)f9;
                var f1f1_2 = f1_2 * (long)f1;
                var f1f2_2 = f1_2 * (long)f2;
                var f1f3_4 = f1_2 * (long)f3_2;
                var f1f4_2 = f1_2 * (long)f4;
                var f1f5_4 = f1_2 * (long)f5_2;
                var f1f6_2 = f1_2 * (long)f6;
                var f1f7_4 = f1_2 * (long)f7_2;
                var f1f8_2 = f1_2 * (long)f8;
                var f1f9_76 = f1_2 * (long)f9_38;
                var f2f2 = f2 * (long)f2;
                var f2f3_2 = f2_2 * (long)f3;
                var f2f4_2 = f2_2 * (long)f4;
                var f2f5_2 = f2_2 * (long)f5;
                var f2f6_2 = f2_2 * (long)f6;
                var f2f7_2 = f2_2 * (long)f7;
                var f2f8_38 = f2_2 * (long)f8_19;
                var f2f9_38 = f2 * (long)f9_38;
                var f3f3_2 = f3_2 * (long)f3;
                var f3f4_2 = f3_2 * (long)f4;
                var f3f5_4 = f3_2 * (long)f5_2;
                var f3f6_2 = f3_2 * (long)f6;
                var f3f7_76 = f3_2 * (long)f7_38;
                var f3f8_38 = f3_2 * (long)f8_19;
                var f3f9_76 = f3_2 * (long)f9_38;
                var f4f4 = f4 * (long)f4;
                var f4f5_2 = f4_2 * (long)f5;
                var f4f6_38 = f4_2 * (long)f6_19;
                var f4f7_38 = f4 * (long)f7_38;
                var f4f8_38 = f4_2 * (long)f8_19;
                var f4f9_38 = f4 * (long)f9_38;
                var f5f5_38 = f5 * (long)f5_38;
                var f5f6_38 = f5_2 * (long)f6_19;
                var f5f7_76 = f5_2 * (long)f7_38;
                var f5f8_38 = f5_2 * (long)f8_19;
                var f5f9_76 = f5_2 * (long)f9_38;
                var f6f6_19 = f6 * (long)f6_19;
                var f6f7_38 = f6 * (long)f7_38;
                var f6f8_38 = f6_2 * (long)f8_19;
                var f6f9_38 = f6 * (long)f9_38;
                var f7f7_38 = f7 * (long)f7_38;
                var f7f8_38 = f7_2 * (long)f8_19;
                var f7f9_76 = f7_2 * (long)f9_38;
                var f8f8_19 = f8 * (long)f8_19;
                var f8f9_38 = f8 * (long)f9_38;
                var f9f9_38 = f9 * (long)f9_38;

                var h0 = f0f0 + f1f9_76 + f2f8_38 + f3f7_76 + f4f6_38 + f5f5_38;
                var h1 = f0f1_2 + f2f9_38 + f3f8_38 + f4f7_38 + f5f6_38;
                var h2 = f0f2_2 + f1f1_2 + f3f9_76 + f4f8_38 + f5f7_76 + f6f6_19;
                var h3 = f0f3_2 + f1f2_2 + f4f9_38 + f5f8_38 + f6f7_38;
                var h4 = f0f4_2 + f1f3_4 + f2f2 + f5f9_76 + f6f8_38 + f7f7_38;
                var h5 = f0f5_2 + f1f4_2 + f2f3_2 + f6f9_38 + f7f8_38;
                var h6 = f0f6_2 + f1f5_4 + f2f4_2 + f3f3_2 + f7f9_76 + f8f8_19;
                var h7 = f0f7_2 + f1f6_2 + f2f5_2 + f3f4_2 + f8f9_38;
                var h8 = f0f8_2 + f1f7_4 + f2f6_2 + f3f5_4 + f4f4 + f9f9_38;
                var h9 = f0f9_2 + f1f8_2 + f2f7_2 + f3f6_2 + f4f5_2;

                var carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 * (1 << 26);
                var carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 * (1 << 26);

                var carry1 = (h1 + (long)(1 << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 * (1 << 25);
                var carry5 = (h5 + (long)(1 << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 * (1 << 25);

                var carry2 = (h2 + (long)(1 << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 * (1 << 26);
                var carry6 = (h6 + (long)(1 << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 * (1 << 26);

                var carry3 = (h3 + (long)(1 << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 * (1 << 25);
                var carry7 = (h7 + (long)(1 << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 * (1 << 25);

                carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 * (1 << 26);
                var carry8 = (h8 + (long)(1 << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 * (1 << 26);

                var carry9 = (h9 + (long)(1 << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 * (1 << 25);

                carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 * (1 << 26);

                return new FieldElement((int)h0, (int)h1, (int)h2, (int)h3, (int)h4, (int)h5, (int)h6, (int)h7, (int)h8, (int)h9);
            }

            public static FieldElement MultiplyBy121666(in FieldElement f)
            {
                var f0 = f.v0;
                var f1 = f.v1;
                var f2 = f.v2;
                var f3 = f.v3;
                var f4 = f.v4;
                var f5 = f.v5;
                var f6 = f.v6;
                var f7 = f.v7;
                var f8 = f.v8;
                var f9 = f.v9;

                var h0 = f0 * (long)121666;
                var h1 = f1 * (long)121666;
                var h2 = f2 * (long)121666;
                var h3 = f3 * (long)121666;
                var h4 = f4 * (long)121666;
                var h5 = f5 * (long)121666;
                var h6 = f6 * (long)121666;
                var h7 = f7 * (long)121666;
                var h8 = f8 * (long)121666;
                var h9 = f9 * (long)121666;

                var carry9 = (h9 + (long)(1 << 24)) >> 25;
                h0 += carry9 * 19;
                h9 -= carry9 << 25;
                var carry1 = (h1 + (long)(1 << 24)) >> 25;
                h2 += carry1;
                h1 -= carry1 << 25;
                var carry3 = (h3 + (long)(1 << 24)) >> 25;
                h4 += carry3;
                h3 -= carry3 << 25;
                var carry5 = (h5 + (long)(1 << 24)) >> 25;
                h6 += carry5;
                h5 -= carry5 << 25;
                var carry7 = (h7 + (long)(1 << 24)) >> 25;
                h8 += carry7;
                h7 -= carry7 << 25;

                var carry0 = (h0 + (long)(1 << 25)) >> 26;
                h1 += carry0;
                h0 -= carry0 << 26;
                var carry2 = (h2 + (long)(1 << 25)) >> 26;
                h3 += carry2;
                h2 -= carry2 << 26;
                var carry4 = (h4 + (long)(1 << 25)) >> 26;
                h5 += carry4;
                h4 -= carry4 << 26;
                var carry6 = (h6 + (long)(1 << 25)) >> 26;
                h7 += carry6;
                h6 -= carry6 << 26;
                var carry8 = (h8 + (long)(1 << 25)) >> 26;
                h9 += carry8;
                h8 -= carry8 << 26;

                return new FieldElement((int)h0, (int)h1, (int)h2, (int)h3, (int)h4, (int)h5, (int)h6, (int)h7, (int)h8, (int)h9);
            }

            /// <summary>
            /// Calculates <paramref name="f"/> ^(-1)
            /// </summary>
            public static FieldElement Invert(in FieldElement f)
            {
                var t0 = Square(in f);
                var t1 = Square(in t0);
                t1 = Square(in t1);
                t1 = f * t1;
                t0 = t0 * t1;

                var t2 = Square(in t0);

                t1 = t1 * t2;
                t2 = Square(t1);
                for (int i = 1; i < 5; ++i)
                    t2 = Square(in t2);

                t1 = t2 * t1;
                t2 = Square(t1);
                for (int i = 1; i < 10; ++i)
                    t2 = Square(in t2);

                t2 = t2 * t1;
                var t3 = Square(t2);
                for (int i = 1; i < 20; ++i)
                    t3 = Square(in t3);

                t2 = t3 * t2;
                t2 = Square(t2);
                for (int i = 1; i < 10; ++i)
                    t2 = Square(in t2);

                t1 = t2 * t1;
                t2 = Square(in t1);
                for (int i = 1; i < 50; ++i)
                    t2 = Square(in t2);

                t2 = t2 * t1;
                t3 = Square(in t2);
                for (int i = 1; i < 100; ++i)
                    t3 = Square(in t3);

                t2 = t3 * t2;
                t2 = Square(in t2);
                for (int i = 1; i < 50; ++i)
                    t2 = Square(in t2);

                t1 = t2 * t1;
                t1 = Square(in t1);
                for (int i = 1; i < 5; ++i)
                    t1 = Square(in t1);

                return t1 * t0;
            }            
        }
    }
}
