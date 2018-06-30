using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using EntitySystemDebugger.Editor.Actions;
using EntitySystemDebugger.Editor.Fields;
using EntitySystemDebugger.Editor.Utils;
using Unity.Entities;
using Unity.Entities.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Unity.Entities.ComponentType;

namespace EntitySystemDebugger.Editor
{
    public class SystemPropertiesView
    {

        private List<FieldInfo> cachedFields = new List<FieldInfo> ();
        private List<MethodInfo> cachedMethods = new List<MethodInfo> ();
        private List<ComponentGroup> activeComponentGroups = new List<ComponentGroup> ();
        private List<ComponentGroup> inactiveComponentGroups = new List<ComponentGroup> ();
        private List<Type> updateBefore = new List<Type> ();
        private List<Type> updateAfter = new List<Type> ();
        private List<Type> updateGroup = new List<Type> ();
        private ComponentSystemBase activeInstance = null;
        private ComponentSystemBase instance = null;
        private EntitySystemDebuggerWindow window;
        private AnimationCurve curve = AnimationCurve.Constant (1, 10, 0);
        private Type currentType;
        private bool awaitingProfiler;
        private bool showInactiveGroups;
        private World lastActiveWorld;

        public SystemPropertiesView (EntitySystemDebuggerWindow window)
        {
            this.window = window;
        }

        public void OnGUI (Rect rect, Type type)
        {
            if (type == null) return;

            if (currentType != type || World.Active != lastActiveWorld)
            {
                GetFields (type);
                GetMethods (type);
                GetUpdateOrder (type);
                lastActiveWorld = World.Active;
                currentType = type;
            }
            GetInstances (type);
            GetComponentGroups (type);

            Rect chartRect = EditorGUILayout.BeginVertical (GUILayout.Height (64));

            GUILayout.Space (0);

            DrawChart (chartRect, type);

            EditorGUILayout.EndVertical ();

            DrawUpdateOrder ();

            var dataRect = EditorGUILayout.BeginVertical ();

            DrawData (type, dataRect);

            EditorGUILayout.EndVertical ();

            OpenProfilerIfAwaiting (type);
        }

        private void DrawData (Type type, Rect dataRect)
        {
            var effectiveInstance = activeInstance ?? instance;
            DrawFields (type, effectiveInstance);
            DrawComponentGroups (dataRect);
            DrawMethods (type, effectiveInstance);
            if (GUI.tooltip != "")
            {
                var tooltipRect = new Rect (dataRect.x, dataRect.y, dataRect.width, 18);
                if (inactiveComponentGroups.Count == 0)
                {
                    tooltipRect.y -= 18;
                }
                GUI.Label (tooltipRect, GUI.tooltip, window.skin.customStyles[3]);
            }
        }

        private void DrawComponentGroups (Rect dataRect)
        {
            if (window.selectedWorld == null) return;
            if (inactiveComponentGroups.Count > 0)
            {
                showInactiveGroups = GUILayout.Toggle (showInactiveGroups, " Show Inactive Component Groups (" + inactiveComponentGroups.Count + ")", GUILayout.ExpandWidth (true));
            }
            foreach (var group in activeComponentGroups)
            {
                DrawComponentGroup (group);
            }
            if (showInactiveGroups)
            {
                foreach (var group in inactiveComponentGroups)
                {
                    DrawComponentGroup (group);
                }
            }
        }

