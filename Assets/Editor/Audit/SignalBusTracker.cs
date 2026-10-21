using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hypocycloid.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hypocycloid.Editor
{
    /// <summary>
    /// SignalBus 事件总线检查工具
    /// </summary>
    public class SignalBusTracker : EditorWindow
    {
        const string MENU_NAME = EditorCommons.CTX + "Audit/Signal Bus Tracker";

        // Static storage for signal data (replaces SignalHub runtime storage)
        static readonly Dictionary<Type, SignalDebugInfo> signalInfoMap = new();
        static CircularBuffer<SignalTriggerHistory> triggerHistory = new(100);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            signalInfoMap.Clear();
            triggerHistory = new CircularBuffer<SignalTriggerHistory>(100);
        }

        // Random colors of recently triggered signals in the history graph.
        static readonly Color[] k_SignalColors =
        {
            new(0.35f, 0.75f, 1.00f),
            new(1.00f, 0.60f, 0.20f),
            new(0.40f, 1.00f, 0.55f),
            new(1.00f, 0.40f, 0.40f),
            new(0.85f, 0.55f, 1.00f),
            new(1.00f, 1.00f, 0.40f),
            new(0.40f, 0.85f, 0.85f),
            new(1.00f, 0.65f, 0.80f),
        };

        [SerializeField]
        MultiColumnHeaderState multiColumnHeaderState;

        [SerializeField]
        TreeViewState<int> treeViewState;

        SignalTreeView treeView;

        const float refreshInterval = 0.5f;
        bool isAutoRefresh;
        bool isPaused;
        DateTime pauseTime;
        double lastRefreshTime;

        bool isShowHistory = true;

        [SerializeField]
        bool isHistoryExpanded;
        bool showSettings = false;
        bool showStatistics = true;

        [SerializeField]
        int historyCapacity = 100;

        [SerializeField]
        int drawHistoryCount = 50;

        [SerializeField]
        int graphTimeWindowSeconds = 60;

        // -1 = no pin; set when user clicks the graph to freeze-inspect a bucket
        int pinnedGraphBucket = -1;

        // Cached GUI styles — created once, avoids per-repaint GUIStyle allocations
        GUIStyle m_GraphLabelR; // right-aligned miniLabel (Y axis)
        GUIStyle m_GraphLabelC; // center-aligned miniLabel (X axis)
        GUIStyle m_LegendEntry; // mutated per legend entry (color, fontStyle)
        GUIStyle m_PinLabel; // center-aligned + yellow (pinned cursor timestamp)

        // Paging
        [SerializeField]
        int itemsPerPage = 20;

        [SerializeField]
        int currentPage = 0;

        SearchField searchInput;
        string searchString = string.Empty;

        [MenuItem(MENU_NAME)]
        public static void ShowWindow()
        {
            var window = GetWindow<SignalBusTracker>("SignalBus Tracker");
            // Fetch the built-in icon. Note: Use "d_" prefix for better visibility in Dark Theme
            Texture icon = EditorGUIUtility.IconContent("d_AnalyticsTracker Icon").image;
            window.titleContent = new GUIContent("Signal Tracker", icon, "Track SignalBus events in memory");
            window.Show();
        }

        void OnEnable()
        {
            SignalBus.OnSignalFired += OnSignalFired;
            InitTreeView();
            historyCapacity = triggerHistory.Capacity;
        }

        void OnDisable()
        {
            SignalBus.OnSignalFired -= OnSignalFired;
        }

        void OnDestroy()
        {
            SignalBus.OnSignalFired -= OnSignalFired;
        }

        void OnSignalFired(Type signalType, int subscriberCount, Object invoker)
        {
            if (isPaused)
                return;
            RegisterSignalEvent(signalType, subscriberCount, invoker);
        }

        void Update()
        {
            if (isAutoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                Repaint();
                lastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        /// <summary>
        /// Registers signal event data
        /// </summary>
        static void RegisterSignalEvent(Type signalType, int subscriberCount, Object invoker = null)
        {
            if (!signalInfoMap.TryGetValue(signalType, out var info))
            {
                info = new SignalDebugInfo { SignalType = signalType, SignalName = signalType.Name };
                signalInfoMap[signalType] = info;
            }

            info.SubscriberCount = subscriberCount;
            info.LastUpdateTime = DateTime.Now;

            if (invoker != null)
            {
                info.LastInvoker = invoker;
                info.TriggerCount++;

                triggerHistory.Add(
                    new SignalTriggerHistory
                    {
                        SignalType = signalType,
                        Invoker = invoker,
                        TriggerTime = DateTime.Now,
                    }
                );
            }

            signalInfoMap[signalType] = info;
        }

        void InitTreeView()
        {
            if (treeView != null)
                return;

            treeViewState ??= new TreeViewState<int>();

            bool firstInit = multiColumnHeaderState == null;
            var headerState = SignalTreeView.CreateDefaultMultiColumnHeaderState(position.width);
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(multiColumnHeaderState, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(multiColumnHeaderState, headerState);
            multiColumnHeaderState = headerState;

            var multiColumnHeader = new MultiColumnHeader(multiColumnHeaderState);
            if (firstInit)
                multiColumnHeader.ResizeToFit();

            treeView = new SignalTreeView(treeViewState, multiColumnHeader);
            searchInput = new SearchField();
            searchInput.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;

            treeView.Reload();
        }

        void OnGUI()
        {
            InitTreeView();

            DrawToolbar();
            DrawSearchBar();

            if (showSettings)
                DrawSettings();
            if (showStatistics)
                DrawStatistics();

            DrawContent();
        }

        void DrawSearchBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUIUtility.labelWidth = 90;
            EditorGUI.BeginChangeCheck();
            int newPageSize = EditorGUILayout.IntField(
                "Items/Page",
                itemsPerPage,
                EditorStyles.toolbarTextField,
                GUILayout.Width(130)
            );
            if (EditorGUI.EndChangeCheck())
            {
                if (newPageSize < 1)
                    newPageSize = 1;
                itemsPerPage = newPageSize;
                currentPage = 0;
            }
            EditorGUIUtility.labelWidth = 0;

            EditorGUI.BeginChangeCheck();
            searchString = searchInput.OnToolbarGUI(searchString);
            if (EditorGUI.EndChangeCheck())
            {
                currentPage = 0;
            }
            GUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh Now", EditorStyles.toolbarButton))
            {
                treeView.Reload();
            }

            isAutoRefresh = GUILayout.Toggle(isAutoRefresh, "Auto Refresh", EditorStyles.toolbarButton);

            EditorGUI.BeginChangeCheck();
            bool newPaused = GUILayout.Toggle(isPaused, "Pause Tracking", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                isPaused = newPaused;
                if (isPaused)
                    pauseTime = DateTime.Now;
                else
                    pinnedGraphBucket = -1;
            }

            GUILayout.Space(10);
            GUILayout.Label("History:", EditorStyles.miniLabel);
            isShowHistory = GUILayout.Toggle(isShowHistory, "Show Recent", EditorStyles.toolbarButton);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                showSettings = !showSettings;
            }

            if (GUILayout.Button("Statistics", EditorStyles.toolbarButton))
            {
                showStatistics = !showStatistics;
            }

            if (GUILayout.Button("Clear History", EditorStyles.toolbarButton))
            {
                signalInfoMap.Clear();
                triggerHistory.Clear();
                treeView.Reload();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Export to Markdown", EditorStyles.toolbarButton))
            {
                ExportToMarkdown();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawSettings()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(10);

            EditorGUIUtility.labelWidth = 100;

            // Capacity
            EditorGUI.BeginChangeCheck();
            int newCap = EditorGUILayout.IntField(
                "History Capacity",
                historyCapacity,
                EditorStyles.toolbarTextField,
                GUILayout.Width(150)
            );
            if (EditorGUI.EndChangeCheck())
            {
                if (newCap < 1)
                    newCap = 1;
                historyCapacity = newCap;
                ResizeHistory(historyCapacity);
            }

            GUILayout.Space(10);

            // Draw Count
            EditorGUIUtility.labelWidth = 80;
            drawHistoryCount = EditorGUILayout.IntField(
                "Draw Count",
                drawHistoryCount,
                EditorStyles.toolbarTextField,
                GUILayout.Width(130)
            );

            EditorGUIUtility.labelWidth = 0;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawStatistics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("📊 Signal Statistics", EditorStyles.boldLabel);

            var stats = CalculateStatistics();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Total Signals:", stats.TotalSignals.ToString());
            EditorGUILayout.LabelField("Total Subscribers:", stats.TotalSubscribers.ToString());
            EditorGUILayout.LabelField("Total Triggers:", stats.TotalTriggers.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Signals (>0 subs):", stats.ActiveSignals.ToString());
            EditorGUILayout.LabelField("Avg Subscribers/Signal:", stats.AvgSubscribersPerSignal.ToString("F2"));
            EditorGUILayout.LabelField("Avg Triggers/Signal:", stats.AvgTriggersPerSignal.ToString("F2"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (stats.MostSubscribedSignal != null)
            {
                EditorGUILayout.LabelField(
                    $"Most Subscribed: {stats.MostSubscribedSignal} ({stats.MaxSubscribers} subs)"
                );
            }
            if (stats.MostTriggeredSignal != null)
            {
                EditorGUILayout.LabelField(
                    $"Most Triggered: {stats.MostTriggeredSignal} ({stats.MaxTriggers} triggers)"
                );
                EditorGUILayout.LabelField(string.Empty); // Spacer
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        static void ResizeHistory(int newCapacity)
        {
            if (newCapacity == triggerHistory.Capacity)
                return;

            var allItems = triggerHistory.GetRecentItems(triggerHistory.Count).ToList();
            var itemsToKeep = allItems.Take(newCapacity).ToList();

            triggerHistory = new CircularBuffer<SignalTriggerHistory>(newCapacity);

            // Add in reverse order (Oldest -> Newest) so they end up in correct order in the new buffer
            for (int i = itemsToKeep.Count - 1; i >= 0; i--)
            {
                triggerHistory.Add(itemsToKeep[i]);
            }
        }

        SignalStatistics CalculateStatistics()
        {
            var stats = new SignalStatistics
            {
                TotalSignals = signalInfoMap.Count,
                TotalSubscribers = signalInfoMap.Values.Sum(x => x.SubscriberCount),
                TotalTriggers = signalInfoMap.Values.Sum(x => x.TriggerCount),
                ActiveSignals = signalInfoMap.Values.Count(x => x.SubscriberCount > 0),
            };

            if (stats.TotalSignals > 0)
            {
                stats.AvgSubscribersPerSignal = (float)stats.TotalSubscribers / stats.TotalSignals;
                stats.AvgTriggersPerSignal = (float)stats.TotalTriggers / stats.TotalSignals;
            }

            var mostSubscribed = signalInfoMap.Values.OrderByDescending(x => x.SubscriberCount).FirstOrDefault();
            if (mostSubscribed.SubscriberCount > 0)
            {
                stats.MostSubscribedSignal = mostSubscribed.SignalName;
                stats.MaxSubscribers = mostSubscribed.SubscriberCount;
            }

            var mostTriggered = signalInfoMap.Values.OrderByDescending(x => x.TriggerCount).FirstOrDefault();
            if (mostTriggered.TriggerCount > 0)
            {
                stats.MostTriggeredSignal = mostTriggered.SignalName;
                stats.MaxTriggers = mostTriggered.TriggerCount;
            }

            return stats;
        }

        void ExportToMarkdown()
        {
            string path = EditorUtility.SaveFilePanel("Export to Markdown", "", "SignalBus_Report", "md");
            if (string.IsNullOrEmpty(path))
                return;

            StringBuilder sb = new();
            var stats = CalculateStatistics();

            sb.AppendLine("# Signal Bus Report");
            sb.AppendLine($"> Generated: {DateTime.Now}");
            sb.AppendLine();

            // Statistics Section
            sb.AppendLine("## 📊 Statistics");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Total Signals Registered | {stats.TotalSignals} |");
            sb.AppendLine($"| Total Subscribers | {stats.TotalSubscribers} |");
            sb.AppendLine($"| Total Triggers | {stats.TotalTriggers} |");
            sb.AppendLine($"| Active Signals (>0 subscribers) | {stats.ActiveSignals} |");
            sb.AppendLine($"| Avg Subscribers per Signal | {stats.AvgSubscribersPerSignal:F2} |");
            sb.AppendLine($"| Avg Triggers per Signal | {stats.AvgTriggersPerSignal:F2} |");

            if (!string.IsNullOrEmpty(stats.MostSubscribedSignal))
            {
                sb.AppendLine(
                    $"| Most Subscribed Signal | {stats.MostSubscribedSignal} ({stats.MaxSubscribers} subs) |"
                );
            }

            if (!string.IsNullOrEmpty(stats.MostTriggeredSignal))
            {
                sb.AppendLine(
                    $"| Most Triggered Signal | {stats.MostTriggeredSignal} ({stats.MaxTriggers} triggers) |"
                );
            }

            sb.AppendLine();

            // Detailed Signal Status
            sb.AppendLine("## Signals (Grouped)");
            sb.AppendLine("```text");

            var signalInfos = signalInfoMap
                .Values.OrderByDescending(x => x.TriggerCount)
                .ThenBy(x => x.SignalName)
                .ToList();

            if (signalInfos.Count == 0)
            {
                sb.AppendLine("  No signals registered yet.");
            }
            else
            {
                foreach (var info in signalInfos)
                {
                    sb.AppendLine($"◆ {info.SignalName}");

                    // Fetch data
                    var subscribers = SignalBus.GetSubscribers(info.SignalType);
                    var signalHistory = triggerHistory
                        .GetRecentItems(triggerHistory.Count)
                        .Where(x => x.SignalType == info.SignalType)
                        .Take(10)
                        .ToList();

                    bool hasSubs = subscribers != null && subscribers.Count > 0;
                    bool hasHistory = signalHistory.Count > 0;
                    bool hasChildren = hasSubs || hasHistory;

                    sb.AppendLine($"   ├─ Subscribers: {info.SubscriberCount}");
                    sb.AppendLine($"   ├─ Trigger Count: {info.TriggerCount}");

                    // Last Invoker
                    string invokerName = info.LastInvoker != null ? info.LastInvoker.name : "None";
                    string timeStr = "";
                    if (info.LastInvoker != null)
                    {
                        var elapsed = DateTime.Now - info.LastUpdateTime;
                        timeStr =
                            elapsed.TotalSeconds < 60
                                ? $" ({elapsed.TotalSeconds:F1}s ago)"
                                : $" ({elapsed.TotalMinutes:F1}m ago)";
                    }

                    string lastTriggerBranch = hasChildren ? "├─" : "└─";
                    sb.AppendLine($"   {lastTriggerBranch} Last Trigger: {invokerName}{timeStr}");

                    // Subscribers Branch
                    if (hasSubs && subscribers != null)
                    {
                        string branch = hasHistory ? "├─" : "└─";
                        string vLine = hasHistory ? "│" : " ";
                        sb.AppendLine($"   {branch} Active Subscribers:");

                        for (int i = 0; i < subscribers.Count; i++)
                        {
                            var sub = subscribers[i];
                            bool isLast = i == subscribers.Count - 1;
                            var prefix = isLast ? "└─" : "├─";

                            string type = sub.IsAsync ? "Async" : "Sync";
                            string owner = sub.Owner != null ? sub.Owner.name : "null (Destroyed)";
                            sb.AppendLine($"   {vLine}  {prefix} {owner} [{type}]");
                        }
                    }

                    // Signal-Specific History Branch
                    if (hasHistory)
                    {
                        sb.AppendLine($"   └─ Recent Triggers:");
                        for (int i = 0; i < signalHistory.Count; i++)
                        {
                            var h = signalHistory[i];
                            bool isLast = i == signalHistory.Count - 1;
                            var prefix = isLast ? "      └─" : "      ├─";
                            sb.AppendLine($"{prefix} [{h.TriggerTime:HH:mm:ss.fff}] {h.Invoker?.name ?? "Unknown"}");
                        }
                    }

                    sb.AppendLine();
                }
            }
            sb.AppendLine("```");
            sb.AppendLine();

            // Global Trigger History
            sb.AppendLine("## History");
            sb.AppendLine("> Sorted by time DESC");
            sb.AppendLine("```text");

            var history = triggerHistory.GetRecentItems(historyCapacity);
            foreach (var h in history)
            {
                string time = $"[{h.TriggerTime:HH:mm:ss.fff}] ";
                string invoker = h.Invoker != null ? h.Invoker.name : "Unknown";
                sb.AppendLine($"  {time}{h.SignalType.Name} ← {invoker}");
            }
            sb.AppendLine("```");

            try
            {
                File.WriteAllText(path, sb.ToString());
                EditorUtility.RevealInFinder(path);
                Debug.Log($"[Tracker] Report exported to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Tracker] Failed to export report: {ex.Message}");
            }
        }

        void DrawContent()
        {
            treeView.ItemsPerPage = itemsPerPage;
            treeView.SetSignalData(signalInfoMap, searchString, isPaused ? pauseTime : DateTime.Now);

            int totalPages = treeView.TotalPages;
            if (currentPage >= totalPages && totalPages > 0)
                currentPage = totalPages - 1;
            if (currentPage < 0)
                currentPage = 0;

            treeView.CurrentPage = currentPage;
            treeView.Reload();

            float historyCollapsedHeight = EditorGUIUtility.singleLineHeight + 6f;
            float historyExpandedHeight = 220f;
            float historyHeight = isShowHistory
                ? (isHistoryExpanded ? historyExpandedHeight : historyCollapsedHeight)
                : 0f;
            float paginationHeight = 25f;
            float toolbarHeight = EditorStyles.toolbar.fixedHeight;
            float searchBarHeight = toolbarHeight;
            float settingsHeight = showSettings ? toolbarHeight : 0f;
            float statsHeight = 0f;
            if (showStatistics)
            {
                var helpBoxStyle = EditorStyles.helpBox;
                statsHeight =
                    (EditorGUIUtility.singleLineHeight * 4f)
                    + helpBoxStyle.padding.vertical
                    + helpBoxStyle.margin.vertical;
            }
            float treeHeight =
                position.height
                - toolbarHeight
                - 6
                - historyHeight
                - paginationHeight
                - searchBarHeight
                - settingsHeight
                - statsHeight;
            treeHeight = Mathf.Max(50f, treeHeight);

            // Tree View
            Rect treeRect = EditorGUILayout.GetControlRect(false, treeHeight);
            treeView.OnGUI(treeRect);

            // Pagination Controls
            DrawPagination(paginationHeight);

            // History View
            if (isShowHistory)
            {
                DrawHistoryPanel(historyHeight, historyCollapsedHeight);
            }
        }

        void DrawPagination(float height)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(height));

            GUILayout.FlexibleSpace();

            int totalPages = treeView.TotalPages;

            // Should normalize current page in case total pages changed
            if (currentPage >= totalPages && totalPages > 0)
                currentPage = totalPages - 1;
            if (currentPage < 0)
                currentPage = 0;

            EditorGUI.BeginDisabledGroup(currentPage == 0);
            if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                currentPage--;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Label($"Page {currentPage + 1} / {Math.Max(1, totalPages)}", EditorStyles.miniLabel);

            EditorGUI.BeginDisabledGroup(currentPage >= totalPages - 1);
            if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                currentPage++;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHistoryPanel(float height, float collapsedHeight)
        {
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            isHistoryExpanded = EditorGUILayout.Foldout(
                isHistoryExpanded,
                $"Recent Trigger History ({triggerHistory.Count} events)",
                true
            );
            if (!isHistoryExpanded)
                return;

            // Time-window selector
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Window:", EditorStyles.miniLabel, GUILayout.Width(52));
            if (GUILayout.Toggle(graphTimeWindowSeconds == 10, "10s", EditorStyles.toolbarButton, GUILayout.Width(32)))
                graphTimeWindowSeconds = 10;
            if (GUILayout.Toggle(graphTimeWindowSeconds == 30, "30s", EditorStyles.toolbarButton, GUILayout.Width(32)))
                graphTimeWindowSeconds = 30;
            if (GUILayout.Toggle(graphTimeWindowSeconds == 60, "60s", EditorStyles.toolbarButton, GUILayout.Width(32)))
                graphTimeWindowSeconds = 60;
            if (GUILayout.Toggle(graphTimeWindowSeconds == 300, "5m", EditorStyles.toolbarButton, GUILayout.Width(32)))
                graphTimeWindowSeconds = 300;
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{triggerHistory.Count}/{triggerHistory.Capacity} buffered", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            float toolbarH = EditorStyles.toolbar.fixedHeight;
            float graphH = Mathf.Max(0f, height - collapsedHeight - toolbarH);

            DrawTriggerGraph(GUILayoutUtility.GetRect(0f, float.MaxValue, graphH, graphH));
        }

        void DrawTriggerGraph(Rect graphRect)
        {
            if (graphRect.width < 20 || graphRect.height < 20)
                return;

            bool isRepaint = Event.current.type == EventType.Repaint;

            // Lazy-init cached styles (null after domain reload or first call)
            m_GraphLabelR ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            m_GraphLabelC ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            m_LegendEntry ??= new GUIStyle(EditorStyles.miniLabel);
            m_PinLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.88f, 0.25f) },
            };

            const float padL = 36f,
                padR = 8f,
                padT = 8f,
                padB = 20f;
            Rect plot =
                new(
                    graphRect.x + padL,
                    graphRect.y + padT,
                    graphRect.width - padL - padR,
                    graphRect.height - padT - padB
                );

            if (isRepaint)
            {
                // Background
                EditorGUI.DrawRect(graphRect, new Color(0.13f, 0.13f, 0.13f));
                EditorGUI.DrawRect(plot, new Color(0.09f, 0.09f, 0.09f));
            }

            var now = isPaused ? pauseTime : DateTime.Now;
            var cutoff = SafeAddSeconds(now, -graphTimeWindowSeconds);

            var allHistory = triggerHistory
                .GetRecentItems(triggerHistory.Count)
                .Where(h => h.TriggerTime >= cutoff)
                .ToList();

            // Top N signals by total trigger count
            var sigTypes = allHistory
                .Select(h => h.SignalType)
                .Distinct()
                .OrderByDescending(t => signalInfoMap.TryGetValue(t, out var si) ? si.TriggerCount : 0)
                .Take(k_SignalColors.Length)
                .ToList();

            int bucketCount = Mathf.Max(30, Mathf.FloorToInt(plot.width / 5f));

            // Build bucket arrays — single pass over history, no per-signal inner loop
            int globalMax = 1;
            var series = new (Type type, int[] data, Color color)[sigTypes.Count];
            for (int s = 0; s < sigTypes.Count; s++)
                series[s] = (sigTypes[s], new int[bucketCount], k_SignalColors[s]);

            foreach (var h in allHistory)
            {
                int si = sigTypes.IndexOf(h.SignalType); // O(N), N <= 8
                if (si < 0)
                    continue;
                double frac = (h.TriggerTime - cutoff).TotalSeconds / graphTimeWindowSeconds;
                int b = Mathf.Clamp((int)(frac * bucketCount), 0, bucketCount - 1);
                series[si].data[b]++;
            }
            // Compute global max in the same O(S*B) pass instead of calling .Max() per signal
            for (int s = 0; s < series.Length; s++)
                for (int b = 0; b < bucketCount; b++)
                    if (series[s].data[b] > globalMax)
                        globalMax = series[s].data[b];

            if (isRepaint)
            {
                // Grid
                for (int i = 0; i <= 4; i++)
                {
                    float fy = (float)i / 4;
                    float gy = plot.yMax - fy * plot.height;
                    EditorGUI.DrawRect(
                        new Rect(plot.x, gy - 0.5f, plot.width, 1),
                        new Color(0.28f, 0.28f, 0.28f, 0.6f)
                    );
                    GUI.Label(
                        new Rect(graphRect.x, gy - 7f, padL - 4f, 14f),
                        Mathf.RoundToInt(fy * globalMax).ToString(),
                        m_GraphLabelR
                    );
                }
                for (int i = 0; i <= 4; i++)
                {
                    float fx = (float)i / 4;
                    float gx = plot.x + fx * plot.width;
                    EditorGUI.DrawRect(
                        new Rect(gx - 0.5f, plot.y, 1, plot.height),
                        new Color(0.28f, 0.28f, 0.28f, 0.4f)
                    );
                    double secOff = (fx - 1.0) * graphTimeWindowSeconds;
                    string xLabel =
                        secOff == 0 ? "now"
                        : graphTimeWindowSeconds >= 120 ? $"{Mathf.Abs((int)(secOff / 60))}m"
                        : $"{(int)secOff}s";
                    GUI.Label(new Rect(gx - 20f, plot.yMax + 3f, 40f, 14f), xLabel, m_GraphLabelC);
                }

                // Fill bars (per bucket, semi-transparent)
                float bw = plot.width / bucketCount;
                foreach (var (_, data, color) in series)
                {
                    for (int i = 0; i < bucketCount; i++)
                    {
                        if (data[i] == 0)
                            continue;
                        float bh = (float)data[i] / globalMax * plot.height;
                        EditorGUI.DrawRect(
                            new Rect(plot.x + i * bw, plot.yMax - bh, bw, bh),
                            new Color(color.r, color.g, color.b, 0.18f)
                        );
                    }
                }

                // Pinned bucket column tint
                if (pinnedGraphBucket >= 0)
                    EditorGUI.DrawRect(
                        new Rect(plot.x + pinnedGraphBucket * bw, plot.y, bw, plot.height),
                        new Color(1f, 0.92f, 0.3f, 0.07f)
                    );

                // Lines + border
                Handles.BeginGUI();
                foreach (var (_, data, color) in series)
                {
                    var pts = new Vector3[bucketCount];
                    for (int i = 0; i < bucketCount; i++)
                    {
                        pts[i] = new Vector3(
                            plot.x + (i + 0.5f) / bucketCount * plot.width,
                            plot.yMax - (float)data[i] / globalMax * plot.height,
                            0f
                        );
                    }
                    Handles.color = color;
                    Handles.DrawAAPolyLine(2.5f, pts);
                }
                Handles.color = new Color(0.35f, 0.35f, 0.35f);
                Handles.DrawSolidRectangleWithOutline(plot, Color.clear, new Color(0.35f, 0.35f, 0.35f));
                Handles.EndGUI();

                // Legend (top-right of plot area)
                if (series.Length > 0)
                {
                    float lx = plot.xMax - 154f;
                    float ly = plot.y + 4f;
                    int drawn = 0;
                    for (int i = 0; i < series.Length; i++)
                    {
                        var (t, data, color) = series[i];
                        string label;

                        if (pinnedGraphBucket >= 0)
                        {
                            // Pinned: only show signals that fired in this bucket
                            int count = data[pinnedGraphBucket];
                            if (count == 0)
                                continue;
                            label = $"{t.Name}  x{count}";
                            m_LegendEntry.fontStyle = FontStyle.Bold;
                        }
                        else
                        {
                            // Default: show all signals with their total count in the visible window
                            int windowTotal = 0;
                            for (int b = 0; b < bucketCount; b++)
                                windowTotal += data[b];
                            label = $"{t.Name}  {windowTotal}";
                            m_LegendEntry.fontStyle = FontStyle.Normal;
                        }

                        float lineY = ly + drawn++ * 14f;
                        EditorGUI.DrawRect(new Rect(lx, lineY + 5f, 10f, 3f), color);
                        m_LegendEntry.normal.textColor = color;
                        GUI.Label(new Rect(lx + 14f, lineY, 140f, 14f), label, m_LegendEntry);
                    }
                }

                // No-data message
                if (allHistory.Count == 0)
                    GUI.Label(plot, "No trigger events in window", EditorStyles.centeredGreyMiniLabel);

                // Hover: tooltip + cursor line
                var mouse = Event.current.mousePosition;
                if (plot.Contains(mouse) && series.Length > 0 && allHistory.Count > 0)
                {
                    int hb = Mathf.Clamp((int)((mouse.x - plot.x) / plot.width * bucketCount), 0, bucketCount - 1);
                    double secPerBucket = (double)graphTimeWindowSeconds / bucketCount;
                    double t0 = hb * secPerBucket - graphTimeWindowSeconds;
                    double t1 = (hb + 1) * secPerBucket - graphTimeWindowSeconds;

                    var sb = new StringBuilder();
                    sb.Append($"t: {t0:+0.0;-0.0}s ~ {t1:+0.0;-0.0}s\n");
                    int lineCount = 1;
                    bool hasData = false;
                    foreach (var (type, data, _) in series)
                    {
                        if (data[hb] > 0)
                        {
                            sb.Append($"{type.Name}: {data[hb]}\n");
                            lineCount++;
                            hasData = true;
                        }
                    }

                    if (hasData)
                    {
                        string tip = sb.ToString().TrimEnd();
                        float tx = Mathf.Min(mouse.x + 12f, graphRect.xMax - 160f);
                        float ty = Mathf.Max(mouse.y - 14f * lineCount - 8f, graphRect.y);
                        GUI.Label(new Rect(tx, ty, 160f, 14f * lineCount + 8f), tip, EditorStyles.helpBox);
                    }

                    // Hover cursor line (dim white, only when no bucket is pinned)
                    if (pinnedGraphBucket < 0)
                    {
                        Handles.BeginGUI();
                        Handles.color = new Color(1f, 1f, 1f, 0.22f);
                        float cx = plot.x + (hb + 0.5f) / bucketCount * plot.width;
                        Handles.DrawLine(new Vector3(cx, plot.y, 0), new Vector3(cx, plot.yMax, 0));
                        Handles.EndGUI();
                    }
                }

                // Pinned cursor line — permanent yellow marker with wall-clock timestamp
                if (pinnedGraphBucket >= 0 && allHistory.Count > 0)
                {
                    float pcx = plot.x + (pinnedGraphBucket + 0.5f) / bucketCount * plot.width;
                    double pinCenterSec =
                        (pinnedGraphBucket + 0.5) / bucketCount * graphTimeWindowSeconds - graphTimeWindowSeconds;
                    DateTime pinTime = SafeAddSeconds(isPaused ? pauseTime : DateTime.Now, pinCenterSec);
                    GUI.Label(new Rect(pcx - 32f, plot.y + 2f, 64f, 12f), pinTime.ToString("HH:mm:ss"), m_PinLabel);
                    Handles.BeginGUI();
                    Handles.color = new Color(1f, 0.88f, 0.25f, 0.8f);
                    Handles.DrawLine(new Vector3(pcx, plot.y + 14f, 0), new Vector3(pcx, plot.yMax, 0));
                    Handles.EndGUI();
                }
            }

            // Operations inside diagram
            if (graphRect.Contains(Event.current.mousePosition))
            {
                // Unified click: legend label -> copy name; plot area -> pin/unpin bucket
                if (Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        var mp = Event.current.mousePosition;
                        bool handled = false;

                        // Legend labels take priority — use same drawn-index logic as rendering
                        if (!handled && series.Length > 0)
                        {
                            float lx = plot.xMax - 154f;
                            float ly = plot.y + 4f;
                            int drawn = 0;
                            for (int i = 0; i < series.Length; i++)
                            {
                                var (t, data, _) = series[i];
                                if (pinnedGraphBucket >= 0 && data[pinnedGraphBucket] == 0)
                                    continue;
                                if (new Rect(lx, ly + drawn++ * 14f, 154f, 14f).Contains(mp))
                                {
                                    EditorGUIUtility.systemCopyBuffer = t.Name;
                                    Event.current.Use();
                                    handled = true;
                                    break;
                                }
                            }
                        }

                        // Plot click: pin or toggle off a bucket; auto-pause on first pin
                        if (!handled && plot.Contains(mp))
                        {
                            int clickedBucket = Mathf.Clamp(
                                (int)((mp.x - plot.x) / plot.width * bucketCount),
                                0,
                                bucketCount - 1
                            );
                            if (pinnedGraphBucket == clickedBucket)
                            {
                                pinnedGraphBucket = -1;
                            }
                            else
                            {
                                if (!isPaused)
                                {
                                    isPaused = true;
                                    pauseTime = DateTime.Now;
                                }
                                pinnedGraphBucket = clickedBucket;
                            }
                            Event.current.Use();
                            Repaint();
                        }
                    }
                    else
                    {
                        // Click anywhere in the graph rect to unpause and clear pin
                        isPaused = false;
                        pinnedGraphBucket = -1;
                        Event.current.Use();
                        Repaint();
                    }
                }

                // Keep tooltip responsive on mouse move
                if (Event.current.type == EventType.MouseMove)
                    Repaint();
            }
        }

        // Clamps seconds before adding so the result never leaves the valid DateTime range.
        static DateTime SafeAddSeconds(DateTime dt, double seconds)
        {
            if (seconds >= 0)
                return dt.AddSeconds(Math.Min(seconds, (DateTime.MaxValue - dt).TotalSeconds));
            return dt.AddSeconds(Math.Max(seconds, -(dt - DateTime.MinValue).TotalSeconds));
        }
    }

    #region Data Structures (Moved from SignalHub)

    public struct SignalStatistics
    {
        public int TotalSignals;
        public int TotalSubscribers;
        public int TotalTriggers;
        public int ActiveSignals;
        public float AvgSubscribersPerSignal;
        public float AvgTriggersPerSignal;
        public string MostSubscribedSignal;
        public int MaxSubscribers;
        public string MostTriggeredSignal;
        public int MaxTriggers;
    }

    public struct SignalDebugInfo
    {
        public Type SignalType;
        public string SignalName;
        public int SubscriberCount;
        public int TriggerCount;
        public Object LastInvoker;
        public DateTime LastUpdateTime;
    }

    public struct SignalTriggerHistory
    {
        public Type SignalType;
        public Object Invoker;
        public DateTime TriggerTime;
    }

    #endregion

    internal class SignalTreeViewItem : TreeViewItem<int>
    {
        public SignalDebugInfo Info;
        public HandlerDebugInfo? SubscriberInfo;
        public bool IsSubscriberRow;

        public SignalTreeViewItem(int id, SignalDebugInfo info)
            : base(id)
        {
            Info = info;
            displayName = "";
            IsSubscriberRow = false;
        }

        public SignalTreeViewItem(int id, HandlerDebugInfo subInfo)
            : base(id)
        {
            SubscriberInfo = subInfo;
            displayName = "";
            IsSubscriberRow = true;
        }
    }

    internal class SignalTreeView : TreeView<int>
    {
        List<SignalDebugInfo> m_SignalInfos = new();
        DateTime m_ReferenceTime;

        enum Columns
        {
            Name,
            Subscribers,
            Triggers,
            LastInvoker,
            LastTime,
        }

        public SignalTreeView(TreeViewState<int> state, MultiColumnHeader header)
            : base(state, header)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = 20;
            // Enable expanding rows
            extraSpaceBeforeIconAndLabel = 18;
        }

        public void SetSignalData(Dictionary<Type, SignalDebugInfo> data, string searchString, DateTime referenceTime)
        {
            m_ReferenceTime = referenceTime;
            // Default sort: TriggerCount descending, then Name
            var query = data.Values.OrderByDescending(x => x.TriggerCount).ThenBy(x => x.SignalName).AsEnumerable();

            if (!string.IsNullOrEmpty(searchString))
            {
                // Fuzzy search: split search string by spaces
                var keywords = searchString.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                query = query.Where(x =>
                {
                    string nameLower = x.SignalName.ToLower();
                    return keywords.All(k => nameLower.Contains(k));
                });
            }

            m_SignalInfos = query.ToList();
        }

        public int ItemsPerPage { get; set; } = 20;
        public int CurrentPage { get; set; } = 0;
        public int TotalPages => Mathf.CeilToInt((float)m_SignalInfos.Count / ItemsPerPage);

        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int>
            {
                id = -1,
                depth = -1,
                displayName = "Root",
            };
            var items = new List<TreeViewItem<int>>();

            // Apply Paging
            int startIndex = CurrentPage * ItemsPerPage;
            var pagedSignalInfos = m_SignalInfos.Skip(startIndex).Take(ItemsPerPage).ToList();

            int id = 0;
            foreach (var info in pagedSignalInfos)
            {
                var signalItem = new SignalTreeViewItem(id++, info) { depth = 0 };
                items.Add(signalItem);

                // Check if this item is expanded
                if (IsExpanded(signalItem.id))
                {
                    // Add subscribers as children if expanded (or just add them and let TreeView handle expansion)
                    // To make this performant we might want to only fetch subscribers for expanded items, but for now let's just fetch them.
                    var subscribers = SignalBus.GetSubscribers(info.SignalType);
                    if (subscribers != null && subscribers.Count > 0)
                    {
                        foreach (var sub in subscribers)
                        {
                            var subItem = new SignalTreeViewItem(id++, sub) { depth = 1 };
                            items.Add(subItem);
                        }
                    }
                }
                else
                {
                    // If not expanded, we still need to tell TreeView it has children so the expand arrow appears
                    if (info.SubscriberCount > 0)
                    {
                        // Add dummy child so arrow appears
                        items.Add(new SignalTreeViewItem(id++, info) { depth = 1, displayName = "" });
                    }
                }
            }

            SetupParentsAndChildrenFromDepths(root, items);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as SignalTreeViewItem;
            if (item == null)
                return;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (Columns)args.GetColumn(i), args);
            }
        }

        void CellGUI(Rect cellRect, SignalTreeViewItem item, Columns column, RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            if (item.IsSubscriberRow && item.SubscriberInfo.HasValue)
            {
                var sub = item.SubscriberInfo.Value;
                switch (column)
                {
                    case Columns.Name:
                        // Indent subscriber name
                        float subIndent = GetContentIndent(item);
                        Rect subLabelRect = cellRect;
                        subLabelRect.xMin += subIndent;

                        string subName = sub.Owner ? sub.Owner.name : "null";
                        EditorGUI.SelectableLabel(subLabelRect, subName, EditorStyles.label);
                        break;
                    case Columns.Subscribers:
                        // Show async status or method name maybe?
                        EditorGUI.SelectableLabel(cellRect, sub.IsAsync ? "Async" : "Sync");
                        break;
                    case Columns.LastInvoker:
                        if (sub.Owner != null)
                            EditorGUI.ObjectField(cellRect, sub.Owner, typeof(Object), true);
                        else
                            EditorGUI.SelectableLabel(cellRect, "null");
                        break;
                    default:
                        // Empty for other columns
                        break;
                }
            }
            else
            {
                // Signal Row
                var info = item.Info;
                switch (column)
                {
                    case Columns.Name:
                        float indent = GetContentIndent(item);
                        Rect labelRect = cellRect;
                        labelRect.xMin += indent;

                        args.rowRect = cellRect;
                        base.RowGUI(args);

                        EditorGUI.SelectableLabel(labelRect, info.SignalName, EditorStyles.label);
                        break;
                    case Columns.Subscribers:
                        EditorGUI.SelectableLabel(cellRect, info.SubscriberCount.ToString());
                        break;
                    case Columns.Triggers:
                        EditorGUI.SelectableLabel(cellRect, info.TriggerCount.ToString());
                        break;
                    case Columns.LastInvoker:
                        if (info.LastInvoker != null)
                            EditorGUI.ObjectField(cellRect, info.LastInvoker, typeof(Object), true);
                        else
                            EditorGUI.SelectableLabel(cellRect, "None");
                        break;
                    case Columns.LastTime:
                        var elapsed = m_ReferenceTime - info.LastUpdateTime;
                        string timeStr =
                            elapsed.TotalSeconds < 60
                                ? $"{elapsed.TotalSeconds:F1}s ago"
                                : $"{elapsed.TotalMinutes:F1}m ago";

                        if (info.TriggerCount == 0)
                            timeStr = "-";
                        EditorGUI.SelectableLabel(cellRect, timeStr);
                        break;
                }
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            // If we wanted to do something on selection, we could do it here
            // var item = FindItem(selectedIds[0], rootItem) as SignalTreeViewItem;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Signal Name"),
                    headerTextAlignment = TextAlignment.Left,
                    width = 200,
                    minWidth = 100,
                    autoResize = true,
                    allowToggleVisibility = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Subs | Status"),
                    headerTextAlignment = TextAlignment.Left,
                    width = 50,
                    minWidth = 40,
                    autoResize = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Triggers"),
                    headerTextAlignment = TextAlignment.Left,
                    width = 60,
                    minWidth = 50,
                    autoResize = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Last Invoker"),
                    headerTextAlignment = TextAlignment.Left,
                    width = 150,
                    minWidth = 100,
                    autoResize = true,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Last Active"),
                    headerTextAlignment = TextAlignment.Left,
                    width = 80,
                    minWidth = 80,
                    autoResize = false,
                },
            };

            return new MultiColumnHeaderState(columns);
        }
    }
}
