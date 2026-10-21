using Hypocycloid.Core;
using Hypocycloid.UI;
using Baseline.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline.Dialogue
{
    /// <summary>Modal, scene-owned dialogue. The caller owns simulation pause policy.</summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class LayoutSettings
        {
            public UIRectLayout title, character, speaker, role, body, illustration;
            public UIRectLayout illustratedRole, illustratedBody, next, nextRequired, close, progress, progressRequired;
            public float requiredBodyBottom, bodyFontSizeMin, illustratedBodyFontSizeMin;
        }

        private readonly DialogRunner runner = new DialogRunner();
        [SerializeField] private GameObject surface;
        [SerializeField] private RectTransform safe, card;
        [SerializeField] private UIPortrait character, secondCharacter;
        [SerializeField] private Image accent;
        [SerializeField] private Image illustration;
        [SerializeField] private TextMeshProUGUI title, speaker, role, body, progress, nextLabel;
        [SerializeField] private Button next, close;
        [SerializeField] private LayoutSettings landscape = new(), portrait = new();
        [SerializeField] private LayoutSettings pairedLayout = new();
        [SerializeField] private UIRectLayout pairedRightCharacter;
        [SerializeField] private Color activeCharacterTint, inactiveCharacterTint;
        [SerializeField] private float pairMinimumWidth, pairMinimumAspect;
        private readonly CharacterDef[] characters = new CharacterDef[2];
        private readonly string[] expressions = new string[2];
        private readonly PortraitFacingOverride[] facings = new PortraitFacingOverride[2];
        private int speakingSlot = -1;
        private GameObject previousSelection;
        private Vector2 lastSize;
        private long displayedRevision;
        private int lastInputFrame = -1;
        private float visibleCharacters;
        private bool compact;
        private bool voiceEnabled, ownsVoice;
        private CharacterDef lastVoiceCharacter;
        private SoundCueDef lastVoice;

        public bool IsOpen => runner.IsOpen;
        // Also swallow the input that closed the panel, regardless of Update ordering.
        public bool BlocksInput => IsOpen || lastInputFrame == Time.frameCount;
        public DialogRunner Runner => runner;

        public static DialoguePanel Create()
        {
            return Instantiate(SharedUIContent.Instance.dialoguePrefab);
        }

        private void Awake()
        {
            if (next != null) next.onClick.AddListener(Advance);
            if (close != null) close.onClick.AddListener(Close);
        }

#if UNITY_EDITOR
        public static DialoguePanel CreatePrefabTemplate()
        {
            var go = new GameObject("Dialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            DialoguePanel panel = go.AddComponent<DialoguePanel>();
            panel.Build();
            return panel;
        }
#endif

        public void Show(DialogDef definition, bool allowClose = true, bool offerSkip = false, bool playVoice = true)
        {
            if (definition == null) throw new System.ArgumentNullException(nameof(definition));
            definition.Validate();
            StopVoice();
            ResetCharacters();
            voiceEnabled = playVoice;
            if (playVoice && Application.isPlaying) _ = SoundManager.Ins;
            GameObject returnSelection = IsOpen ? previousSelection : EventSystem.current?.currentSelectedGameObject;
            lastInputFrame = Time.frameCount;
            runner.Play(definition, allowClose, offerSkip);
            previousSelection = returnSelection;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void Close()
        {
            if (!runner.CanClose) return;
            lastInputFrame = Time.frameCount;
            runner.Close();
        }

        public void Advance()
        {
            if (!IsOpen || lastInputFrame == Time.frameCount) return;
            lastInputFrame = Time.frameCount;
            if (body.maxVisibleCharacters < body.textInfo.characterCount)
            {
                visibleCharacters = body.textInfo.characterCount;
                body.maxVisibleCharacters = int.MaxValue;
                return;
            }
            runner.Advance(displayedRevision);
        }

        private void OnEnable()
        {
            runner.Changed += Refresh;
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            StopVoice();
            lastInputFrame = Time.frameCount;
            runner.Cancel();
            ResetCharacters();
            runner.Changed -= Refresh;
        }

        private void LocaleChanged(Locale _) { if (IsOpen) Refresh(); }

        private void LateUpdate()
        {
            if (UICharacterDialog.BlocksInput) return;
            if (!IsOpen) return;
            if (safe.rect.size != lastSize) Layout();
            if (body.maxVisibleCharacters != int.MaxValue)
            {
                visibleCharacters += Time.unscaledDeltaTime * 48;
                body.maxVisibleCharacters = (int)visibleCharacters;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true) Close();
            else if (keyboard?.enterKey.wasPressedThisFrame == true || keyboard?.spaceKey.wasPressedThisFrame == true) Advance();
        }

        private void Refresh()
        {
            if (surface == null) return;
            surface.SetActive(IsOpen);
            if (!IsOpen)
            {
                StopVoice();
                ResetCharacters();
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    EventSystem.current?.SetSelectedGameObject(previousSelection);
                previousSelection = null;
                return;
            }
            DialogLine line = runner.Current;
            StageCharacter(line);
            illustration.sprite = line.illustration;
            illustration.gameObject.SetActive(line.illustration != null);
            displayedRevision = runner.Revision;
            Localize(title, runner.Definition.titleKey, runner.Definition.title);
            Localize(body, line.textKey, line.text);
            body.ForceMeshUpdate();
            visibleCharacters = 0;
            body.maxVisibleCharacters = 0;
            accent.color = line.speaker.accent;
            progress.ShowPlain((runner.LineIndex + 1) + " / " + runner.Definition.lines.Length);
            Localize(nextLabel, runner.OffersSkip ? "Dialog_Learn"
                : runner.LineIndex + 1 == runner.Definition.lines.Length ? "Dialog_Continue" : "Dialog_Next",
                runner.OffersSkip ? "Show me the basics" : runner.LineIndex + 1 == runner.Definition.lines.Length ? "Continue" : "Next");
            close.gameObject.SetActive(runner.CanClose);
            Localize(close.GetComponentInChildren<TextMeshProUGUI>(),
                runner.OffersSkip ? "Dialog_Skip" : "Dialog_Close",
                runner.OffersSkip ? "Skip explanation" : "Close");
            Layout();
            ShowVoice(line.speaker, line.expression);
        }

        private void StageCharacter(DialogLine line)
        {
            int slot = System.Array.IndexOf(characters, line.speaker);
            if (slot < 0)
                slot = characters[0] == null ? 0 : characters[1] == null ? 1 : 1 - speakingSlot;
            characters[slot] = line.speaker;
            expressions[slot] = line.expression;
            facings[slot] = line.facing;
            speakingSlot = slot;
        }

        private void ResetCharacters()
        {
            characters[0] = characters[1] = null;
            expressions[0] = expressions[1] = null;
            facings[0] = facings[1] = PortraitFacingOverride.CharacterDefault;
            speakingSlot = -1;
            character.sprite = secondCharacter.sprite = null;
            character.Mirrored = secondCharacter.Mirrored = false;
            secondCharacter.gameObject.SetActive(false);
        }

        private void ShowVoice(CharacterDef speaker, string expression)
        {
            SoundCueDef cue = voiceEnabled ? speaker?.ResolveVoice(expression) : null;
            if (cue == null)
            {
                StopVoice();
                return;
            }
            // A repeated reaction and a locale refresh must not restart the same recording.
            if (lastVoiceCharacter == speaker && lastVoice == cue) return;
            lastVoiceCharacter = speaker;
            lastVoice = cue;
            ownsVoice = true;
            SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = this, Cue = cue });
        }

        private void StopVoice()
        {
            if (ownsVoice) SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = this });
            ownsVoice = false;
            lastVoiceCharacter = null;
            lastVoice = null;
        }

        private static void Localize(TextMeshProUGUI label, string key, string fallback)
        {
            label.ShowPlain(Text(key, fallback));
            if (!string.IsNullOrEmpty(key)) label.Localize(new LocalizedString(GameRegister.DEF_STT, key));
        }

        public static string Text(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || !LocalizationSettings.InitializationOperation.IsDone) return fallback;
            var table = LocalizationSettings.StringDatabase.GetTable(GameRegister.DEF_STT);
            string value = table?.GetEntry(key)?.LocalizedValue;
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void Build()
        {
            surface = Rect("Modal surface", transform, Vector2.zero, Vector2.one).gameObject;
            surface.AddComponent<Image>().color = new Color(0.005f, 0.015f, 0.025f, 0.78f);
            safe = Rect("Safe area", surface.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<UISafeArea>();
            card = Rect("Conversation", safe, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.9f));
            card.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.055f, 0.075f, 0.99f);
            accent = Rect("Speaker accent", card, new Vector2(0, 0.99f), Vector2.one).gameObject.AddComponent<Image>();
            accent.raycastTarget = false;
            character = Rect("Default character art", card, new Vector2(0.02f, 0.02f), new Vector2(0.37f, 0.94f)).gameObject.AddComponent<UIPortrait>();
            character.preserveAspect = true;
            character.raycastTarget = false;
            secondCharacter = Rect("Right character art", card, Vector2.zero, Vector2.one).gameObject.AddComponent<UIPortrait>();
            secondCharacter.preserveAspect = true;
            secondCharacter.raycastTarget = false;
            secondCharacter.gameObject.SetActive(false);
            illustration = Rect("Dialogue illustration", card, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            illustration.preserveAspect = true;
            illustration.raycastTarget = false;
            title = Label("Briefing title", card, 22, new Color(0.55f, 0.74f, 0.79f));
            speaker = Label("Speaker", card, 40, Color.white);
            speaker.gameObject.SetActive(false);
            role = Label("Role", card, 23, new Color(0.64f, 0.75f, 0.79f));
            body = Label("Dialogue text", card, 32, new Color(0.93f, 0.95f, 0.94f));
            body.enableAutoSizing = true;
            body.fontSizeMin = 24;
            body.fontSizeMax = 32;
            progress = Label("Line count", card, 20, new Color(0.64f, 0.75f, 0.79f));
            next = Button("Next", card, Advance);
            nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
            close = Button("Close", card, Close);
            nextLabel.Localize(new LocalizedString(GameRegister.DEF_STT, "Dialog_Next"));
            close.GetComponentInChildren<TextMeshProUGUI>().Localize(new LocalizedString(GameRegister.DEF_STT, "Dialog_Close"));
            surface.SetActive(false);
        }
