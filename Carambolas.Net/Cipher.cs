using System;

using Carambolas.Security.Cryptography;
using Carambolas.Security.Cryptography.NaCl;

namespace Carambolas.Net
{
    internal sealed class Cipher: ICipher
    {
        private readonly ChaCha20 cryptobox = new ChaCha20 { Counter = 1 };

        public Key Key
        {
            get => cryptobox.Key;
            set => cryptobox.Key = value;
        }

        public void EncryptInPlace(byte[] buffer, int offset, int length, in Nonce nonce) => cryptobox.Encrypt(buffer, offset, buffer, offset, length, in nonce);

        public void DecryptInPlace(byte[] buffer, int offset, int length, in Nonce nonce) => cryptobox.Decrypt(buffer, offset, buffer, offset, length, in nonce);

        public void Sign(byte[] buffer, int offset, int aadLength, int textLength, in Nonce nonce, out Mac mac) => Poly1305.AEAD.Sign(new ArraySegment<byte>(buffer, offset, aadLength), new ArraySegment<byte>(buffer, offset + aadLength, textLength), cryptobox.CreateKey(in nonce), out mac);

        public bool Verify(byte[] buffer, int offset, int aadLength, int textLength, in Nonce nonce, in Mac mac) => Poly1305.AEAD.Verify(new ArraySegment<byte>(buffer, offset, aadLength), new ArraySegment<byte>(buffer, offset + aadLength, textLength), cryptobox.CreateKey(in nonce), in mac);
    }
}
