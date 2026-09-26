using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MySys22.DialogueEngine.Core;
using MySys22.DialogueEngine.Bridge;
using MySys22.Engine.API;

namespace MySys22.DialogueEngine.UI
{
    public class DialogueUICanvas : MonoBehaviour, IDialogueView
    {
        [Header("UI References")]
        [Tooltip("Panel that contains the texts, choices and continue button. Assigned automatically.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _speakerText;
        [SerializeField] private TextMeshProUGUI _dialogueText;
        [SerializeField] private GameObject _choicesContainer;
        [SerializeField] private GameObject _choiceButtonTemplate;
        [SerializeField] private GameObject _continueButton;

        [Header("Config")]
        [SerializeField] private bool _hideOnEnd = true;
        [SerializeField] private float _typewriterDelay = 0.03f;
        [SerializeField] private KeyCode _continueKey = KeyCode.Space;
        [Tooltip("Also advance on mouse click / touch (mobile builds).")]
        [SerializeField] private bool _advanceOnClick = true;

        [Header("Button Sizes")]
        [SerializeField] private float _buttonWidth = 300f;
        [SerializeField] private float _buttonHeight = 40f;

        private DialogueBridge _bridge;
        private List<GameObject> _choiceButtons = new List<GameObject>();

        private bool _isTyping = false;
        private string _fullText = "";
        private int _currentCharIndex = 0;
        private float _typewriterTimer = 0f;
        private bool _isWaitingForChoice = false;
        private bool _isWaitingForAction = false;
        private bool _isSubscribed = false;
        private bool _uiVisible = false;
        private bool _isReady = false;

        public bool IsWired => _dialogueText != null;

        public string CurrentSpeakerLabel { get; private set; }

        public bool IsPanelVisible => _uiVisible;

        private GameObject Panel
        {
            get
            {
                if (_panel == null && _dialogueText != null)
                    _panel = _dialogueText.transform.parent != null
                        ? _dialogueText.transform.parent.gameObject
                        : _dialogueText.gameObject;
                return _panel;
            }
        }

        public void SetPanelVisible(bool visible)
        {
            _uiVisible = visible;
            if (Panel != null) Panel.SetActive(visible);
            if (!visible)
            {
                _isTyping = false;
                ClearChoices();
            }
        }

        public void ShowText(string speaker, string text)
        {
            SetPanelVisible(true);
            _isTyping = false;
            _isWaitingForChoice = false;
            _isWaitingForAction = false;

            if (!string.IsNullOrEmpty(speaker))
            {
                CurrentSpeakerLabel = speaker;
                if (_speakerText != null)
                {
                    _speakerText.text = speaker;
                    _speakerText.gameObject.SetActive(true);
                }
            }

            _fullText = text ?? "";
            if (_dialogueText != null)
            {
                _dialogueText.text = _fullText;
                _dialogueText.gameObject.SetActive(true);
            }

            if (_continueButton != null) _continueButton.SetActive(false);
            ClearChoices();
        }

        public void ShowText(string text) => ShowText(null, text);

        public void HideText()
        {
            if (_dialogueText != null)
            {
                _dialogueText.text = "";
                _dialogueText.gameObject.SetActive(false);
            }
            if (_speakerText != null) _speakerText.gameObject.SetActive(false);
            _isTyping = false;
        }

        public void HideChoices() => ClearChoices();

        void Awake()
        {
            SetupChoicesContainer();
            _bridge = FindFirstObjectByType<DialogueBridge>();
            if (_bridge != null) SubscribeToBridge();
            else DialogueLogger.LogWarning("Bridge not found at Awake. Will search in Start.");
        }

        void Start()
        {
            if (_bridge == null)
            {
                _bridge = FindFirstObjectByType<DialogueBridge>();
                if (_bridge != null && !_isSubscribed) SubscribeToBridge();
            }
            if (_bridge == null)
            {
                DialogueLogger.LogError("313", "DialogueBridge not found in scene");
                return;
            }
            _isReady = true;

            EnsureEventSystem();

            if (_continueButton != null)
            {
                var button = _continueButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveListener(ContinueDialogue);
                    button.onClick.AddListener(ContinueDialogue);
                }
            }

            if (_dialogueText == null)
            {
                DialogueLogger.LogWarning(
                    "DialogueUICanvas has no 'Dialogue Text' assigned. Use " +
                    "MySys22/Dialogue/Setup Scene or the GameSetup overlay fallback.");
            }
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if MYSYS22_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            DialogueLogger.Log("EventSystem created: dialogue choice buttons need one to receive clicks.");
        }

        void Update()
        {
            if (!_isReady) return;

            if (_uiVisible && !_isWaitingForChoice && !_isWaitingForAction && !_isTyping)
            {
                if (DialogueInput.ContinuePressed(_continueKey) ||
                    (_advanceOnClick && DialogueInput.ClickPressed()))
                    ContinueDialogue();
            }

            if (_isTyping && _dialogueText != null)
            {
                _typewriterTimer += Time.unscaledDeltaTime;
                if (_typewriterTimer >= _typewriterDelay)
                {
                    _typewriterTimer = 0f;
                    _currentCharIndex++;
                    if (_currentCharIndex <= _fullText.Length)
                        _dialogueText.text = _fullText.Substring(0, _currentCharIndex);
                    else
                    {
                        _isTyping = false;
                        if (_continueButton != null && !_isWaitingForChoice && !_isWaitingForAction)
                            _continueButton.SetActive(true);
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (_bridge != null)
            {
                _bridge.OnLine -= OnDialogueLine;
                _bridge.OnChoices -= OnDialogueChoices;
                _bridge.OnDialogueEnd -= OnDialogueEnd;
                _bridge.OnWaitingForActionChanged -= OnWaitingForActionChanged;
            }
        }

        private void SubscribeToBridge()
        {
            if (_bridge == null || _isSubscribed) return;
            _bridge.OnLine += OnDialogueLine;
            _bridge.OnChoices += OnDialogueChoices;
            _bridge.OnDialogueEnd += OnDialogueEnd;
            _bridge.OnWaitingForActionChanged += OnWaitingForActionChanged;
            _isSubscribed = true;
            DialogueLogger.Log("Subscribed to bridge.");
        }

        private void OnWaitingForActionChanged(bool waiting)
        {
            _isWaitingForAction = waiting;
            if (_continueButton != null && !_isWaitingForChoice)
                _continueButton.SetActive(!waiting && !_isTyping && _uiVisible);
        }
        private void OnDialogueLine(string speaker, string text)
        {
            DialogueLogger.Log($"OnDialogueLine: speaker='{speaker}', text='{text}'");
            ShowUI(true);
            _isWaitingForChoice = false;

            CurrentSpeakerLabel = CharacterRegistry.DisplayName(speaker);

            if (_speakerText != null)
            {
                _speakerText.text = CurrentSpeakerLabel;
                _speakerText.gameObject.SetActive(true);
            }

            if (string.IsNullOrEmpty(text))
            {
                text = "[MISSING TEXT]";
                DialogueLogger.LogWarning($"Missing line for {speaker} (DID not found)");
            }

            _fullText = text;
            _currentCharIndex = 0;
            _isTyping = true;
            _typewriterTimer = 0f;
            if (_dialogueText != null)
            {
                _dialogueText.text = "";
                _dialogueText.gameObject.SetActive(true);
            }

            if (_continueButton != null) _continueButton.SetActive(false);
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
            ClearChoices();
        }

        private void OnDialogueChoices(List<ChoiceOption> choices)
        {
            DialogueLogger.Log($"OnDialogueChoices: {choices.Count} choices");
            _isTyping = false;
            _isWaitingForChoice = true;

            if (_dialogueText != null) _dialogueText.text = _fullText;
            if (_continueButton != null) _continueButton.SetActive(false);

            if (_choicesContainer == null || _choiceButtonTemplate == null)
            {
                DialogueLogger.LogWarning("Choices container or template not assigned.");
                return;
            }

            ClearChoices();
            _choicesContainer.SetActive(true);

            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                int index = i;

                GameObject buttonGO = Instantiate(_choiceButtonTemplate, _choicesContainer.transform);
                buttonGO.SetActive(true);

                var rect = buttonGO.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(_buttonWidth, _buttonHeight);

                var tmp = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = choice.Text;

                var btn = buttonGO.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        _bridge.Choose(index);
                        _isWaitingForChoice = false;
                        _choicesContainer.SetActive(false);
                    });
                }

                _choiceButtons.Add(buttonGO);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_choicesContainer.GetComponent<RectTransform>());
        }

