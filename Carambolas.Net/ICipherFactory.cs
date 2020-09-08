using System;

using Carambolas.Security.Cryptography;

namespace Carambolas.Net
{
    public interface ICipherFactory
    {
        ICipher Create();
    }
}
