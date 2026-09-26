using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{

    public interface IVariableSource
    {
        bool TryGetValue(string name, out DialogueValue value);
        bool Has(string name);
    }

    [Serializable]
    public class VariableDeclaration
    {
        public string name;
        public string type = "bool";
        public string value;
        public string description;

        public DialogueValueType ValueType => DialogueValue.ParseType(type);
        public DialogueValue Default => DialogueValue.Parse(value, ValueType);
    }

    [Serializable]
    public class VariableCatalogData
    {
        public List<VariableDeclaration> variables = new List<VariableDeclaration>();
    }

    public class DialogueVariables : IVariableSource
    {
        private readonly Dictionary<string, DialogueValue> _values =
            new Dictionary<string, DialogueValue>(StringComparer.Ordinal);

        private readonly Dictionary<string, DialogueValueType> _declaredTypes =
            new Dictionary<string, DialogueValueType>(StringComparer.Ordinal);

        public event Action<string, DialogueValue> OnChanged;

        public IReadOnlyDictionary<string, DialogueValue> All => _values;

        public bool Has(string name) => !string.IsNullOrEmpty(name) && _values.ContainsKey(name);

        public bool TryGetValue(string name, out DialogueValue value)
        {
            if (string.IsNullOrEmpty(name))
            {
                value = default;
                return false;
            }
            return _values.TryGetValue(name, out value);
        }

        public DialogueValue Get(string name)
            => TryGetValue(name, out DialogueValue value) ? value : DialogueValue.FromBool(false);

        public bool GetBool(string name, bool fallback = false)
        {
            if (TryGetValue(name, out DialogueValue value)) return value.AsBool;
            DialogueLogger.LogWarning($"Variable '{name}' is not set; using {fallback}.");
            return fallback;
        }

        public int GetInt(string name, int fallback = 0)
        {
            if (TryGetValue(name, out DialogueValue value)) return value.AsInt;
            DialogueLogger.LogWarning($"Variable '{name}' is not set; using {fallback}.");
            return fallback;
        }

        public float GetFloat(string name, float fallback = 0f)
        {
            if (TryGetValue(name, out DialogueValue value)) return value.AsFloat;
            DialogueLogger.LogWarning($"Variable '{name}' is not set; using {fallback}.");
            return fallback;
        }

        public string GetString(string name, string fallback = "")
        {
            if (TryGetValue(name, out DialogueValue value)) return value.AsString;
            DialogueLogger.LogWarning($"Variable '{name}' is not set; using '{fallback}'.");
            return fallback;
        }

        public void Set(string name, DialogueValue value)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            if (_values.TryGetValue(name, out DialogueValue previous) &&
                previous.Type == value.Type && previous.CompareTo(value) == 0)
            {
                return;
            }

            _values[name] = value;
            OnChanged?.Invoke(name, value);
        }

        public void SetBool(string name, bool value) => Set(name, DialogueValue.FromBool(value));
        public void SetInt(string name, int value) => Set(name, DialogueValue.FromInt(value));
        public void SetFloat(string name, float value) => Set(name, DialogueValue.FromFloat(value));
        public void SetString(string name, string value) => Set(name, DialogueValue.FromString(value));

        public void AddInt(string name, int delta)
            => SetInt(name, GetInt(name, 0) + delta);

        public bool Remove(string name) => _values.Remove(name);

        public void Clear()
        {
            _values.Clear();
            OnChanged?.Invoke("*", DialogueValue.FromString("cleared"));
        }

        public void Declare(VariableDeclaration declaration)
        {
            if (declaration == null || string.IsNullOrWhiteSpace(declaration.name)) return;

            string name = declaration.name.Trim();
            DialogueValueType type = declaration.ValueType;
            _declaredTypes[name] = type;

            if (!_values.ContainsKey(name))
                _values[name] = DialogueValue.Parse(declaration.value, type);
        }

        public void DeclareAll(IEnumerable<VariableDeclaration> declarations)
        {
            if (declarations == null) return;
            foreach (VariableDeclaration declaration in declarations) Declare(declaration);
        }

        public bool IsDeclared(string name)
            => !string.IsNullOrEmpty(name) && _declaredTypes.ContainsKey(name);

        public IReadOnlyDictionary<string, DialogueValueType> DeclaredTypes => _declaredTypes;

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var pair in _values)
            {
                sb.Append(pair.Key).Append('=')
                  .Append(DialogueValue.TypeName(pair.Value.Type)).Append(':')
                  .Append(pair.Value.Serialize().Replace("\n", "\\n"))
                  .Append('\n');
            }
            return sb.ToString();
        }

        public void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            foreach (string rawLine in data.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string name = line.Substring(0, eq).Trim();
                string rest = line.Substring(eq + 1);

                int colon = rest.IndexOf(':');
                string typeName = colon < 0 ? "" : rest.Substring(0, colon);
                string value = colon < 0 ? rest : rest.Substring(colon + 1);

                Set(name, DialogueValue.Parse(value.Replace("\\n", "\n"), DialogueValue.ParseType(typeName)));
            }
        }

        public override string ToString() => $"{_values.Count} variable(s)";
    }

    public static class DialogueVariableCatalog
    {
        private static readonly List<VariableDeclaration> Declarations = new List<VariableDeclaration>();
        private static readonly Dictionary<string, VariableDeclaration> ByName =
            new Dictionary<string, VariableDeclaration>(StringComparer.Ordinal);

        public static IReadOnlyList<VariableDeclaration> All => Declarations;
        public static bool IsLoaded { get; private set; }

        public static VariableDeclaration Find(string name)
            => !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out VariableDeclaration found) ? found : null;

        public static void Load()
        {
            Reset();

            string path = DialoguePaths.VariablesFile;
            if (!File.Exists(path))
            {
                DialogueLogger.Log($"VariableCatalog: no declarations at {path} (conditions on undeclared variables are allowed).");
                return;
            }

            try
            {
                Parse(File.ReadAllText(path), path);
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("323", "Variable catalog parse failed", $"{path}: {ex.Message}");
            }
        }

        public static async System.Threading.Tasks.Task LoadAsync()
        {
            Reset();

            string relative = $"{DialoguePaths.VariablesFolderName}/{DialoguePaths.VariablesFileName}";
            string text;
            if (DialogueStreamingAssets.TryReadTextDirect(relative, out string direct))
                text = direct;
            else
                text = await DialogueStreamingAssets.ReadTextAsync(relative);

            if (string.IsNullOrEmpty(text))
            {
                DialogueLogger.Log(
                    $"VariableCatalog: no declarations at {DialoguePaths.VariablesFile} " +
                    "(conditions on undeclared variables are allowed).");
                return;
            }

            try
            {
                Parse(text, relative);
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("323", "Variable catalog parse failed", $"{relative}: {ex.Message}");
            }
        }

        private static void Reset()
        {
            Declarations.Clear();
            ByName.Clear();
            IsLoaded = true;
        }

        private static void Parse(string yaml, string source)
        {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var data = deserializer.Deserialize<VariableCatalogData>(yaml);
            if (data?.variables == null) return;

            foreach (VariableDeclaration declaration in data.variables)
            {
                if (declaration == null || string.IsNullOrWhiteSpace(declaration.name)) continue;
                declaration.name = declaration.name.Trim();
                Declarations.Add(declaration);
                ByName[declaration.name] = declaration;
            }

            DialogueLogger.Log($"VariableCatalog: {Declarations.Count} variable(s) declared from {source}");
        }

        public static void ApplyTo(DialogueVariables variables)
        {
            if (variables == null || Declarations.Count == 0) return;
            variables.DeclareAll(Declarations);
        }

        public static List<string> SortedNames()
        {
            var names = new List<string>();
            foreach (VariableDeclaration declaration in Declarations) names.Add(declaration.name);
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
