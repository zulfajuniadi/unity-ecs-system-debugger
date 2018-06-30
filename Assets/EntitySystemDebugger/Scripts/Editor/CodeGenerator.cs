using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor
{

    [System.Serializable]
    public enum GenerateType
    {
        Bootstrap,
        ComponentData,
        SharedComponentData,
        ComponentSystem,
        JobComponentSystem
    }

    [System.Serializable]
    public class CodeGenerator : EditorWindow
    {

        private string createName = "";

        private static GenerateType generateType
        {
            get
            {
                return (GenerateType) PlayerPrefs.GetInt ("codegeneratortype", 0);
            }
            set
            {
                PlayerPrefs.SetInt ("codegeneratortype", (int) value);
            }
        }

        private static bool pendingCreate = false;

        [MenuItem ("Assets/Create/ECS/Bootstrap")]

        private static void CreateBootstrap ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                generateType = GenerateType.Bootstrap;
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        [MenuItem ("Assets/Create/ECS/Component")]
        private static void CreateComponent ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                generateType = GenerateType.ComponentData;
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        [MenuItem ("Assets/Create/ECS/SharedComponent")]
        private static void CreateSharedComponent ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                generateType = GenerateType.SharedComponentData;
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        [MenuItem ("Assets/Create/ECS/ComponentSystem")]
        private static void CreateComponentSystem ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                generateType = GenerateType.ComponentSystem;
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        [MenuItem ("Assets/Create/ECS/JobComponentSystem")]
        private static void CreateJobComponentSystem ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                generateType = GenerateType.JobComponentSystem;
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        [MenuItem ("Assets/Create/ECS/Change Output Directory")]
        private static void ChangeOutputDirectory ()
        {
            var dir = GetBaseDirectory (true);
        }

        private static string GetBaseDirectory (bool skipCheck = false)
        {
            var dir = "";
            var exisitng = PlayerPrefs.GetString ("codegeneratorbasedir", "");
            if (!skipCheck)
            {
                dir = exisitng;
            }
            if (dir == "")
            {
                dir = EditorUtility.SaveFolderPanel ("Choose Output Directory", exisitng, "");
                if (dir != "")
                {
                    PlayerPrefs.SetString ("codegeneratorbasedir", dir);
                    if (pendingCreate)
                        ShowWindow ();
                    pendingCreate = false;
                    return dir;
                }
            }
            else
            {
                return dir;
            }
            return null;
        }

        private static void ShowWindow ()
        {
            GetWindow<CodeGenerator> ("Code Generator", true, typeof (EntitySystemDebuggerWindow)).Show ();
        }

        void OnGUI ()
        {

            var tab = (int) generateType;

            var type = generateType.ToString ();

            GUILayout.Space (8);

            EditorGUILayout.LabelField ("ECS Code Generator", EditorStyles.boldLabel);

            EditorGUILayout.LabelField ("Base Output Directory: ");
            var dir = GetBaseDirectory ();
            EditorGUILayout.LabelField ("..." + dir.Substring (dir.Length - 39, 39));
            if (GUILayout.Button ("Change Base Directory"))
            {
                pendingCreate = true;
                GetBaseDirectory (true);
            }

            GUILayout.Space (8);

            var types = Enum.GetNames (typeof (GenerateType));

            var selectedTab = GUILayout.SelectionGrid (tab, types, 1);
            if (selectedTab != tab)
            {
                tab = selectedTab;
                generateType = (GenerateType) selectedTab;
            }

            GUILayout.Space (8);

            EditorGUILayout.LabelField (type + " Name:");

            createName = EditorGUILayout.TextField (createName);

            EditorGUILayout.HelpBox ("Name with namespace but without extension. For example if you want to create a " + type + " file of Ball in the namespace Items, input: Items.Ball", MessageType.Info);

            if (GUILayout.Button ("Create " + type))
            {
                Create ();
            }
        }

        void Create ()
        {
            var templateName = generateType.ToString () + "Template";
            var overrideTemplate = AssetDatabase.FindAssets ($"Override{templateName}");
            var foundTemplate = AssetDatabase.FindAssets ($"{templateName}");
            string templateGuid = "";
            if (overrideTemplate.Length > 0)
            {
                templateGuid = overrideTemplate[0];
            }
            else if (foundTemplate.Length > 0)
            {
                templateGuid = foundTemplate[0];
            }

            if (templateGuid == "")
            {
                Debug.LogError ($"Template: {templateName} not found!");
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath (templateGuid);
            if (string.IsNullOrEmpty (path))
            {
                Debug.LogError ("Can't load file: " + path);
            }

            var sourceTemplate = "";
            try
            {
                using (StreamReader reader = new StreamReader (path))
                {
                    sourceTemplate = reader.ReadToEnd ();
                }
            }
            catch { };

            if (string.IsNullOrEmpty (sourceTemplate))
            {
                Debug.LogError ("Can't read file: " + path);
            }

            createName = MakeValidFileName (createName.Trim ());
            if (createName.ToLowerInvariant ().EndsWith (".cs"))
            {
                createName = createName.Remove (createName.Length - 3);
            }
            if (
                (
                    generateType == GenerateType.ComponentData ||
                    generateType == GenerateType.SharedComponentData

                ) &&
                createName.ToLowerInvariant ().EndsWith ("component")
            )
            {
                createName = createName.Remove (createName.Length - 9);
            }
            if (
                (
                    generateType == GenerateType.ComponentSystem ||
                    generateType == GenerateType.JobComponentSystem
                ) &&
                !createName.ToLowerInvariant ().EndsWith ("system"))
            {
                createName = createName + "System";
            }
            if (!createName.Contains ("."))
            {
                if (!EditorUtility.DisplayDialog ("Set Namespace to Game?", "You didn't include a namespace so it will be set to a default 'Game' namespace. Continue?", "Yes", "No"))
                {
                    return;
                }
            }
            var assembly = Assembly.GetAssembly (typeof (GameObject));
            var editorAssembly = Assembly.GetAssembly (typeof (EditorGUILayout));
            var lastDot = createName.LastIndexOf (".");
            var foundNamespace = "";
            var className = "";
            if (lastDot > -1)
            {
                foundNamespace = createName.Remove (lastDot);
                className = createName.Substring (lastDot + 1, createName.Length - lastDot - 1);
            }
            else
            {
                foundNamespace = "Game";
                className = createName;
            }

            var classNameWithNamespace = foundNamespace + "." + className;

            if (assembly.GetType (classNameWithNamespace) != null || editorAssembly.GetType (classNameWithNamespace) != null)
            {
                if (!EditorUtility.DisplayDialog ("Existing Type Name", $"The Type {className} already exists in the namespace {foundNamespace}. Ignore?", "Yes", "No"))
                {
                    return;
                }
            }

            var model = new Dictionary<string, object> ();
            model.Add ("NAMESPACE", foundNamespace);
            model.Add ("CLASSNAME", className);
            model.Add ("WORLDNAME", foundNamespace);

            var fileName = className + ".cs";

            var template = Mustachio.Parser.Parse (sourceTemplate);
            var content = template (model);

            var dir = GetBaseDirectory ();
            foreach (var namesp in foundNamespace.Split ('.'))
            {
                dir += (Path.AltDirectorySeparatorChar + namesp);
                if (!Directory.Exists (dir))
                {
                    Directory.CreateDirectory (dir);
                }
            }

            using (StreamWriter writer = new StreamWriter (dir + Path.AltDirectorySeparatorChar + fileName, true))
            {
                writer.WriteLine (content);
            }

            AssetDatabase.Refresh ();

            var foundGenerated = AssetDatabase.FindAssets ($"t:script {className}");
            if (foundGenerated.Length > 0)
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object> (AssetDatabase.GUIDToAssetPath (foundGenerated[0]));
                if (obj != null)
                {
                    AssetDatabase.OpenAsset (obj, -1);
                }
            }
        }

        private static string MakeValidFileName (string name)
        {
            var invalids = System.IO.Path.GetInvalidFileNameChars ();
            return String.Join ("_", name.Split (invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd ('.');
        }
    }
}