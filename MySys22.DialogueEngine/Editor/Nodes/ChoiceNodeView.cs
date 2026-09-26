using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public class ChoiceNodeView : Node
    {
        public string NodeId { get; private set; }

        private VisualElement _choicesContainer;
        private List<ChoiceEntry> _choiceEntries = new List<ChoiceEntry>();
        private Port _inputPort;
        private PopupField<string> _speakerDropdown;
        private TextField _speakerFieldFallback;
        private readonly List<string> _availableSpeakers = new List<string>();

        public class ChoiceEntry
        {
            public string Guid;
            public TextField TextField;
            public IntegerField TextDidField;

            public Port OutputPort;
            public VisualElement Container;
        }

        public ChoiceNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("choice-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(int));
            _inputPort.portName = "In";
            inputContainer.Add(_inputPort);

            LoadAvailableSpeakers();

            if (_availableSpeakers.Count > 0)
            {
                _speakerDropdown = new PopupField<string>(
                    "Speaker", _availableSpeakers, 0, SpeakerLabel, SpeakerLabel);
                _speakerDropdown.tooltip =
                    "Line database used to resolve text_did on the options. Leave empty to use the " +
                    $"engine default '{DialogueGraphPlayer.DefaultChoiceSpeaker}'.";
                _speakerDropdown.style.marginTop = 6;
                extensionContainer.Add(_speakerDropdown);
            }
            else
            {
                _speakerFieldFallback = new TextField("Speaker (CID)") { value = "" };
                _speakerFieldFallback.tooltip =
                    "Optional. CID used to resolve the options' text_did.";
                _speakerFieldFallback.style.marginTop = 6;
                extensionContainer.Add(_speakerFieldFallback);
            }

            _choicesContainer = new VisualElement();
            _choicesContainer.style.marginTop = 8;
            extensionContainer.Add(_choicesContainer);

            var addButton = new Button(() => AddChoice()) { text = "+ Add Choice" };
            addButton.style.marginTop = 8;
            extensionContainer.Add(addButton);

            AddChoice();

            RefreshExpandedState();
        }

        private void LoadAvailableSpeakers()
        {
            _availableSpeakers.Clear();
            DialoguePaths.EnsureLayout();

            CharacterRegistry.Load();
            foreach (var entry in CharacterRegistry.Entries)
            {
                string cid = entry.Cid;
                if (!string.IsNullOrEmpty(cid) && !_availableSpeakers.Contains(cid))
                    _availableSpeakers.Add(cid);
            }

            _availableSpeakers.Sort((a, b) => string.CompareOrdinal(SpeakerLabel(a), SpeakerLabel(b)));
        }

        private static string SpeakerLabel(string cid)
        {
            if (string.IsNullOrEmpty(cid)) return cid;
            if (CharacterRegistry.TryGetDisplayName(cid, out string name) && !string.IsNullOrWhiteSpace(name))
                return $"{name} ({cid})";
            return cid;
        }

        private void AddChoice(string text = "New choice", string guid = null, int textDid = 0)
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString();

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginBottom = 4;
            container.name = "Choice_" + guid;

            var textField = new TextField("Option") { value = text };
            textField.style.flexGrow = 1;
            textField.style.marginRight = 4;
            textField.tooltip = "Authoring label. With text_did > 0 it is only the fallback text.";
            container.Add(textField);

            var didField = new IntegerField("DID") { value = textDid };
            didField.style.width = 70;
            didField.style.marginRight = 4;
            didField.tooltip = "Localized option text (0 = use the inline label).";
            container.Add(didField);

            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(int));
            port.portName = "→";
            port.userData = guid;
            port.style.width = 20;
            container.Add(port);

            var removeButton = new Button(() =>
            {
                _choicesContainer.Remove(container);
                _choiceEntries.RemoveAll(e => e.Container == container);
                RefreshExpandedState();
            }) { text = "✕" };
            removeButton.style.width = 20;
            container.Add(removeButton);

            _choicesContainer.Add(container);
            _choiceEntries.Add(new ChoiceEntry
            {
                Guid = guid,
                TextField = textField,
                TextDidField = didField,
                OutputPort = port,
                Container = container
            });

            RefreshExpandedState();
        }

        public void SetData(NodeData data)
        {
            foreach (var entry in _choiceEntries)
                _choicesContainer.Remove(entry.Container);
            _choiceEntries.Clear();

            foreach (var choice in data.Choices)
            {
                string guid = string.IsNullOrEmpty(choice.PortGuid) ? System.Guid.NewGuid().ToString() : choice.PortGuid;
                AddChoice(choice.Text, guid, choice.TextDid);
            }

            SetSpeakerValue(data.Speaker);
            RefreshExpandedState();
        }

        private void SetSpeakerValue(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker)) return;

            if (_speakerDropdown != null)
            {
                if (!_availableSpeakers.Contains(speaker))
                {
                    _availableSpeakers.Add(speaker);
                    _speakerDropdown.choices = new List<string>(_availableSpeakers);
                }
                _speakerDropdown.value = speaker;
            }
            else if (_speakerFieldFallback != null)
            {
                _speakerFieldFallback.value = speaker;
            }
        }

        private string GetSpeakerValue()
        {
            string speaker = _speakerDropdown != null
                ? _speakerDropdown.value
                : _speakerFieldFallback?.value ?? "";
            return string.IsNullOrWhiteSpace(speaker) ? null : speaker.Trim();
        }

        public NodeData GetData()
        {
            var choices = new List<ChoiceData>();
            foreach (var entry in _choiceEntries)
            {
                choices.Add(new ChoiceData
                {
                    Text = entry.TextField.value,
                    TextDid = entry.TextDidField.value,
                    PortGuid = entry.Guid,
                    Next = null
                });
            }

            return new NodeData
            {
                Id = NodeId,
                Type = "choice",
                Speaker = GetSpeakerValue(),
                Choices = choices,
                Position = new PositionData
                {
                    X = GetPosition().x,
                    Y = GetPosition().y
                }
            };
        }

        public List<ChoiceEntry> GetChoiceEntries() => _choiceEntries;
        public Port GetInputPort() => _inputPort;
    }
}
