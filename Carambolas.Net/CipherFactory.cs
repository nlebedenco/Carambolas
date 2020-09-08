using System;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{
    internal sealed class CipherFactory: ICipherFactory
    {
        public static readonly ICipherFactory Default = new CipherFactory();

        public ICipher Create() => new Cipher();
    }
}
