using UnityEditor;
using UnityEngine;
using System.IO;
using Obfuz.Settings;
using ObfuzResolver.Runtime;

namespace ObfuzResolver.Editor
{
    public enum DefuzLogMode
    {
        Original,
        DeObfuz
    }

    public class ObfuzResolveWindow : EditorWindow
    {
        private ObfuzResolveSettings settings;
        private string inputText = "";
        private string outputText = "";
        private ObfuzResolveManager obfuzDebugManager;
        private DefuzLogMode logType;

        [MenuItem("ObfuzResolve/ObfuzResolver")]
        public static void ShowWindow()
        {
            GetWindow<ObfuzResolveWindow>("ObfuzResolver");
        }

        private void OnEnable()
        {
            settings = ObfuzResolveSettings.Instance;
            obfuzDebugManager = ObfuzResolveManager.Instance;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            settings.HookUnityDebug =
                EditorGUILayout.Toggle("HookUnityDebug", settings.HookUnityDebug, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                HookUnityDebugIfNeeded();
                ObfuzResolveSettings.Save();
            }

            EditorGUI.BeginChangeCheck();
            settings.RemoveMethodGeneratedByObfuz =
                EditorGUILayout.Toggle("RemoveObfuzGenMethod", settings.RemoveMethodGeneratedByObfuz,
                    GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                ObfuzResolveManager.Instance.SetObfuzGenMethodState(settings.RemoveMethodGeneratedByObfuz);
                ObfuzResolveSettings.Save();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            settings.UseExternalMappingFile = EditorGUILayout.Toggle("ExternalMappingFile",
                settings.UseExternalMappingFile,
                GUILayout.Width(200));
            if (settings.UseExternalMappingFile)
            {
                settings.ExternalMappingFile = EditorGUILayout.TextField(settings.ExternalMappingFile);
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    var file = EditorUtility.OpenFilePanel("Select external mapping File", "", "xml");
                    if (!string.IsNullOrEmpty(file))
                    {
                        settings.ExternalMappingFile = file;
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                LoadMappingFile();
                ObfuzResolveSettings.Save();
            }

            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Resolve LogFile"))
                ResolveLogFile();


            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
            EditorGUI.BeginChangeCheck();
            logType = (DefuzLogMode)GUILayout.Toolbar((int)logType,
                new[] { new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton",
                GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                SelectLogMode(logType);
            switch (logType)
            {
                case DefuzLogMode.Original:
                    inputText = EditorGUILayout.TextArea(inputText, GUILayout.MinHeight(100),
                        GUILayout.ExpandHeight(true));
                    break;
                case DefuzLogMode.DeObfuz:
                    EditorGUILayout.TextArea(outputText, GUILayout.MinHeight(100), GUILayout.ExpandHeight(true));
                    break;
            }
        }


        private void SelectLogMode(DefuzLogMode mode)
        {
            if (mode == DefuzLogMode.DeObfuz && !string.IsNullOrEmpty(inputText))
            {
                outputText = obfuzDebugManager.ObfuzResolve(inputText);
            }

            GUI.FocusControl(string.Empty);
            Repaint();
        }

        private void ResolveLogFile()
        {
            var filePath = EditorUtility.OpenFilePanel("Select obfuscated log File", "", "log");
            if (!string.IsNullOrEmpty(filePath))
            {
                var content = File.ReadAllText(filePath);
                var deobfuz = obfuzDebugManager.ObfuzResolve(content);
                var savefile = EditorUtility.SaveFilePanel("Save deobfuscated log File", "", "deobfuscated", "log");
                if (!string.IsNullOrEmpty(savefile))
                {
                    File.WriteAllText(savefile, deobfuz);
                }
            }
        }


        [InitializeOnLoadMethod]
        static void OnInitializeOnLoadMethod()
        {
            LoadMappingFile();
            HookUnityDebugIfNeeded();
        }

        public static void LoadMappingFile()
        {
            var mappingFile = "";
            var settings = ObfuzResolveSettings.Instance;
            if (settings.UseExternalMappingFile)
            {
                mappingFile = settings.ExternalMappingFile;
            }
            else
            {
                var _settings = ObfuzSettings.Instance.symbolObfusSettings;
                mappingFile = _settings.debug ? _settings.debugSymbolMappingFile : _settings.symbolMappingFile;
            }

            if (!string.IsNullOrEmpty(mappingFile))
            {
                Debug.Log($"Set Mappping File:{mappingFile}");
                ObfuzResolveManager.Instance.LoadMapFile(mappingFile);
            }
        }

        public static void HookUnityDebugIfNeeded()
        {
            if (ObfuzResolveSettings.Instance.HookUnityDebug)
                ObfuzResolveManager.Instance.HookUnityDebug();
            else
                ObfuzResolveManager.Instance.UnHookUnityDebug();
            Debug.Log($"Unity Debug Hooked:{ObfuzResolveSettings.Instance.HookUnityDebug}");
        }
    }
}