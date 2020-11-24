using System;
using System.Runtime.CompilerServices;

using Carambolas.Internal;

namespace Carambolas.Security.Cryptography.NaCl
{
    /// <summary>
    /// ChaCha20 is a stream cipher designed by D. J. Bernstein.  It is a
    /// refinement of the Salsa20 algorithm that uses a 256-bit key.
    /// </summary>
    /// <remarks>
    /// ChaCha20 successively calls the ChaCha20 block function, with the
    /// same key and nonce, and with successively increasing block counter
    /// parameters. It then serializes the resulting state by writing
    /// the numbers in little-endian order, creating a keystream block.
    /// See https://tools.ietf.org/html/rfc7539 for more info.
    /// This implementation is based on https://github.com/daviddesmet/NaCl.Core.
    /// </remarks>
    public class ChaCha20
    {
        public const int BlockSize = 64;

        protected const int StateLength = BlockSize / sizeof(uint);

        protected static readonly uint[] Sigma = new uint[] { 0x61707865, 0x3320646E, 0x79622D32, 0x6B206574 }; // equivalent to Encoding.ASCII.GetBytes("expand 32-byte k");

        private readonly byte[] block = new byte[BlockSize];
        private readonly uint[] state = new uint[StateLength];
        private readonly uint[] local = new uint[StateLength];

        public Key Key;
        public uint Counter;
        
        public Key CreateKey(in Nonce nonce, uint counter = 0)
        {
            Process(block, counter, in nonce);
            return new Key(block);
        }

        public void Encrypt(byte[] source, int sourceIndex, byte[] destination, int destinationIndex, int length, in Nonce nonce)
        {
            ValidateArguments(source, sourceIndex, destination, destinationIndex, length);

            if (length > 0)
                Process(source, sourceIndex, destination, destinationIndex, length, in nonce);
        }

        public void Decrypt(byte[] source, int sourceIndex, byte[] destination, int destinationIndex, int length, in Nonce nonce)
        {
            ValidateArguments(source, sourceIndex, destination, destinationIndex, length);

            if (length > 0)
                Process(source, sourceIndex, destination, destinationIndex, length, in nonce);
        }

        protected virtual void SetInitialState(uint[] state, uint counter, in Nonce nonce)
        {
            // The first four words (0-3) are constants: 0x61707865, 0x3320646e, 0x79622d32, 0x6b206574.
            // The next eight words (4-11) are taken from the 256-bit key in little-endian order, in 4-byte chunks.
            SetSigma(state);
            Key.CopyTo(state, 4);

            // Word 12 is a block counter. Since each block is 64-byte, a 32-bit word is enough for 256 gigabytes of data. Ref: https://tools.ietf.org/html/rfc8439#section-2.3.
            state[12] = counter;

            // Words 13-15 are a nonce, which must not be repeated for the same key.
            // The 13th word is the first 32 bits of the input nonce taken as a little-endian integer, while the 15th word is the last 32 bits.
            nonce.CopyTo(state, 13);
        }

        /// <summary>
        /// Process <paramref name="block"/> from <paramref name="nonce"/> and <paramref name="counter"/>.
        /// <para/>
        /// From this function, the encryption function can be constructed using the counter mode. 
        /// For example, the ChaCha20 block function and how it can be used to construct the 
        /// ChaCha20 encryption function are described in section 2.3 and 2.4 of RFC 8439.
        /// </summary>        
        protected internal virtual void Process(byte[] block, uint counter, in Nonce nonce) // Made internal for testing
        {
            // Set the initial state based on https://tools.ietf.org/html/rfc8439#section-2.3
            SetInitialState(state, counter, in nonce);

            for (int i = 0; i < StateLength; ++i)
                local[i] = state[i];

            Shuffle(local);

            // At the end of the rounds, add the result to the original state.
            state[0] += local[0];
            state[1] += local[1];
            state[2] += local[2];
            state[3] += local[3];
            state[4] += local[4];
            state[5] += local[5];
            state[6] += local[6];
            state[7] += local[7];
            state[8] += local[8];
            state[9] += local[9];
            state[10] += local[10];
            state[11] += local[11];
            state[12] += local[12];
            state[13] += local[13];
            state[14] += local[14];
            state[15] += local[15];

            StoreArray16UInt32LittleEndian(state, block);
        }

