using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class MaskField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = Convert.ToInt32 (field.GetValue (instance));
            var values = Enum.GetNames (field.FieldType);
            var newVal = EditorGUILayout.MaskField (humanName, value, values);
            if (newVal != value)
            {
                field.SetValue (instance, newVal);
            }
        }
    }
}