#endif

        private void Layout()
        {
            lastSize = safe.rect.size;
            compact = lastSize.x < lastSize.y;
            bool pairKnown = runner.Definition.dualCharacters && characters[1] != null;
            bool paired = pairKnown && !compact && lastSize.x >= pairMinimumWidth
                && lastSize.x >= lastSize.y * pairMinimumAspect;
            character.sprite = (paired ? characters[0] : characters[speakingSlot])
                .ResolvePortrait(paired ? expressions[0] : expressions[speakingSlot], compact);
            int leftSlot = paired ? 0 : speakingSlot;
            character.Mirrored = characters[leftSlot].ResolveFacing(facings[leftSlot]) == PortraitFacing.Mirrored;
            character.color = !paired || speakingSlot == 0 ? activeCharacterTint : inactiveCharacterTint;
            secondCharacter.gameObject.SetActive(paired);
            if (paired)
            {
                secondCharacter.sprite = characters[1].ResolvePortrait(expressions[1], false);
                secondCharacter.Mirrored = characters[1].ResolveFacing(facings[1]) == PortraitFacing.Mirrored;
                secondCharacter.color = speakingSlot == 1 ? activeCharacterTint : inactiveCharacterTint;
                pairedRightCharacter.Apply(secondCharacter.rectTransform);
            }
            speaker.gameObject.SetActive(paired);
            if (paired) Localize(speaker, runner.Current.speaker.nameKey, runner.Current.speaker.displayName);
            CharacterDef current = runner.Current.speaker;
            Localize(role, pairKnown && !paired ? current.nameKey : current.roleKey,
                pairKnown && !paired ? current.displayName : current.role);
            LayoutSettings layout = paired ? pairedLayout : compact ? portrait : landscape;
            bool illustrated = runner.Current.illustration != null;
            layout.title.Apply(title.rectTransform);
            layout.character.Apply(character.rectTransform);
            layout.speaker.Apply(speaker.rectTransform);
            (illustrated ? layout.illustratedRole : layout.role).Apply(role.rectTransform);
            UIRectLayout bodyLayout = illustrated ? layout.illustratedBody : layout.body;
            if (!runner.CanClose) bodyLayout.anchorMin.y = layout.requiredBodyBottom;
            bodyLayout.Apply(body.rectTransform);
            body.fontSizeMin = illustrated ? layout.illustratedBodyFontSizeMin : layout.bodyFontSizeMin;
            layout.illustration.Apply(illustration.rectTransform);
            (runner.CanClose ? layout.next : layout.nextRequired).Apply((RectTransform)next.transform);
            layout.close.Apply((RectTransform)close.transform);
            (runner.CanClose ? layout.progress : layout.progressRequired).Apply(progress.rectTransform);
        }

#if UNITY_EDITOR
        private static Button Button(string name, Transform parent, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.20f, 0.25f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            TextMeshProUGUI label = Label("Label", rect, 25, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, int size, Color colour)
        {
            TextMeshProUGUI label = UILocalizedText.Create(parent, name);
            Place(label.rectTransform, 0, 0, 1, 1);
            label.ShowPlain("");
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = colour;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
#endif

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
