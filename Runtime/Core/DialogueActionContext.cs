using System.Collections.Generic;
using System.Threading.Tasks;

namespace MySys22.DialogueEngine.Core
{

    public enum DialogueFunctionResult
    {

        Neutral = 0,
        True = 1,
        False = 2
    }

    public sealed class DialogueActionCompletion
    {
        private readonly TaskCompletionSource<bool> _tcs =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _tcs.Task;

        public bool IsComplete => _tcs.Task.IsCompleted;

        public void Complete() => _tcs.TrySetResult(true);
    }

    public sealed class DialogueActionContext
    {

        public string ActionId { get; set; }

        public NodeData Node { get; set; }

        public GraphData Graph { get; set; }

        public string Speaker { get; set; }

        public string Language { get; set; }

        public IReadOnlyDictionary<string, string> Parameters { get; set; }
            = EmptyParameters;

        public DialogueGraphPlayer Player { get; set; }

        public DialogueVariables Variables { get; set; }

        public IQuestService Quests { get; set; }

        public DialogueActionCompletion Completion { get; set; }

        public DialogueFunctionResult Result { get; private set; } = DialogueFunctionResult.Neutral;

        public static DialogueActionContext Current { get; private set; }

        public static void SetCurrent(DialogueActionContext context) => Current = context;

        public void SetResult(bool result)
        {
            Result = result ? DialogueFunctionResult.True : DialogueFunctionResult.False;
            Completion?.Complete();
        }

        public void SetNeutral()
        {
            Result = DialogueFunctionResult.Neutral;
            Completion?.Complete();
        }

        public void Complete() => Completion?.Complete();

        private static readonly Dictionary<string, string> EmptyParameters =
            new Dictionary<string, string>();

        public bool TryGetParameter(string key, out string value)
        {
            value = null;
            return !string.IsNullOrEmpty(key)
                   && Parameters != null
                   && Parameters.TryGetValue(key, out value) && value != null;
        }

        public string GetParameter(string key, string fallback = null)
            => TryGetParameter(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        public int GetInt(string key, int fallback = 0)
            => int.TryParse(GetParameter(key), out int parsed) ? parsed : fallback;

        public float GetFloat(string key, float fallback = 0f)
            => float.TryParse(GetParameter(key),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;

        public bool GetBool(string key, bool fallback = false)
            => bool.TryParse(GetParameter(key), out bool parsed) ? parsed : fallback;

        public override string ToString()
            => $"{ActionId}({Parameters?.Count ?? 0} param(s)) @ node {Node?.Id}";
    }
}
