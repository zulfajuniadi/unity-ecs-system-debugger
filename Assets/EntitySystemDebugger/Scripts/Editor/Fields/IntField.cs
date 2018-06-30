using System;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.Fields
{
    public static class IntField
    {
        public static void Draw (Type type, FieldInfo field, object instance)
        {
            string humanName = StringUtils.Humanize (field.Name);
            var value = Convert.ToInt32 (field.GetValue (instance));
            var intVal = EditorGUILayout.IntField (humanName, value);
            if (intVal != value)
            {
                field.SetValue (instance, intVal);
            }
        }
    }
}