using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public class GraphicCommandLineInterface: CommandLineInterface
    {
        protected override void Clear() => throw new NotImplementedException();

        protected override Writer StartWriter() => throw new NotImplementedException();

        protected override bool TryRead(out string value) => throw new NotImplementedException();
    }
}