        private void DrawComponentGroup (ComponentGroup group)
        {
            List<ComponentType> components = new List<ComponentType> ();
            foreach (var type in group.Types.Skip (1))
            {
                var managedType = type.GetManagedType ();
                if (type.AccessModeType == AccessMode.Subtractive)
                {
                    components.Add (type);
                }
                else
                {
                    components.Insert (0, type);
                }
            }
            var componentRect = EditorGUILayout.BeginHorizontal ();
            EditorGUILayout.HelpBox ("", MessageType.None);
            float offX = 0;
            foreach (var component in components)
            {
                var managedType = component.GetManagedType ();
                switch (component.AccessModeType)
                {
                    case AccessMode.ReadOnly:
                        GUI.Label (new Rect (offX + componentRect.x, componentRect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (managedType.Name), "[ReadOnly] " + managedType.FullName), window.skin.customStyles[0]);
                        break;
                    case AccessMode.ReadWrite:
                        GUI.Label (new Rect (offX + componentRect.x, componentRect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (managedType.Name), "[ReadWrite] " + managedType.FullName), window.skin.customStyles[1]);
                        break;
                    case AccessMode.Subtractive:
                        GUI.Label (new Rect (offX + componentRect.x, componentRect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (managedType.Name), "[Subtractive] " + managedType.FullName), window.skin.customStyles[2]);
                        break;
                }
                offX += 35f;
            }
            var buttonRect = new Rect (componentRect.x + componentRect.width - 80, componentRect.y, 80, componentRect.height);
            var entityArray = group.GetEntityArray ();
            var entityCount = entityArray.Length;
            if (entityCount > 0 && GUI.Button (buttonRect, entityCount.ToString ("N0")))
            {
                var selectionProxy = ScriptableObject.CreateInstance<EntitySelectionProxy> ();
                selectionProxy.hideFlags = HideFlags.HideAndDontSave;
                selectionProxy.SetEntity (window.selectedWorld, entityArray[UnityEngine.Random.Range (0, entityArray.Length)]);
                Selection.activeObject = selectionProxy;
            }
            EditorGUILayout.EndHorizontal ();
        }

        private void GetComponentGroups (Type type)
        {
            activeComponentGroups.Clear ();
            inactiveComponentGroups.Clear ();
            var effectiveInstance = activeInstance ?? instance;
            if (effectiveInstance != null && effectiveInstance.ComponentGroups != null)
            {
                foreach (var component in effectiveInstance.ComponentGroups)
                {
                    if (component.GetEntityArray ().Length > 0)
                    {
                        activeComponentGroups.Insert (0, component);
                    }
                    else
                    {
                        inactiveComponentGroups.Add (component);
                    }
                }
            }
        }

        private void GetUpdateOrder (Type type)
        {
            var updateInGroupType = typeof (UpdateInGroupAttribute);
            var updateAfterType = typeof (UpdateAfterAttribute);
            var updateBeforeType = typeof (UpdateBeforeAttribute);
            updateBefore.Clear ();
            updateAfter.Clear ();
            updateGroup.Clear ();
            foreach (var attribute in type.GetCustomAttributes (true))
            {
                var attributeType = attribute.GetType ();
                if (attributeType == updateBeforeType)
                {
                    updateBefore.Add ((attribute as UpdateBeforeAttribute).SystemType);
                }
                else if (attributeType == updateAfterType)
                {
                    updateAfter.Add ((attribute as UpdateAfterAttribute).SystemType);
                }
                else if (attributeType == updateInGroupType)
                {
                    updateGroup.Add ((attribute as UpdateInGroupAttribute).GroupType);
                }
            }
        }

        private void DrawMethods (Type type, ComponentSystemBase effectiveInstance)
        {
            foreach (var method in cachedMethods)
            {
                if (method.GetParameters ().Length == 0 && method.GetGenericArguments ().Length == 0)
                {
                    Button.Draw (type, method, effectiveInstance);
                }
            }
        }

        private void DrawFields (Type type, ComponentSystemBase effectiveInstance)
        {
            foreach (var field in cachedFields)
            {

                if (field.FieldType == typeof (System.Int32))
                {
                    IntField.Draw (type, field, effectiveInstance);
                }
                else if (field.FieldType == typeof (System.Single))
                {
                    FloatField.Draw (type, field, effectiveInstance);
                }
                else if (field.FieldType == typeof (System.String))
                {
                    StringField.Draw (type, field, effectiveInstance);
                }
                else if (field.FieldType == typeof (System.Boolean))
                {
                    BoolField.Draw (type, field, effectiveInstance);
                }
                else if (field.FieldType.IsEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType (field.FieldType);
                    if (underlyingType == typeof (short))
                    {
                        MaskField.Draw (type, field, effectiveInstance);
                    }
                    else
                    {
                        EnumField.Draw (type, field, effectiveInstance);
                    }
                }
                else
                {
                    // monobehaviours
                    switch (field.FieldType.ToString ())
                    {
                        case "UnityEngine.Mesh":
                            MeshField.Draw (type, field, effectiveInstance);
                            break;
                        case "UnityEngine.Material":
                            MaterialField.Draw (type, field, effectiveInstance);
                            break;
                    }
                }
            }
        }

