using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Actions
{
    public static class Button
    {
        public static void Draw (Type type, MethodInfo method, object instance)
        {
            string humanName = StringUtils.Humanize (method.Name);
            if (GUILayout.Button (humanName))
            {
                method.Invoke (instance, null);
            }
        }
    }
}