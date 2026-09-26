using UnityEngine;
using MySys22.DialogueEngine.Core;
using MySys22.DialogueEngine.Bridge;
using MySys22.DialogueEngine.Core.SIMD;
using MySys22.DialogueEngine.UI;

namespace MySys22.DialogueEngine
{

    [DisallowMultipleComponent]
    public class GameSetup : MonoBehaviour
    {
        [Header("Dialogue")]
        [Tooltip("Graph file. Empty = first *.graph.yaml in StreamingAssets/" + DialoguePaths.GraphsFolderName + ".")]
        [SerializeField] private string _graphPath = "";
        [SerializeField] private bool _autoStartDialogue = true;

        [Header("Language")]
        [Tooltip("Optional override. Empty = engine.manifest.json default / saved preference.")]
        [SerializeField] private string _languageOverride = "";

        [Header("Presentation")]
        [Tooltip("Creates a simple IMGUI overlay when no wired DialogueUICanvas is present.")]
        [SerializeField] private bool _createOverlayFallback = true;

        [Header("Diagnostics")]
        [SerializeField] private bool _enableLogs = true;
        [SerializeField] private bool _validateOnLoad = true;

        [Header("SIMD")]
        [SerializeField] private bool _enableSimd = true;
        [SerializeField] private bool _logSimdArchitecture = false;

        private DialogueBridge _bridge;

        public DialogueBridge Bridge => _bridge;

        void Awake()
        {
            DialogueLogger.Enabled = _enableLogs;

            SimdOptimizer.Enabled = _enableSimd;
            SimdHelper.EnableSimd = _enableSimd;
            if (_logSimdArchitecture) SimdHelper.LogArchitecture();

            DialoguePaths.EnsureLayout();
            RegisterExampleActions();

            _bridge = FindFirstObjectByType<DialogueBridge>();
            if (_bridge == null)
            {
                var go = new GameObject("DialogueBridge");
                go.transform.SetParent(transform, false);
                _bridge = go.AddComponent<DialogueBridge>();
            }

            if (!string.IsNullOrWhiteSpace(_graphPath))
                _bridge.SetGraphPath(_graphPath);

            if (!string.IsNullOrWhiteSpace(_languageOverride))
                LanguageManager.CurrentLanguage = _languageOverride;

            EnsurePresentation();
        }

        void Start()
        {
            if (_bridge == null) return;

            if (_validateOnLoad && _bridge.Project?.Graph != null)
                _bridge.Validate();

            if (_autoStartDialogue && !_bridge.IsDialogueActive)
                _bridge.StartDialogue();
        }

        private void EnsurePresentation()
        {
            var canvas = FindFirstObjectByType<DialogueUICanvas>();
            bool wired = canvas != null && canvas.IsWired;

            if (!wired)
            {
                if (!_createOverlayFallback)
                {
                    DialogueLogger.LogWarning(
                        "No wired DialogueUICanvas found. Assign the TMP references in the inspector " +
                        "or enable the overlay fallback on GameSetup.");
                }
                else if (FindFirstObjectByType<DialogueOverlayUI>() == null)
                {
                    var go = new GameObject("DialogueOverlayUI");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<DialogueOverlayUI>();
                    DialogueLogger.Log("Overlay fallback created: dialogue text will render on screen.");
                }
            }
        }

        private void RegisterExampleActions() => ActionRegistry.EnsureBuiltIns();
    }
}