        private void DrawChart (Rect rect, Type type)
        {
            float current = 0;
            float max = 0;
            float average = 0;
            int entityCount = GetEntityCount ();

            curve.keys = new Keyframe[0];
            if (window.recorders.ContainsKey (type))
            {
                var recorder = window.recorders[type];
                var buffer = recorder.buffer;
                for (int i = 0; i < 100; i++)
                {
                    curve.AddKey (i, buffer[i]);
                    if (i == 99)
                    {
                        current = buffer[i];
                    }
                }
                max = recorder.max;
                average = recorder.average;
            }
            var size = rect.size;
            size.x -= 8;
            rect.size = size;
            var center = rect.center;
            center.x += 5;
            rect.center = center;
            GUI.enabled = false;
            EditorGUI.CurveField (rect, curve, Color.green, new Rect (1, 0, 98, max * 1.1f));
            GUI.enabled = true;

            DrawButtons (rect, type);

            var managerNameRect = rect;
            managerNameRect.size = new Vector3 (rect.width - 100, 20);
            managerNameRect.y += 4;
            managerNameRect.x += 4;
            GUI.Label (managerNameRect, new GUIContent (StringUtils.Humanize (type.Name, 30) + " (" + entityCount.ToString ("N0") + ")", "Active Entities"), EditorStyles.miniBoldLabel);

            var maxDtRect = rect;
            maxDtRect.size = new Vector3 (rect.width - 100, 20);
            maxDtRect.y += 18;
            maxDtRect.x += 4;
            GUI.Label (maxDtRect, new GUIContent (StringUtils.FormatDt ("MAX:", max), "Maximum Delta Time"), EditorStyles.miniBoldLabel);

            var currentDtRect = rect;
            currentDtRect.size = new Vector3 (rect.width - 100, 20);
            currentDtRect.y += 32;
            currentDtRect.x += 4;
            GUI.Label (currentDtRect, new GUIContent (StringUtils.FormatDt ("CUR:", current), "Current Delta Time"), EditorStyles.miniBoldLabel);

            var averageDtRect = rect;
            averageDtRect.size = new Vector3 (rect.width - 100, 20);
            averageDtRect.y += 46;
            averageDtRect.x += 4;
            GUI.Label (averageDtRect, new GUIContent (StringUtils.FormatDt ("AVG:", average), "Average Delta Time"), EditorStyles.miniBoldLabel);
        }

        private void DrawUpdateOrder ()
        {
            GUILayout.Space (4);
            if (updateAfter.Count == 0 && updateGroup.Count == 0 && updateBefore.Count == 0) return;
            var rect = EditorGUILayout.BeginVertical (GUILayout.Height (18));
            rect.y -= 22;
            rect.height = 18;
            float offX = rect.width - 40f;
            foreach (var before in updateBefore)
            {
                if (GUI.Button (new Rect (offX, rect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (before.Name), "[UpdateBefore] " + before.FullName), window.skin.customStyles[2]))
                {
                    window.systemTreeView.SelectSystem (before);
                }
                offX -= 35f;
            }
            foreach (var group in updateGroup)
            {
                if (GUI.Button (new Rect (offX, rect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (group.Name), "[UpdateGroup] " + group.FullName), window.skin.customStyles[1]))
                {
                    window.systemTreeView.SelectSystem (group);
                }
                offX -= 35f;
            }
            foreach (var after in updateAfter)
            {
                if (GUI.Button (new Rect (offX, rect.y, 35f, 18), new GUIContent (StringUtils.GetAbbreviation (after.Name), "[UpdateAfter] " + after.FullName), window.skin.customStyles[0]))
                {
                    window.systemTreeView.SelectSystem (after);
                }
                offX -= 35f;
            }
            EditorGUILayout.EndHorizontal ();
        }

