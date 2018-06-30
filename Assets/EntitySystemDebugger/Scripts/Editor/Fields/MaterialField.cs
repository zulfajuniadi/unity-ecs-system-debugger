using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class MaterialField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = field.GetValue (instance) as Material;
            var newVal = EditorGUILayout.ObjectField (humanName, value, typeof (Material), true);
            if (newVal != value)
            {
                field.SetValue (instance, newVal);
            }
        }
    }
}