using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MySys22.DialogueEngine.UI;
using MySys22.DialogueEngine.Bridge;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    [InitializeOnLoad]
    public static class DialogueSceneSetup
    {
        private const string AUTO_SETUP_KEY = "MySys22.DialogueEngine.AutoSetup";
        private const double CHECK_INTERVAL = 5.0;

        private static double _nextCheck;

        static DialogueSceneSetup()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        public static bool AutoSetupEnabled
        {
            get => EditorPrefs.GetBool(AUTO_SETUP_KEY, true);
            set => EditorPrefs.SetBool(AUTO_SETUP_KEY, value);
        }

        [MenuItem("MySys22/Dialogue/Auto Setup Scene On/Off")]
        private static void ToggleAutoSetup()
        {
            AutoSetupEnabled = !AutoSetupEnabled;
            DialogueLogger.Log($"Auto scene setup {(AutoSetupEnabled ? "enabled" : "disabled")}.");
        }

        [MenuItem("MySys22/Dialogue/Setup Scene")]
        public static void SetupScene()
        {
            if (EnsureDialogueSystemExists(force: true))
                DialogueLogger.Log("Scene setup complete. Save the scene (Ctrl+S) to persist it.");
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AutoSetupEnabled) return;
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + CHECK_INTERVAL;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return;

            bool systemReady = Object.FindFirstObjectByType<GameSetup>() != null
                               && Object.FindFirstObjectByType<DialogueUICanvas>() is { IsWired: true };
            if (systemReady) return;

            EnsureDialogueSystemExists(force: false);
        }

        private static bool EnsureDialogueSystemExists(bool force)
        {
            bool created = false;

            GameObject settings = GameObject.Find("Settings");
            if (settings == null)
            {
                settings = new GameObject("Settings");
                Undo.RegisterCreatedObjectUndo(settings, "Create Settings");
                created = true;
            }

            GameObject mySys22 = FindOrCreate(settings.transform, "MySys22", ref created);
            GameObject dialogue = FindOrCreate(mySys22.transform, "Dialogue", ref created);
            GameObject system = FindOrCreate(dialogue.transform, "DialogueSystem", ref created);

            var gameSetup = system.GetComponent<GameSetup>();
            if (gameSetup == null)
            {
                gameSetup = system.AddComponent<GameSetup>();
                Undo.RegisterCreatedObjectUndo(system, "Add GameSetup");
                created = true;
            }

            var bridge = system.GetComponent<DialogueBridge>();
            if (bridge == null)
            {
                bridge = system.AddComponent<DialogueBridge>();
                Undo.RegisterCreatedObjectUndo(system, "Add DialogueBridge");
                created = true;
            }

            GameObject dialogueUI = BuildDialogueUI(dialogue.transform, ref created);

            if (created)
            {
                EditorSceneManager.MarkSceneDirty(system.scene);
                DialogueLogger.Log($"Dialogue scene hierarchy created under Settings/MySys22/Dialogue " +
                                   $"({dialogueUI.name}). Save the scene to keep it.");
            }
            else if (force)
            {
                DialogueLogger.Log("Dialogue scene hierarchy already present.");
            }

            return created;
        }

        private static GameObject FindOrCreate(Transform parent, string name, ref bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            created = true;
            return go;
        }

        public static GameObject BuildDialogueUI(Transform parent, ref bool created)
        {
            GameObject canvasGO = FindOrCreate(parent, "DialogueUI", ref created);

            var canvas = canvasGO.GetComponent<Canvas>();
            if (canvas == null) { canvas = canvasGO.AddComponent<Canvas>(); created = true; }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler == null) { scaler = canvasGO.AddComponent<CanvasScaler>(); created = true; }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGO.AddComponent<GraphicRaycaster>();
                created = true;
            }

            var ui = canvasGO.GetComponent<DialogueUICanvas>();
            if (ui == null) { ui = canvasGO.AddComponent<DialogueUICanvas>(); created = true; }

            GameObject panel = FindOrCreateRect(canvasGO.transform, "DialoguePanel", ref created);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(-120f, 260f);
            var panelImage = panel.GetComponent<Image>();
            if (panelImage == null) { panelImage = panel.AddComponent<Image>(); created = true; }
            panelImage.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            TextMeshProUGUI speakerText = FindOrCreateText(panel.transform, "SpeakerText", ref created,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -16f), new Vector2(-40f, 44f),
                30f, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Left);

            TextMeshProUGUI dialogueText = FindOrCreateText(panel.transform, "DialogueText", ref created,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -34f), new Vector2(-40f, -100f),
                26f, Color.white, TextAlignmentOptions.TopLeft);

            GameObject choices = FindOrCreateRect(panel.transform, "ChoicesContainer", ref created);
            var choicesRect = choices.GetComponent<RectTransform>();
            choicesRect.anchorMin = new Vector2(0f, 0f);
            choicesRect.anchorMax = new Vector2(1f, 0f);
            choicesRect.pivot = new Vector2(0.5f, 0f);
            choicesRect.anchoredPosition = new Vector2(0f, 12f);
            choicesRect.sizeDelta = new Vector2(-40f, 40f);

            var layout = choices.GetComponent<VerticalLayoutGroup>();
            if (layout == null) { layout = choices.AddComponent<VerticalLayoutGroup>(); created = true; }
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var fitter = choices.GetComponent<ContentSizeFitter>();
            if (fitter == null) { fitter = choices.AddComponent<ContentSizeFitter>(); created = true; }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject template = BuildChoiceButton(choices.transform, ref created);

            GameObject continueGO = FindOrCreateRect(panel.transform, "ContinueButton", ref created);
            var continueRect = continueGO.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(1f, 0f);
            continueRect.anchorMax = new Vector2(1f, 0f);
            continueRect.pivot = new Vector2(1f, 0f);
            continueRect.anchoredPosition = new Vector2(-16f, 12f);
            continueRect.sizeDelta = new Vector2(150f, 40f);
            var continueImage = continueGO.GetComponent<Image>();
            if (continueImage == null) { continueImage = continueGO.AddComponent<Image>(); created = true; }
            continueImage.color = new Color(0.2f, 0.35f, 0.55f, 0.95f);
            if (continueGO.GetComponent<Button>() == null)
            {
                continueGO.AddComponent<Button>();
                created = true;
            }
            FindOrCreateText(continueGO.transform, "Label", ref created,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                20f, Color.white, TextAlignmentOptions.Center);

            var so = new SerializedObject(ui);
            Assign(so, "_speakerText", speakerText);
            Assign(so, "_dialogueText", dialogueText);
            Assign(so, "_choicesContainer", choices);
            Assign(so, "_choiceButtonTemplate", template);
            Assign(so, "_continueButton", continueGO);
            so.ApplyModifiedPropertiesWithoutUndo();

            continueGO.SetActive(false);
            choices.SetActive(false);

            return canvasGO;
        }

        private static GameObject BuildChoiceButton(Transform parent, ref bool created)
        {
            GameObject button = FindOrCreateRect(parent, "ChoiceButtonTemplate", ref created);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 40f);

            var image = button.GetComponent<Image>();
            if (image == null) { image = button.AddComponent<Image>(); created = true; }
            image.color = new Color(0.16f, 0.16f, 0.2f, 0.95f);

            if (button.GetComponent<Button>() == null)
            {
                button.AddComponent<Button>();
                created = true;
            }

            FindOrCreateText(button.transform, "Label", ref created,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                20f, Color.white, TextAlignmentOptions.Center);

            return button;
        }

        private static GameObject FindOrCreateRect(Transform parent, string name, ref bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            created = true;
            return go;
        }

        private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, ref bool created,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = FindOrCreateRect(parent, name, ref created);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (text == null) { text = go.AddComponent<TextMeshProUGUI>(); created = true; }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset == null)
            {
                DialogueLogger.LogWarning(
                    "TextMeshPro essential resources are not imported. " +
                    "Window > TextMeshPro > Import TMP Essential Resources. " +
                    "The DialogueOverlayUI fallback keeps dialogue visible until then.");
            }
            else
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static void Assign(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
