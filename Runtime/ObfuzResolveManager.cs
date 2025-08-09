using System.IO;
using System.Text;
using DeobfuscateStackTrace;
using UnityEngine;

namespace ObfuzResolver.Runtime
{
    public class ObfuzResolveManager
    {
        private SymbolMappingReader reader;
        private ObfuzDebugHandler _obfuzDebugHandler = new();
        private ILogHandler defaultHandler;
        private static ObfuzResolveManager _instance;
        private StringBuilder stringBuilder = new();
        private bool removeMethodGeneratedByObfuz;

        public static ObfuzResolveManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new();
                return _instance;
            }
        }

        public static void LoadObfuzResolver()
        {
            var mappingFile = Path.Combine(Application.persistentDataPath, "mapping.xml");
            Debug.Log($"mappingFile:{mappingFile}");
            if (File.Exists(mappingFile))
            {
                Instance.LoadMapFile(mappingFile);
                Instance.HookUnityDebug();
            }
        }

        public void LoadMapFile(string mappingFile)
        {
            reader = new SymbolMappingReader(mappingFile);
            _obfuzDebugHandler.SetMappingReader(reader);
        }

        public void SetObfuzGenMethodState(bool remove)
        {
            removeMethodGeneratedByObfuz = remove;
        }

        public void HookUnityDebug()
        {
            var handler = Debug.unityLogger.logHandler;
            if (handler is not ObfuzDebugHandler)
            {
                defaultHandler = handler;
                _obfuzDebugHandler.SetInternalHandler(defaultHandler);
                Debug.unityLogger.logHandler = _obfuzDebugHandler;
            }
        }

        public void UnHookUnityDebug()
        {
            var handler = Debug.unityLogger.logHandler;
            if (handler is ObfuzDebugHandler)
            {
                Debug.unityLogger.logHandler = defaultHandler;
            }
        }

        public string ObfuzResolve(string content)
        {
            content = content.Replace("\r\n", "\n");
            var alllines = content.Split('\n');
            stringBuilder.Clear();
            foreach (var line in alllines)
            {
                var deobfuz = ResolveLine(line);
                if (!removeMethodGeneratedByObfuz || !deobfuz.StartsWith("$Obfuz$"))
                {
                    stringBuilder.AppendLine(deobfuz);
                }
            }

            return stringBuilder.ToString();
        }

        private string ResolveLine(string line)
        {
            if (!(reader.TryDeobfuscateExceptionStackTrace(line, out var newContent) ||
                  reader.TryDeobfuscateDebugLogStackTrace(line, out newContent)))
            {
                newContent = line;
            }

            var deobfuz = reader.TryDeobfuscateTypeName(newContent);
            return deobfuz;
        }
    }
}