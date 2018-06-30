using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using Unity.Entities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace EntitySystemDebugger.Editor
{
    public class SystemTreeView : TreeView
    {

        private EntitySystemDebuggerWindow window;
        private Dictionary<int, Type> typeIds = new Dictionary<int, Type> ();
        private List<Type> disabledSystems = new List<Type> ();
        private IList<int> prevSelected;
        private Dictionary<string, TreeViewItem> roots = new Dictionary<string, TreeViewItem> ();
        private Dictionary<int, string> namespaceIds = new Dictionary<int, string> ();
        private Dictionary<int, string> behavioursIds = new Dictionary<int, string> ();
        private bool pendingSelectionUpdate = false;

        public SystemTreeView (TreeViewState treeViewState, EntitySystemDebuggerWindow window) : base (treeViewState)
        {
            this.window = window;
            disabledSystems.Clear ();
            var disabled = PlayerPrefs.GetString ("disabledsystems", "");
            var names = disabled.Split (':');
            foreach (var name in names)
            {
                disabledSystems.Add (Type.GetType (name));
            }
            Reload ();
        }

        protected override bool CanMultiSelect (TreeViewItem item)
        {
            return false;
        }

        protected override TreeViewItem BuildRoot ()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            roots.Clear ();
            typeIds.Clear ();
            namespaceIds.Clear ();
            behavioursIds.Clear ();

            var treeItems = new List<TreeViewItem> { };

            int nextId = 1;
            for (int i = 0; i < window.namespaces.Count; i++)
            {
                var name = window.namespaces[i];
                if (roots.ContainsKey (name)) continue;
                var treeViewItem = new TreeViewItem { id = nextId, depth = 0, displayName = name };
                namespaceIds[nextId] = treeViewItem.displayName;
                root.AddChild (treeViewItem);
                roots.Add (name, treeViewItem);
                treeItems.Add (treeViewItem);
                nextId++;
            }

            for (int i = 0; i < window.managers.Count; i++)
            {
                var type = window.managers[i];
                var name = type.Name;
                var treeViewItem = new TreeViewItem { id = nextId, depth = 1, displayName = StringUtils.Humanize (name) };
                typeIds[nextId] = type;
                behavioursIds[nextId] = treeViewItem.displayName;
                if (type.Namespace != null && type.Namespace != "")
                {
                    roots[type.Namespace].AddChild (treeViewItem);
                }
                else
                {
                    treeViewItem.depth = 0;
                    root.AddChild (treeViewItem);
                }
                nextId++;
            }

            SetupParentsAndChildrenFromDepths (root, treeItems);
            UpdateExpanded ();
            pendingSelectionUpdate = true;

            return root;
        }

        private void UpdateExpanded ()
        {
            List<int> expanded = new List<int> ();
            var savedString = PlayerPrefs.GetString ("expandednamespaces", "");
            var expandedNamespaces = savedString.Split (':');
            foreach (var exapanded in expandedNamespaces)
            {
                foreach (var item in namespaceIds)
                {
                    if (item.Value == exapanded)
                    {
                        expanded.Add (item.Key);
                    }
                }
            }
            SetExpanded (expanded);
        }

        protected override void RowGUI (RowGUIArgs args)
        {
            bool disabled = false;
            var label = "";
            var labelColor = EditorGUIUtility.isProSkin ? window.skin.customStyles[4] : window.skin.customStyles[5];
            if (window.selectedWorld != null && behavioursIds.ContainsValue (args.item.displayName))
            {
                var type = typeIds[args.item.id];
                var manager = window.selectedWorld.GetExistingManager (type) as ComponentSystemBase;
                if (manager != null)
                {
                    var counts = EntityCount (manager.ComponentGroups);
                    if (!manager.Enabled ||
                        manager.ComponentGroups == null ||
                        manager.ComponentGroups.Length == 0 ||
                        counts == 0
                    )
                    {
                        disabled = true;
                    }
                    if (!manager.Enabled)
                    {
                        args.label += " (Disabled)";
                    }
                    if (counts > 0)
                    {
                        label += " [" + counts.ToString ("N0") + "]";
                    }
                    if (window.recorders.ContainsKey (type))
                    {
                        label += " [" + StringUtils.FormatDt ("", window.recorders[type].max) + "]";
                    }
                }
                else
                {
                    disabled = true;
                }
            }
            if (string.IsNullOrEmpty (searchString))
            {
                args.rowRect.x += 4f;
            }
            else
            {
                args.rowRect.x += 8f;
            }
            args.rowRect.width -= 18f;
            if (behavioursIds.ContainsValue (args.item.displayName))
            {
                var type = typeIds[args.item.id];
                var toggleRect = args.rowRect;
                if (string.IsNullOrEmpty (searchString))
                {
                    toggleRect.x += 12;
                }
                else
                {
                    toggleRect.x -= 2;
                }
                toggleRect.size = new Vector2 (18f, args.rowRect.height);
                var enabled = !disabledSystems.Contains (type);
                var isEnabled = GUI.Toggle (toggleRect, enabled, "Enabled");
                if (isEnabled != enabled)
                {
                    if (!isEnabled)
                    {
                        disabledSystems.Add (type);
                        UpdateDisabledPref ();
                        if (World.AllWorlds == null) return;
                        foreach (var world in World.AllWorlds)
                        {
                            var manager = world.GetExistingManager (type) as ComponentSystemBase;
                            if (manager != null)
                            {
                                manager.Enabled = false;
                            }
                        }
                    }
                    else
                    {
                        disabledSystems.Remove (type);
                        UpdateDisabledPref ();
                        if (World.AllWorlds == null) return;
                        foreach (var world in World.AllWorlds)
                        {
                            var manager = world.GetExistingManager (type) as ComponentSystemBase;
                            if (manager != null)
                            {
                                manager.Enabled = true;
                            }
                        }
                    }
                }
            }
            if (disabled)
            {
                GUI.enabled = false;
            }
            base.RowGUI (args);
            if (label != "")
            {
                var rect = args.rowRect;
                rect.x = args.rowRect.width - 85;
                rect.width = 100;
                GUI.Label (rect, label, labelColor);
            }
            if (disabled)
            {
                GUI.enabled = true;
            }
        }

        private void UpdateDisabledPref ()
        {
            var names = new List<string> ();
            foreach (var type in disabledSystems)
            {
                if (type == null) continue;
                names.Add (type.AssemblyQualifiedName);
            }
            var str = String.Join (":", names);
            PlayerPrefs.SetString ("disabledsystems", str);
        }

        private int EntityCount (ComponentGroup[] componentGroups)
        {
            if (componentGroups == null) return 0;
            var count = 0;
            foreach (var group in componentGroups)
            {
                count += group.GetEntityArray ().Length;
            }
            return count;
        }

        protected override void AfterRowsGUI ()
        {
            base.AfterRowsGUI ();
            if (pendingSelectionUpdate)
            {
                UpdateSelection ();
                pendingSelectionUpdate = false;
            }
        }

        private void UpdateSelection ()
        {
            var savedSelection = PlayerPrefs.GetString ("selectedsystem", "");
            if (savedSelection != "")
            {
                foreach (var item in typeIds)
                {
                    if (item.Value.AssemblyQualifiedName == savedSelection)
                    {
                        SetSelection (new int[1] { item.Key });
                    }
                }
            }
        }

        public void Refresh (bool unsetSelection = false)
        {
            if (unsetSelection)
            {
                SelectionChanged (null);
            }
            BuildRoot ();
            Reload ();
        }

        protected override void ExpandedStateChanged ()
        {
            base.ExpandedStateChanged ();
            var expanded = new List<string> ();
            foreach (var item in GetExpanded ())
            {
                expanded.Add (namespaceIds[item]);
            }
            PlayerPrefs.SetString ("expandednamespaces", String.Join (":", expanded));
        }

        protected override void SelectionChanged (IList<int> selectedIds)
        {
            if (selectedIds?.Count > 0)
            {
                if (typeIds.ContainsKey (selectedIds[0]))
                {
                    var type = typeIds[selectedIds[0]];
                    window.SetSelected (type);
                    prevSelected = selectedIds;
                    PlayerPrefs.SetString ("selectedsystem", type.AssemblyQualifiedName);
                }
                else if (prevSelected != null)
                {
                    PlayerPrefs.SetString ("selectedsystem", "");
                    this.SetSelection (prevSelected);
                }
                else
                {
                    PlayerPrefs.SetString ("selectedsystem", "");
                    this.SetSelection (new int[0]);
                }
            }
        }

        public void SelectSystem (Type selectedType)
        {
            foreach (var entry in typeIds)
            {
                if (entry.Value == selectedType)
                {
                    SelectionChanged (new int[] { entry.Key });
                    return;
                }
            }
        }
    }
}