        private void OnDialogueEnd()
        {
            DialogueLogger.Log("OnDialogueEnd");
            _isTyping = false;
            _isWaitingForChoice = false;
            ClearChoices();

            if (_hideOnEnd)
                ShowUI(false);
            else
            {
                if (_speakerText != null) { _speakerText.text = "*** END ***"; _speakerText.gameObject.SetActive(true); }
                if (_dialogueText != null) { _dialogueText.text = "Dialogue has ended."; _dialogueText.gameObject.SetActive(true); }
                if (_continueButton != null) _continueButton.SetActive(false);
                if (_choicesContainer != null) _choicesContainer.SetActive(false);
            }
        }

        public void ContinueDialogue()
        {
            _bridge?.Continue();
        }

        public void SetSpeaker(string speaker)
        {
            if (_speakerText != null) _speakerText.text = speaker;
        }

        public void SetText(string text)
        {
            if (_dialogueText != null) _dialogueText.text = text;
        }

        public void SkipTyping()
        {
            if (_isTyping)
            {
                _isTyping = false;
                if (_dialogueText != null) _dialogueText.text = _fullText;
                if (_continueButton != null && !_isWaitingForChoice)
                    _continueButton.SetActive(true);
            }
        }

        private void ShowUI(bool show)
        {
            _uiVisible = show;
            if (Panel != null) Panel.SetActive(show);
            if (_dialogueText != null) _dialogueText.gameObject.SetActive(show);
            if (_speakerText != null) _speakerText.gameObject.SetActive(show);
            if (_continueButton != null) _continueButton.SetActive(false);
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
        }

