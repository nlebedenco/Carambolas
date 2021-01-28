using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using Carambolas.IO;

namespace Carambolas.UnityEngine
{
    internal enum GUIConsoleState
    {
        Closed = 0,
        Closing,
        Open,
        Opening        
    }

    [AddComponentMenu("Carambolas/GUIConsole")]
    public sealed class GUIConsole: Console
    {
        public string Prompt;
        public Font Font;
        public Rect Area;

        /// <summary>
        /// Number of console entries buffered. A console entry may be comprised of up to 16382 
        /// characters which is the maximum number of characters unity can display in a single
        /// TextArea. Strings larger than this are truncated.
        /// </summary>
        public int Scrollback;

        public bool CanDrag;

        private Vector2 scrollViewPosition;

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

        protected override TextWriter GetErrorWriter() => default;
        protected override TextWriter GetOutputWriter() => default;

        protected override bool TryReadLine(out string value)
        {
            value = default;
            return false;
        }

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

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            if (Font is null)
                Font = Font.CreateDynamicFontFromOSFont("Courier New", 16);            
        }

        private void OnGUI()
        {
            Area = GUILayout.Window(0, Area, OnWindow, "Console");            
        }

        private string inputText;
        private bool pendingFocus;

        public void SetFocus() => pendingFocus = true;

        private void OnWindow(int id)
        {
            GUILayout.BeginVertical();
            {
                scrollViewPosition = GUILayout.BeginScrollView(scrollViewPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                {
                    GUILayout.FlexibleSpace();
                    // TODO: Draw buffer entries
                }
                GUILayout.EndScrollView();

                // TODO: move cursor
                // TODO: check keyboard events (escape, history up/down, tab completion)

                GUILayout.BeginHorizontal();
                {
                    if (!string.IsNullOrEmpty(Prompt))
                        GUILayout.Label(Prompt);

                    GUI.SetNextControlName(nameof(GUIConsole));
                    inputText = GUILayout.TextField(inputText, GUILayout.ExpandHeight(true));

                    if (pendingFocus)
                    {
                        GUI.FocusControl(nameof(GUIConsole));
                        pendingFocus = false;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            if (CanDrag)
            {
                // GUI.skin.window.border.top is the height of the window title.
                GUI.DragWindow(new Rect(0, 0, Area.width, GUI.skin.window.border.top));
            }
        }
    }
}
