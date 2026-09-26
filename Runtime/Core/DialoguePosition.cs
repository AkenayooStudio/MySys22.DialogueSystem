using System;

using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    [Serializable]
    [Preserve]
    public sealed class DialoguePosition
    {
        public string GraphId;
        public string NodeId;
        public string NodeType;
        public string Speaker;
        public int LineIndex;
        public int Did;
        public int LineCount;
        public bool WaitingForChoice;
        public bool WaitingForAction;
        public string FunctionActionId;
        public string FunctionProgress;
        public string Language;
        public bool Running;

        public bool IsInsideFunction => WaitingForAction || !string.IsNullOrEmpty(FunctionActionId);

        public string ToSnapshot()
        {
            return string.Join("|",
                Escape(GraphId),
                Escape(NodeId),
                Escape(NodeType),
                Escape(Speaker),
                LineIndex.ToString(),
                Did.ToString(),
                LineCount.ToString(),
                WaitingForChoice ? "1" : "0",
                WaitingForAction ? "1" : "0",
                Escape(FunctionActionId),
                Escape(FunctionProgress),
                Escape(Language));
        }

        public static DialoguePosition FromSnapshot(string snapshot)
        {
            var position = new DialoguePosition();
            if (string.IsNullOrEmpty(snapshot)) return position;

            string[] parts = snapshot.Split('|');
            if (parts.Length < 12) return position;

            position.GraphId = Unescape(parts[0]);
            position.NodeId = Unescape(parts[1]);
            position.NodeType = Unescape(parts[2]);
            position.Speaker = Unescape(parts[3]);
            int.TryParse(parts[4], out position.LineIndex);
            int.TryParse(parts[5], out position.Did);
            int.TryParse(parts[6], out position.LineCount);
            position.WaitingForChoice = parts[7] == "1";
            position.WaitingForAction = parts[8] == "1";
            position.FunctionActionId = Unescape(parts[9]);
            position.FunctionProgress = Unescape(parts[10]);
            position.Language = Unescape(parts[11]);
            position.Running = true;
            return position;
        }

        private static string Escape(string value)
            => string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "\\\\").Replace("|", "\\p");

        private static string Unescape(string value)
            => string.IsNullOrEmpty(value) ? value : value.Replace("\\p", "|").Replace("\\\\", "\\");

        public override string ToString()
            => $"{GraphId}#{NodeId} ({NodeType}) line {LineIndex}/{LineCount} did {Did}" +
               (WaitingForChoice ? " [choice]" : "") +
               (WaitingForAction ? $" [function {FunctionActionId}{(string.IsNullOrEmpty(FunctionProgress) ? "" : ":" + FunctionProgress)}]" : "");
    }
}
