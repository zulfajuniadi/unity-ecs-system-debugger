using System;
using System.Collections.Generic;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using Unity.Entities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;

namespace EntitySystemDebugger.Editor
{
    public class EntitySystemDebuggerWindow : EditorWindow, ISerializationCallbackReceiver
    {
        [SerializeField] private TreeViewState treeViewState;
        [SerializeField] private string selectedTypeName = "";

        private Type selectedType;
        public SystemTreeView systemTreeView;
        private SystemPropertiesView SystemPropertiesView;
        public GUISkin skin;
        public int selectedWorldIndex;
        public World selectedWorld;
        public List<World> worlds = new List<World> ();
        public List<string> namespaces = new List<string> ();
        public List<Type> managers = new List<Type> ();
        public Dictionary<Type, AverageRecorder> recorders = new Dictionary<Type, AverageRecorder> ();
        public string[] worldNames;

        private void OnEnable ()
        {
            Boot ();
        }

        private void Boot ()
        {
            skin = Resources.Load ("ESDSkin") as GUISkin;
            CreateTree ();
            CreatePropertiesView ();
            if (selectedTypeName != "" && selectedType == null)
            {
                selectedType = Type.GetType (selectedTypeName);
            }
            RefreshCache ();
        }

        private void CreatePropertiesView ()
        {
            SystemPropertiesView = new SystemPropertiesView (this);
        }

        private void CreateTree ()
        {
            if (treeViewState == null)
                treeViewState = new TreeViewState ();

            systemTreeView = new SystemTreeView (treeViewState, this);
        }

#if UNITY_2018_2_OR_NEWER
        [MenuItem ("Window/Debug/Entity System Debugger")]
#else
        [MenuItem ("Window/Entity System Debugger", false, 2017)]
#endif
        private static void ShowWindow ()
        {
            var assembly = Assembly.GetAssembly (typeof (EditorGUI));
            var hierarchyType = assembly.GetType ("UnityEditor.SceneHierarchyWindow");
            var window = GetWindow<EntitySystemDebuggerWindow> ("Entity System Debugger", true, hierarchyType);
            window.minSize = new Vector2 (320, 320);
            window.Show ();
        }

        private void OnGUI ()
        {
            if (systemTreeView == null)
            {
                Boot ();
            }
            if (
                worlds.Count != World.AllWorlds.Count ||
                (selectedWorld != null && World.AllWorlds != null && World.AllWorlds.IndexOf (selectedWorld) == -1)
            )
            {
                RefreshCache ();
            }
            DrawGUI ();
        }

        private void DrawGUI ()
        {
            Rect searchArea = EditorGUILayout.BeginVertical (GUILayout.Height (27));
            GUILayout.Space (0);
            if (systemTreeView.searchString != "" && systemTreeView.searchString != null)
            {
                searchArea.xMax -= 16;
            }

            Rect searchBoxRect = searchArea;
            searchBoxRect.height -= 4;
            searchBoxRect.width -= 4;
            searchBoxRect.xMin += 6;
            searchBoxRect.yMin += 4;

            if (selectedWorld != null)
            {
                searchBoxRect.width /= 2;
                Rect selectedWorldRect = new Rect (searchBoxRect.width + 5, searchArea.y + 5, searchBoxRect.width, searchArea.height);
                if (systemTreeView.searchString != "" && systemTreeView.searchString != null)
                {
                    selectedWorldRect.x += 19f;
                }
                var index = EditorGUI.Popup (selectedWorldRect, selectedWorldIndex, worldNames);
                if (index != selectedWorldIndex)
                {
                    systemTreeView.SetSelection (new int[0]);
                    selectedType = null;
                    selectedTypeName = "";
                    selectedWorld = worlds[index];
                    selectedWorldIndex = index;
                    systemTreeView.Refresh (true);
                    RefreshCache ();
                }
            }

            systemTreeView.searchString = EditorGUI.TextField (searchBoxRect, systemTreeView.searchString);
            if (systemTreeView.searchString == "" || systemTreeView.searchString == null)
            {
                var placeholderRect = searchBoxRect;
                placeholderRect.xMin += 2;
                placeholderRect.yMin += 1;
                EditorGUI.LabelField (placeholderRect, "Search", EditorStyles.boldLabel);
            }
            else if (GUI.Button (new Rect (searchBoxRect.max.x, searchBoxRect.min.y - 1, 19, 19), "X"))
            {
                systemTreeView.searchString = null;
                GUI.FocusControl ("TreeView");
            }
            EditorGUILayout.EndVertical ();

            if (selectedType != null)
            {
                GUI.SetNextControlName ("TreeView");
                var systemTreeViewRect = EditorGUILayout.BeginVertical (GUILayout.ExpandHeight (true));
                systemTreeView.Reload ();
                systemTreeView.OnGUI (systemTreeViewRect);
                EditorGUILayout.EndVertical ();

                var systemPropertiesViewRect = EditorGUILayout.BeginVertical ();
                SystemPropertiesView.OnGUI (systemPropertiesViewRect, selectedType);
                EditorGUILayout.EndVertical ();
            }
            else
            {
                var systemTreeViewRect = EditorGUILayout.BeginVertical (GUILayout.ExpandHeight (true));
                systemTreeView.Reload ();
                systemTreeView.OnGUI (systemTreeViewRect);
                EditorGUILayout.EndVertical ();
            }
        }

