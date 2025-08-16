using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Text.RegularExpressions;
using ObfuzResolver.Runtime;

namespace ObfuzResolver.Editor
{
    public class ObfuzResolveEditorConsole : EditorWindow
    {
        private Vector2 scrollPosition;
        private Vector2 detailsScrollPosition;
        private LogEntry selectedLog;
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showMessages = true;
        private bool collapse = false;
        private string searchText = "";

        // 日志类型颜色定义
        private static readonly Color errorColor = new(1f, 0.3f, 0.3f);
        private static readonly Color warningColor = new(1f, 0.8f, 0.4f);
        private static readonly Color messageColor = new(0.8f, 0.8f, 0.8f);
        private static readonly Color selectedColor = new(0.3f, 0.5f, 0.8f);

        // 静态日志存储
        private List<LogEntry> logs = new();
        private Dictionary<string, LogEntry> collapsedLogs = new();

        // 布局设置
        private const float ICON_SIZE = 16f;
        private const float ICON_PADDING = 5f;
        private const float COUNT_WIDTH = 40f;
        private const float TEXT_PADDING = 5f;
        private const float ITEM_HEIGHT = 40f; // 两行文本的高度

        private GUIContent settingsIcon;
        private GUIContent messageIcon;
        private GUIContent warningIcon;
        private GUIContent errorIcon;
        private int maxLogCount = 500; // 最大日志条目数

        private int messageCount;
        private int warningCount;
        private int errorCount;


        // 富文本标签正则表达式
        private static readonly Regex richTextRegex = new(@"<[^>]*>", RegexOptions.Compiled);

        [MenuItem("ObfuzResolver/Resolver Console")]
        public static void ShowWindow()
        {
            GetWindow<ObfuzResolveEditorConsole>("ObfuzResolverConsole");
        }

        private void OnEnable()
        {
            // 注册日志回调
            CreateIcon();
            Clear();
            Application.logMessageReceived += HandleLog;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void CreateIcon()
        {
            settingsIcon = EditorGUIUtility.IconContent("SettingsIcon");
            messageIcon = EditorGUIUtility.IconContent("console.infoicon");
            warningIcon = EditorGUIUtility.IconContent("console.warnicon");
            errorIcon = EditorGUIUtility.IconContent("console.erroricon");
            SetIconNum();
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Clear();
            }
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            // 如果达到最大日志数量，移除最旧的日志
            if (logs.Count >= maxLogCount)
            {
                RemoveOldestLog();
            }

            logString = logString.Replace("\r\n", "");
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
                string key = $"{type}-{logString}";
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
            // 自动滚动到底部
            scrollPosition.y = Mathf.Infinity;
            Repaint();
        }

        private void SetIconNum()
        {
            errorIcon.text = $"{errorCount}";
            warningIcon.text = $"{warningCount}";
            messageIcon.text = $"{messageCount}";
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            DrawToolbar();
            DrawLogList();
            DrawDetailsPanel();
            GUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                Clear();
            }

            GUILayout.Space(10);
            collapse = GUILayout.Toggle(collapse, "Collapse", EditorStyles.toolbarButton);


            EditorGUI.BeginChangeCheck();
            var settings = ObfuzResolveSettings.Instance;
            EditorGUI.BeginChangeCheck();
            settings.HookUnityLog =
                GUILayout.Toggle(settings.HookUnityLog, "HookUnityLog", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                ObfuzResolveUtility.HookUnityDebugIfNeeded();
                ObfuzResolveSettings.Save();
            }

            GUILayout.FlexibleSpace();


            // 搜索框
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField;
            searchStyle.alignment = TextAnchor.MiddleCenter;
            searchText = GUILayout.TextField(searchText, searchStyle, GUILayout.Width(200));
            GUILayout.Space(10);
            showErrors = GUILayout.Toggle(showErrors, errorIcon, EditorStyles.toolbarButton);
            showWarnings = GUILayout.Toggle(showWarnings, warningIcon, EditorStyles.toolbarButton);
            showMessages = GUILayout.Toggle(showMessages, messageIcon, EditorStyles.toolbarButton);
            GUILayout.Space(10);
            if (GUILayout.Button(settingsIcon, EditorStyles.toolbarButton))
            {
                ObfuzResolveUtility.ShowWindow();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLogList()
        {
            // 计算可视区域高度
            float detailsHeight = selectedLog != null ? 180f : 0f;
            float toolbarHeight = EditorStyles.toolbar.fixedHeight;
            float visibleHeight = position.height - toolbarHeight - detailsHeight - 10f;

            scrollPosition = GUILayout.BeginScrollView(scrollPosition,
                GUILayout.Height(Mathf.Max(visibleHeight, 100f)));

            // 创建日志项样式 - 启用富文本
            GUIStyle logStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
                richText = true // 启用富文本支持
            };

            // 计数样式
            GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                fixedWidth = COUNT_WIDTH
            };

