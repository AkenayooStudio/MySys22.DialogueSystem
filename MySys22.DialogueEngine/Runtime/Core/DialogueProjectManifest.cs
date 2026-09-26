using System;
using System.IO;
using UnityEngine;

namespace MySys22.DialogueEngine.Core
{

    [Serializable]
    public class DialogueProjectManifest
    {
        public int formatVersion = 1;
        public string projectId;
        public string name;
        public string dialogueVersion;
        public string chapter;
        public string defaultLanguage = DialoguePaths.DefaultLanguage;
        public string[] languages;
        public int graphs;
        public string generatedBy;
        public string generatedAt;

        public bool HasLanguages => languages != null && languages.Length > 0;

        public override string ToString()
            => $"{(string.IsNullOrEmpty(name) ? projectId : name)} v{dialogueVersion} " +
               $"[{(languages == null ? "-" : string.Join(",", languages))}]";
    }

    public static class DialogueProjectManifestLoader
    {
        public static DialogueProjectManifest LoadOrDefault()
        {
            DialogueProjectManifest manifest = Load();
            if (manifest != null) return manifest;

            DialogueLogger.LogWarning(
                $"engine.manifest.json not found at {DialoguePaths.EngineManifest}. " +
                "Using defaults (deploy from the MySys22 Dialogue Editor to generate it).");

            return Defaults();
        }

        public static async System.Threading.Tasks.Task<DialogueProjectManifest> LoadOrDefaultAsync()
        {
            DialogueProjectManifest manifest = await LoadAsync();
            if (manifest != null) return manifest;

            DialogueLogger.LogWarning(
                $"engine.manifest.json not found at {DialoguePaths.EngineManifest}. " +
                "Using defaults (deploy from the MySys22 Dialogue Editor to generate it).");

            return Defaults();
        }

        public static async System.Threading.Tasks.Task<DialogueProjectManifest> LoadAsync()
        {
            string relative = DialoguePaths.EngineManifestFileName;
            if (DialogueStreamingAssets.TryReadTextDirect(relative, out string direct))
                return Parse(direct, relative);

            string text = await DialogueStreamingAssets.ReadTextAsync(relative);
            return string.IsNullOrEmpty(text) ? null : Parse(text, relative);
        }

        private static DialogueProjectManifest Parse(string json, string source)
        {
            try
            {
                var manifest = JsonUtility.FromJson<DialogueProjectManifest>(json);
                if (manifest == null)
                {
                    DialogueLogger.LogWarning($"engine.manifest.json is empty or invalid: {source}");
                    return null;
                }
                return manifest;
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("320", "Manifest parse failed", $"{source}: {ex.Message}");
                return null;
            }
        }

        private static DialogueProjectManifest Defaults()
            => new DialogueProjectManifest
            {
                projectId = "local",
                name = "Local Project",
                defaultLanguage = DialoguePaths.DefaultLanguage,
                languages = new[] { DialoguePaths.DefaultLanguage }
            };

        public static DialogueProjectManifest Load()
        {
            string path = DialoguePaths.EngineManifest;
            if (!File.Exists(path)) return null;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("320", "Manifest read failed", $"{path}: {ex.Message}");
                return null;
            }

            return Parse(json, path);
        }
    }
}
