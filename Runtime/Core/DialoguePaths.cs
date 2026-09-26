using System;
using System.IO;
using UnityEngine;

namespace MySys22.DialogueEngine.Core
{

    public static class DialoguePaths
    {
        public const string LegacyRootFolderName = "MySys22.DialogueEngine";
        public const string GraphsFolderName = "Graphs";
        public const string DialogueFolderName = "Dialogue";
        public const string CharactersFolderName = "Characters";
        public const string VariablesFolderName = "Variables";
        public const string CharactersFileName = "characters.list.yaml";
        public const string VariablesFileName = "variables.yaml";
        public const string EngineManifestFileName = "engine.manifest.json";
        public const string EngineDigestFileName = "engine.graphdigest.json";
        public const string GraphFileExtension = ".graph.yaml";

        public const string DefaultLanguage = "EN";

        public static readonly string[] SupportedLanguages = { "EN", "IT", "ES", "RO", "JA" };

        private static readonly string[] LayoutEntries =
        {
            GraphsFolderName, DialogueFolderName, CharactersFolderName, VariablesFolderName,
            EngineManifestFileName, EngineDigestFileName
        };

        public static string Root => Application.streamingAssetsPath;
        public static string Graphs => Path.Combine(Root, GraphsFolderName);
        public static string Dialogue => Path.Combine(Root, DialogueFolderName);
        public static string Characters => Path.Combine(Root, CharactersFolderName);
        public static string CharactersFile => Path.Combine(Characters, CharactersFileName);
        public static string Variables => Path.Combine(Root, VariablesFolderName);
        public static string VariablesFile => Path.Combine(Variables, VariablesFileName);
        public static string EngineManifest => Path.Combine(Root, EngineManifestFileName);

        public static string EngineDigest => Path.Combine(Root, EngineDigestFileName);

        public static string LanguageFolder(string language)
            => Path.Combine(Dialogue, NormalizeLanguage(language));

        public static string GraphFile(string graphName)
            => Path.Combine(Graphs, EnsureGraphExtension(Path.GetFileName(graphName)));

        public static string NormalizeLanguage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Trim().ToUpperInvariant();
        }

        public static bool IsSupportedLanguage(string raw)
        {
            string lang = NormalizeLanguage(raw);
            if (lang == null) return false;
            for (int i = 0; i < SupportedLanguages.Length; i++)
                if (SupportedLanguages[i] == lang) return true;
            return false;
        }

        public static string EnsureGraphExtension(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string lower = name.ToLowerInvariant();
            if (lower.EndsWith(".yaml") || lower.EndsWith(".yml")) return name;
            return name + GraphFileExtension;
        }

        public static string ResolveGraphPath(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return null;

            string raw = configured.Trim().Replace('\\', '/');

            raw = raw.Replace("MySys22.DialogueAssets/", LegacyRootFolderName + "/");
            raw = raw.Replace("quest_rescue.graph.graph.yaml", "quest_rescue.graph.yaml");

            int streamingIndex = raw.IndexOf("StreamingAssets/", StringComparison.OrdinalIgnoreCase);
            if (streamingIndex >= 0)
                raw = raw.Substring(streamingIndex + "StreamingAssets/".Length);

            if (raw.StartsWith(LegacyRootFolderName + "/", StringComparison.Ordinal))
                raw = raw.Substring(LegacyRootFolderName.Length + 1);

            if (Path.IsPathRooted(raw))
                return Path.GetFullPath(raw);

            string streaming = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath,
                raw.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(streaming)) return streaming;

            string rootRelative = Path.GetFullPath(Path.Combine(Root,
                raw.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(rootRelative)) return rootRelative;

            string graphsRelative = Path.GetFullPath(Path.Combine(Graphs,
                EnsureGraphExtension(Path.GetFileName(raw)).Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(graphsRelative)) return graphsRelative;

            return graphsRelative;
        }

        public static string ToStreamingRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
            string root = Path.GetFullPath(Application.streamingAssetsPath)
                .TrimEnd(Path.DirectorySeparatorChar, '/');
            string full = Path.GetFullPath(absolutePath);
            if (full.StartsWith(root, StringComparison.Ordinal))
                return full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                    .Replace('\\', '/');
            return absolutePath;
        }

        public static bool RootExists => Directory.Exists(Root);

        public static string LegacyRoot =>
            Path.Combine(Application.streamingAssetsPath, LegacyRootFolderName);

        public static void EnsureLayout()
        {
            if (DialogueStreamingAssets.RequiresAsync) return;

            MigrateLegacyRoot();
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Graphs);
            Directory.CreateDirectory(Dialogue);
            Directory.CreateDirectory(Characters);
            Directory.CreateDirectory(Variables);
        }

        public static bool MigrateLegacyRoot()
        {
            try
            {
                string legacy = LegacyRoot;
                if (!Directory.Exists(legacy)) return false;
                if (Directory.Exists(Graphs)) return false;

                Directory.CreateDirectory(Root);
                MoveDirectory(legacy, GraphsFolderName);
                MoveDirectory(legacy, DialogueFolderName);
                MoveDirectory(legacy, CharactersFolderName);
                MoveDirectory(legacy, VariablesFolderName);
                MoveFile(legacy, EngineManifestFileName);
                MoveFile(legacy, EngineDigestFileName);
                for (int i = 0; i < LayoutEntries.Length; i++)
                    MoveFile(legacy, LayoutEntries[i] + ".meta");
                DeleteDirectoryIfEmpty(legacy);
                File.Delete(legacy + ".meta");
                DialogueLogger.Log($"Engine content moved from {legacy} to {Root}.");
                return true;
            }
            catch (Exception ex)
            {
                DialogueLogger.LogWarning($"Engine folder migration failed: {ex.Message}");
                return false;
            }
        }

        private static void MoveDirectory(string legacy, string name)
        {
            string source = Path.Combine(legacy, name);
            string target = Path.Combine(Root, name);
            if (Directory.Exists(source) && !Directory.Exists(target))
                Directory.Move(source, target);
        }

        private static void MoveFile(string legacy, string name)
        {
            string source = Path.Combine(legacy, name);
            string target = Path.Combine(Root, name);
            if (File.Exists(source) && !File.Exists(target))
                File.Move(source, target);
        }

        private static void DeleteDirectoryIfEmpty(string path)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, false);
            }
            catch (IOException)
            {
            }
        }
    }
}
