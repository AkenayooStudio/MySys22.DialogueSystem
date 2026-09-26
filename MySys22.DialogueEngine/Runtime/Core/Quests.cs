using System;
using System.Collections.Generic;

using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    public enum QuestState
    {
        Unknown = 0,
        Available = 1,
        Active = 2,
        Completed = 3,
        Failed = 4
    }

    public interface IQuestService
    {

        bool IsKnown(string questId);

        QuestState GetState(string questId);

        bool HasStep(string questId, string stepId);

        void Accept(string questId);
        void Complete(string questId);
        void Fail(string questId);
        void SetStep(string questId, string stepId, bool done);

        event Action<string, QuestState> OnQuestStateChanged;

        event Action<string, string, bool> OnQuestStepChanged;
    }

    [Preserve]
    public class InMemoryQuestService : IQuestService
    {
        private readonly Dictionary<string, QuestState> _states = new Dictionary<string, QuestState>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _steps = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        public event Action<string, QuestState> OnQuestStateChanged;
        public event Action<string, string, bool> OnQuestStepChanged;

        public void Register(string questId, QuestState state = QuestState.Available)
        {
            if (string.IsNullOrWhiteSpace(questId)) return;
            _known.Add(questId);
            _states[questId] = state;
        }

        public bool IsKnown(string questId) => !string.IsNullOrEmpty(questId) && _known.Contains(questId);

        public QuestState GetState(string questId)
            => !string.IsNullOrEmpty(questId) && _states.TryGetValue(questId, out QuestState state)
                ? state
                : QuestState.Unknown;

        public bool HasStep(string questId, string stepId)
            => !string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(stepId) &&
               _steps.TryGetValue(questId, out HashSet<string> steps) && steps.Contains(stepId);

        public void Accept(string questId) => SetState(questId, QuestState.Active);
        public void Complete(string questId) => SetState(questId, QuestState.Completed);
        public void Fail(string questId) => SetState(questId, QuestState.Failed);

        public void SetState(string questId, QuestState state)
        {
            if (string.IsNullOrWhiteSpace(questId)) return;
            if (_states.TryGetValue(questId, out QuestState previous) && previous == state) return;

            _known.Add(questId);
            _states[questId] = state;
            OnQuestStateChanged?.Invoke(questId, state);
            DialogueLogger.Log($"Quest '{questId}' -> {state}");
        }

        public void SetStep(string questId, string stepId, bool done)
        {
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(stepId)) return;

            if (!_steps.TryGetValue(questId, out HashSet<string> steps))
            {
                steps = new HashSet<string>(StringComparer.Ordinal);
                _steps[questId] = steps;
            }

            bool changed = done ? steps.Add(stepId) : steps.Remove(stepId);
            if (!changed) return;

            _known.Add(questId);
            OnQuestStepChanged?.Invoke(questId, stepId, done);
            DialogueLogger.Log($"Quest '{questId}' step '{stepId}' -> {(done ? "done" : "pending")}");
        }

        public IReadOnlyDictionary<string, QuestState> States => _states;

        public string Serialize()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var pair in _states) sb.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            foreach (var pair in _steps)
                foreach (string step in pair.Value)
                    sb.Append(pair.Key).Append(".step=").Append(step).Append('\n');
            return sb.ToString();
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            foreach (string rawLine in data.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key.EndsWith(".step", StringComparison.Ordinal))
                {
                    SetStep(key.Substring(0, key.Length - ".step".Length), value, true);
                }
                else if (Enum.TryParse(value, true, out QuestState state))
                {
                    SetState(key, state);
                }
            }
        }
    }

    public static class QuestRegistry
    {
        private static IQuestService _default;

        public static IQuestService Default => _default ??= new InMemoryQuestService();

        public static void SetDefault(IQuestService service)
        {
            _default = service;
            DialogueLogger.Log($"Quest service: {(service == null ? "none" : service.GetType().Name)}");
        }
    }

    [Preserve]
    public sealed class QuestVariableMirror : IDisposable
    {
        private readonly IQuestService _quests;
        private readonly DialogueVariables _variables;
        private bool _disposed;

        private QuestVariableMirror(IQuestService quests, DialogueVariables variables)
        {
            _quests = quests;
            _variables = variables;

            _quests.OnQuestStateChanged += HandleState;
            _quests.OnQuestStepChanged += HandleStep;
        }

        public static QuestVariableMirror Attach(IQuestService quests, DialogueVariables variables)
        {
            if (quests == null || variables == null) return null;

            var mirror = new QuestVariableMirror(quests, variables);
            if (quests is InMemoryQuestService memory)
            {
                foreach (var pair in memory.States) mirror.WriteState(pair.Key, pair.Value);
            }
            DialogueLogger.Log("Quest variables mirroring active.");
            return mirror;
        }

        private void HandleState(string questId, QuestState state) => WriteState(questId, state);

        private void HandleStep(string questId, string stepId, bool done)
            => _variables.SetBool($"quest.{questId}.step.{stepId}", done);

        private void WriteState(string questId, QuestState state)
        {
            _variables.SetString($"quest.{questId}.state", state.ToString());
            _variables.SetBool($"quest.{questId}.available", state == QuestState.Available);
            _variables.SetBool($"quest.{questId}.active", state == QuestState.Active);
            _variables.SetBool($"quest.{questId}.completed", state == QuestState.Completed);
            _variables.SetBool($"quest.{questId}.failed", state == QuestState.Failed);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _quests.OnQuestStateChanged -= HandleState;
            _quests.OnQuestStepChanged -= HandleStep;
        }
    }
}
