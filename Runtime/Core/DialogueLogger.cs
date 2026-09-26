using System;

namespace MySys22.DialogueEngine.Core
{
    public static class DialogueLogger
    {

        public const string Prefix = "[MySys22.DialogueEngine]";

        public static bool Enabled { get; set; } = true;

        public static void Log(string message)
        {
            if (Enabled)
                UnityEngine.Debug.Log($"{Prefix} {message}");
        }

        public static void LogWarning(string message)
        {
            if (Enabled)
                UnityEngine.Debug.LogWarning($"{Prefix} {message}");
        }

        public static void LogError(string message)
        {
            if (Enabled)
                UnityEngine.Debug.LogError($"{Prefix} {message}");
        }

        public static void LogError(string code, string description, string detail = null)
        {
            if (!Enabled) return;
            string msg = $"{Prefix} [{code}] {description}";
            if (!string.IsNullOrEmpty(detail))
                msg += $": {detail}";
            UnityEngine.Debug.LogError(msg);
        }
    }
}
