using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class MeshField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = field.GetValue (instance) as Mesh;
            var newVal = EditorGUILayout.ObjectField (humanName, value, typeof (Mesh), true);
            if (newVal != value)
            {
                field.SetValue (instance, newVal);
            }
        }
    }
}