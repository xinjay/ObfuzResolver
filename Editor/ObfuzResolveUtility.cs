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

    public class ObfuzResolveUtility : EditorWindow
    {
        private ObfuzResolveSettings settings;
        private string inputText = "";
        private string outputText = "";
        private ObfuzResolveManager obfuzDebugManager;
        private DefuzLogMode logType;

        public static void ShowWindow()
        {
            GetWindow<ObfuzResolveUtility>("ObfuzResolveUtility");
        }

        private void OnEnable()
        {
            settings = ObfuzResolveSettings.Instance;
            obfuzDebugManager = ObfuzResolveManager.Instance;
        }

        private void OnGUI()
        {
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();


            //EditorGUILayout.Space();
            //GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
            EditorGUI.BeginChangeCheck();
            logType = (DefuzLogMode)GUILayout.Toolbar((int)logType,
                new[] { new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton",
                GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                SelectLogMode(logType);
            // 详情面板也支持富文本
            GUIStyle richTextStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = true, // 启用富文本
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            switch (logType)
            {
                case DefuzLogMode.Original:
                    inputText = EditorGUILayout.TextArea(inputText, richTextStyle, GUILayout.MinHeight(100),
                        GUILayout.ExpandHeight(true));
                    break;
                case DefuzLogMode.DeObfuz:
                    EditorGUILayout.TextArea(outputText, richTextStyle, GUILayout.MinHeight(100),
                        GUILayout.ExpandHeight(true));
                    break;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            EditorGUI.BeginChangeCheck();
            settings.HookUnityLog =
                EditorGUILayout.Toggle("HookUnityLog", settings.HookUnityLog);
            if (EditorGUI.EndChangeCheck())
            {
                HookUnityDebugIfNeeded();
                ObfuzResolveSettings.Save();
            }

            EditorGUI.BeginChangeCheck();
            settings.RemoveMethodGeneratedByObfuz =
                EditorGUILayout.Toggle("RemoveObfuzGenMethod", settings.RemoveMethodGeneratedByObfuz);
            if (EditorGUI.EndChangeCheck())
            {
                ObfuzResolveManager.Instance.SetObfuzGenMethodState(settings.RemoveMethodGeneratedByObfuz);
                ObfuzResolveSettings.Save();
            }

            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
            EditorGUI.BeginChangeCheck();
            settings.UseExternalMappingFile = EditorGUILayout.Toggle("ExternalMappingFile",
                settings.UseExternalMappingFile);
            if (settings.UseExternalMappingFile)
            {
                if (GUILayout.Button("Browse"))
                {
                    var file = EditorUtility.OpenFilePanel("Select external mapping File", "", "xml");
                    if (!string.IsNullOrEmpty(file))
                    {
                        settings.ExternalMappingFile = file;
                    }
                }

                settings.ExternalMappingFile = EditorGUILayout.TextField(settings.ExternalMappingFile);
            }

            if (EditorGUI.EndChangeCheck())
            {
                LoadMappingFile();
                ObfuzResolveSettings.Save();
            }

            EditorGUILayout.Space(20);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
            if (GUILayout.Button("Resolve LogFile"))
                ResolveLogFile();
            EditorGUILayout.EndHorizontal();
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
            if (ObfuzResolveSettings.Instance.HookUnityLog)
                ObfuzResolveManager.Instance.HookUnityLog();
            else
                ObfuzResolveManager.Instance.UnHookUnityLog();
            //Debug.Log($"Unity Log Hooked:{ObfuzResolveSettings.Instance.HookUnityLog}");
        }
    }
}