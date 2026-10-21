using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hypocycloid.Core;
using Baseline.Defs;
using Baseline.Dialogue;
using Baseline.Presentation;
using Baseline.Quests;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Baseline.Editor
{
    public sealed class QuestDebugger : EditorWindow, IQuestRunObserver
    {
        [SerializeField] QuestDef selected;
        [SerializeField] DialogDef dialog;
        [SerializeField] int tab, locale, line;
        [SerializeField] bool compact, autoVoice;
        [SerializeField] string signal = QuestRun.BeginSignal;
        [SerializeField] int amount = 1;
        QuestAuditReport report;
        QuestPreviewSession preview;
        IQuestRunObserver previousObserver;
        readonly List<string> trace = new();
        readonly Dictionary<QuestRun, int> depths = new();
        Vector2 scroll, traceScroll;
        string error;
        bool auditioning;

        [MenuItem("Tools/Hypocycloid/Audit/Quests")]
        [MenuItem("Tools/Baseline/Quest Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestDebugger>("Quest Audit");
            window.minSize = new Vector2(740, 620);
        }

        void OnEnable()
        {
            previousObserver = QuestRun.Observer;
            QuestRun.Observer = this;
            EditorApplication.projectChanged += RefreshAudit;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            RefreshAudit();
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshAudit;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            if (ReferenceEquals(QuestRun.Observer, this)) QuestRun.Observer = previousObserver;
            preview?.Dispose();
            preview = null;
            StopVoice();
        }

        void PlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.ExitingPlayMode) return;
            preview?.Dispose();
            preview = null;
            StopVoice();
        }

        void RefreshAudit()
        {
            report = QuestAudit.Scan();
            if (selected == null)
                selected = report.Quests.FirstOrDefault();
            Repaint();
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tab = GUILayout.Toolbar(tab, new[] { "Audit", "Preview", "Live runs", "Quest log" }, EditorStyles.toolbarButton);
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) Queue(RefreshAudit);
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawAudit(); break;
                case 1: DrawPreview(); break;
                case 2: DrawLive(); break;
                default: DrawLog(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawAudit()
        {
            if (report == null) return;
            EditorGUILayout.LabelField("Quest definitions and linked dialogue", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(report.Quests.Count + " quests · " + report.ErrorCount + " errors · " + report.WarningCount + " warnings",
                report.ErrorCount > 0 ? MessageType.Error : report.WarningCount > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.LabelField("Signal checks verify declared producers. Use Preview or a live run to inspect transitions.", EditorStyles.wordWrappedMiniLabel);
            foreach (QuestDef quest in report.Quests)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField(quest, typeof(QuestDef), false);
                    GUILayout.Label(quest.DefName, GUILayout.MinWidth(160));
                    if (GUILayout.Button("Preview", GUILayout.Width(75))) Queue(() => SelectQuest(quest));
                }
            foreach (QuestAuditIssue issue in report.Issues)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(issue.Location, EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(issue.Message, issue.Severity == QuestAuditSeverity.Error ? MessageType.Error : MessageType.Warning);
                    EditorGUILayout.ObjectField(issue.Asset, typeof(UnityEngine.Object), false);
                }
        }

        void SelectQuest(QuestDef quest)
        {
            StopVoice();
            preview?.Dispose();
            preview = null;
            selected = quest;
            tab = 1;
            trace.Clear();
            depths.Clear();
            if (quest == null) return;
            preview = new QuestPreviewSession(quest);
            preview.Begin();
            dialog = quest.Guards.Select(guard => guard.dialogue).FirstOrDefault(value => value != null)
                ?? dialog ?? report.Dialogues.FirstOrDefault();
            line = 0;
        }

        void DrawPreview()
        {
            QuestDef next = (QuestDef)EditorGUILayout.ObjectField("Quest", selected, typeof(QuestDef), false);
            if (next != selected) Queue(() => SelectQuest(next));
            EditorGUILayout.HelpBox("Preview runs use the real quest rules. They never write quest progress or launch gameplay.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected == null))
                    if (GUILayout.Button(preview == null ? "Start preview" : "Restart preview")) Queue(() => SelectQuest(selected));
                if (GUILayout.Button("Clear trace", GUILayout.Width(100))) trace.Clear();
            }
            if (preview != null)
            {
                DrawRun(preview.Run, true);
                DrawSignalInput(preview.Run, true);
            }
            EditorGUILayout.Space(10);
            DrawDialogue();
            DrawTrace();
        }

        void DrawRun(QuestRun run, bool isPreview)
        {
            string state = run.IsFinished ? run.Ending.Outcome + (run.Ending.Passes ? " · passes" : " · does not pass") : "Running";
            EditorGUILayout.LabelField(state, EditorStyles.boldLabel);
            foreach (QuestSnapshot step in run.Steps.Snapshot)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string status = step.Failed ? "Failed" : step.Done ? "Done" : step.Shown ? step.Progress + " / " + step.Definition.Required : "Hidden";
                    EditorGUILayout.LabelField(step.Definition.id + " · " + status, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(Localized(step.Definition.textKey, step.Definition.text), EditorStyles.wordWrappedLabel);
                    if (step.Definition.requiresObjectives.Length > 0)
                        EditorGUILayout.LabelField("Requires: " + string.Join(", ", step.Definition.requiresObjectives), EditorStyles.miniLabel);
                    if (isPreview)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(run.IsFinished || string.IsNullOrEmpty(step.Definition.showOn)))
                                if (GUILayout.Button("Show")) Queue(() => RaisePreview(step.Definition.showOn));
                            using (new EditorGUI.DisabledScope(run.IsFinished || string.IsNullOrEmpty(step.Definition.completeOn)))
                                if (GUILayout.Button("Raise completion")) Queue(() => RaisePreview(step.Definition.completeOn));
                            using (new EditorGUI.DisabledScope(run.IsFinished || string.IsNullOrEmpty(step.Definition.failOn)))
                                if (GUILayout.Button("Raise failure")) Queue(() => RaisePreview(step.Definition.failOn));
                        }
                }
            DrawGuards(run, isPreview);
            foreach (QuestEnding ending in run.Quest.Endings)
                EditorGUILayout.LabelField(ending.Signal + " → " + ending.Outcome + (ending.Passes ? " (passes)" : ""), EditorStyles.miniLabel);
        }

        void DrawGuards(QuestRun run, bool isPreview)
        {
            foreach (var action in run.Quest.Guards.GroupBy(guard => guard.action))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Action guards: " + action.Key, EditorStyles.boldLabel);
                    foreach (var guard in action)
                    {
                        if (isPreview)
                        {
                            bool value = preview.Condition(guard.condition);
                            bool next = EditorGUILayout.ToggleLeft(guard.condition, value);
                            if (next != value) preview.SetCondition(guard.condition, next);
                        }
                        else EditorGUILayout.LabelField(guard.condition);
                        EditorGUILayout.LabelField(Localized(guard.hintKey, guard.hint), EditorStyles.wordWrappedMiniLabel);
                    }
                    if (!isPreview) continue;
                    using (new EditorGUI.DisabledScope(run.IsFinished))
                        if (GUILayout.Button("Try " + action.Key))
                        {
                            string requested = action.Key;
                            Queue(() =>
                            {
                                bool allowed = preview.TryAction(requested);
                                var guard = preview.LastBlocker;
                                Record(run, requested + (allowed ? " allowed (no progression evidence)" : " blocked by " + guard.condition), 0);
                                if (guard?.dialogue != null)
                                {
                                    StopVoice();
                                    dialog = guard.dialogue;
                                    line = 0;
                                    if (autoVoice) PlayCurrentVoice();
                                }
                            });
                        }
                    if (preview.LastBlocker?.action == action.Key)
                        EditorGUILayout.HelpBox(Localized(preview.LastBlocker.hintKey, preview.LastBlocker.hint), MessageType.Warning);
                }
        }

        void DrawSignalInput(QuestRun run, bool isPreview)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                signal = EditorGUILayout.TextField("Signal", signal);
                amount = Mathf.Max(1, EditorGUILayout.IntField(amount, GUILayout.Width(50)));
                using (new EditorGUI.DisabledScope(run.IsFinished || string.IsNullOrWhiteSpace(signal)))
                    if (GUILayout.Button("Raise", GUILayout.Width(65)))
                    {
                        string tag = signal;
                        int count = amount;
                        Queue(() =>
                        {
                            if (isPreview) RaisePreview(tag, count);
                            else SignalBus.Fire(new QuestSignal(tag, run, count));
                        });
                    }
            }
            if (GUILayout.Button("Choose a declared signal", EditorStyles.miniButton))
            {
                var menu = new GenericMenu();
                foreach (string tag in QuestSignalCatalog.Raised(run.Quest).Keys.OrderBy(value => value))
                {
                    string captured = tag;
                    menu.AddItem(new GUIContent(captured), signal == captured, () => { signal = captured; Repaint(); });
                }
                menu.ShowAsContext();
            }
        }

        void RaisePreview(string tag, int count = 1)
        {
            if (preview == null) return;
            preview.Raise(tag, count);
        }

        void DrawDialogue()
        {
            EditorGUILayout.LabelField("Dialogue and character voice", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                locale = EditorGUILayout.Popup(locale, new[] { "English", "中文" }, GUILayout.Width(100));
                compact = GUILayout.Toggle(compact, "Compact portrait", GUILayout.Width(130));
            }
            DialogDef next = (DialogDef)EditorGUILayout.ObjectField("Dialogue", dialog, typeof(DialogDef), false);
            if (next != dialog) { StopVoice(); dialog = next; line = 0; }
            if (dialog?.lines == null || dialog.lines.Length == 0)
            {
                EditorGUILayout.HelpBox("Choose a dialogue asset to preview. A quest's first guard dialogue is selected automatically.", MessageType.Info);
                return;
            }
            line = Mathf.Clamp(line, 0, dialog.lines.Length - 1);
            DialogLine current = dialog.lines[line];
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(line == 0))
                    if (GUILayout.Button("Previous", GUILayout.Width(90))) Queue(() => MoveLine(-1));
                GUILayout.Label((line + 1) + " / " + dialog.lines.Length, GUILayout.Width(60));
                using (new EditorGUI.DisabledScope(line == dialog.lines.Length - 1))
                    if (GUILayout.Button("Next", GUILayout.Width(90))) Queue(() => MoveLine(1));
                autoVoice = GUILayout.Toggle(autoVoice, "Play voice on line change");
            }
            if (current?.speaker == null) { EditorGUILayout.HelpBox("This line has no speaker.", MessageType.Error); return; }
            Sprite sprite = current.speaker.ResolvePortrait(current.expression, compact);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect art = GUILayoutUtility.GetRect(compact ? 190 : 200, compact ? 190 : 300, GUILayout.Width(compact ? 190 : 200));
                DrawSprite(art, sprite);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(Localized(dialog.titleKey, dialog.title), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(Localized(current.speaker.nameKey, current.speaker.displayName), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(Localized(current.speaker.roleKey, current.speaker.role));
                    EditorGUILayout.LabelField("Expression: " + current.expression, EditorStyles.miniLabel);
                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField(Localized(current.textKey, current.text), EditorStyles.wordWrappedLabel, GUILayout.MinHeight(90));
                    SoundCueDef voice = current.speaker.ResolveVoice(current.expression);
                    EditorGUILayout.ObjectField("Voice", voice, typeof(SoundCueDef), false);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(voice == null))
                            if (GUILayout.Button("Play voice")) Queue(() => PlayVoice(voice));
                        if (GUILayout.Button("Stop voice")) StopVoice();
                    }
                }
            }
            EditorGUILayout.LabelField("Voice preview auditions the original reaction clip; gameplay applies Effects/Master volume and pitch variation.", EditorStyles.wordWrappedMiniLabel);
        }

        void MoveLine(int offset)
        {
            StopVoice();
            line = Mathf.Clamp(line + offset, 0, dialog.lines.Length - 1);
            if (autoVoice) PlayCurrentVoice();
        }

        string Localized(string key, string fallback)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Locales/Tables/StringTable_" + (locale == 1 ? "zh" : "en") + ".asset");
            string text = string.IsNullOrEmpty(key) ? null : table?.GetEntry(key)?.Value;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        static void DrawSprite(Rect bounds, Sprite sprite)
        {
            EditorGUI.DrawRect(bounds, new Color(.025f, .055f, .075f));
            if (sprite == null) return;
            Rect source = sprite.rect;
            float scale = Mathf.Min(bounds.width / source.width, bounds.height / source.height);
            var destination = new Rect(bounds.center.x - source.width * scale / 2, bounds.center.y - source.height * scale / 2, source.width * scale, source.height * scale);
            GUI.DrawTextureWithTexCoords(destination, sprite.texture, new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height, source.width / sprite.texture.width, source.height / sprite.texture.height), true);
        }

        void PlayCurrentVoice()
        {
            DialogLine current = dialog?.lines?.ElementAtOrDefault(line);
            PlayVoice(current?.speaker?.ResolveVoice(current.expression));
        }

        void PlayVoice(SoundCueDef cue)
        {
            StopVoice();
            AudioClip clip = cue?.ClipAt(0);
            if (clip == null) return;
            AudioPreviewMethod("PlayPreviewClip").Invoke(null, new object[] { clip, 0, false });
            auditioning = true;
        }

        void StopVoice()
        {
            if (!auditioning) return;
            AudioPreviewMethod("StopAllPreviewClips").Invoke(null, null);
            auditioning = false;
        }

        static MethodInfo AudioPreviewMethod(string name) => typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil")
            .GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        void DrawLive()
        {
            EditorGUILayout.HelpBox("These are live gameplay runs. Raising a signal here can change and save real progress.", MessageType.Info);
            QuestRun[] runs = QuestRun.Running.Where(run => !run.IsPreview).ToArray();
            if (runs.Length == 0) EditorGUILayout.LabelField("No live quest runs.");
            foreach (QuestRun run in runs)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField(run.Quest, typeof(QuestDef), false);
                    DrawRun(run, false);
                    DrawSignalInput(run, false);
                }
            DrawTrace();
        }

        void DrawLog()
        {
            EditorGUILayout.LabelField("Saved quest progress", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(QuestLog.FilePath, EditorStyles.textField, GUILayout.Height(20));
            foreach (QuestDef quest in report.Quests)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField(quest, typeof(QuestDef), false);
                    EditorGUILayout.LabelField(QuestLog.Passed(quest) ? "Passed" : QuestLog.Locked(quest) ? "Locked" : "Available");
                    foreach (QuestValue value in QuestLog.Values(quest)) EditorGUILayout.LabelField(value.id, value.value.ToString());
                }
        }

        void DrawTrace()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Signal trace", EditorStyles.boldLabel);
            traceScroll = EditorGUILayout.BeginScrollView(traceScroll, GUILayout.Height(155));
            foreach (string entry in trace) EditorGUILayout.LabelField(entry, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        void Queue(Action action) => EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            try { error = null; action(); }
            catch (Exception exception) { error = exception.Message; Debug.LogException(exception); }
            Repaint();
        };

        void Record(QuestRun run, string text, int depth)
        {
            trace.Add((run.IsPreview ? "[Preview] " : "[Live] ") + run.Quest.DefName + " " + new string(' ', depth * 2) + text);
            if (trace.Count > 250) trace.RemoveAt(0);
            Repaint();
        }

        public void Heard(QuestRun run, QuestSignal value, bool heard)
        {
            previousObserver?.Heard(run, value, heard);
            depths.TryGetValue(run, out int depth);
            Record(run, value.Tag + " ×" + value.Amount + (heard ? "" : " (ignored after ending)"), depth);
            depths[run] = depth + 1;
        }

        public void Moved(QuestRun run, QuestSnapshot step)
        {
            previousObserver?.Moved(run, step);
            depths.TryGetValue(run, out int depth);
            Record(run, step.Definition.id + ": " + (step.Failed ? "failed" : step.Done ? "done" : step.Shown ? step.Progress + "/" + step.Definition.Required : "hidden"), depth);
        }

        public void Ended(QuestRun run, QuestEnding ending)
        {
            previousObserver?.Ended(run, ending);
            depths.TryGetValue(run, out int depth);
            Record(run, "Ending: " + ending.Outcome, depth);
        }

        public void Handled(QuestRun run, QuestSignal value)
        {
            previousObserver?.Handled(run, value);
            depths.TryGetValue(run, out int depth);
            if (depth <= 1) depths.Remove(run);
            else depths[run] = depth - 1;
        }
    }
}
