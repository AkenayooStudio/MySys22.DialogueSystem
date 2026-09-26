using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public static class ManifestGenerator
    {

        [MenuItem("MySys22/Dialogue/Generate Index Manifests", false, 20)]
        public static void GenerateAllManifests()
        {
            if (!Directory.Exists(DialoguePaths.Dialogue))
            {
                DialoguePaths.EnsureLayout();
                DialogueLogger.Log($"Engine folder created: {DialoguePaths.Root}. " +
                                   "Deploy from the MySys22 Dialogue Editor to populate it.");
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Engine folder created",
                    $"Created:\n{DialoguePaths.Root}\n\n" +
                    "Deploy a project from the MySys22 Dialogue Editor to fill Dialogue/, " +
                    "Characters/ and engine.manifest.json.", "OK");
                return;
            }

            var languageDirs = Directory.GetDirectories(DialoguePaths.Dialogue);
            int total = 0;
            foreach (string langDir in languageDirs)
            {
                if (WriteLanguageManifest(langDir)) total++;
            }

            bool graphManifest = WriteGraphManifest();

            AssetDatabase.Refresh();
            DialogueLogger.Log($"Index manifests updated: {total} language(s), " +
                               $"graph manifest {(graphManifest ? "written" : "skipped")}.");
        }

        [MenuItem("MySys22/Dialogue/Generate Manifest for Current Language", false, 21)]
        public static void GenerateManifestForCurrentLanguage()
        {
            string lang = LanguageManager.CurrentLanguage;
            string langDir = DialoguePaths.LanguageFolder(lang);

            if (!Directory.Exists(langDir))
            {
                DialoguePaths.EnsureLayout();
                Directory.CreateDirectory(langDir);
                AssetDatabase.Refresh();
                DialogueLogger.LogWarning($"Language folder '{lang}' did not exist; it was created. " +
                                          "Deploy lines from the MySys22 Dialogue Editor.");
                return;
            }

            WriteLanguageManifest(langDir);
            AssetDatabase.Refresh();
        }

        public static bool WriteGraphManifest()
        {
            Directory.CreateDirectory(DialoguePaths.Graphs);

            string manifestPath = Path.Combine(DialoguePaths.Graphs, DialogueGraphIndex.IndexFileName);
            var files = Directory.GetFiles(DialoguePaths.Graphs, "*.yaml", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n) && !n.StartsWith("."))
                .OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                File.WriteAllText(manifestPath, "# No dialogue graphs deployed." + System.Environment.NewLine);
                DialogueLogger.LogWarning($"No .yaml graph files in {DialoguePaths.Graphs}.");
                return false;
            }

            File.WriteAllLines(manifestPath, files);
            DialogueLogger.Log($"Graph manifest: {files.Count} graph(s) -> {manifestPath}");
            return true;
        }

        [MenuItem("MySys22/Dialogue/Open Engine Folder", false, 40)]
        public static void OpenEngineFolder()
        {
            DialoguePaths.EnsureLayout();
            EditorUtility.RevealInFinder(DialoguePaths.Root);
        }

        [MenuItem("MySys22/Dialogue/Engine Status", false, 41)]
        public static void LogStatus()
        {
            DialoguePaths.EnsureLayout();

            var manifest = DialogueProjectManifestLoader.LoadOrDefault();
            int lineFiles = 0;
            int graphFiles = 0;

            if (Directory.Exists(DialoguePaths.Dialogue))
            {
                lineFiles = Directory.GetFiles(DialoguePaths.Dialogue, "*.yaml", SearchOption.AllDirectories).Length;
            }
            if (Directory.Exists(DialoguePaths.Graphs))
            {
                graphFiles = Directory.GetFiles(DialoguePaths.Graphs, "*.yaml", SearchOption.TopDirectoryOnly).Length;
            }

            string provider;
            if (YamlLineProvider.Current == null)
            {
                provider = "not initialized (enter play mode to load)";
            }
            else
            {
                provider = YamlLineProvider.Current.LineCount + " lines / "
                           + YamlLineProvider.Current.FileCount + " files @ "
                           + YamlLineProvider.Current.LanguageCode;
            }

            var lines = new List<string>
            {
                $"Root:       {DialoguePaths.Root}",
                $"Project:    {manifest}",
                $"Default:    {manifest.defaultLanguage}",
                $"Languages:  {(manifest.languages == null ? "-" : string.Join(", ", manifest.languages))}",
                $"Generated:  {manifest.generatedBy} @ {manifest.generatedAt}",
                $"Line files: {lineFiles}",
                $"Graphs:     {graphFiles}",
                $"Registry:   {(File.Exists(DialoguePaths.CharactersFile) ? "present" : "missing")}",
                $"Provider:   {provider}"
            };

            DialogueLogger.Log("Engine status:\n" + string.Join("\n", lines));
            EditorUtility.DisplayDialog("MySys22.DialogueEngine status", string.Join("\n", lines), "OK");
        }

        private static bool WriteLanguageManifest(string langDir)
        {
            string langName = Path.GetFileName(langDir);
            string manifestPath = Path.Combine(langDir, "manifest.txt");
            var yamlFiles = Directory.GetFiles(langDir, "*.yaml", SearchOption.TopDirectoryOnly);

            if (yamlFiles.Length == 0)
            {
                File.WriteAllText(manifestPath, "# No line databases deployed for " + langName);
                DialogueLogger.LogWarning($"No .yaml line databases in {langName}.");
                return false;
            }

            var names = yamlFiles
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            File.WriteAllLines(manifestPath, names);
            DialogueLogger.Log($"Manifest for '{langName}': {names.Count} file(s).");
            return true;
        }
    }
}
