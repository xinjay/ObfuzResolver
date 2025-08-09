using System;
using DeobfuscateStackTrace;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObfuzResolver.Runtime
{
    public class ObfuzDebugHandler : ILogHandler
    {
        private ILogHandler internalHandler;
        private SymbolMappingReader reader;

        public void SetMappingReader(SymbolMappingReader reader)
        {
            this.reader = reader;
        }

        public void SetInternalHandler(ILogHandler internalHandler)
        {
            this.internalHandler = internalHandler;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (arg is string content)
                {
                    var newContent = ObfuzResolveManager.Instance.ObfuzResolve(content);
                    args[index] = $"<color=red>[DeObfuz]</color>{newContent}";
                }
            }

            internalHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            internalHandler.LogException(exception, context);
        }
    }
}

