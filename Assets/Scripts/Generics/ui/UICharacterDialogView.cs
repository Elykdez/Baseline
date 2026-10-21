using System;
using Hypocycloid.Core;
using Baseline.Dialogue;
using Baseline.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline
{
    public sealed class UICharacterDialogView : MonoBehaviour
    {
        [Serializable]
        public sealed class LayoutSettings
        {
            public Vector2 maxSize;
            public float edgeMargin, referenceWidth;
            public UIRectLayout portrait, role, title, message, note, actions;
        }

        public RectTransform frame;
        public TextMeshProUGUI title, copy, note;
        public Button[] actions;
        public CharacterDialogStyleDef style;
        public UIPortrait portrait;
        public Image accent;
        public TextMeshProUGUI role;
        public ScrollRect messageScroll;
        [SerializeField] Button destructiveAction;
        [SerializeField] LayoutSettings landscape = new(), portraitLayout = new();
        CharacterDialogStyle appearance;
        string expression;
        Action cancel;
        GameObject previousSelection;
        int openedFrame, revision;
        bool ownsVoice;
        SoundCueDef currentVoice;
        (TextMeshProUGUI label, float size, float min, float max)[] typography;
        (LayoutElement element, float min, float preferred)[] actionSizes;
        VerticalLayoutGroup actionLayout;
        float actionSpacing;

        void CaptureLayout()
        {
            if (typography != null) return;
            var labels = new System.Collections.Generic.List<TextMeshProUGUI> { role, title, copy, note };
            var sizes = new System.Collections.Generic.List<(LayoutElement, float, float)>();
            foreach (Button button in actions) Capture(button);
            Capture(destructiveAction);
            typography = labels.ConvertAll(label => (label, label.fontSize, label.fontSizeMin, label.fontSizeMax)).ToArray();
            actionSizes = sizes.ToArray();
            actionLayout = actions[0].GetComponentInParent<VerticalLayoutGroup>(true);
            actionSpacing = actionLayout.spacing;

            void Capture(Button button)
            {
                labels.Add(button.GetComponentInChildren<TextMeshProUGUI>(true));
                LayoutElement size = button.GetComponent<LayoutElement>();
                sizes.Add((size, size.minHeight, size.preferredHeight));
            }
        }

        public void Show(string heading, string body, string footer, params (string label, Action action)[] buttons)
            => ShowCharacter(DialogAlertLevel.Info, "default", heading, body, footer, null, buttons);

        public void ShowCharacter(DialogAlertLevel level, string heading, string body, string footer,
            Action onCancel, params (string label, Action action)[] buttons)
            => ShowCharacter(level, null, heading, body, footer, onCancel, buttons);

        public void ShowCharacter(DialogAlertLevel level, string characterExpression, string heading, string body, string footer,
            Action onCancel, params (string label, Action action)[] buttons)
            => ShowCharacter(level, characterExpression, heading, body, footer, onCancel, true, buttons);

        public void ShowCharacter(DialogAlertLevel level, string characterExpression, string heading, string body, string footer,
            Action onCancel, bool playVoice, params (string label, Action action)[] buttons)
        {
            if (buttons.Length == 0 || buttons.Length > actions.Length)
                throw new ArgumentOutOfRangeException(nameof(buttons));
            if (!gameObject.activeSelf) previousSelection = EventSystem.current?.currentSelectedGameObject;
            openedFrame = Time.frameCount;
            int shownRevision = ++revision;
            cancel = onCancel;
            appearance = style.For(level);
            expression = characterExpression;
            RefreshCharacter();
            title.ShowPlain(heading); note.ShowPlain(footer);
            copy.ShowPlain(body);
            destructiveAction.gameObject.SetActive(false);
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].gameObject.SetActive(i < buttons.Length);
                actions[i].onClick.RemoveAllListeners();
                if (i >= buttons.Length) continue;
                var action = buttons[i].action;
                actions[i].GetComponentInChildren<TextMeshProUGUI>().ShowPlain(buttons[i].label);
                actions[i].onClick.AddListener(() =>
                {
                    if (revision != shownRevision || openedFrame == Time.frameCount || !isActiveAndEnabled) return;
                    openedFrame = Time.frameCount;
                    if (AudioManager.Ins != null) AudioManager.Ins.PlayClick();
                    action();
                });
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Fit();
            messageScroll.verticalNormalizedPosition = 1;
            EventSystem.current?.SetSelectedGameObject(actions[0].gameObject);
            SoundCueDef cue = !playVoice || string.IsNullOrEmpty(expression) ? null : appearance.character?.ResolveVoice(expression);
            if (cue == currentVoice) return;
            StopVoice();
            if (Application.isPlaying && cue != null)
            {
                _ = SoundManager.Ins;
                ownsVoice = true;
                currentVoice = cue;
                SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = this, Cue = cue });
            }
        }

        public void ShowDestructiveAction(int index)
        {
            Button action = actions[index];
            destructiveAction.GetComponentInChildren<TextMeshProUGUI>(true)
                .ShowPlain(action.GetComponentInChildren<TextMeshProUGUI>(true).text);
            destructiveAction.onClick = action.onClick;
            destructiveAction.transform.SetSiblingIndex(action.transform.GetSiblingIndex() + 1);
            action.gameObject.SetActive(false);
            destructiveAction.gameObject.SetActive(true);
            Fit();
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            StopVoice();
            revision++;
            if (previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }
        void LocaleChanged(Locale _) => RefreshCharacter();
        void RefreshCharacter()
        {
            if (appearance?.character == null) return;
            role.ShowPlain(DialoguePanel.Text(appearance.character.roleKey, appearance.character.role));
            accent.color = role.color = appearance.colour;
        }
        void StopVoice()
        {
            if (ownsVoice) SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = this });
            ownsVoice = false;
            currentVoice = null;
        }
        void LateUpdate()
        {
            Fit();
            if (openedFrame != Time.frameCount && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                openedFrame = Time.frameCount;
                cancel?.Invoke();
            }
        }
        void Fit()
        {
            var available = ((RectTransform)transform).rect;
            if (available.width <= 0 || available.height <= 0) return;
            CaptureLayout();
            bool compact = available.width < available.height;
            LayoutSettings layout = compact ? portraitLayout : landscape;
            float unit = Mathf.Max(1, layout.referenceWidth / available.width);
            frame.sizeDelta = Vector2.Min(layout.maxSize, available.size - Vector2.one * layout.edgeMargin);
            foreach (var font in typography)
            {
                font.label.fontSize = font.size * unit;
                font.label.fontSizeMin = font.min * unit;
                font.label.fontSizeMax = font.max * unit;
            }
            actionLayout.spacing = actionSpacing * unit;
            foreach (var size in actionSizes)
            {
                size.element.minHeight = size.min * unit;
                size.element.preferredHeight = size.preferred * unit;
            }
            float actionHeight = 0;
            int visibleActions = 0;
            foreach (var size in actionSizes)
            {
                if (!size.element.gameObject.activeSelf) continue;
                actionHeight += size.element.preferredHeight;
                visibleActions++;
            }
            actionHeight += Mathf.Max(0, visibleActions - 1) * actionLayout.spacing;
            float noteHeight = string.IsNullOrEmpty(note.text) ? 0 : (layout.note.offsetMax.y - layout.note.offsetMin.y) * unit;
            if (appearance?.character != null)
            {
                portrait.sprite = appearance.character.ResolvePortrait(expression, compact);
                portrait.Mirrored = appearance.character.ResolveFacing() == PortraitFacing.Mirrored;
            }
            layout.portrait.Apply(portrait.rectTransform, unit);
            layout.role.Apply(role.rectTransform, unit);
            layout.title.Apply(title.rectTransform, unit);
            RectTransform message = (RectTransform)messageScroll.transform;
            layout.message.Apply(message, unit);
            message.offsetMin += Vector2.up * (actionHeight + noteHeight);
            layout.note.Apply(note.rectTransform, unit);
            note.rectTransform.offsetMin += Vector2.up * actionHeight;
            var noteTop = note.rectTransform.offsetMax;
            noteTop.y = note.rectTransform.offsetMin.y + noteHeight;
            note.rectTransform.offsetMax = noteTop;
            RectTransform buttons = (RectTransform)actions[0].transform.parent;
            layout.actions.Apply(buttons, unit);
            buttons.offsetMax += Vector2.up * actionHeight;
            // The scroll content must track the wrapped text height, including wrapped paragraphs.
            float textHeight = copy.GetPreferredValues(copy.text, messageScroll.viewport.rect.width, 0).y;
            messageScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        }

    }
}
