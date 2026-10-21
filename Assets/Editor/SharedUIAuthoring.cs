using System.IO;
using System.Linq;
using Baseline.Dialogue;
using Baseline.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Baseline.Editor
{
    /// <summary>Authors missing defaults once; existing customized assets are preserved.</summary>
    public static class SharedUIAuthoring
    {
        public const string Folder = "Assets/Bundles/Core/Prefab/UI";
        public const string ContentPath = SharedUIContent.EditorAssetPath;
        const string StylePath = "Assets/Bundles/Core/Config/Dialogue/CharacterDialogStyle.asset";

        [MenuItem("Tools/Baseline/Create Missing Shared UI Content")]
        public static void CreateMissingContent()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory("Assets/Bundles/Core/Config/Generics");
            Directory.CreateDirectory("Assets/Bundles/Core/Config/Dialogue");
            AssetDatabase.Refresh();
            AddTranslations();
            var content = AssetDatabase.LoadAssetAtPath<SharedUIContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<SharedUIContent>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }
            content.textPrefab ??= AssetDatabase.LoadAssetAtPath<TextMeshProUGUI>(Folder + "/ui_text.prefab");
            content.textSettings ??= AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/Bundles/Core/Settings/TextMeshPro/TMP Settings.asset");
            content.contextPrefab ??= AssetDatabase.LoadAssetAtPath<ContextManager>(Folder + "/GameContext.prefab");
            EditorUtility.SetDirty(content);
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var group = settings.FindGroup("Core");
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID("Assets/Bundles/Core"), group)
                .SetAddress("Core");
            AssetDatabase.SaveAssets();

            var guide = AssetDatabase.LoadAssetAtPath<CharacterDef>("Assets/Bundles/Core/Config/Dialogue/GuideCharacter.asset");
            if (guide == null)
            {
                guide = ScriptableObject.CreateInstance<CharacterDef>();
                guide.displayName = "Guide";
                guide.role = "Assistant";
                guide.nameKey = "Baseline_Guide_Name";
                guide.roleKey = "Baseline_Guide_Role";
                var art = AssetDatabase.LoadAllAssetsAtPath("Assets/Bundles/Core/Texture/Characters/RobotGuide.png").OfType<Sprite>().ToArray();
                var placeholder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bundles/Core/Texture/icon_missing.png");
                guide.illustration = art.FirstOrDefault(sprite => sprite.name == "RobotGuide-illustration") ?? placeholder;
                guide.portrait = art.FirstOrDefault(sprite => sprite.name == "RobotGuide-portrait") ?? placeholder;
                AssetDatabase.CreateAsset(guide, "Assets/Bundles/Core/Config/Dialogue/GuideCharacter.asset");
            }
            var style = AssetDatabase.LoadAssetAtPath<CharacterDialogStyleDef>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<CharacterDialogStyleDef>();
                style.info = Style(new Color32(107, 191, 231, 255));
                style.success = Style(new Color32(124, 207, 154, 255));
                style.warning = Style(new Color32(231, 184, 107, 255));
                style.danger = Style(new Color32(245, 130, 118, 255));
                AssetDatabase.CreateAsset(style, StylePath);
            }
            if (content.characterDialogPrefab == null)
            {
                string path = Folder + "/ui_character_dialog.prefab";
                content.characterDialogPrefab = AssetDatabase.LoadAssetAtPath<UICharacterDialogView>(path);
                if (content.characterDialogPrefab == null)
                {
                    GameObject root = CreateAlert(style);
                    try { content.characterDialogPrefab = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<UICharacterDialogView>(); }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            if (content.dialoguePrefab == null)
            {
                string path = Folder + "/ui_dialogue.prefab";
                content.dialoguePrefab = AssetDatabase.LoadAssetAtPath<DialoguePanel>(path);
                if (content.dialoguePrefab == null)
                {
                    DialoguePanel panel = DialoguePanel.CreatePrefabTemplate();
                    ConfigureDialogueLayouts(panel);
                    try { content.dialoguePrefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, path).GetComponent<DialoguePanel>(); }
                    finally { Object.DestroyImmediate(panel.gameObject); }
                }
            }
            if (content.characterAlertsPrefab == null)
            {
                string path = Folder + "/ui_character_alerts.prefab";
                content.characterAlertsPrefab = AssetDatabase.LoadAssetAtPath<UICharacterDialog>(path);
                if (content.characterAlertsPrefab == null)
                {
                    UICharacterDialog service = UICharacterDialog.CreatePrefabTemplate(content.characterDialogPrefab);
                    try { content.characterAlertsPrefab = PrefabUtility.SaveAsPrefabAsset(service.gameObject, path).GetComponent<UICharacterDialog>(); }
                    finally { Object.DestroyImmediate(service.gameObject); }
                }
            }
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            Debug.Log("Baseline shared UI content is ready.");

            CharacterDialogStyle Style(Color color) => new() { character = guide, colour = color };
        }

        static GameObject CreateAlert(CharacterDialogStyleDef style)
        {
            var root = Rect("Character dialog", null);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            var view = root.gameObject.AddComponent<UICharacterDialogView>();
            view.style = style;
            view.frame = Rect("Frame", root);
            view.frame.anchorMin = view.frame.anchorMax = view.frame.pivot = new Vector2(.5f, .5f);
            view.frame.sizeDelta = new Vector2(1120, 820);
            view.frame.gameObject.AddComponent<Image>().color = new Color32(16, 29, 45, 255);
            view.portrait = Rect("Portrait", view.frame).gameObject.AddComponent<UIPortrait>();
            view.portrait.preserveAspect = true;
            view.portrait.raycastTarget = false;
            view.accent = Rect("Accent", view.frame).gameObject.AddComponent<Image>();
            view.accent.rectTransform.anchorMin = new Vector2(0, .99f);
            view.accent.raycastTarget = false;
            view.role = Label("Role", view.frame);
            view.title = Label("Title", view.frame);
            view.title.enableAutoSizing = true;
            view.note = Label("Note", view.frame);
            var scroll = Rect("Message scroll", view.frame);
            view.messageScroll = scroll.gameObject.AddComponent<ScrollRect>();
            view.messageScroll.horizontal = false;
            view.messageScroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", scroll);
            viewport.gameObject.AddComponent<RectMask2D>();
            view.copy = Label("Body", viewport);
            view.copy.richText = false;
            view.copy.rectTransform.anchorMin = new Vector2(0, 1);
            view.copy.rectTransform.anchorMax = Vector2.one;
            view.copy.rectTransform.pivot = new Vector2(.5f, 1);
            view.messageScroll.viewport = viewport;
            view.messageScroll.content = view.copy.rectTransform;
            var buttons = Rect("Actions", view.frame);
            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            view.actions = new Button[3];
            for (int i = 0; i < view.actions.Length; i++)
            {
                var rect = Rect("Action " + (i + 1), buttons);
                var image = rect.gameObject.AddComponent<Image>();
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                Label("Label", rect).alignment = TextAlignmentOptions.Center;
                view.actions[i] = button;
            }
            ConfigureAlertLayouts(view);
            return root.gameObject;
        }

        public static void ConfigureDialogueLayouts(DialoguePanel panel)
        {
            var serialized = new SerializedObject(panel);
            foreach (bool compact in new[] { false, true })
            {
                SerializedProperty layout = serialized.FindProperty(compact ? "portrait" : "landscape");
                float left = compact ? .07f : .40f;
                SetRect(layout, "title", .06f, .88f, .94f, .95f);
                SetRect(layout, "character", compact ? .05f : .01f, compact ? .65f : .02f, compact ? .31f : .37f, compact ? .87f : .88f);
                SetRect(layout, "speaker", compact ? .35f : left, compact ? .76f : .69f, .94f, compact ? .84f : .79f);
                SetRect(layout, "role", compact ? .35f : left, compact ? .70f : .68f, .94f, compact ? .82f : .78f);
                SetRect(layout, "body", left, .28f, .93f, compact ? .59f : .56f);
                SetRect(layout, "illustration", compact ? .35f : left, .54f, .93f, .79f);
                SetRect(layout, "illustratedRole", compact ? .35f : left, .81f, .94f, .87f);
                SetRect(layout, "illustratedBody", left, .28f, .93f, .52f);
                SetRect(layout, "next", left, .13f, .94f, .21f);
                SetRect(layout, "nextRequired", left, .04f, .94f, .12f);
                SetRect(layout, "close", left, .04f, .94f, .12f);
                SetRect(layout, "progress", left, .225f, .94f, .265f);
                SetRect(layout, "progressRequired", left, .135f, .94f, .175f);
                layout.FindPropertyRelative("requiredBodyBottom").floatValue = .19f;
                layout.FindPropertyRelative("bodyFontSizeMin").floatValue = 24;
                layout.FindPropertyRelative("illustratedBodyFontSizeMin").floatValue = 20;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            StyleButton((Button)serialized.FindProperty("next").objectReferenceValue, true);
            StyleButton((Button)serialized.FindProperty("close").objectReferenceValue, false);
            ConfigurePairedDialogueLayout(panel);
        }

        public static void ConfigurePairedDialogueLayout(DialoguePanel panel)
        {
            var serialized = new SerializedObject(panel);
            var first = (Image)serialized.FindProperty("character").objectReferenceValue;
            var second = (Image)serialized.FindProperty("secondCharacter").objectReferenceValue;
            if (second == null)
            {
                second = Object.Instantiate(first, first.transform.parent);
                second.name = "Right character art";
                second.transform.SetSiblingIndex(first.transform.GetSiblingIndex() + 1);
                serialized.FindProperty("secondCharacter").objectReferenceValue = second;
            }
            second.sprite = null;
            second.rectTransform.pivot = new Vector2(1, 0);
            second.gameObject.SetActive(false);
            serialized.FindProperty("activeCharacterTint").colorValue = Color.white;
            serialized.FindProperty("inactiveCharacterTint").colorValue = new Color32(164, 168, 161, 255);
            serialized.FindProperty("pairMinimumWidth").floatValue = 1100;
            serialized.FindProperty("pairMinimumAspect").floatValue = 1.1f;
            SerializedProperty layout = serialized.FindProperty("pairedLayout");
            SetRect(layout, "title", .06f, .88f, .94f, .95f);
            SetRect(layout, "character", .01f, .02f, .28f, .86f);
            SetRect(serialized.FindProperty("pairedRightCharacter"), .72f, .02f, .99f, .86f);
            SetRect(layout, "speaker", .31f, .79f, .70f, .86f);
            SetRect(layout, "role", .31f, .72f, .70f, .78f);
            SetRect(layout, "body", .31f, .28f, .70f, .68f);
            SetRect(layout, "illustration", .31f, .50f, .70f, .70f);
            SetRect(layout, "illustratedRole", .31f, .72f, .70f, .78f);
            SetRect(layout, "illustratedBody", .31f, .28f, .70f, .48f);
            SetRect(layout, "next", .31f, .13f, .70f, .21f);
            SetRect(layout, "nextRequired", .31f, .04f, .70f, .12f);
            SetRect(layout, "close", .31f, .04f, .70f, .12f);
            SetRect(layout, "progress", .31f, .225f, .70f, .265f);
            SetRect(layout, "progressRequired", .31f, .135f, .70f, .175f);
            layout.FindPropertyRelative("requiredBodyBottom").floatValue = .19f;
            layout.FindPropertyRelative("bodyFontSizeMin").floatValue = 24;
            layout.FindPropertyRelative("illustratedBodyFontSizeMin").floatValue = 20;
            var label = (TextMeshProUGUI)serialized.FindProperty("speaker").objectReferenceValue;
            label.color = new Color32(63, 58, 52, 255);
            label.fontSize = label.fontSizeMax = 28;
            label.fontSizeMin = 20;
            label.enableAutoSizing = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigureAlertLayouts(UICharacterDialogView view)
        {
            view.portrait.rectTransform.pivot = Vector2.zero;
            for (int i = 0; i < view.actions.Length; i++)
            {
                StyleButton(view.actions[i], i == 0);
                view.actions[i].GetComponentInChildren<TextMeshProUGUI>(true).fontSize = 23;
                var size = view.actions[i].GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 60;
            }
            view.role.fontSize = view.role.fontSizeMax = 24;
            view.role.fontSizeMin = 16;
            view.role.enableAutoSizing = true;
            view.title.fontSize = view.title.fontSizeMax = 28;
            view.title.fontSizeMin = 20;
            view.copy.fontSize = 25;
            view.note.fontSize = 19;
            view.actions[0].GetComponentInParent<VerticalLayoutGroup>(true).spacing = 8;
            var serialized = new SerializedObject(view);
            var destructive = (Button)serialized.FindProperty("destructiveAction").objectReferenceValue;
            if (destructive == null)
            {
                destructive = Object.Instantiate(view.actions[1], view.actions[1].transform.parent);
                destructive.name = "Destructive action";
                serialized.FindProperty("destructiveAction").objectReferenceValue = destructive;
            }
            StyleButton(destructive, false, true);
            destructive.gameObject.SetActive(false);
            foreach (bool compact in new[] { false, true })
            {
                SerializedProperty layout = serialized.FindProperty(compact ? "portraitLayout" : "landscape");
                layout.FindPropertyRelative("maxSize").vector2Value = new Vector2(1120, compact ? 1320 : 820);
                layout.FindPropertyRelative("edgeMargin").floatValue = 40;
                layout.FindPropertyRelative("referenceWidth").floatValue = compact ? 1500 : 0;
                float left = compact ? 0 : .37f;
                float inset = compact ? 28 : 0;
                SetRect(layout, "portrait", 0, compact ? 1 : 0, compact ? 0 : .37f, 1,
                    compact ? 20 : 8, compact ? -184 : 12, compact ? 180 : -8, compact ? -20 : -24);
                SetRect(layout, "role", left, 1, 1, 1, compact ? 200 : 0, -78, -28, -32);
                SetRect(layout, "title", left, 1, 1, 1, compact ? 200 : 0, compact ? -180 : -140, -28, -100);
                SetRect(layout, "message", left, 0, 1, 1, inset, 48, -28, compact ? -214 : -168);
                SetRect(layout, "note", left, 0, 1, 0, inset, 36, -28, 102);
                SetRect(layout, "actions", left, 0, 1, 0, inset, 28, -28, 28);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRect(SerializedProperty layout, string field, float left, float bottom, float right, float top,
            float insetLeft = 0, float insetBottom = 0, float insetRight = 0, float insetTop = 0)
        {
            SetRect(layout.FindPropertyRelative(field), left, bottom, right, top, insetLeft, insetBottom, insetRight, insetTop);
        }

        static void SetRect(SerializedProperty rect, float left, float bottom, float right, float top,
            float insetLeft = 0, float insetBottom = 0, float insetRight = 0, float insetTop = 0)
        {
            rect.FindPropertyRelative("anchorMin").vector2Value = new Vector2(left, bottom);
            rect.FindPropertyRelative("anchorMax").vector2Value = new Vector2(right, top);
            rect.FindPropertyRelative("offsetMin").vector2Value = new Vector2(insetLeft, insetBottom);
            rect.FindPropertyRelative("offsetMax").vector2Value = new Vector2(insetRight, insetTop);
        }

        static void StyleButton(Button button, bool primary, bool destructive = false)
        {
            Color background = primary ? new Color32(28, 123, 116, 255)
                : destructive ? new Color32(247, 232, 227, 255) : new Color32(242, 239, 232, 255);
            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = colors.selectedColor = Color.Lerp(background, Color.black, .08f);
            colors.pressedColor = Color.Lerp(background, Color.black, .16f);
            colors.disabledColor = new Color32(201, 197, 189, 255);
            colors.colorMultiplier = 1;
            colors.fadeDuration = .08f;
            button.targetGraphic.color = Color.white;
            button.colors = colors;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            label.color = primary ? new Color32(255, 253, 249, 255)
                : destructive ? new Color32(157, 64, 48, 255) : new Color32(63, 58, 52, 255);
            label.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 0;
            label.richText = true;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(24, 0);
            label.rectTransform.offsetMax = new Vector2(-24, 0);
        }

        static TextMeshProUGUI Label(string name, Transform parent)
        {
            var label = UILocalizedText.Create(parent, name);
            label.ShowPlain("");
            label.fontSize = 25;
            label.color = Color.white;
            label.raycastTarget = false;
            label.richText = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void AddTranslations()
        {
            var entries = new (string key, string en, string zh)[]
            {
                ("Dialog_Next", "Next", "下一步"), ("Dialog_Close", "Close", "关闭"),
                ("Dialog_Continue", "Continue", "继续"), ("Dialog_Learn", "Show me the basics", "了解基础知识"),
                ("Dialog_Skip", "Skip explanation", "跳过说明"), ("Dialog_Alert_Cancel", "Cancel", "取消"),
                ("Dialog_Alert_Confirm", "Confirm", "确认"), ("Dialog_Alert_Okay", "Okay", "好的"),
                ("Baseline_Guide_Name", "Guide", "向导"), ("Baseline_Guide_Role", "Assistant", "助手"),
            };
            foreach (string code in new[] { "en", "zh" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Locales/Tables/StringTable_{code}.asset");
                foreach (var entry in entries)
                    if (table.GetEntry(entry.key) == null) table.AddEntry(entry.key, code == "en" ? entry.en : entry.zh);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
        }
    }
}
