using System;

namespace Carambolas.Security.Cryptography
{
    public interface IRandomNumberGenerator
    {
        int GetValue();
        Key GetKey();
    }
}
