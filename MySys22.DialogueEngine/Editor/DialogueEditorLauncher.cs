using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public static class DialogueEditorLauncher
    {
        internal const string PathKey = "MySys22.DialogueEngine.JavaEditorPath";
        internal const string MavenKey = "MySys22.DialogueEngine.MavenExecutable";
        internal const string JarKey = "MySys22.DialogueEngine.EditorJar";

        private const string DefaultMaven = "mvn";
        private const string PomFileName = "pom.xml";

        internal const string EditorFolderName = "MySys22.DialogueEditor";

        internal const string LegacyEditorFolderName = "Java Editor";

        [MenuItem("MySys22/Dialogue/Open Dialogue Editor (Java)", false, 60)]
        public static void OpenJavaEditor()
        {
            EditorSettings settings = LoadSettings();

            if (!settings.IsUsable)
            {
                EditorUtility.DisplayDialog("MySys22 Dialogue Editor",
                    "The Java Dialogue Editor is not configured yet.\n\n" +
                    "Pick the folder that contains the editor 'pom.xml' in the window that opens.",
                    "Configure now");

                DialogueEditorSettingsWindow.ShowWindow();
                return;
            }

            try
            {
                Launch(settings);
                DialogueLogger.Log($"Dialogue Editor launched: {settings.Describe()}");
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("340", "Cannot launch the Dialogue Editor", ex.Message);
                EditorUtility.DisplayDialog("MySys22 Dialogue Editor",
                    "Launch failed:\n" + ex.Message +
                    "\n\nCheck the Maven executable and the editor folder in Editor Settings.",
                    "Open Settings");
                DialogueEditorSettingsWindow.ShowWindow();
            }
        }

        [MenuItem("MySys22/Dialogue/Editor Settings...", false, 61)]
        public static void OpenSettingsWindow() => DialogueEditorSettingsWindow.ShowWindow();

        [MenuItem("MySys22/Dialogue/Open Project Folder", false, 62)]
        public static void OpenProjectFolder() => EditorUtility.RevealInFinder(ProjectRoot);

        internal sealed class EditorSettings
        {
            public string EditorFolder;
            public string MavenExecutable = DefaultMaven;
            public string JarPath;

            public string PomPath =>
                string.IsNullOrEmpty(EditorFolder) ? null : Path.Combine(EditorFolder, PomFileName);

            public bool HasPom => PomPath != null && File.Exists(PomPath);
            public bool HasJar => !string.IsNullOrEmpty(JarPath) && File.Exists(JarPath);
            public bool IsUsable => HasPom || HasJar;

            public string Describe()
                => HasPom ? $"mvn javafx:run ({EditorFolder})" : $"java -jar ({JarPath})";
        }

        internal static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

        internal static EditorSettings LoadSettings()
        {
            var settings = new EditorSettings
            {
                EditorFolder = EditorPrefs.GetString(PathKey, ""),
                MavenExecutable = EditorPrefs.GetString(MavenKey, DefaultMaven),
                JarPath = EditorPrefs.GetString(JarKey, "")
            };

            if (!string.IsNullOrEmpty(settings.EditorFolder) &&
                !FolderIsUsable(settings.EditorFolder))
            {
                string migrated = MigrateLegacyFolder(settings.EditorFolder);
                if (migrated != null)
                {
                    settings.EditorFolder = migrated;
                    EditorPrefs.SetString(PathKey, migrated);
                    DialogueLogger.Log($"Dialogue Editor path migrated to: {migrated}");
                }
            }

            if (string.IsNullOrEmpty(settings.EditorFolder) || !FolderIsUsable(settings.EditorFolder))
            {
                string guessed = GuessEditorFolder();
                if (!string.IsNullOrEmpty(guessed)) settings.EditorFolder = guessed;
            }

            return settings;
        }

        internal static void SaveSettings(EditorSettings settings)
        {
            EditorPrefs.SetString(PathKey, settings.EditorFolder ?? "");
            EditorPrefs.SetString(MavenKey, string.IsNullOrWhiteSpace(settings.MavenExecutable)
                ? DefaultMaven
                : settings.MavenExecutable.Trim());
            EditorPrefs.SetString(JarKey, settings.JarPath ?? "");
        }

        internal static bool FolderIsUsable(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
            return File.Exists(Path.Combine(folder, PomFileName)) ||
                   Directory.GetFiles(folder, "*.jar").Length > 0;
        }

        private static string MigrateLegacyFolder(string storedFolder)
        {
            try
            {
                string parent = Directory.GetParent(storedFolder)?.FullName;
                if (parent == null) return null;

                string renamed = Path.Combine(parent, EditorFolderName);
                return FolderIsUsable(renamed) ? renamed : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GuessEditorFolder()
        {
            string[] candidates =
            {
                Path.Combine(ProjectRoot, EditorFolderName),
                Path.Combine(ProjectRoot, "Tools", EditorFolderName),
                Path.Combine(ProjectRoot, "..", EditorFolderName),
                Path.Combine(ProjectRoot, LegacyEditorFolderName),
                Path.Combine(ProjectRoot, "Tools", LegacyEditorFolderName),
                Path.Combine(ProjectRoot, "..", LegacyEditorFolderName)
            };

            foreach (string candidate in candidates)
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(full, PomFileName)) ||
                    File.Exists(Path.Combine(full, "mysys22-dialogue-editor.jar")))
                {
                    return full;
                }
            }
            return "";
        }

        private static void Launch(EditorSettings settings)
        {
            string logFile = Path.Combine(ProjectRoot, "Logs", "mysys22-dialogue-editor.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile) ?? ProjectRoot);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = settings.HasPom ? settings.EditorFolder : ProjectRoot
            };

            if (settings.HasPom)
            {
                string maven = string.IsNullOrWhiteSpace(settings.MavenExecutable)
                    ? DefaultMaven
                    : settings.MavenExecutable;

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c start \"MySys22 Dialogue Editor\" {maven} -f \"{settings.PomPath}\" javafx:run";
                }
                else
                {
                    psi.FileName = "/bin/bash";
                    psi.Arguments = $"-lc '{maven} -f \"{settings.PomPath}\" javafx:run >> \"{logFile}\" 2>&1 &'";
                }
            }
            else
            {
                psi.FileName = Application.platform == RuntimePlatform.WindowsEditor ? "cmd.exe" : "/bin/bash";
                string javaArgs = $"-jar \"{settings.JarPath}\"";
                if (psi.FileName == "cmd.exe")
                {
                    psi.Arguments = $"/c start \"MySys22 Dialogue Editor\" java {javaArgs}";
                }
                else
                {
                    psi.Arguments = $"-lc 'java {javaArgs} >> \"{logFile}\" 2>&1 &'";
                }
            }

            Process.Start(psi);
        }

        internal static string Validate(EditorSettings settings)
        {
            if (!settings.IsUsable)
                return "Not configured yet: select the editor folder (the one with pom.xml) or a built jar.";
            return "OK - will run: " + settings.Describe();
        }
    }

    internal sealed class DialogueEditorSettingsWindow : EditorWindow
    {
        private string _folder;
        private string _maven;
        private string _jar;

        internal static void ShowWindow()
        {
            var window = GetWindow<DialogueEditorSettingsWindow>(true, "Dialogue Editor Settings");
            window.minSize = new Vector2(520, 210);
            window.Load();
            window.Show();
        }

        private void Load()
        {
            var settings = DialogueEditorLauncher.LoadSettings();
            _folder = settings.EditorFolder;
            _maven = settings.MavenExecutable;
            _jar = settings.JarPath;
        }

        private void OnGUI()
        {
            DialogueInspectorGUI.DrawProjectInfo();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("MySys22 Dialogue Editor (Java)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _folder = EditorGUILayout.TextField("Editor folder", _folder);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select the folder containing pom.xml",
                    string.IsNullOrEmpty(_folder) ? DialogueEditorLauncher.ProjectRoot : _folder, "");
                if (!string.IsNullOrEmpty(picked)) _folder = picked;
            }
            EditorGUILayout.EndHorizontal();

            _maven = EditorGUILayout.TextField(
                new GUIContent("Maven executable", "Command used to run 'javafx:run'. Default: mvn"),
                _maven);

            EditorGUILayout.BeginHorizontal();
            _jar = EditorGUILayout.TextField(
                new GUIContent("Built jar (optional)", "Used when no pom.xml is selected."),
                _jar);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFilePanel("Select mysys22-dialogue-editor.jar",
                    string.IsNullOrEmpty(_jar) ? DialogueEditorLauncher.ProjectRoot : _jar, "jar");
                if (!string.IsNullOrEmpty(picked)) _jar = picked;
            }
            EditorGUILayout.EndHorizontal();

            var settings = new DialogueEditorLauncher.EditorSettings
            {
                EditorFolder = _folder,
                MavenExecutable = string.IsNullOrWhiteSpace(_maven) ? "mvn" : _maven,
                JarPath = _jar
            };

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(DialogueEditorLauncher.Validate(settings), MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                DialogueEditorLauncher.SaveSettings(settings);
                Close();
            }
            if (GUILayout.Button("Save and open"))
            {
                DialogueEditorLauncher.SaveSettings(settings);
                Close();
                DialogueEditorLauncher.OpenJavaEditor();
            }
            if (GUILayout.Button("Show project folder")) EditorUtility.RevealInFinder(DialogueEditorLauncher.ProjectRoot);
            EditorGUILayout.EndHorizontal();
        }
    }
}
