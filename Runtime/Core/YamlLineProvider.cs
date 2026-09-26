using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MySys22.DialogueEngine.Core
{

    public class YamlLineProvider : ILineProvider
    {

        private static YamlLineProvider _instance;

        private readonly Dictionary<(string character, int did), string> _lineCache
            = new Dictionary<(string, int), string>();
        private readonly HashSet<string> _characters = new HashSet<string>(StringComparer.Ordinal);

        private string _languageCode;
        private int _fileCount;

        private YamlLineProvider(string languageCode) : this(languageCode, loadNow: true)
        {
        }

        private YamlLineProvider(string languageCode, bool loadNow)
        {
            _languageCode = DialoguePaths.NormalizeLanguage(languageCode) ?? DialoguePaths.DefaultLanguage;
            if (loadNow) LoadAllLines();
        }

        public static void Initialize(string languageCode)
        {
            _instance = new YamlLineProvider(languageCode);
        }

        public static async Task InitializeAsync(string languageCode)
        {
            string code = DialoguePaths.NormalizeLanguage(languageCode) ?? DialoguePaths.DefaultLanguage;
            var provider = new YamlLineProvider(code, loadNow: false);
            await provider.LoadAllLinesAsync();
            _instance = provider;
        }

        public static void ForceReload(string languageCode)
        {
            if (_instance == null)
            {
                Initialize(languageCode);
                return;
            }
            _instance._languageCode = DialoguePaths.NormalizeLanguage(languageCode) ?? DialoguePaths.DefaultLanguage;
            _instance.Reload();
        }

        public static ILineProvider Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException(
                        "YamlLineProvider not initialized. Call LanguageManager.Initialize() first.");
                return _instance;
            }
        }

        public static YamlLineProvider Current => _instance;

        public string LanguageCode => _languageCode;

        public int FileCount => _fileCount;

        public int LineCount => _lineCache.Count;

        public sealed class Snapshot
        {
            public string Language;
            public readonly Dictionary<(string character, int did), string> Lines =
                new Dictionary<(string, int), string>();
            public readonly HashSet<string> Characters = new HashSet<string>(StringComparer.Ordinal);
            public int FileCount;

            public bool TryGet(string character, int did, out string text)
                => Lines.TryGetValue((character, did), out text);

            public bool Has(string character, int did) => Lines.ContainsKey((character, did));
        }

        public static Snapshot LoadLanguage(string language)
        {
            string code = DialoguePaths.NormalizeLanguage(language) ?? DialoguePaths.DefaultLanguage;
            var snapshot = new Snapshot { Language = code };

            string basePath = DialoguePaths.LanguageFolder(code);
            if (!Directory.Exists(basePath))
            {
                DialogueLogger.LogError("315", "Language folder not found", basePath);
                return snapshot;
            }

            string[] yamlFiles = Directory.GetFiles(basePath, "*.yaml", SearchOption.TopDirectoryOnly);
            if (yamlFiles.Length == 0)
            {
                DialogueLogger.LogWarning($"No YAML line databases in {basePath}");
                return snapshot;
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            int totalLines = 0;

            foreach (string filePath in yamlFiles)
            {
                try
                {
                    var data = deserializer.Deserialize<LineDatabaseData>(File.ReadAllText(filePath));
                    if (data == null || data.Lines == null)
                    {
                        DialogueLogger.LogWarning($"Empty or malformed YAML: {filePath}");
                        continue;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string character = data.Character;

                    if (string.IsNullOrWhiteSpace(character))
                    {
                        DialogueLogger.LogWarning(
                            $"Line database '{fileName}' has no 'character' field; using the file name. ({filePath})");
                        character = fileName;
                    }

                    if (!string.Equals(character, fileName, StringComparison.OrdinalIgnoreCase))
                    {

                        DialogueLogger.Log($"Line database '{fileName}.yaml' declares character '{character}'.");
                    }

                    snapshot.Characters.Add(character);

                    foreach (var line in data.Lines)
                    {
                        if (!snapshot.Lines.TryAdd((character, line.Did), line.Text))
                        {
                            DialogueLogger.LogWarning(
                                $"Duplicate DID {character}-{line.Did} in {Path.GetFileName(filePath)}. Ignored.");
                        }
                        totalLines++;
                    }

                    snapshot.FileCount++;
                }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("302", "YAML parsing error", $"{filePath}: {ex.Message}");
                }
            }

            DialogueLogger.Log(
                $"Line snapshot: {snapshot.FileCount} file(s), {totalLines} lines (lang: {code})");
            return snapshot;
        }

        public void Reload()
        {
            _lineCache.Clear();
            _characters.Clear();

            if (DialogueStreamingAssets.RequiresAsync)
            {
                LoadAllLinesAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        DialogueLogger.LogError("302", "Async reload failed", task.Exception?.GetBaseException()?.Message);
                });
                return;
            }

            LoadAllLines();
            DialogueLogger.Log($"YamlLineProvider reloaded (language: {_languageCode})");
        }

        public async Task LoadAllLinesAsync()
        {
            _lineCache.Clear();
            _characters.Clear();
            _fileCount = 0;

            string folder = $"{DialoguePaths.DialogueFolderName}/{_languageCode}";
            string[] documents = await ReadLanguageDocumentsAsync(folder);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            foreach (string document in documents)
            {
                try
                {
                    var data = deserializer.Deserialize<LineDatabaseData>(document);
                    if (data?.Lines == null) continue;

                    string character = string.IsNullOrWhiteSpace(data.Character) ? "?" : data.Character;
                    _characters.Add(character);

                    foreach (var line in data.Lines)
                    {
                        if (!_lineCache.TryAdd((character, line.Did), line.Text))
                            DialogueLogger.LogWarning($"Duplicate DID {character}-{line.Did}. Ignored.");
                    }
                    _fileCount++;
                }
                catch (Exception ex)
                {
                    DialogueLogger.LogError("302", "YAML parsing error", ex.Message);
                }
            }

            DialogueLogger.Log(
                $"YamlLineProvider loaded: {_fileCount} file(s), {_lineCache.Count} line(s) (lang: {_languageCode})");
        }

        private static async Task<string[]> ReadLanguageDocumentsAsync(string folder)
        {
            if (!DialogueStreamingAssets.RequiresAsync)
            {
                string[] files = DialogueStreamingAssets.ListFiles(
                    folder.Replace(DialoguePaths.DialogueFolderName + "/", DialoguePaths.DialogueFolderName + "/"),
                    "*.yaml");

                var documents = new System.Collections.Generic.List<string>();
                foreach (string file in files)
                {
                    string languageFolder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file));
                    string fileName = System.IO.Path.GetFileName(file);
                    string relative = DialoguePaths.DialogueFolderName + "/" + languageFolder + "/" + fileName;

                    string text = DialogueStreamingAssets.ReadText(relative);
                    if (!string.IsNullOrEmpty(text)) documents.Add(text);
                }
                return documents.ToArray();
            }

            string index = await DialogueStreamingAssets.ReadTextAsync($"{folder}/manifest.txt");
            if (string.IsNullOrEmpty(index))
            {
                DialogueLogger.LogError("315", "Line manifest missing",
                    $"{folder}/manifest.txt is required on this platform. " +
                    "Regenerate it with MySys22 ▸ Dialogue ▸ Generate Index Manifests.");
                return System.Array.Empty<string>();
            }

            var result = new System.Collections.Generic.List<string>();
            string[] rawNames = index.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in rawNames)
            {
                string name = raw.Trim();
                if (name.Length == 0 || name.StartsWith("#")) continue;

                string document = await DialogueStreamingAssets.ReadTextAsync($"{folder}/{name}.yaml");
                if (!string.IsNullOrEmpty(document)) result.Add(document);
            }
            return result.ToArray();
        }

        public string GetLine(string character, int did)
        {
            if (character != null && _lineCache.TryGetValue((character, did), out string text))
                return text;

            string fallback = $"[MISSING: {character} - DID {did}]";
            DialogueLogger.LogWarning(fallback);
            return fallback;
        }

        public bool TryGetLine(string character, int did, out string text)
        {
            if (character != null) return _lineCache.TryGetValue((character, did), out text);
            text = null;
            return false;
        }

        public bool HasLine(string character, int did) => TryGetLine(character, did, out _);

        public IReadOnlyCollection<string> AvailableCharacters => _characters;

        private void LoadAllLines()
        {
            var snapshot = LoadLanguage(_languageCode);

            _fileCount = snapshot.FileCount;
            foreach (var pair in snapshot.Lines) _lineCache[pair.Key] = pair.Value;
            foreach (string character in snapshot.Characters) _characters.Add(character);

            DialogueLogger.Log(
                $"YamlLineProvider loaded: {_fileCount} file(s), {_lineCache.Count} total lines (lang: {_languageCode})");
        }
    }
}
