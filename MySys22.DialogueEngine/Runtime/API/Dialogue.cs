using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MySys22.DialogueEngine.Bridge;
using MySys22.DialogueEngine.Core;

namespace MySys22.Engine.API
{

    public static class Dialogue
    {
        private static DialogueBridge _bridge;
        private static IDialogueView _view;

        public static DialogueBridge Bridge
        {
            get
            {
                if (_bridge == null)
                    _bridge = UnityEngine.Object.FindFirstObjectByType<DialogueBridge>();
                return _bridge;
            }
        }

        public static void SetBridge(DialogueBridge bridge) => _bridge = bridge;

        public static DialogueGraphPlayer Player => Bridge?.GetPlayer();
        public static DialogueVariables Variables => Bridge?.Variables;

        public static IDialogueView View
        {
            get
            {
                if (_view == null) _view = FindView();
                return _view;
            }
        }

        public static void SetView(IDialogueView view) => _view = view;

        private static IDialogueView FindView()
        {
            var canvas = UnityEngine.Object.FindFirstObjectByType<MySys22.DialogueEngine.UI.DialogueUICanvas>();
            if (canvas != null) return canvas;
            return UnityEngine.Object.FindFirstObjectByType<MySys22.DialogueEngine.UI.DialogueOverlayUI>();
        }

        public static void ShowPanel() => View?.SetPanelVisible(true);
        public static void HidePanel() => View?.SetPanelVisible(false);
        public static bool IsPanelVisible => View != null && View.IsPanelVisible;

        public static void ShowText(string speaker, string text)
        {
            if (View == null)
            {
                DialogueLogger.LogWarning($"ShowText ignored (no dialogue view): {speaker}: {text}");
                return;
            }
            View.SetPanelVisible(true);
            View.ShowText(speaker, text);
        }

        public static void ShowText(string text) => ShowText(null, text);

        public static void SetSpeaker(string speaker) => View?.SetSpeaker(speaker);
        public static void SetText(string text) => View?.SetText(text);
        public static void HideText() => View?.HideText();
        public static void HideChoices() => View?.HideChoices();

        public static void Complete() => Complete(true);

        public static void Complete(bool result)
        {
            DialogueActionContext context = DialogueActionContext.Current;
            if (context == null)
            {
                DialogueLogger.LogWarning("Complete() called outside a dialogue function.");
                return;
            }
            context.SetResult(result);
        }

        public static void CompleteNeutral()
        {
            DialogueActionContext context = DialogueActionContext.Current;
            if (context == null)
            {
                DialogueLogger.LogWarning("CompleteNeutral() called outside a dialogue function.");
                return;
            }
            context.SetNeutral();
        }

        public static Task WaitForCompletion(DialogueActionContext context)
            => context?.Completion?.Task ?? Task.CompletedTask;

        public static bool IsFunctionRunning => Bridge != null && Bridge.IsWaitingForAction;

        public static void PlayAudio(string track, bool loop = false) => DialogueAudio.Play(track, loop);

        public static void StopAudio() => DialogueAudio.Stop();

        public static void PlayVoice(string speakerCid, int did) => DialogueAudio.PlayVoice(speakerCid, did);

        public static void StopVoice() => DialogueAudio.StopVoice();

        public static void StopAllAudio() => DialogueAudio.StopAll();

        public static void RegisterAudioClip(string track, AudioClip clip) => DialogueAudio.Register(track, clip);

        public static bool IsAudioPlaying => DialogueAudio.IsPlaying;

        public static string CurrentAudioTrack => DialogueAudio.CurrentTrack;

        public static bool VoiceEnabled
        {
            get => DialogueAudio.VoiceEnabled;
            set => DialogueAudio.VoiceEnabled = value;
        }

        public static DialoguePosition Position => Bridge?.GetPosition();

        public static event Action<DialoguePosition> OnPositionChanged
        {
            add
            {
                if (Bridge != null) Bridge.OnPositionChanged += value;
            }
            remove
            {
                if (Bridge != null) Bridge.OnPositionChanged -= value;
            }
        }

        public static string SavePosition() => Bridge?.SavePosition() ?? "";

        public static DialoguePosition RestorePosition(string snapshot)
            => DialoguePosition.FromSnapshot(snapshot);

        public static void SetFunctionProgress(string progress) => Bridge?.SetFunctionProgress(progress);

        public static void RunAction(string actionId, IDictionary<string, string> parameters = null)
        {
            IAction action = ActionRegistry.Resolve(actionId);
            if (action == null) return;

            var context = new DialogueActionContext
            {
                ActionId = actionId,
                Node = Player?.CurrentNode,
                Graph = Player?.Graph,
                Speaker = Player?.CurrentSpeaker,
                Language = LanguageManager.CurrentLanguage,
                Parameters = parameters == null
                    ? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()
                    : new Dictionary<string, string>(parameters),
                Player = Player,
                Variables = Variables,
                Quests = Player?.Quests
            };

            try { action.Execute(context); }
            catch (Exception ex)
            {
                DialogueLogger.LogError("305", "External action error", $"{actionId}: {ex.Message}");
            }
        }

        public static bool HasAction(string actionId) => ActionRegistry.IsRegistered(actionId);

        public static IReadOnlyCollection<string> Actions => ActionRegistry.RegisteredActions;

        public static bool GetBool(string name, bool fallback = false) => Variables != null && Variables.GetBool(name, fallback);
        public static int GetInt(string name, int fallback = 0) => Variables != null ? Variables.GetInt(name, fallback) : fallback;
        public static float GetFloat(string name, float fallback = 0f) => Variables != null ? Variables.GetFloat(name, fallback) : fallback;
        public static string GetString(string name, string fallback = "") => Variables != null ? Variables.GetString(name, fallback) : fallback;

        public static void SetBool(string name, bool value) => Variables?.SetBool(name, value);
        public static void SetInt(string name, int value) => Variables?.SetInt(name, value);
        public static void SetFloat(string name, float value) => Variables?.SetFloat(name, value);
        public static void SetString(string name, string value) => Variables?.SetString(name, value);
        public static void AddInt(string name, int delta) => Variables?.AddInt(name, delta);

        public static bool Evaluate(string expression)
            => Player != null ? Player.EvaluateCondition(expression) : false;

        public static void AcceptQuest(string questId) => Bridge?.AcceptQuest(questId);
        public static void CompleteQuest(string questId) => Bridge?.CompleteQuest(questId);
        public static void FailQuest(string questId) => Bridge?.FailQuest(questId);
        public static void SetQuestStep(string questId, string stepId, bool done = true)
            => Bridge?.SetQuestStep(questId, stepId, done);

        public static QuestState GetQuestState(string questId)
            => Bridge?.GetQuestState(questId) ?? QuestState.Unknown;
    }
}
