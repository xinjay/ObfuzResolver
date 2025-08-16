using System.Reflection;
using System.Runtime.CompilerServices;
using MonoHook;
using UnityEngine;

namespace ObfuzResolver.Runtime
{
    public class UnityLogHook
    {
        private delegate void LogCallbackHandler(string logString, string stackTrace, LogType type,
            bool invokedOnMainThread);

        private static MethodHook _hook;
        //private static Application.LogCallback s_LogCallbackHandlerThreaded;
        //private static Application.LogCallback s_LogCallbackHandler;

        private static FieldInfo filed_s_LogCallbackHandler;
        private static FieldInfo filed_s_LogCallbackHandlerThreaded;

        public static void HookUnityLog()
        {
           // if (_hook == null)
            {
                var type = typeof(Application);
                filed_s_LogCallbackHandler =
                    type.GetField("s_LogCallbackHandler", BindingFlags.Static | BindingFlags.NonPublic);
                filed_s_LogCallbackHandlerThreaded =
                    type.GetField("s_LogCallbackHandlerThreaded", BindingFlags.Static | BindingFlags.NonPublic);
                var targetMethod = type.GetMethod("CallLogCallback", BindingFlags.Static | BindingFlags.NonPublic);
                var newMethod = new LogCallbackHandler(NewMethod).Method;
                var proxyMethod = new LogCallbackHandler(ProxyMethod).Method;
                _hook = new MethodHook(targetMethod, newMethod, proxyMethod);
            }
            _hook.Install();
        }

        public static void UnHookUnityLog()
        {
            _hook?.Uninstall();
        }

        public static bool IsUnityLogHooked()
        {
            return _hook?.isHooked ?? false;
        }

        private static void NewMethod(string logString, string stackTrace, LogType type, bool invokedOnMainThread)
        {
            logString = ObfuzResolveManager.Instance.ObfuzResolve(logString);
            stackTrace = ObfuzResolveManager.Instance.ObfuzResolve(stackTrace);
            if (invokedOnMainThread)
            {
                var logCallbackHandler = filed_s_LogCallbackHandler.GetValue(null) as Application.LogCallback;
                if (logCallbackHandler != null)
                    logCallbackHandler(logString, stackTrace, type);
            }

            var callbackHandlerThreaded = filed_s_LogCallbackHandlerThreaded.GetValue(null) as Application.LogCallback;
            if (callbackHandlerThreaded == null)
                return;
            callbackHandlerThreaded(logString, stackTrace, type);
        }


        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void ProxyMethod(string logString, string stackTrace, LogType type, bool invokedOnMainThread)
        {
            for (var i = 0; i < 10000; i++)
            {
                var j = i;
                var k = i + j;
                if (k == 0)
                    break;
            }
        }
    }
}