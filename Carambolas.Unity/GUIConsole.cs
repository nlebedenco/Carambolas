using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using Carambolas.IO;

namespace Carambolas.UnityEngine
{
    public class GUIConsole: Console
    {
        protected override void Clear() => throw new NotImplementedException();

        protected override TextWriter GetErrorWriter() => throw new NotImplementedException();
        protected override TextWriter GetOutputWriter() => throw new NotImplementedException();

        protected override bool TryReadLine(out string value) => throw new NotImplementedException();
    }
}
