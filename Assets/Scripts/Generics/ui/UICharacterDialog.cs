using System;
using System.Collections.Generic;
using Baseline.Dialogue;
using Baseline.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>Queued application alerts, available from menus and directly opened gameplay scenes.</summary>
    public sealed class UICharacterDialog : MonoBehaviour
    {
        sealed class Request
        {
            public LocalizedString heading, content, accept;
            public Action<bool> completed;
            public bool confirmation;
            public DialogAlertLevel level;
        }

        static UICharacterDialog active;
        static int closedFrame = -1;
        readonly Queue<Request> pending = new();
        Request current;
        [SerializeField] UICharacterDialogView view;
        [SerializeField] Image shield;
        GameObject previousSelection;
        bool cancelling;
        public static bool BlocksInput => active != null && (active.current != null || active.pending.Count > 0)
            || closedFrame == Time.frameCount;
        public bool IsOpen => current != null;
        public UICharacterDialogView View => view;

        public static void Alert(LocalizedString heading, LocalizedString content, DialogAlertLevel level = DialogAlertLevel.Info)
            => Enqueue(new Request { heading = heading, content = content, level = level });

        public static void Confirm(LocalizedString heading, LocalizedString content, Action<bool> completed,
            LocalizedString accept = null, DialogAlertLevel level = DialogAlertLevel.Danger)
            => Enqueue(new Request { heading = heading, content = content, completed = completed,
                accept = accept, confirmation = true, level = level });

        static void Enqueue(Request request)
        {
            if (active == null)
            {
                active = Instantiate(SharedUIContent.Instance.characterAlertsPrefab);
                if (Application.isPlaying) DontDestroyOnLoad(active.gameObject);
            }
            if (active.cancelling) { request.completed?.Invoke(false); return; }
            if (active.current == null && active.pending.Count == 0)
            {
                active.previousSelection = EventSystem.current?.currentSelectedGameObject;
                EventSystem.current?.SetSelectedGameObject(null);
            }
            active.pending.Enqueue(request);
            active.shield.enabled = true;
            active.ShowNext();
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += SceneChanged;
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        }
        void OnDisable()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            CancelAll();
            if (active == this) active = null;
        }
        void Update() => ShowNext();
        void ShowNext()
        {
            if (current != null || pending.Count == 0 || closedFrame == Time.frameCount
                || !LocalizationSettings.InitializationOperation.IsDone) return;
            current = pending.Dequeue();
            Refresh();
        }
        void LocaleChanged(Locale _) { if (current != null) Refresh(); }
        void Refresh()
        {
            var request = current;
            var buttons = new List<(string, Action)>();
            if (request.confirmation)
                buttons.Add((Text("Dialog_Alert_Cancel"), () => Respond(false)));
            buttons.Add((request.accept != null && !request.accept.IsEmpty ? request.accept.GetLocalizedString()
                : Text(request.confirmation ? "Dialog_Alert_Confirm" : "Dialog_Alert_Okay"), () => Respond(true)));
            view.ShowCharacter(request.level, request.heading.GetLocalizedString(), request.content.GetLocalizedString(),
                "", () => Respond(false), buttons.ToArray());
            if (request.confirmation && request.level == DialogAlertLevel.Danger)
                view.ShowDestructiveAction(buttons.Count - 1);
            view.copy.richText = false;
        }
        static string Text(string key) => DialoguePanel.Text(key, key switch
        {
            "Dialog_Alert_Cancel" => "Cancel",
            "Dialog_Alert_Confirm" => "Confirm",
            _ => "Okay",
        });

        public void Respond(bool accepted)
        {
            if (current == null) return;
            Action<bool> callback = current.completed;
            current = null;
            closedFrame = Time.frameCount;
            view.gameObject.SetActive(false);
            if (pending.Count == 0)
            {
                shield.enabled = false;
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    EventSystem.current?.SetSelectedGameObject(previousSelection);
                previousSelection = null;
            }
            else EventSystem.current?.SetSelectedGameObject(null);
            callback?.Invoke(accepted);
        }
        void SceneChanged(Scene _, Scene __) => CancelAll();

#if UNITY_EDITOR
        public static UICharacterDialog CreatePrefabTemplate(UICharacterDialogView template)
        {
            var root = new GameObject("Character alerts", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var service = root.AddComponent<UICharacterDialog>();
            service.shield = root.AddComponent<Image>();
            service.shield.color = Color.clear;
            service.shield.enabled = false;
            service.view = ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(template.gameObject, root.transform))
                .GetComponent<UICharacterDialogView>();
            return service;
        }
#endif

        void CancelAll()
        {
            cancelling = true;
            var cancelled = new List<Request>();
            if (current != null) cancelled.Add(current);
            current = null;
            while (pending.Count > 0) cancelled.Add(pending.Dequeue());
            if (view != null) view.gameObject.SetActive(false);
            if (shield != null) shield.enabled = false;
            previousSelection = null;
            if (cancelled.Count > 0) closedFrame = Time.frameCount;
            try { foreach (var request in cancelled) request.completed?.Invoke(false); }
            finally { cancelling = false; }
        }
    }
}
