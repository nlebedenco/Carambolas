using System;

using UnityEngine;

namespace Carambolas.UnityEngine
{
    public static class EventExtensions
    {
        internal static bool MainActionKeyForControl(this Event self, int controlId)
        {
            if (GUIUtility.keyboardControl != controlId)
                return false;

            var anyModifierOn = self.alt || self.shift || self.command || self.control;
            if (self.type != EventType.KeyDown || self.character != 32 || anyModifierOn)
                return self.type == EventType.KeyDown && (self.keyCode == KeyCode.Space || self.keyCode == KeyCode.Return || self.keyCode == KeyCode.KeypadEnter) && !anyModifierOn;

            self.Use();
            return false;
        }
    }
}
