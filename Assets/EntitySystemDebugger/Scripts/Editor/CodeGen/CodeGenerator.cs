using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using EntitySystemDebugger.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace EntitySystemDebugger.Editor.CodeGen
{

    [System.Serializable]
    public enum GenerateType
    {
        // Bootstrap,
        // Archetype,
        ComponentData,
        SharedComponentData,
        ComponentSystem,
        JobComponentSystem,
        BasicJob,
        ParallelJob,
        ComponentDataJob,
        NativeMultiHashMapJob,
    }

    [System.Serializable]
    public class CodeGenerator : EditorWindow
    {
        private string componentsDirectory = "";
        private string systemsDirectory = "";
        private string jobsDirectory = "";
        private string bootstrapFile = "";
        private string currentNamespace = "";
        private string secondaryNamespace = "";

        private string typeName = "";
        private string typeNamespace = "";
        private string fullName = "";
        private string fileName = "";
        private string fileDirectory = "";

        private string fileNameInput = "";
        private bool showNameHelp = false;

        private bool componentsDirectoryExists = false;
        private bool systemsDirectoryExists = false;
        private bool jobsDirectoryExists = false;

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

        [MenuItem ("Assets/Create/ECS", false, 0)]
        private static void OpenWindow ()
        {
            var dir = GetBaseDirectory ();
            if (dir != "")
            {
                ShowWindow ();
            }
            else
            {
                Debug.LogWarning ("Blank base dir");
            }
        }

        private static string GetBaseDirectory (bool skipCheck = false)
        {
            var dir = "";
            var existing = PlayerPrefs.GetString ("codegeneratorbasedir", "");
            if (Directory.Exists (existing))
            {
                dir = existing;
            }
            if (dir == "" || skipCheck == true)
            {
                dir = EditorUtility.SaveFolderPanel ("Choose Output Directory", existing, "");
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
            var window = GetWindow<CodeGenerator> ("Code Generator", true, typeof (EntitySystemDebuggerWindow));
            window.minSize = new Vector2 (320, 320);
            window.Show ();
        }

        void OnGUI ()
        {
            var tab = (int) generateType;
            var type = generateType.ToString ();
            var dataDirectory = Application.dataPath;
            var dirLength = dataDirectory.Length;
            var baseDirectory = GetBaseDirectory ();
            var searchDirectory = baseDirectory.Substring (dirLength + 1);
            currentNamespace = GetBaseNamespaceFromDirectory (baseDirectory);

            GUILayout.Space (4);

            DrawBasePath (baseDirectory);

            CheckPaths (baseDirectory);

            if (!componentsDirectoryExists ||
                !systemsDirectoryExists ||
                !jobsDirectoryExists
            )
            {
                return;
            }
            // GUILayout.Space (8);
            string[] types = GetValidTypes ();

            CheckBootstrap (baseDirectory);

            var tempFileDirectory = baseDirectory;
            var selectedTab = GUILayout.SelectionGrid (tab, types, 2);
            if (selectedTab != tab)
            {
                tab = selectedTab;
                generateType = (GenerateType) selectedTab;
            }

            switch ((GenerateType) selectedTab)
            {
                // case GenerateType.Bootstrap:
                //     secondaryNamespace = "";
                //     break;
                case GenerateType.ComponentData:
                case GenerateType.SharedComponentData:
                    tempFileDirectory = componentsDirectory;
                    secondaryNamespace = "Components.";
                    break;
                case GenerateType.ComponentSystem:
                case GenerateType.JobComponentSystem:
                    tempFileDirectory = systemsDirectory;
                    secondaryNamespace = "Systems.";
                    break;
                case GenerateType.BasicJob:
                case GenerateType.ParallelJob:
                case GenerateType.NativeMultiHashMapJob:
                case GenerateType.ComponentDataJob:
                    tempFileDirectory = jobsDirectory;
                    secondaryNamespace = "Jobs.";
                    break;
            }

            GUILayout.Space (8);

            var rect = EditorGUILayout.BeginVertical ();
            var prefixNamespace = currentNamespace + "." + secondaryNamespace;

            EditorGUILayout.LabelField ($"Create New {type}:");
            EditorGUILayout.BeginHorizontal ();
            GUI.enabled = false;
            EditorGUILayout.TextField (prefixNamespace, GUILayout.MaxWidth (Mathf.RoundToInt (prefixNamespace.Length * 7f)));
            GUI.enabled = true;

            fileNameInput = EditorGUILayout.TextField (fileNameInput, GUILayout.ExpandWidth (true));

            showNameHelp = GUILayout.Toggle (showNameHelp, EditorGUIUtility.IconContent ("_Help"), EditorStyles.label, GUILayout.Width (18));

            EditorGUILayout.EndHorizontal ();

            if (showNameHelp)
                EditorGUILayout.HelpBox ("Name with namespace but without extension. For example if you want to create a " + type + " file of Ball in the namespace " + prefixNamespace + "Items, input: Items.Ball", MessageType.Info);

            EditorGUILayout.EndVertical ();

            if (string.IsNullOrEmpty (fileNameInput.Trim ()))
            {
                return;
            }

            var tempTypeName = StringUtils.ValidClassName (fileNameInput.Trim ());
            tempTypeName = StringUtils.CreateFileName (tempTypeName);

            fullName = prefixNamespace;
            var fileSubDirectory = "";

            if (tempTypeName.IndexOf ('.') > -1)
            {
                var subNamespaces = tempTypeName.Split ('.');
                for (int i = 0; i < subNamespaces.Length - 1; i++)
                {
                    fullName += subNamespaces[i] + ".";
                    fileSubDirectory += "/" + subNamespaces[i];
                }
                tempTypeName = subNamespaces[subNamespaces.Length - 1];
            }

            if (
                (
                    generateType == GenerateType.ComponentData ||
                    generateType == GenerateType.SharedComponentData

                ) &&
                !tempTypeName.ToLowerInvariant ().EndsWith ("component")
            )
            {
                tempTypeName = tempTypeName + "Component";
            }

            if (
                (
                    generateType == GenerateType.ComponentSystem ||
                    generateType == GenerateType.JobComponentSystem
                ) &&
                !tempTypeName.ToLowerInvariant ().EndsWith ("system"))
            {
                tempTypeName = tempTypeName + "System";
            }

            if (
                (
                    generateType == GenerateType.BasicJob ||
                    generateType == GenerateType.ParallelJob ||
                    generateType == GenerateType.NativeMultiHashMapJob ||
                    generateType == GenerateType.ComponentDataJob
                ) &&
                !tempTypeName.ToLowerInvariant ().EndsWith ("job"))
            {
                tempTypeName = tempTypeName + "Job";
            }

            fileName = tempTypeName + ".cs";
            typeName = tempTypeName;
            typeNamespace = fullName.Substring (0, fullName.Length - 1);
            fullName += typeName;
            fileDirectory = tempFileDirectory + fileSubDirectory + "/";
            EditorGUILayout.LabelField ("Full Name: " + fullName);
            var horizontalRect = GUILayoutUtility.GetLastRect ();

            GUILayout.Space (4);

            if (string.IsNullOrEmpty (typeName)) return;
            if (string.IsNullOrEmpty (typeNamespace)) return;
            if (string.IsNullOrEmpty (fullName)) return;
            if (string.IsNullOrEmpty (fileDirectory)) return;
            if (string.IsNullOrEmpty (fileName)) return;

            GUILayout.Label (ShortenPath (fileDirectory + fileName, horizontalRect.width));
            if (GUILayout.Button ("Create " + type))
            {
                Create ();
            }

            if (GUILayout.Button ("Create " + type + " Then Open Editor"))
            {
                Create (true);
            }
        }

        private void DrawBasePath (string baseDirectory)
        {
            var labelRect = EditorGUILayout.BeginVertical ();
            EditorGUILayout.LabelField ("Base Output Directory: ");
            var directoryLabel = ShortenPath (baseDirectory, labelRect.width);
            EditorGUILayout.LabelField (directoryLabel);

            var changeButtonRect = labelRect;
            changeButtonRect.width = 50;
            changeButtonRect.height = 16;
            changeButtonRect.x = labelRect.width - 50;

            if (GUI.Button (changeButtonRect, "Change", EditorStyles.miniButton))
            {
                pendingCreate = true;
                GetBaseDirectory (true);
            }
            EditorGUILayout.EndVertical ();
        }

        private static string ShortenPath (string path, float width)
        {
            int len = (int) Mathf.Min (Mathf.RoundToInt (0.137f * width) + 3, width);
            var str = path;
            if (path.Length > len && len > 0)
            {
                var start = path.Length - len;
                var length = len;
                str = "..." + path.Substring (start, length);
            }

            return str;
        }

        private void CheckPaths (string baseDirectory)
        {

            var lastRect = GUILayoutUtility.GetLastRect ();
            componentsDirectory = baseDirectory + "/Components";
            if (!Directory.Exists (componentsDirectory))
            {
                componentsDirectoryExists = false;
                GUILayout.Space (4);
                EditorGUILayout.LabelField (ShortenPath (componentsDirectory, lastRect.width));
                if (GUILayout.Button ("Create Components Directory"))
                {
                    Directory.CreateDirectory (componentsDirectory);
                    AssetDatabase.Refresh ();
                }
            }
            else
            {
                componentsDirectoryExists = true;
            }

            systemsDirectory = baseDirectory + "/Systems";
            if (!Directory.Exists (systemsDirectory))
            {
                systemsDirectoryExists = false;
                GUILayout.Space (4);
                EditorGUILayout.LabelField (ShortenPath (systemsDirectory, lastRect.width));
                if (GUILayout.Button ("Create Systems Directory"))
                {
                    Directory.CreateDirectory (systemsDirectory);
                    AssetDatabase.Refresh ();
                }
            }
            else
            {
                systemsDirectoryExists = true;
            }

            jobsDirectory = baseDirectory + "/Jobs";
            if (!Directory.Exists (jobsDirectory))
            {
                jobsDirectoryExists = false;
                GUILayout.Space (4);
                EditorGUILayout.LabelField (ShortenPath (jobsDirectory, lastRect.width));
                if (GUILayout.Button ("Create Jobs Directory"))
                {
                    Directory.CreateDirectory (jobsDirectory);
                    AssetDatabase.Refresh ();
                }
            }
            else
            {
                jobsDirectoryExists = true;
            }
        }

        private void CheckBootstrap (string baseDirectory)
        {
            var lastRect = GUILayoutUtility.GetLastRect ();
            var fileName = "/" + currentNamespace + "Bootstrap.cs";
            bootstrapFile = baseDirectory + fileName;
            if (!File.Exists (bootstrapFile))
            {
                GUILayout.Space (4);
                EditorGUILayout.LabelField (ShortenPath (bootstrapFile, lastRect.width));
                if (GUILayout.Button ("Create Bootstrap File"))
                {
                    var sourceTemplate = LoadTemplate ("BootstrapTemplate");
                    if (sourceTemplate == null) return;
                    var model = new Dictionary<string, object> ();
                    model.Add ("NAMESPACE", currentNamespace);
                    model.Add ("CLASSNAME", currentNamespace + "Bootstrap");
                    model.Add ("WORLDNAME", currentNamespace + " World");
                    if (CreateFile (sourceTemplate, model, baseDirectory, fileName))
                    {
                        var searchName = fileName.Substring (1, fileName.Length - 4);
                        OpenFile (searchName);
                    }
                }
            }
            else
            {
                // todo
                // if (GUILayout.Button ("Create Archetype"))
                // {
                // }
            }
        }

        private static string GetBaseNamespaceFromDirectory (string baseDirectory)
        {
            TextInfo textInfo = new CultureInfo ("en-US", false).TextInfo;
            return StringUtils.ValidClassName (textInfo.ToTitleCase (new DirectoryInfo (baseDirectory).Name));
        }

        private string[] GetValidTypes ()
        {
            return Enum.GetNames (typeof (GenerateType));
        }

        void Create (bool open = false)
        {

            var sourceTemplate = LoadTemplate (generateType.ToString () + "Template");
            if (sourceTemplate == null) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ())
            {
                if (assembly.GetType (fullName) != null)
                {
                    if (!EditorUtility.DisplayDialog ("Existing Type Name", $"The Type {fullName} already exists. Ignore?", "Yes", "No"))
                    {
                        return;
                    }
                }
            }

            var model = new Dictionary<string, object> ();
            model.Add ("COMPONENTNAME", typeName.Replace ("System", "").Replace ("Component", ""));
            model.Add ("NAMESPACE", typeNamespace);
            model.Add ("CLASSNAME", typeName);

            if (CreateFile (sourceTemplate, model, fileDirectory, fileName))
            {
                if (open)
                {
                    OpenFile (typeName);
                }
            }
        }

        private void OpenFile (string fileNameWithoutExtension)
        {
            var foundGenerated = AssetDatabase.FindAssets ($"t:script {fileNameWithoutExtension}");
            if (foundGenerated.Length > 0)
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object> (AssetDatabase.GUIDToAssetPath (foundGenerated[0]));
                if (obj != null)
                {
                    AssetDatabase.OpenAsset (obj, -1);
                }
            }
        }

        private string LoadTemplate (string templateName)
        {
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
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath (templateGuid);
            if (string.IsNullOrEmpty (path))
            {
                Debug.LogError ("Can't load file: " + path);
                return null;
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

            return sourceTemplate;
        }

        private bool CreateFile (string sourceTemplate, Dictionary<string, object> model, string directory, string fileName)
        {
            var template = Mustachio.Parser.Parse (sourceTemplate);
            var content = template (model);

            if (!Directory.Exists (directory))
            {
                Directory.CreateDirectory (directory);
            }

            var fullPath = directory + fileName;

            if (File.Exists (fullPath))
            {
                if (!EditorUtility.DisplayDialog ("Existing File", $"The File {fullPath} already exists. Overwrite?", "Yes", "No"))
                {
                    return false;
                }

                File.Delete (fullPath);
            }

            using (StreamWriter writer = new StreamWriter (fullPath, true))
            {
                writer.WriteLine (content);
            }

            AssetDatabase.Refresh ();

            return true;
        }
    }
}