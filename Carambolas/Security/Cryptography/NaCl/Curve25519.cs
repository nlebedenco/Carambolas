using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Carambolas.Security.Cryptography.NaCl
{    
    public static partial class Curve25519
    {
        /// <summary>
        /// Length in bytes of a scalar in the curve.
        /// </summary>
        private const int ScalarSize = Key.Size;

        private static readonly Key basepoint = new Key(9, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Computes a public key from an arbitrary private key.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Key CreatePublicKey(in Key privateKey) => Operate(in privateKey, in basepoint);

        /// <summary>
        /// Computes a shared key from a private key and the counterpart's public key
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Key CreateSharedKey(in Key privateKey, in Key remoteKey) => Operate(in privateKey, in remoteKey);

        /// <summary>
        /// Returns <paramref name="f"/> X <paramref name="g"/>
        /// </summary>
        private static Key Operate(in Key f, in Key g)
        {
            var (k0, k1, k2, k3, k4, k5, k6, k7) = f;

            Span<byte> k = stackalloc byte[Key.Size];

            k[0] = (byte)((k0 >> 0) & 248);
            k[1] = (byte)(k0 >> 8);
            k[2] = (byte)(k0 >> 16);
            k[3] = (byte)(k0 >> 24);
            k[4] = (byte)(k1 >> 0);
            k[5] = (byte)(k1 >> 8);
            k[6] = (byte)(k1 >> 16);
            k[7] = (byte)(k1 >> 24);
            k[8] = (byte)(k2 >> 0);
            k[9] = (byte)(k2 >> 8);
            k[10] = (byte)(k2 >> 16);
            k[11] = (byte)(k2 >> 24);
            k[12] = (byte)(k3 >> 0);
            k[13] = (byte)(k3 >> 8);
            k[14] = (byte)(k3 >> 16);
            k[15] = (byte)(k3 >> 24);
            k[16] = (byte)(k4 >> 0);
            k[17] = (byte)(k4 >> 8);
            k[18] = (byte)(k4 >> 16);
            k[19] = (byte)(k4 >> 24);
            k[20] = (byte)(k5 >> 0);
            k[21] = (byte)(k5 >> 8);
            k[22] = (byte)(k5 >> 16);
            k[23] = (byte)(k5 >> 24);
            k[24] = (byte)(k6 >> 0);
            k[25] = (byte)(k6 >> 8);
            k[26] = (byte)(k6 >> 16);
            k[27] = (byte)(k6 >> 24);
            k[28] = (byte)(k7 >> 0);
            k[29] = (byte)(k7 >> 8);
            k[30] = (byte)(k7 >> 16);
            k[31] = (byte)(((k7 >> 24) & 127) | 64);

            var x1 = new FieldElement(in g);
            var x2 = new FieldElement(1);
            var z2 = default(FieldElement);
            var x3 = x1;
            var z3 = new FieldElement(1);

            var swap = 0;
            for (int pos = 254; pos >= 0; --pos)
            {
                var b = k[pos / 8] >> (pos & 7);
                b &= 1;
                swap ^= b;
                FieldElement.ConditionalSwap(ref x2, ref x3, swap);
                FieldElement.ConditionalSwap(ref z2, ref z3, swap);
                swap = b;
                var t0 = x3 - z3;
                var t1 = x2 - z2;
                x2 = x2 + z2;
                z2 = x3 + z3;
                z3 = t0 * x2;
                z2 = z2 * t1;
                t0 = FieldElement.Square(in t1);
                t1 = FieldElement.Square(in x2);
                x3 = z3 + z2;
                z2 = z3 - z2;
                x2 = t1 * t0;
                t1 = t1 - t0;
                z2 = FieldElement.Square(in z2);
                z3 = FieldElement.MultiplyBy121666(in t1);
                x3 = FieldElement.Square(in x3);
                t0 = t0 + z3;
                z3 = x1 * z2;
                z2 = t1 * t0;
            }

            FieldElement.ConditionalSwap(ref x2, ref x3, swap);
            FieldElement.ConditionalSwap(ref z2, ref z3, swap);

            z2 = FieldElement.Invert(in z2);
            x2 = x2 * z2;

            return (Key)x2;
        }        
    }
}
