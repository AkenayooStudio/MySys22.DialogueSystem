using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MySys22.DialogueEngine.Bridge;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    internal static class DialogueInspectorGUI
    {
        private const double CACHE_SECONDS = 3.0;

        private static List<string> _languages = new List<string>();
        private static List<string> _graphs = new List<string>();
        private static double _fetchedAt = -999;

        internal static void RefreshCache()
        {
            _languages = ScanLanguages();
            _graphs = ScanGraphs();
            _fetchedAt = EditorApplication.timeSinceStartup;
        }

        private static void EnsureCache()
        {
            if (_languages.Count == 0 && _graphs.Count == 0)
            {
                RefreshCache();
                return;
            }
            if (EditorApplication.timeSinceStartup - _fetchedAt > CACHE_SECONDS) RefreshCache();
        }

        private static List<string> ScanLanguages()
        {
            var result = new List<string>();
            string root = Path.Combine(DialoguePaths.Dialogue);
            if (!Directory.Exists(root)) return result;

            foreach (string dir in Directory.GetDirectories(root))
            {
                string code = DialoguePaths.NormalizeLanguage(Path.GetFileName(dir));
                if (code == null) continue;
                if (Directory.GetFiles(dir, "*.yaml").Length == 0 &&
                    Directory.GetFiles(dir, "*.yml").Length == 0) continue;
                if (!result.Contains(code)) result.Add(code);
            }
            result.Sort();
            return result;
        }

        private static List<string> ScanGraphs()
        {
            var result = new List<string>();
            foreach (string file in DialogueProjectLoader.ListGraphFiles())
            {
                string name = Path.GetFileName(file);
                if (!result.Contains(name)) result.Add(name);
            }
            return result;
        }

        internal static void DrawProjectInfo()
        {
            EnsureCache();

            var manifest = DialogueProjectManifestLoader.Load();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("MySys22.DialogueEngine Project", EditorStyles.boldLabel);

            if (manifest == null)
            {
                EditorGUILayout.LabelField("engine.manifest.json not found.",
                    "Deploy from the editor or run the project setup.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Project", string.IsNullOrWhiteSpace(manifest.name)
                    ? manifest.projectId : manifest.name);
                EditorGUILayout.LabelField("Version", $"{manifest.dialogueVersion}  (chapter {manifest.chapter})");
                EditorGUILayout.LabelField("Default language", manifest.defaultLanguage);
                EditorGUILayout.LabelField("Deployed", _languages.Count == 0
                    ? "no language found"
                    : string.Join(", ", _languages));
                EditorGUILayout.LabelField("Graphs", _graphs.Count == 0
                    ? "none"
                    : string.Join(", ", _graphs));
                EditorGUILayout.LabelField("Generated", manifest.generatedBy);
            }

            EditorGUILayout.LabelField("Root", DialoguePaths.Root, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        internal static void DrawLanguagePopup(SerializedProperty property, string label = "Language")
        {
            EnsureCache();

            var options = new List<string> { "" };
            var labels = new List<string> { "Project default" };
            foreach (string code in _languages)
            {
                options.Add(code);
                labels.Add(LanguageManager.LanguageLabel(code));
            }

            string current = property.stringValue ?? "";
            int index = options.IndexOf(current);
            if (index < 0)
            {

                options.Add(current);
                labels.Add($"{current}  (not deployed)");
                index = options.Count - 1;
            }

            int chosen = EditorGUILayout.Popup(label, index, labels.ToArray());
            if (chosen != index)
            {
                property.stringValue = options[chosen];
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        internal static void DrawGraphPopup(SerializedProperty property, string label = "Graph")
        {
            EnsureCache();

            var options = new List<string> { "" };
            var labels = new List<string> { "First graph found" };
            foreach (string name in _graphs)
            {
                options.Add(name);
                labels.Add(name);
            }

            string current = property.stringValue ?? "";
            string currentName = string.IsNullOrEmpty(current) ? "" : Path.GetFileName(current);
            int index = options.IndexOf(currentName);
            if (index < 0)
            {
                options.Add(current);
                labels.Add($"{current}  (not found)");
                index = options.Count - 1;
            }

            int chosen = EditorGUILayout.Popup(label, index, labels.ToArray());
            if (chosen != index)
            {
                property.stringValue = options[chosen];
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        internal static void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", EditorStyles.miniButtonLeft)) RefreshCache();
            if (GUILayout.Button("Validate", EditorStyles.miniButtonMid)) DialogueValidator.ValidateProject();
            if (GUILayout.Button("Export Digest", EditorStyles.miniButtonMid)) DialogueDigestTools.ExportDigest();
            if (GUILayout.Button("Graph Editor", EditorStyles.miniButtonRight))
                DialogueGraphWindow.OpenWindow();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open StreamingAssets")) ManifestGenerator.OpenEngineFolder();
            if (GUILayout.Button("Open Dialogue Editor (Java)")) DialogueEditorLauncher.OpenJavaEditor();
            if (GUILayout.Button("Editor Settings")) DialogueEditorLauncher.OpenSettingsWindow();
            EditorGUILayout.EndHorizontal();
        }
    }

    [CustomEditor(typeof(DialogueBridge))]
    internal sealed class DialogueBridgeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DialogueInspectorGUI.DrawProjectInfo();
            EditorGUILayout.Space();

            DialogueInspectorGUI.DrawLanguagePopup(serializedObject.FindProperty("_language"));
            DialogueInspectorGUI.DrawGraphPopup(serializedObject.FindProperty("_graphPath"));
            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "_language", "_graphPath", "m_Script");
            EditorGUILayout.Space();

            DialogueInspectorGUI.DrawActions();

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(GameSetup))]
    internal sealed class GameSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DialogueInspectorGUI.DrawProjectInfo();
            EditorGUILayout.Space();

            DialogueInspectorGUI.DrawLanguagePopup(serializedObject.FindProperty("_languageOverride"));
            DialogueInspectorGUI.DrawGraphPopup(serializedObject.FindProperty("_graphPath"));
            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "_languageOverride", "_graphPath", "m_Script");
            EditorGUILayout.Space();

            DialogueInspectorGUI.DrawActions();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
