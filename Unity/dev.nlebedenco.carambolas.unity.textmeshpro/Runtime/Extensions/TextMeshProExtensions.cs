using System;
using System.Diagnostics;
using System.Threading;

using UnityEngine;
using TMPro;
using Carambolas.Text;
using System.Globalization;

namespace Carambolas.UnityEngine
{
    public static class TextMeshProExtensions
    {
        public static void SetCharArray(this TMP_Text self, ArraySegment<char> arraySegment) => self.SetCharArray(arraySegment.Array, arraySegment.Offset, arraySegment.Count);

        public static void SetText(this TMP_Text self, StringBuilder sb) => self.SetCharArray(sb.AsArraySegment());
        public static void SetText(this TMP_Text self, StringBuilder.Buffer sb) => self.SetCharArray(sb.AsArraySegment());

        public static void SetTextFormat<T0>(this TMP_Text text, string format, T0 arg0)
        {
            using (var sb = new StringBuilder.Buffer())
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg0);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1>(this TMP_Text text, string format, T0 arg0, T1 arg1)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg0, arg1);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2>(this TMP_Text text, string format, T0 arg0, T1 arg1, T2 arg2)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg0, arg1, arg2);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2, T3>(this TMP_Text text, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg0, arg1, arg2, arg3);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2, T3, T4>(this TMP_Text text, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(CultureInfo.CurrentCulture, format, arg0, arg1, arg2, arg3);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0>(this TMP_Text text, IFormatProvider formatProvider, string format, T0 arg0)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(formatProvider, format, arg0);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1>(this TMP_Text text, IFormatProvider formatProvider, string format, T0 arg0, T1 arg1)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(formatProvider, format, arg0, arg1);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2>(this TMP_Text text, IFormatProvider formatProvider, string format, T0 arg0, T1 arg1, T2 arg2)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(formatProvider, format, arg0, arg1, arg2);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2, T3>(this TMP_Text text, IFormatProvider formatProvider, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(formatProvider, format, arg0, arg1, arg2, arg3);
                text.SetCharArray(sb.AsArraySegment());
            }
        }

        public static void SetTextFormat<T0, T1, T2, T3, T4>(this TMP_Text text, IFormatProvider formatProvider, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            using (var sb = new StringBuilder.Buffer())
            {

                sb.AppendFormat(formatProvider, format, arg0, arg1, arg2, arg3);
                text.SetCharArray(sb.AsArraySegment());
            }
        }
    }
}
