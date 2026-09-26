using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using MySys22.DialogueEngine.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MySys22.DialogueEngine.Editor
{

    public class DialogueNodeView : Node
    {
        public string NodeId { get; private set; }

        private PopupField<string> _speakerDropdown;
        private TextField _speakerFieldFallback;
        private IntegerField _startDidField;
        private IntegerField _endDidField;

        private ObjectField _audioField;
        private TextField _audioNameField;
        private Toggle _audioLoopToggle;
        private Toggle _voiceToggle;
        private TextField _onParallelField;
        private TextField _onCompleteField;
        private Port _inputPort;
        private Port _outputPort;

        private string _currentLanguage = DialoguePaths.DefaultLanguage;
        private readonly List<string> _availableSpeakers = new List<string>();

        public DialogueNodeView(string title, string nodeId)
        {
            NodeId = nodeId;
            titleContainer.Remove(titleContainer.Q<Label>());
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(titleLabel);

            AddToClassList("dialogue-node");

            _inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(int));
            _inputPort.portName = "In";
            inputContainer.Add(_inputPort);

            var mainContainer = new VisualElement();
            mainContainer.style.marginTop = 8;

            _currentLanguage = LanguageManager.IsInitialized
                ? LanguageManager.CurrentLanguage
                : DialoguePaths.DefaultLanguage;

            LoadAvailableSpeakers();

            if (_availableSpeakers.Count > 0)
            {
                _speakerDropdown = new PopupField<string>(
                    "Speaker", _availableSpeakers, _availableSpeakers[0], SpeakerLabel, SpeakerLabel);
                _speakerDropdown.style.marginBottom = 4;
                mainContainer.Add(_speakerDropdown);
            }
            else
            {
                _speakerFieldFallback = new TextField("Speaker (CID)") { value = "" };
                _speakerFieldFallback.tooltip =
                    "Character CID. Deploy a project from the MySys22 Dialogue Editor to get a dropdown.";
                _speakerFieldFallback.style.marginBottom = 4;
                mainContainer.Add(_speakerFieldFallback);
            }

            _startDidField = new IntegerField("Start DID") { value = 1 };
            _startDidField.style.marginBottom = 4;
            mainContainer.Add(_startDidField);

            _endDidField = new IntegerField("End DID") { value = 2 };
            _endDidField.style.marginBottom = 4;
            mainContainer.Add(_endDidField);

            _audioNameField = new TextField("Audio") { value = "" };
            _audioNameField.tooltip =
                "Audio track name resolved by the game (Resources, provider or RegisterAudioClip).\n" +
                "Played when the node starts.";
            _audioNameField.style.marginBottom = 4;
            mainContainer.Add(_audioNameField);

            _audioField = new ObjectField("Add clip")
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            _audioField.tooltip = "Pick a clip from the project to fill the track name.";
            _audioField.style.marginBottom = 4;
            _audioField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is AudioClip clip) _audioNameField.value = clip.name;
            });
            mainContainer.Add(_audioField);

            _audioLoopToggle = new Toggle("Loop audio") { value = false };
            _audioLoopToggle.style.marginBottom = 4;
            mainContainer.Add(_audioLoopToggle);

            _voiceToggle = new Toggle("Voice per line") { value = false };
            _voiceToggle.tooltip =
                "Plays <speaker>/<did> for every line of this node (voice over convention).";
            _voiceToggle.style.marginBottom = 4;
            mainContainer.Add(_voiceToggle);

            _onParallelField = new TextField("On Parallel") { value = "" };
            _onParallelField.style.marginBottom = 4;
            mainContainer.Add(_onParallelField);

            _onCompleteField = new TextField("On Complete") { value = "" };
            mainContainer.Add(_onCompleteField);

            mainContainer.Add(new VisualElement { style = { height = 8 } });

            _outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(int));
            _outputPort.portName = "Next";
            outputContainer.Add(_outputPort);

            extensionContainer.Add(mainContainer);
            RefreshExpandedState();
        }

        private void LoadAvailableSpeakers()
        {
            _availableSpeakers.Clear();

            DialoguePaths.EnsureLayout();

            try
            {
                string basePath = DialoguePaths.LanguageFolder(_currentLanguage);
                if (Directory.Exists(basePath))
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();

                    foreach (string filePath in Directory.GetFiles(basePath, "*.yaml", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var data = deserializer.Deserialize<LineDatabaseData>(File.ReadAllText(filePath));
                            if (data != null && !string.IsNullOrWhiteSpace(data.Character)
                                && !_availableSpeakers.Contains(data.Character))
                            {
                                _availableSpeakers.Add(data.Character);
                            }
                        }
                        catch {  }
                    }
                }
            }
            catch {  }

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

        public void SetData(NodeData data)
        {
            if (_speakerDropdown != null)
            {
                if (!string.IsNullOrEmpty(data.Speaker) && _availableSpeakers.Contains(data.Speaker))
                    _speakerDropdown.value = data.Speaker;
                else if (!string.IsNullOrEmpty(data.Speaker))
                {

                    _availableSpeakers.Add(data.Speaker);
                    _speakerDropdown.choices = new List<string>(_availableSpeakers);
                    _speakerDropdown.value = data.Speaker;
                }
                else if (_availableSpeakers.Count > 0)
                    _speakerDropdown.value = _availableSpeakers[0];
            }
            else if (_speakerFieldFallback != null)
            {
                _speakerFieldFallback.value = data.Speaker ?? "";
            }

            _startDidField.value = data.StartDid ?? 1;
            _endDidField.value = data.EndDid ?? 1;
            _onParallelField.value = data.OnParallel ?? "";
            _onCompleteField.value = data.OnComplete ?? "";
        }

        public NodeData GetData()
        {
            string speaker = _speakerDropdown != null
                ? _speakerDropdown.value
                : _speakerFieldFallback?.value ?? "";

            speaker = speaker == null ? "" : speaker.Trim();

            return new NodeData
            {
                Id = NodeId,
                Type = "dialogue",
                Speaker = speaker,
                StartDid = _startDidField.value,
                EndDid = _endDidField.value,
                OnParallel = string.IsNullOrEmpty(_onParallelField.value) ? null : _onParallelField.value,
                OnComplete = string.IsNullOrEmpty(_onCompleteField.value) ? null : _onCompleteField.value,
                Position = new PositionData { X = GetPosition().x, Y = GetPosition().y }
            };
        }

        public Port GetOutputPort() => _outputPort;

        public void RefreshSpeakers()
        {
            string previous = _speakerDropdown?.value;
            LoadAvailableSpeakers();

            if (_speakerDropdown != null)
            {
                _speakerDropdown.choices = new List<string>(_availableSpeakers);
                if (!string.IsNullOrEmpty(previous) && _availableSpeakers.Contains(previous))
                    _speakerDropdown.value = previous;
                else if (_availableSpeakers.Count > 0)
                    _speakerDropdown.value = _availableSpeakers[0];
            }
        }
    }
}
