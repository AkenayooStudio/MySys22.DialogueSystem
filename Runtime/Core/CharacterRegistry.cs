using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{

    [Preserve]
    public class CharacterListData
    {
        [YamlMember(Alias = "characters")]
        public List<CharacterListEntry> Characters { get; set; } = new List<CharacterListEntry>();
    }

    [Preserve]
    public class CharacterListEntry
    {
        [YamlMember(Alias = "id")]
        public int Id { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "language")]
        public string Language { get; set; }

        public string Cid => Id.ToString();
    }

    public static class CharacterRegistry
    {
        private static readonly List<CharacterListEntry> _entries = new List<CharacterListEntry>();
        private static readonly Dictionary<string, string> _displayNames =
            new Dictionary<string, string>();

        public static IReadOnlyList<CharacterListEntry> Entries => _entries;
        public static bool IsLoaded { get; private set; }

        public static void Load()
        {
            Reset();

            string path = DialoguePaths.CharactersFile;
            if (!File.Exists(path))
            {
                DialogueLogger.Log($"CharacterRegistry: no registry at {path}. CIDs will be shown as-is.");
                IsLoaded = true;
                return;
            }

            try
            {
                Parse(File.ReadAllText(path), path);
            }
            catch (System.Exception ex)
            {
                DialogueLogger.LogError("321", "Character registry parse failed", $"{path}: {ex.Message}");
            }

            IsLoaded = true;
        }

        public static async System.Threading.Tasks.Task LoadAsync()
        {
            Reset();

            string relative = $"{DialoguePaths.CharactersFolderName}/{DialoguePaths.CharactersFileName}";
            string text = null;
            if (DialogueStreamingAssets.TryReadTextDirect(relative, out string direct))
                text = direct;
            else
                text = await DialogueStreamingAssets.ReadTextAsync(relative);

            if (string.IsNullOrEmpty(text))
            {
                DialogueLogger.Log(
                    $"CharacterRegistry: no registry at {DialoguePaths.CharactersFile}. CIDs will be shown as-is.");
                IsLoaded = true;
                return;
            }

            try
            {
                Parse(text, relative);
            }
            catch (System.Exception ex)
            {
                DialogueLogger.LogError("321", "Character registry parse failed", $"{relative}: {ex.Message}");
            }

            IsLoaded = true;
        }

        private static void Reset()
        {
            _entries.Clear();
            _displayNames.Clear();
            IsLoaded = false;
        }

        private static void Parse(string yaml, string source)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var data = deserializer.Deserialize<CharacterListData>(yaml);
            if (data?.Characters != null)
            {
                foreach (var entry in data.Characters)
                {
                    if (entry == null) continue;
                    _entries.Add(entry);

                    string cid = entry.Cid;
                    if (string.IsNullOrEmpty(cid)) continue;

                    if (!_displayNames.ContainsKey(cid))
                        _displayNames[cid] = string.IsNullOrWhiteSpace(entry.Name) ? cid : entry.Name.Trim();
                }
            }

            DialogueLogger.Log($"CharacterRegistry: {_entries.Count} character(s) loaded from {source}");
        }

        public static string DisplayName(string cid)
        {
            if (string.IsNullOrEmpty(cid)) return cid;
            return _displayNames.TryGetValue(cid, out string name) ? name : cid;
        }

        public static bool TryGetDisplayName(string cid, out string name)
            => _displayNames.TryGetValue(cid ?? string.Empty, out name);

        public static List<CharacterListEntry> ForLanguage(string language)
        {
            string lang = DialoguePaths.NormalizeLanguage(language);
            var result = new List<CharacterListEntry>();
            if (lang != null)
            {
                foreach (var e in _entries)
                    if (DialoguePaths.NormalizeLanguage(e.Language) == lang) result.Add(e);
            }
            return result.Count > 0 ? result : new List<CharacterListEntry>(_entries);
        }
    }
}
