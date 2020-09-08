using System;

namespace Carambolas
{
    public static class HexConverter
    {
        public static byte[] ToBytes(string s)
        {
            if (s == null)
                return null;

            var result = (s.Length == int.MaxValue) ? new byte[int.MaxValue >> 1] : new byte[(s.Length + 1) >> 1];            
            if (s.Length > 0)
            {
                int i;
                if ((s.Length & 0x01) > 0)
                {
                    result[0] = Convert.ToByte(s.Substring(0, 1), 16);
                    i = 1;
                }
                else
                {
                    i = 0;
                }

                for (int j = 0; j < result.Length; ++j, i += 2)
                    result[j] = Convert.ToByte(s.Substring(i, 2), 16);
            }
            
            return result;
        }

        public static string ToHex(byte[] b, int index, int length) => BitConverter.ToString(b, index, length).Replace("-", string.Empty);
    }
}