        private void Process(byte[] source, int sourceIndex, byte[] destination, int destinationIndex, int length, in Nonce nonce)
        {
            var k = length / BlockSize;
            var n = k + 1;
            var counter = Counter;

            for (int i = 0; i < n; ++i, ++counter)
            {
                Process(block, counter, in nonce);

                var m = (i == k) ? length % BlockSize : BlockSize;
                for (var j = 0; j < m; ++j, ++destinationIndex, ++sourceIndex)
                    destination[destinationIndex] = (byte)(source[sourceIndex] ^ block[j]);

                Array.Clear(block, 0, block.Length);
            }
        }     

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Shuffle(uint[] state)
        {
            for (var i = 0; i < 10; ++i)
            {
                QuarterRound(ref state[0], ref state[4], ref state[8], ref state[12]);
                QuarterRound(ref state[1], ref state[5], ref state[9], ref state[13]);
                QuarterRound(ref state[2], ref state[6], ref state[10], ref state[14]);
                QuarterRound(ref state[3], ref state[7], ref state[11], ref state[15]);
                QuarterRound(ref state[0], ref state[5], ref state[10], ref state[15]);
                QuarterRound(ref state[1], ref state[6], ref state[11], ref state[12]);
                QuarterRound(ref state[2], ref state[7], ref state[8], ref state[13]);
                QuarterRound(ref state[3], ref state[4], ref state[9], ref state[14]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetSigma(uint[] state)
        {
            state[0] = Sigma[0];
            state[1] = Sigma[1];
            state[2] = Sigma[2];
            state[3] = Sigma[3];
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d) // Made internal for testing
        {
            a += b;
            d = BitUtils.RotateLeft(d ^ a, 16);
            c += d;
            b = BitUtils.RotateLeft(b ^ c, 12);
            a += b;
            d = BitUtils.RotateLeft(d ^ a, 8);
            c += d;
            b = BitUtils.RotateLeft(b ^ c, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateArguments(byte[] source, int sourceIndex, byte[] destination, int destinationIndex, int length)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (sourceIndex < 0 || sourceIndex > source.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));

            if (destinationIndex < 0 || destinationIndex > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));

            if (sourceIndex > source.Length - length)
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(sourceIndex), nameof(length), nameof(source)), nameof(length));

            if (destinationIndex > destination.Length - length)
                throw new ArgumentException(string.Format(Resources.GetString(Strings.IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements), nameof(destinationIndex), nameof(length), nameof(destination)), nameof(length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreUI32LittleEndian(uint value, byte[] destination, int offset = 0)
        {
            destination[offset] = (byte)(value);
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreArray16UInt32LittleEndian(uint[] source, byte[] destination, int offset = 0)
        {
            StoreUI32LittleEndian(source[0], destination, offset + 0);
            StoreUI32LittleEndian(source[1], destination, offset + 4);
            StoreUI32LittleEndian(source[2], destination, offset + 8);
            StoreUI32LittleEndian(source[3], destination, offset + 12);
            StoreUI32LittleEndian(source[4], destination, offset + 16);
            StoreUI32LittleEndian(source[5], destination, offset + 20);
            StoreUI32LittleEndian(source[6], destination, offset + 24);
            StoreUI32LittleEndian(source[7], destination, offset + 28);
            StoreUI32LittleEndian(source[8], destination, offset + 32);
            StoreUI32LittleEndian(source[9], destination, offset + 36);
            StoreUI32LittleEndian(source[10], destination, offset + 40);
            StoreUI32LittleEndian(source[11], destination, offset + 44);
            StoreUI32LittleEndian(source[12], destination, offset + 48);
            StoreUI32LittleEndian(source[13], destination, offset + 52);
            StoreUI32LittleEndian(source[14], destination, offset + 56);
            StoreUI32LittleEndian(source[15], destination, offset + 60);
        }
    }
}
