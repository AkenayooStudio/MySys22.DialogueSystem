using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MySys22.DialogueEngine.Core
{

    public sealed class DialogueProject
    {
        public DialogueProjectManifest Manifest { get; set; }
        public GraphData Graph { get; set; }
        public string GraphPath { get; set; }
        public string Language { get; set; }

        public string DisplayName =>
            Manifest == null ? "Dialogue Project"
                : (string.IsNullOrWhiteSpace(Manifest.name) ? Manifest.projectId : Manifest.name);
    }

    public static class DialogueProjectLoader
    {

        public static DialogueProjectManifest PrepareProject()
        {
            DialoguePaths.EnsureLayout();

            var manifest = DialogueProjectManifestLoader.LoadOrDefault();
            LanguageManager.Initialize(manifest);
            CharacterRegistry.Load();
            DialogueVariableCatalog.Load();
            ActionRegistry.EnsureBuiltIns();

            ActionRegistry.DiscoverActions();

            DialogueLogger.Log($"Project ready: {manifest}");
            return manifest;
        }

        public static async System.Threading.Tasks.Task<DialogueProjectManifest> PrepareProjectAsync()
        {
            DialoguePaths.EnsureLayout();

            var manifest = await DialogueProjectManifestLoader.LoadOrDefaultAsync();
            await LanguageManager.InitializeAsync(manifest);
            await CharacterRegistry.LoadAsync();
            await DialogueVariableCatalog.LoadAsync();
            ActionRegistry.EnsureBuiltIns();
            ActionRegistry.DiscoverActions();

            await DialogueGraphIndex.RefreshAsync();

            DialogueLogger.Log($"Project ready (async): {manifest}");
            return manifest;
        }

        public static async System.Threading.Tasks.Task<DialogueProject> LoadAsync(string configuredGraphPath = null)
        {
            var manifest = await PrepareProjectAsync();
            var project = new DialogueProject { Manifest = manifest, Language = LanguageManager.CurrentLanguage };

            string relative = ResolveGraphRelative(configuredGraphPath);
            if (relative == null)
            {
                string direct = DialoguePaths.ResolveGraphPath(configuredGraphPath);
                if (!DialogueStreamingAssets.RequiresAsync &&
                    !string.IsNullOrEmpty(direct) && File.Exists(direct))
                {
                    project.GraphPath = direct;
                    project.Graph = GraphYamlParser.LoadFromFile(direct);
                    return project;
                }

                DialogueLogger.LogError("317", "No dialogue graph found", DialoguePaths.Graphs);
                return project;
            }

            project.GraphPath = Path.Combine(DialoguePaths.Root,
                relative.Replace('/', Path.DirectorySeparatorChar));
            project.Graph = DialogueGraphIndex.Get(relative) ??
                            await GraphYamlParser.LoadFromStreamingAssetsAsync(relative);
            return project;
        }

        private static string ResolveGraphRelative(string configuredGraphPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredGraphPath))
            {
                string absolute = DialoguePaths.ResolveGraphPath(configuredGraphPath);
                if (!string.IsNullOrEmpty(absolute))
                {
                    string relative = DialoguePaths.ToStreamingRelative(absolute).Replace('\\', '/');
                    if (IsStreamingRelative(relative) && GraphExists(relative))
                        return relative;
                }

                string byName = DialogueGraphIndex.FindById(configuredGraphPath) ??
                                DialogueGraphIndex.FindByName(configuredGraphPath);
                if (byName != null) return byName;

                string fallback = DialogueGraphIndex.FindFirst();
                if (fallback == null)
                {
                    DialogueLogger.LogError("319", "Configured graph not found", configuredGraphPath);
                    return null;
                }

                DialogueLogger.LogWarning(
                    $"Configured graph '{configuredGraphPath}' is not deployed; using '{fallback}'.");
                return fallback;
            }

            return DialogueGraphIndex.FindFirst();
        }

        private static bool GraphExists(string relative)
        {
            if (DialogueGraphIndex.Contains(relative)) return true;
            if (DialogueStreamingAssets.RequiresAsync) return false;

            return File.Exists(Path.Combine(DialoguePaths.Root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static bool IsStreamingRelative(string relative)
        {
            if (string.IsNullOrEmpty(relative)) return false;
            if (relative.StartsWith("/") || Path.IsPathRooted(relative)) return false;
            return relative.StartsWith(DialoguePaths.GraphsFolderName + "/", StringComparison.Ordinal);
        }

        public static DialogueProject Load(string configuredGraphPath = null)
        {
            var manifest = PrepareProject();
            var project = new DialogueProject
            {
                Manifest = manifest,
                Language = LanguageManager.CurrentLanguage
            };

            string relative = ResolveGraphRelative(configuredGraphPath);
            string path = relative == null
                ? null
                : Path.Combine(DialoguePaths.Root, relative.Replace('/', Path.DirectorySeparatorChar));

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                DialogueLogger.LogError("317", "No dialogue graph found",
                    $"Looked in {DialoguePaths.Graphs}. Author one in MySys22/Dialogue/Dialogue Graph Editor.");
                return project;
            }

            project.GraphPath = path;
            project.Graph = GraphYamlParser.LoadFromFile(path);
            return project;
        }

        public static List<string> ListGraphFiles()
        {
            var result = new List<string>();
            var indexed = DialogueGraphIndex.AbsolutePaths;
            if (indexed.Count > 0)
            {
                result.AddRange(indexed);
                return result;
            }

            if (!Directory.Exists(DialoguePaths.Graphs)) return result;

            try
            {
                var files = Directory.GetFiles(DialoguePaths.Graphs, "*.yaml", SearchOption.TopDirectoryOnly);
                result.AddRange(files
                    .Where(f => !Path.GetFileName(f).StartsWith("."))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("318", "Graph discovery failed", ex.Message);
            }
            return result;
        }

        public static string FindFirstGraphFile()
            => DialogueGraphIndex.FindFirst();

        public static GraphData LoadGraphByName(string nameOrId)
        {
            GraphData graph = DialogueGraphIndex.Resolve(nameOrId);
            if (graph != null) return graph;

            DialogueLogger.LogError("319", "Graph not found", nameOrId);
            return null;
        }

        public static string FindGraphFileByName(string nameOrId)
            => DialogueGraphIndex.FindByName(nameOrId);

        public static string FindGraphById(string graphId)
            => DialogueGraphIndex.FindById(graphId);
    }
}
