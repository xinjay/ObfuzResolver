using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ObfuzResolver.Runtime
{
    public class ObfuzResolveRuntimeConsole : MonoBehaviour
    {
        private static ObfuzResolveRuntimeConsole _instance;

        public static ObfuzResolveRuntimeConsole Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = GameObject.FindObjectOfType<ObfuzResolveRuntimeConsole>() ??
                                new GameObject("ObfuzResolveRuntimeConsole").AddComponent<ObfuzResolveRuntimeConsole>();
                    DontDestroyOnLoad(_instance);
                }

                return _instance;
            }
        }

        private bool showConsole = true;
        private Vector2 scrollPosition;
        private Vector2 detailsScrollPosition;
        private LogEntry selectedLog;
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showMessages = true;
        private bool collapse = false;
        private bool hookunityLog = false;
        private string searchText = "";
        private Rect windowRect = new Rect(20, 20, 1600, 1000);
        private static readonly Color errorColor = new(1f, 0.3f, 0.3f);
        private static readonly Color warningColor = new(1f, 0.8f, 0.4f);
        private static readonly Color messageColor = new(0.8f, 0.8f, 0.8f);
        private static readonly Color selectedColor = new(0.3f, 0.5f, 0.8f);
        private List<LogEntry> logs = new();
        private Dictionary<string, LogEntry> collapsedLogs = new();
        private const float ICON_SIZE = 20f;
        private const float ICON_PADDING = 5f;
        private const float COUNT_WIDTH = 40f;
        private const float TEXT_PADDING = 5f;
        private const float ITEM_HEIGHT = 40f;
        private static readonly Regex richTextRegex = new(@"<[^>]*>", RegexOptions.Compiled);

        private GUIStyle toolbarStyle;
        private GUIStyle toolbarButtonStyle;
        private GUIStyle logStyle;
        private GUIStyle countStyle;
        private GUIStyle detailsStyle;
        private GUIStyle titleStyle;
        private GUIStyle windowTitleStyle;
        private GUIStyle searchFieldStyle;

        private Texture2D errorIcon;
        private Texture2D warningIcon;
        private Texture2D infoIcon;

        private GUIContent messageIconContent;
        private GUIContent warningIconContent;
        private GUIContent errorIconContent;
        private int maxLogCount = 500;

        private int messageCount;
        private int warningCount;
        private int errorCount;

        public void OnEnable()
        {
            hookunityLog = ObfuzResolveManager.Instance.IsUnityLogHooked();
            Application.logMessageReceived += HandleLog;
            CreateIcons();
        }

        public void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
            Clear();
        }


        public void SetConsoleState(bool show)
        {
            this.showConsole = show;
        }

        private float lastx = 0;
        private float lasty = 0;

        private void OnGUI()
        {
            InitStyles();
            var width = showConsole ? (int)(Screen.width * 0.75) : 60;
            var height = showConsole ? (int)(Screen.height * 0.75) : 60;
            windowRect.width = Mathf.Clamp(windowRect.width, 0, width);
            windowRect.height = Mathf.Clamp(windowRect.height, 0, height);
            if (!showConsole)
            {
                windowRect.x = lastx;
                windowRect.y = lasty;
                windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
                windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);
                windowRect = GUI.Window(0, windowRect, DrawOpenBtn, "", GUI.skin.box);
                lastx = windowRect.x;
                lasty = windowRect.y;
            }
            else
            {
                windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
                windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);
                windowRect = GUI.Window(0, windowRect, DrawConsoleWindow, "ObfuzResolver Console");
            }
        }

        private void CreateIcons()
        {
            errorIcon = CreateColoredTexture(errorColor);
            warningIcon = CreateColoredTexture(warningColor);
            infoIcon = CreateColoredTexture(messageColor);

            messageIconContent = new GUIContent(infoIcon);
            warningIconContent = new GUIContent(warningIcon);
            errorIconContent = new GUIContent(errorIcon);
            Clear();
        }

        private Texture2D CreateColoredTexture(Color color)
        {
            var tex = new Texture2D(16, 16);
            var pixels = new Color[16 * 16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }


        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (logs.Count >= maxLogCount)
            {
                RemoveOldestLog();
            }

            logString = logString.Trim().Replace("\r\n", "\n");
            var firstStackTrace = stackTrace.Replace("\r\n", "\n").Split("\n")[0];
            var entry = new LogEntry
            {
                message = $"{logString}\n{firstStackTrace}",
                stackTrace = stackTrace,
                type = type,
                count = 1,
                timestamp = DateTime.Now
            };

            if (collapse)
            {
                var key = $"{type}-{logString}";
                if (collapsedLogs.TryGetValue(key, out LogEntry existing))
                {
                    existing.count++;
                    existing.timestamp = DateTime.Now;
                }
                else
                {
                    logs.Add(entry);
                    collapsedLogs.Add(key, entry);
                }
            }
            else
            {
                logs.Add(entry);
            }

            errorCount = warningCount = messageCount = 0;
            foreach (var log in logs)
            {
                switch (log.type)
                {
                    case LogType.Assert:
                    case LogType.Error:
                    case LogType.Exception:
                        errorCount++;
                        break;
                    case LogType.Log:
                        messageCount++;
                        break;
                    case LogType.Warning:
                        warningCount++;
                        break;
                }
            }

            SetIconNum();
            scrollPosition.y = Mathf.Infinity;
        }

        private void SetIconNum()
        {
            errorIconContent.text = $"{errorCount}";
            warningIconContent.text = $"{warningCount}";
            messageIconContent.text = $"{messageCount}";
        }

        private void InitStyles()
        {
            if (toolbarStyle == null)
            {
                toolbarStyle = new(GUI.skin.box)
                {
                    padding = new(5, 5, 5, 5),
                    margin = new(0, 0, 20, 0)
                };

                toolbarButtonStyle = new(GUI.skin.button)
                {
                    padding = new(5, 5, 3, 3),
                    margin = new(2, 2, 2, 2),
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18
                };

                logStyle = new(GUI.skin.label)
                {
                    padding = new(0, 0, 0, 0),
                    margin = new(0, 0, 0, 0),
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.UpperLeft,
                    richText = true
                };

                countStyle = new(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new(0.7f, 0.7f, 0.7f) },
                    fontSize = 20,
                    fixedWidth = COUNT_WIDTH
                };

                detailsStyle = new(GUI.skin.textArea)
                {
                    richText = true,
                    wordWrap = true,
                    fontSize = 18,
                    padding = new(5, 5, 5, 5)
                };

                titleStyle = new(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                searchFieldStyle = new(GUI.skin.textField)
                {
                    padding = new(5, 5, 3, 3),
                    margin = new(2, 2, 2, 2),
                    fontSize = 18
                };
            }
        }

        private void DrawOpenBtn(int windowID)
        {
            var elementWidth = 30;
            var elementHeight = 30;
            var centerX = (windowRect.width - elementWidth) * 0.5f;
            var centerY = (windowRect.height - elementHeight) * 0.5f;
            var last = GUI.color;
            GUI.color = Color.red;
            if (GUI.Button(new Rect(centerX, centerY, elementWidth, elementHeight), "+"))
            {
                windowRect.width = 1600;
                windowRect.height = 1000;
                SetConsoleState(true);
            }

            GUI.color = last;
            GUI.DragWindow(new Rect(0, 0, 10000, 140));
        }

        private void DrawConsoleWindow(int windowID)
        {
            GUILayout.BeginVertical();
            DrawToolbar();
            DrawLogList();
            DrawDetailsPanel();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 140));
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(toolbarStyle, GUILayout.Height(30));

            if (GUILayout.Button("Clear", toolbarButtonStyle, GUILayout.Width(60)))
            {
                Clear();
            }

            GUILayout.Space(10);
            collapse = GUILayout.Toggle(collapse, "Collapse", toolbarButtonStyle);

            var hooked = GUILayout.Toggle(hookunityLog, "HookUnityLog", toolbarButtonStyle);
            if (hooked != hookunityLog)
            {
                if (hooked)
                {
                    ObfuzResolveManager.Instance.HookUnityLog();
                }
                else
                {
                    ObfuzResolveManager.Instance.UnHookUnityLog();
                }

                hookunityLog = hooked;
            }

            if (GUILayout.Button("LoadMapppingFile", toolbarButtonStyle))
            {
                ObfuzResolveManager.Instance.LoadDefaultMappingFile();
            }

            GUILayout.FlexibleSpace();

            searchText = GUILayout.TextField(searchText, searchFieldStyle, GUILayout.Width(150));
            showErrors = GUILayout.Toggle(showErrors, errorIconContent, toolbarButtonStyle, GUILayout.Width(50));
            showWarnings = GUILayout.Toggle(showWarnings, warningIconContent, toolbarButtonStyle, GUILayout.Width(50));
            showMessages = GUILayout.Toggle(showMessages, messageIconContent, toolbarButtonStyle, GUILayout.Width(50));
            GUILayout.Space(10);

            GUILayout.Space(10);
            if (GUILayout.Button("-", toolbarButtonStyle, GUILayout.Width(30)))
            {
                SetConsoleState(false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLogList()
        {
            var detailsHeight = selectedLog != null ? 180f : 0f;
            var visibleHeight = windowRect.height - 100 - detailsHeight;
            scrollPosition =
                GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Mathf.Max(visibleHeight, 100f)));

            for (var i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                if (!ShouldShow(log))
                    continue;
                if (!string.IsNullOrEmpty(searchText))
                {
                    var plainText = StripRichTextTags(log.message);
                    if (plainText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                var logRect = GUILayoutUtility.GetRect(windowRect.width - 30, ITEM_HEIGHT);
                var isSelected = selectedLog == log;
                var bgColor = isSelected ? selectedColor : messageColor * ((i % 2 != 0) ? 0.2f : 0.3f);
                GUI.backgroundColor = bgColor;
                GUI.Box(logRect, GUIContent.none);
                GUI.backgroundColor = Color.white;
                var iconRect = new Rect(
                    logRect.x + ICON_PADDING,
                    logRect.y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                    ICON_SIZE,
                    ICON_SIZE
                );
                DrawLogIcon(iconRect, log.type);

                var textStartX = iconRect.xMax + TEXT_PADDING;
                var textWidth = logRect.width - textStartX - TEXT_PADDING;

                if (collapse && log.count > 1)
                {
                    textWidth -= COUNT_WIDTH + TEXT_PADDING;
                }

                var displayText = $"[{log.timestamp:HH:mm:ss}]{log.message}";
                var textRect = new Rect(
                    textStartX,
                    logRect.y + TEXT_PADDING,
                    textWidth,
                    ITEM_HEIGHT - TEXT_PADDING * 2
                );

                GUI.Label(textRect, displayText, logStyle);

                if (collapse && log.count > 1)
                {
                    var countRect = new Rect(
                        logRect.xMax - COUNT_WIDTH - TEXT_PADDING,
                        logRect.y,
                        COUNT_WIDTH,
                        ITEM_HEIGHT
                    );
                    GUI.Label(countRect, $"{log.count}x", countStyle);
                }

                if (GUI.Button(logRect, "", GUIStyle.none))
                {
                    selectedLog = log;
                    detailsScrollPosition = Vector2.zero;
                }
            }

            if (logs.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("No logs available", GUILayout.Height(50));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return richTextRegex.Replace(text, "");
        }

        private void DrawLogIcon(Rect position, LogType type)
        {
            var icon = default(Texture2D);
            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    icon = errorIcon;
                    break;
                case LogType.Warning:
                    icon = warningIcon;
                    break;
                default:
                    icon = infoIcon;
                    break;
            }

            if (icon)
            {
                GUI.DrawTexture(position, icon);
            }
        }

        private void DrawDetailsPanel()
        {
            if (selectedLog == null)
                return;
            GUILayout.Space(5);
            GUILayout.Label("Details", titleStyle);
            detailsScrollPosition = GUILayout.BeginScrollView(detailsScrollPosition,
                GUILayout.Height(150));
            var details = $"<b>Message:</b>\n{selectedLog.message}\n\n<b>Stack Trace:</b>\n{selectedLog.stackTrace}";
            GUILayout.Label(details, detailsStyle);
            GUILayout.EndScrollView();
        }

        private bool ShouldShow(LogEntry log)
        {
            return log.type switch
            {
                LogType.Error => showErrors,
                LogType.Assert => showErrors,
                LogType.Exception => showErrors,
                LogType.Warning => showWarnings,
                _ => showMessages
            };
        }

        private void RemoveOldestLog()
        {
            if (logs.Count == 0) return;
            var oldestLog = logs[0];
            var oldestIndex = 0;
            for (var i = 1; i < logs.Count; i++)
            {
                if (logs[i].timestamp < oldestLog.timestamp)
                {
                    oldestLog = logs[i];
                    oldestIndex = i;
                }
            }
            logs.RemoveAt(oldestIndex);
            if (selectedLog == oldestLog)
            {
                selectedLog = null;
            }
            if (collapse)
            {
                var key = $"{oldestLog.type}-{oldestLog.message}";
                if (collapsedLogs.ContainsKey(key) && collapsedLogs[key] == oldestLog)
                {
                    collapsedLogs.Remove(key);
                }
            }
        }

        private void Clear()
        {
            logs.Clear();
            collapsedLogs.Clear();
            selectedLog = null;
            errorCount = messageCount = warningCount = 0;
            SetIconNum();
        }

        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public int count;
            public DateTime timestamp;
        }
    }
}