        private void DrawButtons (Rect rect, Type type)
        {
            var closeButtonRect = rect;
            closeButtonRect.x = closeButtonRect.width - 17;
            closeButtonRect.y += 4;
            closeButtonRect.size = new Vector2 (18, 18);
            if (GUI.Button (closeButtonRect, new GUIContent ("X", "Close System"), EditorStyles.miniButton))
            {
                window.SetSelected (null);
            }

            var profilerButtonRect = rect;
            profilerButtonRect.x = profilerButtonRect.width - 36;
            profilerButtonRect.y += 4;
            profilerButtonRect.size = new Vector2 (18, 18);
            if (GUI.Button (profilerButtonRect, new GUIContent ("P", "Open Profiler"), EditorStyles.miniButton))
            {
                awaitingProfiler = true;
            }

            var editButton = rect;
            editButton.x = editButton.width - 55;
            editButton.y += 4;
            editButton.size = new Vector2 (18, 18);
            if (GUI.Button (editButton, new GUIContent ("E", "Edit File"), EditorStyles.miniButton))
            {
                var search = AssetDatabase.FindAssets ("t:script " + type.Name);
                if (search.Length > 0)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object> (AssetDatabase.GUIDToAssetPath (search[0]));
                    if (obj != null)
                    {
                        AssetDatabase.OpenAsset (obj, -1);
                    }
                    else
                    {
                        Debug.LogWarning ("Can't load asset");
                    }
                }
            }

            if (window.selectedWorld != null)
            {
                var ecsDebuggerButtonRect = rect;
                ecsDebuggerButtonRect.x = ecsDebuggerButtonRect.width - 74;
                ecsDebuggerButtonRect.y += 4;
                ecsDebuggerButtonRect.size = new Vector2 (18, 18);
                if (GUI.Button (ecsDebuggerButtonRect, new GUIContent ("D", "Open Entity Debugger"), EditorStyles.miniButton))
                {
                    var entityDebugger = EditorWindow.GetWindow<EntityDebugger> (false, "Entity Debugger", true);
                    entityDebugger.Show ();
                    entityDebugger.SetSystemSelection (window.selectedWorld.GetExistingManager (type.GetType ()), true, true);
                }
            }
        }

        private void OpenProfilerIfAwaiting (Type type)
        {
            if (awaitingProfiler)
            {
                var assembly = typeof (EditorWindow).Assembly;
                var profilerType = assembly.GetType ("UnityEditor.ProfilerWindow");
                var profilerWindow = EditorWindow.GetWindow (profilerType, false, "Profiler", true);
                profilerWindow.Show ();

                var viewField = profilerType.GetField ("m_ViewType", BindingFlags.Instance | BindingFlags.NonPublic);
                var hierarchyArea = Enum.GetValues (viewField.FieldType).GetValue (0);
                viewField.SetValue (profilerWindow, hierarchyArea);

                var areaField = profilerType.GetField ("m_CurrentArea", BindingFlags.Instance | BindingFlags.NonPublic);
                var cpuArea = Enum.GetValues (areaField.FieldType).GetValue (0);
                areaField.SetValue (profilerWindow, cpuArea);

                var profilerHierarchyWindow = assembly.GetType ("UnityEditorpublic.Profiling.ProfilerFrameDataHierarchyView");
                var hierarchyViewInstance = profilerType.GetField ("m_CPUFrameDataHierarchyView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (profilerWindow);
                if (window.selectedWorld != null)
                {
                    var profilerTreeView = hierarchyViewInstance.GetType ().GetField ("m_TreeView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (hierarchyViewInstance) as TreeView;
                    if (profilerTreeView != null)
                    {
                        profilerTreeView.searchString = $"{window.selectedWorld.Name} {type.FullName}";
                        awaitingProfiler = false;
                    }
                }
                else
                {
                    awaitingProfiler = false;
                }
            }
        }

        private int GetEntityCount ()
        {
            if (activeInstance == null) return 0;
            var entityCount = 0;
            foreach (var entry in activeComponentGroups)
            {
                entityCount += entry.GetEntityArray ().Length;
            }
            return entityCount;
        }

        private void GetInstances (Type type)
        {
            if (window.selectedWorld != null)
            {
                activeInstance = window.selectedWorld.GetExistingManager (type) as ComponentSystemBase;
            }
            else
            {
                activeInstance = null;
            }
            if (instance == null || instance.GetType () != type)
            {
                instance = Activator.CreateInstance (type) as ComponentSystemBase;
            }
        }

        private void GetFields (Type type)
        {
            cachedFields.Clear ();
            var fields = type.GetFields (BindingFlags.Instance | BindingFlags.Public);
            cachedFields.AddRange (fields);
        }

        private void GetMethods (Type type)
        {
            cachedMethods.Clear ();
            if (type.Name == "EntityManager") return;
            var methods = type.GetMethods (BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var name = method.Name;
                if (name.StartsWith ("set_")) continue;
                if (name.StartsWith ("get_")) continue;
                cachedMethods.Add (method);
            }
        }
    }
}