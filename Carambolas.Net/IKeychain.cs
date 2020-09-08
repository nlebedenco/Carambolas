using System;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{
    public interface IKeychain
    {
        Key CreatePublicKey(in Key privateKey);
        Key CreateSharedKey(in Key privateKey, in Key remoteKey);
    }
}
