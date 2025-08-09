using System.IO;
using UnityEngine;

namespace ObfuzResolver.Editor
{
    [System.Serializable]
    public class ObfuzResolveSettings
    {
        public bool HookUnityDebug;
        public bool UseExternalMappingFile;
        public bool RemoveMethodGeneratedByObfuz;
        public string ExternalMappingFile;
        private static ObfuzResolveSettings _instance;
        private const string settingFilePath = "ProjectSettings/ObfuzResolveSettings.json";

        public static ObfuzResolveSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }

                return _instance;
            }
        }

        public static ObfuzResolveSettings LoadSettings()
        {
            var json = File.Exists(settingFilePath) ? File.ReadAllText(settingFilePath) : string.Empty;
            var settigns = string.IsNullOrEmpty(json) ? new() : JsonUtility.FromJson<ObfuzResolveSettings>(json);
            return settigns;
        }

        public static void Save()
        {
            var json = JsonUtility.ToJson(_instance);
            File.WriteAllText(settingFilePath, json);
        }
    }
}