using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class BoolField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = Convert.ToBoolean (field.GetValue (instance));
            var newVal = EditorGUILayout.Toggle (humanName, value);
            if (newVal != value)
            {
                field.SetValue (instance, newVal);
            }
        }
    }
}