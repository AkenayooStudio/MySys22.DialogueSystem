using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MySys22.DialogueEngine.Core
{

    public static class DialogueGraphIndex
    {
        public const string IndexFileName = "manifest.txt";

        private static readonly List<string> Relative = new List<string>();
        private static readonly List<string> Absolute = new List<string>();
        private static readonly Dictionary<string, GraphData> Graphs =
            new Dictionary<string, GraphData>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> RelativePaths => Relative;

        public static IReadOnlyList<string> AbsolutePaths => Absolute;

        public static int Count => Relative.Count;

        public static string IndexRelativePath =>
            $"{DialoguePaths.GraphsFolderName}/{IndexFileName}";

        public static void Clear()
        {
            Relative.Clear();
            Absolute.Clear();
            Graphs.Clear();
        }

        public static void Refresh()
        {
            Clear();

            string[] files = ListFilesOnDisk();
            for (int i = 0; i < files.Length; i++)
                Register(files[i], null);

            DialogueLogger.Log($"Graph index refreshed: {Relative.Count} graph(s) from {DialoguePaths.Graphs}");
        }

        public static async Task RefreshAsync()
        {
            Clear();

            if (!DialogueStreamingAssets.RequiresAsync)
            {
                Refresh();
                return;
            }

            string index = await DialogueStreamingAssets.ReadTextAsync(IndexRelativePath);
            if (string.IsNullOrEmpty(index))
            {
                DialogueLogger.LogError("317", "Graph index missing",
                    $"{IndexRelativePath} is required on this platform. " +
                    "Deploy from MySys22 Dialogue Editor or run MySys22 ▸ Dialogue ▸ Generate Index Manifests.");
                return;
            }

            string[] names = index.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i].Trim();
                if (name.Length == 0 || name.StartsWith("#")) continue;

                string relative = $"{DialoguePaths.GraphsFolderName}/{name}";
                GraphData graph;
                try
                {
                    graph = await GraphYamlParser.LoadFromStreamingAssetsAsync(relative);
                }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("317", "Graph load failed", $"{relative}: {ex.Message}");
                    continue;
                }
                Register(relative, graph);
            }

            DialogueLogger.Log($"Graph index loaded: {Relative.Count} graph(s) from {IndexRelativePath}");
        }

        public static string FindFirst()
        {
            EnsureLoaded();
            for (int i = 0; i < Relative.Count; i++)
                if (Relative[i].EndsWith(DialoguePaths.GraphFileExtension, StringComparison.OrdinalIgnoreCase))
                    return Relative[i];
            return Relative.Count > 0 ? Relative[0] : null;
        }

        public static string FindById(string graphId)
        {
            if (string.IsNullOrWhiteSpace(graphId)) return null;
            EnsureLoaded();

            for (int i = 0; i < Relative.Count; i++)
                if (string.Equals(Stem(Relative[i]), graphId, StringComparison.OrdinalIgnoreCase))
                    return Relative[i];

            return null;
        }

        public static string FindByName(string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(nameOrId)) return null;
            EnsureLoaded();

            string wanted = Path.GetFileName(nameOrId.Trim());
            wanted = wanted.Replace(".graph.yaml", "").Replace(".graph.yml", "")
                           .Replace(".yaml", "").Replace(".yml", "");

            for (int i = 0; i < Relative.Count; i++)
                if (string.Equals(Stem(Relative[i]), wanted, StringComparison.OrdinalIgnoreCase))
                    return Relative[i];

            return null;
        }

        public static GraphData Get(string relativePath)
            => !string.IsNullOrEmpty(relativePath) && Graphs.TryGetValue(relativePath, out GraphData graph)
                ? graph
                : null;

        public static bool Contains(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;
            EnsureLoaded();

            string normalized = Normalize(relativePath);
            for (int i = 0; i < Relative.Count; i++)
                if (string.Equals(Relative[i], normalized, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public static GraphData Resolve(string nameOrId)
        {
            string relative = FindById(nameOrId) ?? FindByName(nameOrId);
            if (relative == null) return null;

            GraphData cached = Get(relative);
            if (cached != null) return cached;

            if (DialogueStreamingAssets.RequiresAsync) return null;

            string absolute = Path.Combine(DialoguePaths.Root,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (!GraphYamlParser.TryLoadFromFile(absolute, out GraphData graph, out string error))
            {
                DialogueLogger.LogError("318", "Graph load failed", $"{absolute}: {error}");
                return null;
            }

            Graphs[relative] = graph;
            return graph;
        }

        public static void Register(string pathOrRelative, GraphData graph)
        {
            string relative = Normalize(pathOrRelative);
            if (relative == null) return;

            if (!Relative.Contains(relative))
            {
                Relative.Add(relative);
                Absolute.Add(Path.Combine(DialoguePaths.Root,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
            }

            if (graph != null) Graphs[relative] = graph;
        }

        private static string Normalize(string pathOrRelative)
        {
            if (string.IsNullOrWhiteSpace(pathOrRelative)) return null;

            string relative = DialoguePaths.ToStreamingRelative(pathOrRelative).Replace('\\', '/');
            if (relative.StartsWith("/")) relative = relative.Substring(1);
            if (!relative.StartsWith(DialoguePaths.GraphsFolderName + "/", StringComparison.Ordinal))
                relative = $"{DialoguePaths.GraphsFolderName}/{Path.GetFileName(relative)}";
            return relative;
        }

        private static void EnsureLoaded()
        {
            if (Relative.Count > 0 || DialogueStreamingAssets.RequiresAsync) return;
            Refresh();
        }

        private static string Stem(string relativePath)
        {
            string name = Path.GetFileNameWithoutExtension(relativePath);
            return name.EndsWith(".graph", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - ".graph".Length)
                : name;
        }

        private static string[] ListFilesOnDisk()
        {
            if (!Directory.Exists(DialoguePaths.Graphs)) return Array.Empty<string>();

            try
            {
                string[] files = Directory.GetFiles(DialoguePaths.Graphs, "*.yaml",
                    SearchOption.TopDirectoryOnly);
                var result = new List<string>(files.Length);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                    if (!Path.GetFileName(files[i]).StartsWith(".")) result.Add(files[i]);
                return result.ToArray();
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("318", "Graph discovery failed", ex.Message);
                return Array.Empty<string>();
            }
        }
    }
}
