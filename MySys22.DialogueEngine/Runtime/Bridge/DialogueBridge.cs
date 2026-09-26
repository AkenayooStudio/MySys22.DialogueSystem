using System;
using System.Collections.Generic;
using UnityEngine;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Bridge
{

    [DisallowMultipleComponent]
    public class DialogueBridge : MonoBehaviour
    {
        [Header("Project")]
        [Tooltip("Graph file. Empty = first *.graph.yaml in StreamingAssets/" + DialoguePaths.GraphsFolderName + ".")]
        [SerializeField] private string _graphPath = "";

        [Tooltip("Startup language. Empty = saved preference, then engine.manifest.json, then the first deployed folder.")]
        [SerializeField] private string _language = "";

        [Header("Behaviour")]
        [SerializeField] private bool _autoStartOnAwake = true;
        [SerializeField] private bool _logEvents = false;
        [SerializeField] private bool _logEveryLine = true;
        [SerializeField] private bool _validateOnLoad = true;

        private DialogueGraphPlayer _player;
        private ILineProvider _lineProvider;
        private DialogueProject _project;

        private bool _isInitialized;
        private bool _isDialogueActive;
        private bool _startWhenReady;
        private bool _initializing;

        public event Action<string, string> OnLine;

        public event Action<List<ChoiceOption>> OnChoices;

        public event Action OnDialogueEnd;

        public event Action<bool> OnWaitingForActionChanged;

        public event Action<string, string> OnGraphJumped;

        public event Action<string, DialogueValue> OnVariableChanged;

        public event Action<string, QuestState> OnQuestChanged;
        public event Action<DialoguePosition> OnPositionChanged;

        public bool IsInitialized => _isInitialized;
        public bool IsDialogueActive => _isDialogueActive;

        public bool IsWaitingForAction => _player != null && _player.IsWaitingForAction;

        public string PendingActionId => _player?.PendingActionId;

        public DialogueProject Project => _project;
        public GraphData Graph => _project?.Graph;
        public string GraphPath => _project?.GraphPath;
        public DialogueProjectManifest Manifest => _project?.Manifest;

        public DialogueGraphPlayer GetPlayer() => _player;

        public DialoguePosition Position => _player?.GetPosition();

        public DialoguePosition GetPosition() => _player?.GetPosition();

        public void SetFunctionProgress(string progress) => _player?.SetFunctionProgress(progress);

        public string SavePosition() => _player?.GetPosition()?.ToSnapshot() ?? "";

        void Awake()
        {
            if (DialogueStreamingAssets.RequiresAsync)
            {
                InitializeAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        DialogueLogger.LogError("311", "Async initialization failed",
                            task.Exception?.GetBaseException()?.Message);
                });
                return;
            }
            Initialize();
        }

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            if (_isInitialized || _initializing) return;
            _initializing = true;

            try
            {
                _project = await DialogueProjectLoader.LoadAsync(_graphPath);
                if (_project.Graph == null)
                {
                    DialogueLogger.LogError("317", "Graph load failed", "Cannot load any dialogue graph");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_language))
                    LanguageManager.TrySetLanguage(_language, persist: false);

                _lineProvider = YamlLineProvider.Instance;
                _player = new DialogueGraphPlayer();
                _player.GraphResolver = DialogueProjectLoader.LoadGraphByName;
                SubscribeEvents();
                _isInitialized = true;

                AttachVariables(force: true);
                DialogueLogger.Log($"Bridge initialized (async). Graph: {_project.Graph.GraphId}, " +
                                   $"language: {LanguageManager.CurrentLanguage}");

                if (_validateOnLoad)
                    DialogueProjectValidator.ValidateAndLog(_project.Graph, _lineProvider, _project.Language);

                if (_startWhenReady)
                {
                    _startWhenReady = false;
                    StartDialogue();
                }
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("311", "Bridge async initialization error", ex.Message);
            }
            finally
            {
                _initializing = false;
            }
        }

        void Start()
        {

            bool ownedByGameSetup = FindFirstObjectByType<GameSetup>() != null;
            if (_autoStartOnAwake && !ownedByGameSetup) StartDialogue();
        }

        void Update()
        {

            if (_isDialogueActive) _player?.Tick();
        }

        void OnDestroy()
        {
            _player?.Stop();
            _player?.DisposeIntegrations();
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            if (_initializing)
            {
                _startWhenReady = true;
                DialogueLogger.Log("Initialization already in progress (async); start deferred.");
                return;
            }

            try
            {
                _project = DialogueProjectLoader.Load(_graphPath);
                if (_project.Graph == null)
                {
                    DialogueLogger.LogError("317", "Graph load failed", "Cannot load any dialogue graph");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_language))
                {
                    if (LanguageManager.TrySetLanguage(_language, persist: false))
                        _project.Language = LanguageManager.CurrentLanguage;
                    else
                        DialogueLogger.LogWarning(
                            $"Inspector language '{_language}' is not deployed; keeping " +
                            $"'{LanguageManager.CurrentLanguage}'.");
                }

                _lineProvider = YamlLineProvider.Instance;
                _player = new DialogueGraphPlayer();

                _player.GraphResolver = DialogueProjectLoader.LoadGraphByName;

                SubscribeEvents();
                _isInitialized = true;

                if (_graphPath != _project.GraphPath)
                    _graphPath = _project.GraphPath;

                AttachVariables(force: true);

                DialogueLogger.Log($"Bridge initialized. Graph: {_project.Graph.GraphId} " +
                                   $"({System.IO.Path.GetFileName(_project.GraphPath)}), " +
                                   $"language: {LanguageManager.CurrentLanguage}");

                if (_validateOnLoad)
                    DialogueProjectValidator.ValidateAndLog(_project.Graph, _lineProvider, _project.Language);

                if (_startWhenReady)
                {
                    _startWhenReady = false;
                    StartDialogue();
                }
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("311", "Bridge initialization error", ex.Message);
            }
        }

        public void StartDialogue()
        {
            if (!_isInitialized)
            {
                if (_initializing || DialogueStreamingAssets.RequiresAsync)
                {
                    _startWhenReady = true;
                    DialogueLogger.Log("Dialogue start deferred until initialization completes.");
                    return;
                }

                Initialize();
            }
            if (!_isInitialized)
            {
                DialogueLogger.LogWarning("Bridge not initialized.");
                return;
            }
            if (_project?.Graph == null)
            {
                DialogueLogger.LogError("311", "Missing graph", "Reload the graph before starting.");
                return;
            }
            if (_isDialogueActive)
            {
                DialogueLogger.LogWarning("Dialogue already active.");
                return;
            }

            _isDialogueActive = true;
            _player.Start(_project.Graph, _lineProvider);
            DialogueLogger.Log("Dialogue started.");
        }

        public void Continue()
        {
            if (!GuardActive()) return;
            _player.Continue();
        }

        public void Choose(int index)
        {
            if (!GuardActive()) return;
            _player.Choose(index);
        }

        public void StopDialogue()
        {
            _player?.Stop();
            _isDialogueActive = false;
            DialogueLogger.Log("Dialogue stopped.");
        }

        public void Restart()
        {
            if (!GuardActive()) return;
            _player.Restart();
            _isDialogueActive = true;
        }

        public void SetGraphPath(string graphPath) => _graphPath = graphPath;

        public IReadOnlyList<string> AvailableLanguages => LanguageManager.AvailableLanguages;

        public string CurrentLanguage => LanguageManager.CurrentLanguage;

        public string CurrentLanguageLabel => LanguageManager.LanguageLabel(LanguageManager.CurrentLanguage);

        public bool SetLanguage(string language, bool restartIfActive = true)
        {
            if (!LanguageManager.TrySetLanguage(language)) return false;

            _lineProvider = YamlLineProvider.Instance;
            _player?.SetLineProvider(_lineProvider);

            if (_project != null) _project.Language = LanguageManager.CurrentLanguage;

            if (restartIfActive && _isDialogueActive)
            {
                DialogueLogger.Log($"Restarting dialogue in {LanguageManager.CurrentLanguage}.");
                _player.Restart();
            }

            return true;
        }

        public string CycleLanguage(bool restartIfActive = true)
        {
            SetLanguage(LanguageManager.NextLanguage(), restartIfActive);
            return CurrentLanguage;
        }

        public void RefreshLanguages()
        {
            LanguageManager.RefreshAvailableLanguages(_project?.Manifest);
            LanguageManager.TrySetLanguage(LanguageManager.CurrentLanguage);
        }

        public void Reload()
        {
            StopDialogue();
            _isInitialized = false;
            _player = null;

            if (DialogueStreamingAssets.RequiresAsync)
            {
                InitializeAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        DialogueLogger.LogError("311", "Async reload failed",
                            task.Exception?.GetBaseException()?.Message);
                });
                return;
            }

            Initialize();
        }

        public DialogueProjectValidator.Report Validate()
            => DialogueProjectValidator.Validate(_project?.Graph, _lineProvider, _project?.Language);

        public DialogueVariables Variables => _player?.Variables;

        public void SetVariables(DialogueVariables variables)
        {
            if (variables == null) return;
            if (_player == null)
            {
                DialogueLogger.LogWarning("Cannot set variables before initialization.");
                return;
            }

            _player.Variables = variables;
            AttachVariables(force: true);
        }

        public bool HasVariable(string name) => Variables != null && Variables.Has(name);

        public bool GetBool(string name, bool fallback = false) => Variables != null && Variables.GetBool(name, fallback);
        public int GetInt(string name, int fallback = 0) => Variables != null ? Variables.GetInt(name, fallback) : fallback;
        public float GetFloat(string name, float fallback = 0f) => Variables != null ? Variables.GetFloat(name, fallback) : fallback;
        public string GetString(string name, string fallback = "") => Variables != null ? Variables.GetString(name, fallback) : fallback;

        public void SetBool(string name, bool value) => Variables?.SetBool(name, value);
        public void SetInt(string name, int value) => Variables?.SetInt(name, value);
        public void SetFloat(string name, float value) => Variables?.SetFloat(name, value);
        public void SetString(string name, string value) => Variables?.SetString(name, value);

        public void AddInt(string name, int delta) => Variables?.AddInt(name, delta);

        public string SaveVariableState() => Variables?.Serialize() ?? "";

        public void LoadVariableState(string state) => Variables?.Deserialize(state);

        public IQuestService QuestService => _player?.Quests;

        public void SetQuestService(IQuestService service)
        {
            if (service == null || _player == null) return;

            _player.Quests = service;
            AttachVariables(force: true);
            DialogueLogger.Log($"Quest service set: {service.GetType().Name}");
        }

        public QuestState GetQuestState(string questId)
            => _player?.Quests?.GetState(questId) ?? QuestState.Unknown;

        public bool IsQuestActive(string questId) => GetQuestState(questId) == QuestState.Active;
        public bool IsQuestCompleted(string questId) => GetQuestState(questId) == QuestState.Completed;

        public void AcceptQuest(string questId) => _player?.Quests?.Accept(questId);
        public void CompleteQuest(string questId) => _player?.Quests?.Complete(questId);
        public void FailQuest(string questId) => _player?.Quests?.Fail(questId);
        public void SetQuestStep(string questId, string stepId, bool done = true)
            => _player?.Quests?.SetStep(questId, stepId, done);

        public string SaveNarrativeState()
        {
            string quests = _player?.Quests is InMemoryQuestService memory ? memory.Serialize() : "";
            return "# variables\n" + SaveVariableState() + "# quests\n" + quests;
        }

        private void AttachVariables(bool force = false)
        {
            if (_player == null) return;

            _player.Variables ??= new DialogueVariables();
            DialogueVariableCatalog.ApplyTo(_player.Variables);
            _player.Quests ??= QuestRegistry.Default;

            _player.RefreshIntegrations();

            _player.Variables.OnChanged -= ForwardVariableChanged;
            _player.Variables.OnChanged += ForwardVariableChanged;

            _player.Quests.OnQuestStateChanged -= ForwardQuestChanged;
            _player.Quests.OnQuestStateChanged += ForwardQuestChanged;

            if (force) DialogueLogger.Log($"Variables ready ({_player.Variables.All.Count} value(s)).");
        }

        private void ForwardVariableChanged(string name, DialogueValue value)
            => OnVariableChanged?.Invoke(name, value);

        private void ForwardQuestChanged(string questId, QuestState state)
            => OnQuestChanged?.Invoke(questId, state);

        private bool GuardActive()
        {
            if (!_isInitialized || !_isDialogueActive)
            {
                DialogueLogger.LogWarning("Dialogue not active.");
                return false;
            }
            return true;
        }

        private void SubscribeEvents()
        {
            if (_player == null) return;

            _player.OnLine += (speaker, text) =>
            {
                if (_logEvents || _logEveryLine)
                    DialogueLogger.Log($"Line: [{speaker}] {text}");
                OnLine?.Invoke(speaker, text);
            };

            _player.OnChoices += (choices) =>
            {
                if (_logEvents) DialogueLogger.Log($"Choices: {choices.Count} options");
                OnChoices?.Invoke(choices);
            };

            _player.OnGraphJumped += (fromId, toId) =>
            {
                if (_logEvents) DialogueLogger.Log($"Graph switched: {fromId} -> {toId}");
                OnGraphJumped?.Invoke(fromId, toId);
            };

            _player.OnPositionChanged += position => OnPositionChanged?.Invoke(position);

            _player.OnWaitingForActionChanged += waiting =>
            {
                if (_logEvents)
                    DialogueLogger.Log(waiting
                        ? $"Waiting for action '{_player.PendingActionId}'..."
                        : "Action finished, dialogue resumes.");
                OnWaitingForActionChanged?.Invoke(waiting);
            };

            _player.OnEnd += () =>
            {
                if (_logEvents) DialogueLogger.Log("Dialogue ended.");
                _isDialogueActive = false;
                OnDialogueEnd?.Invoke();
            };
        }
    }
}
