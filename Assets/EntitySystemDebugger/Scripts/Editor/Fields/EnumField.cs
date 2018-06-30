using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class EnumField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = (Enum) field.GetValue (instance);
            var newVal = EditorGUILayout.EnumPopup (humanName, value);
            if (newVal != value)
            {
                field.SetValue (instance, newVal);
            }
        }
    }
}