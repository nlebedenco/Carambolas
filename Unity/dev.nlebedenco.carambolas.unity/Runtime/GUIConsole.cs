using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using Carambolas.IO;

namespace Carambolas.UnityEngine
{
    [AddComponentMenu("Carambolas/GUIConsole")]
    public sealed class GUIConsole: Console
    {
        private sealed class GUIErrorWriter: TextWriter
        {
            public override void Write(char value)
            {
                throw new NotImplementedException();
            }

            public override void Write(string s)
            {
                throw new NotImplementedException();
            }

            public override void Write(char[] buffer, int index, int count)
            {
                throw new NotImplementedException();
            }

            public override void Write(char[] buffer)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class GUIOutputWriter: TextWriter
        {
            public override void Write(char value)
            {
                throw new NotImplementedException();
            }

            public override void Write(string s)
            {
                throw new NotImplementedException();
            }

            public override void Write(char[] buffer, int index, int count)
            {
                throw new NotImplementedException();
            }

            public override void Write(char[] buffer)
            {
                throw new NotImplementedException();
            }
        }

        protected override void Clear() => throw new NotImplementedException();

        protected override TextWriter GetErrorWriter() => throw new NotImplementedException();
        protected override TextWriter GetOutputWriter() => throw new NotImplementedException();

        protected override bool TryReadLine(out string value) => throw new NotImplementedException();

        protected override void OnSingletonAwaking()
        {
            base.OnSingletonAwaking();
            if (!Application.isEditor && Application.isBatchMode)
            {
                Debug.LogWarning(string.Format("{0}{1} cannot be used in batchmode and is going to be destroyed.",
                        GetType().FullName,
                        string.IsNullOrEmpty(name) ? string.Empty : $" ({name})"));

                Destroy(this);
            }
        }
    }
}
