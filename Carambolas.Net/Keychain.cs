using System;

using Carambolas.Security.Cryptography;
using Carambolas.Security.Cryptography.NaCl;

namespace Carambolas.Net
{
    internal sealed class Keychain: IKeychain
    {
        public static IKeychain Default => new Keychain();

        public Key CreatePublicKey(in Key privateKey) => Curve25519.CreatePublicKey(in privateKey);

        public Key CreateSharedKey(in Key privateKey, in Key remoteKey) => Curve25519.CreateSharedKey(in privateKey, in remoteKey);
    }
}
