using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MySys22.DialogueEngine.Core
{

    public static class LanguageManager
    {
        private const string PREF_KEY = "MySys22_DialogueEngine_Language";

        private static string _currentLanguage = DialoguePaths.DefaultLanguage;
        private static readonly List<string> _availableLanguages = new List<string>();
        private static bool _initialized;

        public static event Action<string> OnLanguageChanged;

        public static string CurrentLanguage
        {
            get => _currentLanguage;
            set => ApplyLanguage(value, persist: true);
        }

        private static bool ApplyLanguage(string value, bool persist)
        {
            string lang = DialoguePaths.NormalizeLanguage(value);
            if (lang == null || _currentLanguage == lang) return false;

            _currentLanguage = lang;

            if (persist)
            {
                PlayerPrefs.SetString(PREF_KEY, lang);
                PlayerPrefs.Save();
            }

            YamlLineProvider.ForceReload(lang);
            OnLanguageChanged?.Invoke(lang);
            DialogueLogger.Log($"Language changed: {lang}{(persist ? "" : " (not persisted)")}");
            return true;
        }

        public static IReadOnlyList<string> AvailableLanguages
        {
            get
            {
                if (_availableLanguages.Count == 0) RefreshAvailableLanguages(null);
                return _availableLanguages.AsReadOnly();
            }
        }

        public static int AvailableLanguageCount => AvailableLanguages.Count;

        public static bool IsInitialized => _initialized;

        public static bool HasLanguage(string language)
        {
            string lang = DialoguePaths.NormalizeLanguage(language);
            if (lang == null) return false;

            var languages = AvailableLanguages;
            for (int i = 0; i < languages.Count; i++)
                if (languages[i] == lang) return true;
            return false;
        }

        public static bool TrySetLanguage(string language, bool persist = true)
        {
            string lang = DialoguePaths.NormalizeLanguage(language);
            if (lang == null)
            {
                DialogueLogger.LogWarning("SetLanguage: empty language code.");
                return false;
            }
            if (!HasLanguage(lang))
            {
                DialogueLogger.LogWarning(
                    $"SetLanguage: '{lang}' is not deployed. Available: {string.Join(", ", AvailableLanguages)}.");
                return false;
            }

            ApplyLanguage(lang, persist);
            return true;
        }

        public static string NextLanguage()
        {
            var languages = AvailableLanguages;
            if (languages.Count == 0) return _currentLanguage;

            int index = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == _currentLanguage) { index = i; break; }
            }
            return languages[(index + 1) % languages.Count];
        }

        public static string CycleLanguage()
        {
            string next = NextLanguage();
            TrySetLanguage(next);
            return _currentLanguage;
        }

        public static string LanguageDisplayName(string language)
        {
            string lang = DialoguePaths.NormalizeLanguage(language);
            if (lang == null) return "";

            try
            {
                var culture = new System.Globalization.CultureInfo(lang);
                string name = culture.NativeName;
                if (string.IsNullOrWhiteSpace(name)) return lang;
                if (name.Length == 1) return name.ToUpperInvariant();
                return char.ToUpperInvariant(name[0]) + name.Substring(1);
            }
            catch (System.Exception)
            {
                return lang;
            }
        }

        public static string LanguageLabel(string language)
        {
            string lang = DialoguePaths.NormalizeLanguage(language);
            if (lang == null) return "";
            string display = LanguageDisplayName(lang);
            return display == lang ? lang : $"{display} ({lang})";
        }

        public static async System.Threading.Tasks.Task InitializeAsync(DialogueProjectManifest manifest = null)
        {
            if (manifest == null) manifest = DialogueProjectManifestLoader.LoadOrDefault();

            RefreshAvailableLanguages(manifest);

            string preferred = DialoguePaths.NormalizeLanguage(manifest.defaultLanguage);
            string chosen = preferred != null && _availableLanguages.Contains(preferred)
                ? preferred
                : (_availableLanguages.Count > 0 ? _availableLanguages[0] : DialoguePaths.DefaultLanguage);

            string saved = PlayerPrefs.GetString(PREF_KEY, null);
            if (!string.IsNullOrEmpty(saved) && _availableLanguages.Contains(saved)) chosen = saved;

            _currentLanguage = chosen;
            _initialized = true;
            PlayerPrefs.SetString(PREF_KEY, chosen);
            PlayerPrefs.Save();

            await YamlLineProvider.InitializeAsync(chosen);
            DialogueLogger.Log($"LanguageManager initialized (async). Current: {chosen}");
        }

        public static void Initialize(DialogueProjectManifest manifest = null)
        {
            if (manifest == null) manifest = DialogueProjectManifestLoader.LoadOrDefault();

            RefreshAvailableLanguages(manifest);

            string saved = PlayerPrefs.GetString(PREF_KEY, null);
            string preferred = DialoguePaths.NormalizeLanguage(manifest.defaultLanguage);

            string chosen;
            if (!string.IsNullOrEmpty(saved) && _availableLanguages.Contains(saved))
                chosen = saved;
            else if (!string.IsNullOrEmpty(preferred) && _availableLanguages.Contains(preferred))
                chosen = preferred;
            else
                chosen = _availableLanguages.Count > 0 ? _availableLanguages[0] : DialoguePaths.DefaultLanguage;

            _currentLanguage = chosen;
            _initialized = true;
            PlayerPrefs.SetString(PREF_KEY, chosen);
            PlayerPrefs.Save();

            YamlLineProvider.Initialize(chosen);
            DialogueLogger.Log($"LanguageManager initialized. Current: {chosen} " +
                               $"(available: {string.Join(", ", _availableLanguages)})");
        }

        public static void RefreshAvailableLanguages(DialogueProjectManifest manifest)
        {
            _availableLanguages.Clear();

            if (manifest?.languages != null)
            {
                foreach (string raw in manifest.languages)
                {
                    string lang = DialoguePaths.NormalizeLanguage(raw);
                    if (lang != null && !_availableLanguages.Contains(lang))
                        _availableLanguages.Add(lang);
                }
            }

            if (Directory.Exists(DialoguePaths.Dialogue))
            {
                foreach (string dir in Directory.GetDirectories(DialoguePaths.Dialogue))
                {
                    string lang = DialoguePaths.NormalizeLanguage(Path.GetFileName(dir));
                    if (lang == null || _availableLanguages.Contains(lang)) continue;
                    if (!HasLineDatabases(dir))
                    {
                        DialogueLogger.Log($"Ignoring empty language folder: {dir}");
                        continue;
                    }
                    _availableLanguages.Add(lang);
                }
            }

            if (_availableLanguages.Count == 0)
            {
                _availableLanguages.Add(DialoguePaths.DefaultLanguage);
                DialogueLogger.LogWarning(
                    $"No dialogue languages deployed in {DialoguePaths.Dialogue}. " +
                    $"Falling back to {DialoguePaths.DefaultLanguage}.");
            }

            DialogueLogger.Log($"Available languages: {string.Join(", ", _availableLanguages)}");
        }

        public static void RefreshAvailableLanguages()
            => RefreshAvailableLanguages(DialogueProjectManifestLoader.LoadOrDefault());

        private static bool HasLineDatabases(string folder)
        {
            if (Directory.GetFiles(folder, "*.yaml", SearchOption.TopDirectoryOnly).Length > 0)
                return true;
            return Directory.GetFiles(folder, "*.yml", SearchOption.TopDirectoryOnly).Length > 0;
        }
    }
}