        private void ClearChoices()
        {
            foreach (var go in _choiceButtons) Destroy(go);
            _choiceButtons.Clear();
            if (_choicesContainer != null) _choicesContainer.SetActive(false);
        }

        private void SetupChoicesContainer()
        {
            if (_choicesContainer == null)
            {
                DialogueLogger.LogWarning("ChoicesContainer not assigned. Choices won't be shown.");
                return;
            }

            var layoutGroup = _choicesContainer.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = _choicesContainer.AddComponent<VerticalLayoutGroup>();
                DialogueLogger.Log("Added VerticalLayoutGroup to ChoicesContainer");
            }
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.spacing = 5;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;

            var sizeFitter = _choicesContainer.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null)
            {
                sizeFitter = _choicesContainer.AddComponent<ContentSizeFitter>();
                DialogueLogger.Log("Added ContentSizeFitter to ChoicesContainer");
            }
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rect = _choicesContainer.GetComponent<RectTransform>();
            if (rect != null && rect.sizeDelta.x == 0)
                rect.sizeDelta = new Vector2(_buttonWidth + 20, rect.sizeDelta.y);

            if (_choiceButtonTemplate != null && _choiceButtonTemplate.activeSelf)
            {
                _choiceButtonTemplate.SetActive(false);
                DialogueLogger.Log("ChoiceButtonTemplate disabled automatically.");
            }
            DialogueLogger.Log("ChoicesContainer configured automatically.");
        }
    }
}