            for (int i = 0; i < logs.Count; i++)
            {
                LogEntry log = logs[i];

                // 应用过滤器
                if (!ShouldShow(log)) continue;

                // 应用搜索
                if (!string.IsNullOrEmpty(searchText))
                {
                    // 移除富文本标签进行搜索
                    string plainText = StripRichTextTags(log.message);
                    if (plainText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                // 开始日志项布局
                Rect logRect = GUILayoutUtility.GetRect(position.width, ITEM_HEIGHT);

                // 设置背景颜色
                bool isSelected = selectedLog == log;
                var bgColor = isSelected ? selectedColor : messageColor * ((i % 2 != 0) ? 0.2f : 0.3f);

                // 绘制背景
                EditorGUI.DrawRect(logRect, bgColor);

                // 绘制日志类型图标
                Rect iconRect = new Rect(
                    logRect.x + ICON_PADDING,
                    logRect.y + (ITEM_HEIGHT - ICON_SIZE) / 2,
                    ICON_SIZE,
                    ICON_SIZE
                );
                DrawLogIcon(iconRect, log.type);

                // 计算文本区域
                float textStartX = iconRect.xMax + TEXT_PADDING;
                float textWidth = logRect.width - textStartX - TEXT_PADDING;

                // 如果有计数显示，预留空间
                if (collapse && log.count > 1)
                {
                    textWidth -= COUNT_WIDTH + TEXT_PADDING;
                }

                string displayText = $"[{log.timestamp:HH:mm:ss}]{log.message}";
                // 绘制日志文本 - 支持富文本
                Rect textRect = new Rect(
                    textStartX,
                    logRect.y + TEXT_PADDING,
                    textWidth,
                    ITEM_HEIGHT - TEXT_PADDING * 2
                );

                GUI.Label(textRect, displayText, logStyle);

                // 绘制日志计数（折叠模式）
                if (collapse && log.count > 1)
                {
                    Rect countRect = new Rect(
                        logRect.xMax - COUNT_WIDTH - TEXT_PADDING,
                        logRect.y,
                        COUNT_WIDTH,
                        ITEM_HEIGHT
                    );
                    GUI.Label(countRect, $"{log.count}x", countStyle);
                }

                // 添加分隔线
                if (i < logs.Count - 1)
                {
                    Rect lineRect = new Rect(
                        logRect.x + ICON_PADDING,
                        logRect.yMax - 1,
                        logRect.width - ICON_PADDING * 2,
                        1
                    );
                    EditorGUI.DrawRect(lineRect, new Color(0.2f, 0.2f, 0.2f, 0.2f));
                }

                // 添加透明按钮用于选择
                if (GUI.Button(logRect, "", GUIStyle.none))
                {
                    selectedLog = log;
                    detailsScrollPosition = Vector2.zero;
                }
            }

            // 添加空区域确保所有日志项都能正确显示
            if (logs.Count == 0)
            {
                GUILayout.Label("No logs available", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(50));
            }

            GUILayout.EndScrollView();
        }

        // 移除富文本标签的辅助方法
        private string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return richTextRegex.Replace(text, "");
        }

        private void DrawLogIcon(Rect position, LogType type)
        {
            Texture2D icon = null;
            Color color = Color.white;

            switch (type)
            {
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    icon = EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;
                    color = errorColor;
                    break;

                case LogType.Warning:
                    icon = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
                    color = warningColor;
                    break;

                default:
                    icon = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D;
                    color = messageColor;
                    break;
            }

            if (icon != null)
            {
                GUI.color = color;
                GUI.DrawTexture(position, icon);
                GUI.color = Color.white;
            }
        }

        private void DrawDetailsPanel()
        {
            if (selectedLog == null) return;

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

            detailsScrollPosition = GUILayout.BeginScrollView(detailsScrollPosition,
                GUILayout.Height(150));

            // 详情面板也支持富文本
            GUIStyle richTextStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = true, // 启用富文本
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };

            string details = $"{selectedLog.message}\n\n{selectedLog.stackTrace}";
            EditorGUILayout.TextArea(details, richTextStyle);

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

            // 找到最旧的日志
            LogEntry oldestLog = logs[0];
            int oldestIndex = 0;
            for (int i = 1; i < logs.Count; i++)
            {
                if (logs[i].timestamp < oldestLog.timestamp)
                {
                    oldestLog = logs[i];
                    oldestIndex = i;
                }
            }

            // 从列表中移除
            logs.RemoveAt(oldestIndex);

            // 如果被移除的日志是当前选中的，清除选择
            if (selectedLog == oldestLog)
            {
                selectedLog = null;
            }

            // 如果日志是折叠的，从折叠字典中移除
            if (collapse)
            {
                string key = $"{oldestLog.type}-{oldestLog.message}";
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