        public void RefreshCache ()
        {
            worlds.Clear ();
            worldNames = new string[World.AllWorlds.Count];
            namespaces.Clear ();
            managers.Clear ();
            recorders.Clear ();

            if (World.Active != null)
            {
                var found = false;
                for (int i = 0; i < World.AllWorlds.Count; i++)
                {
                    var world = World.AllWorlds[i];
                    worlds.Add (world);
                    worldNames[i] = world.Name;
                    if (selectedType != null && world.GetExistingManager (selectedType) != null)
                    {
                        found = true;
                        selectedWorld = world;
                        selectedWorldIndex = i;
                    }
                    else if (selectedWorld == null && World.Active == world)
                    {
                        found = true;
                        selectedWorld = world;
                        selectedWorldIndex = i;
                    }
                    else if (world == selectedWorld)
                    {
                        found = true;
                        selectedWorld = world;
                        selectedWorldIndex = i;
                    }
                }
                if (!found)
                {
                    selectedWorld = World.Active;
                }
            }
            else
            {
                selectedWorld = null;
            }

            if (selectedWorld != null && selectedWorld.BehaviourManagers != null)
            {
                foreach (var manager in selectedWorld.BehaviourManagers)
                {
                    Type type = manager.GetType ();
                    if (type.Namespace != null && !namespaces.Contains (type.Namespace))
                    {
                        namespaces.Add (type.Namespace);
                    }
                    managers.Add (type);
                    var recorder = Recorder.Get ($"{selectedWorld.Name} {type.FullName}");
                    recorders.Add (type, new AverageRecorder (recorder));
                }
            }
            else
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies ())
                {
                    foreach (Type type in assembly.GetTypes ())
                    {
                        if (type.IsSubclassOf (typeof (ComponentSystemBase)) && type.GetConstructor (Type.EmptyTypes) != null)
                        {
                            if (type.Namespace != null && !namespaces.Contains (type.Namespace))
                            {
                                namespaces.Add (type.Namespace);
                            }
                            managers.Add (type);
                        }
                    }
                }
            }
            systemTreeView.Reload ();
        }

        public void SetSelected (Type type)
        {
            if (type == null)
            {
                systemTreeView.SetSelection (new int[0]);
                selectedTypeName = "";
            }

            selectedType = type;
            selectedTypeName = type != null ? type.AssemblyQualifiedName : "";
        }

        int lastTimedFrame;
        public void Update ()
        {
            if (Time.frameCount == lastTimedFrame)
                return;

            foreach (var recorder in recorders.Values)
            {
                recorder.Update ();
            }

            lastTimedFrame = Time.frameCount;
            if (selectedType != null)
                Repaint ();
        }

        public void OnBeforeSerialize () { }

        public void OnAfterDeserialize () { }
    }
}