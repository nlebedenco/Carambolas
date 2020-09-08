using System;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{   
    public interface ICipher
    {
        Key Key { get; set; }

        void EncryptInPlace(byte[] buffer, int offset, int length, in Nonce nonce);
        void DecryptInPlace(byte[] buffer, int offset, int length, in Nonce nonce);

        void Sign(byte[] buffer, int offset, int aadLength, int textLength, in Nonce nonce, out Mac mac);
        bool Verify(byte[] buffer, int offset, int aadLength, int textLength, in Nonce nonce, in Mac mac);        
    }
}
