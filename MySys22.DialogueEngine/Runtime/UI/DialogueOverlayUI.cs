using System.Collections.Generic;
using UnityEngine;
using MySys22.DialogueEngine.Bridge;
using MySys22.DialogueEngine.Core;
using MySys22.Engine.API;

namespace MySys22.DialogueEngine.UI
{

    [DisallowMultipleComponent]
    public class DialogueOverlayUI : MonoBehaviour, IDialogueView
    {
        [Header("References")]
        [Tooltip("Leave empty to auto-find a DialogueBridge in the scene.")]
        [SerializeField] private DialogueBridge _bridge;

        [Header("Look")]
        [SerializeField] private bool _visible = true;
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private float _panelHeight = 170f;
        [SerializeField] private float _margin = 24f;
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.78f);
        [SerializeField] private Color _speakerColor = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private KeyCode _continueKey = KeyCode.Space;
        [SerializeField] private bool _clickToContinue = true;

        [Header("Behaviour")]
        [SerializeField] private bool _hideOnEnd = true;

        private string _speaker = "";
        private string _text = "";
        private readonly List<ChoiceOption> _choices = new List<ChoiceOption>();
        private bool _showing;
        private bool _waitingForChoice;
        private bool _waitingForAction;
        private bool _subscribed;

        void Awake()
        {
            ResolveBridge();
            Subscribe();
        }

        void Start()
        {
            if (!_subscribed)
            {
                ResolveBridge();
                Subscribe();
            }
        }

        void OnDestroy() => Unsubscribe();

        private void ResolveBridge()
        {
            if (_bridge == null) _bridge = FindFirstObjectByType<DialogueBridge>();
        }

        private void Subscribe()
        {
            if (_bridge == null || _subscribed) return;
            _bridge.OnLine += HandleLine;
            _bridge.OnChoices += HandleChoices;
            _bridge.OnDialogueEnd += HandleEnd;
            _bridge.OnWaitingForActionChanged += HandleWaitingForAction;
            _subscribed = true;
            DialogueLogger.Log("DialogueOverlayUI subscribed.");
        }

        private void Unsubscribe()
        {
            if (_bridge == null || !_subscribed) return;
            _bridge.OnLine -= HandleLine;
            _bridge.OnChoices -= HandleChoices;
            _bridge.OnDialogueEnd -= HandleEnd;
            _bridge.OnWaitingForActionChanged -= HandleWaitingForAction;
            _subscribed = false;
        }

        public bool IsPanelVisible => _showing && _visible;

        public void SetPanelVisible(bool visible)
        {
            _showing = visible;
            if (!visible)
            {
                _choices.Clear();
                _waitingForChoice = false;
                _waitingForAction = false;
            }
        }

        public void ShowText(string speaker, string text)
        {
            if (!string.IsNullOrEmpty(speaker)) _speaker = speaker;
            _text = text ?? "";
            _choices.Clear();
            _waitingForChoice = false;
            _waitingForAction = false;
            _showing = true;
        }

        public void ShowText(string text) => ShowText(null, text);

        public void SetSpeaker(string speaker) => _speaker = speaker ?? "";

        public void SetText(string text) => _text = text ?? "";

        public void HideText() => _text = "";

        public void HideChoices()
        {
            _choices.Clear();
            _waitingForChoice = false;
        }

        public void SetBridge(DialogueBridge bridge)
        {
            Unsubscribe();
            _bridge = bridge;
            Subscribe();
        }

        private void HandleLine(string speaker, string text)
        {
            _speaker = CharacterRegistry.DisplayName(speaker);
            _text = string.IsNullOrEmpty(text) ? "[MISSING TEXT]" : text;
            _choices.Clear();
            _waitingForChoice = false;
            _showing = true;
        }

        private void HandleChoices(List<ChoiceOption> choices)
        {
            _choices.Clear();
            if (choices != null) _choices.AddRange(choices);
            _waitingForChoice = _choices.Count > 0;
            _showing = true;
        }

        private void HandleWaitingForAction(bool waiting)
        {
            _waitingForAction = waiting;
            if (waiting)
            {
                _choices.Clear();
                _waitingForChoice = false;
                _showing = true;
            }
        }

        private void HandleEnd()
        {
            _showing = !_hideOnEnd;
            _waitingForChoice = false;
            _choices.Clear();
        }

        void Update()
        {
            if (!_showing || !_visible) return;
            if (_waitingForChoice || _waitingForAction) return;
            if (DialogueInput.ContinuePressed(_continueKey) ||
                (_clickToContinue && DialogueInput.ClickPressed()))
                _bridge?.Continue();
        }

        void OnGUI()
        {
            if (!_visible || !_showing) return;

            float width = Screen.width - _margin * 2f;
            var panel = new Rect(_margin, Screen.height - _panelHeight - _margin, width, _panelHeight);

            GUI.color = _panelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var speakerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            speakerStyle.normal.textColor = _speakerColor;

            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                wordWrap = true
            };
            textStyle.normal.textColor = _textColor;

            GUI.Label(new Rect(panel.x + 16, panel.y + 12, panel.width - 32, 26), _speaker, speakerStyle);
            GUI.Label(new Rect(panel.x + 16, panel.y + 44, panel.width - 32, panel.height - 56), _text, textStyle);

            if (_waitingForAction)
            {
                var waitingStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _fontSize,
                    fontStyle = FontStyle.Italic
                };
                waitingStyle.normal.textColor = _speakerColor;
                string action = _bridge != null ? _bridge.PendingActionId : null;
                GUI.Label(new Rect(panel.x + 16, panel.y + panel.height - 30, panel.width - 32, 24),
                    string.IsNullOrEmpty(action) ? "..." : $"... waiting for '{action}'", waitingStyle);
            }
            else if (_waitingForChoice)
            {
                float buttonY = panel.y + 44;
                for (int i = 0; i < _choices.Count; i++)
                {
                    if (GUI.Button(new Rect(panel.x + 16, buttonY + i * 30, panel.width - 32, 26),
                            $"{i + 1}. {_choices[i].Text}"))
                    {
                        _bridge?.Choose(i);
                        _waitingForChoice = false;
                        _choices.Clear();
                    }
                }
            }
            else if (_clickToContinue &&
                     Event.current != null && Event.current.type == EventType.MouseDown &&
                     panel.Contains(Event.current.mousePosition))
            {
                _bridge?.Continue();
            }
        }
    }
}
