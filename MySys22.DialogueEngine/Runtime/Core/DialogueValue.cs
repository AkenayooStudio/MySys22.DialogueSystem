using System;
using System.Globalization;

using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    public enum DialogueValueType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        String = 3
    }

    [Serializable]
    [Preserve]
    public struct DialogueValue
    {
        public DialogueValueType Type;
        private string _raw;

        public static DialogueValue FromBool(bool value)
            => new DialogueValue { Type = DialogueValueType.Bool, _raw = value ? "true" : "false" };

        public static DialogueValue FromInt(int value)
            => new DialogueValue { Type = DialogueValueType.Int, _raw = value.ToString(CultureInfo.InvariantCulture) };

        public static DialogueValue FromFloat(float value)
            => new DialogueValue { Type = DialogueValueType.Float, _raw = value.ToString("R", CultureInfo.InvariantCulture) };

        public static DialogueValue FromString(string value)
            => new DialogueValue { Type = DialogueValueType.String, _raw = value ?? "" };

        public static DialogueValue Parse(string raw)
        {
            string text = raw == null ? "" : raw.Trim();

            if (bool.TryParse(text, out bool b)) return FromBool(b);
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return FromInt(i);
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return FromFloat(f);
            return FromString(text);
        }

        public static DialogueValue Parse(string raw, DialogueValueType type)
        {
            string text = raw == null ? "" : raw.Trim();
            switch (type)
            {
                case DialogueValueType.Bool:
                    return FromBool(bool.TryParse(text, out bool b) ? b
                        : text == "1" || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase));
                case DialogueValueType.Int:
                    return FromInt(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                        ? i : (int)(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float fi) ? fi : 0f));
                case DialogueValueType.Float:
                    return FromFloat(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f);
                default:
                    return FromString(raw);
            }
        }

        public static DialogueValueType ParseType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return DialogueValueType.Bool;
            switch (type.Trim().ToLowerInvariant())
            {
                case "bool": case "boolean": return DialogueValueType.Bool;
                case "int": case "integer": return DialogueValueType.Int;
                case "float": case "number": case "double": return DialogueValueType.Float;
                case "string": case "text": return DialogueValueType.String;
                default: return DialogueValueType.Bool;
            }
        }

        public static string TypeName(DialogueValueType type) => type switch
        {
            DialogueValueType.Bool => "bool",
            DialogueValueType.Int => "int",
            DialogueValueType.Float => "float",
            _ => "string"
        };

        public bool AsBool
        {
            get
            {
                switch (Type)
                {
                    case DialogueValueType.Bool: return bool.TryParse(_raw, out bool b) && b;
                    case DialogueValueType.Int: return AsInt != 0;
                    case DialogueValueType.Float: return Math.Abs(AsFloat) > float.Epsilon;
                    default: return !string.IsNullOrEmpty(_raw);
                }
            }
        }

        public int AsInt
        {
            get
            {
                switch (Type)
                {
                    case DialogueValueType.Bool: return AsBool ? 1 : 0;
                    case DialogueValueType.Int:
                        return int.TryParse(_raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
                    default:
                        return (int)AsFloat;
                }
            }
        }

        public float AsFloat
        {
            get
            {
                switch (Type)
                {
                    case DialogueValueType.Bool: return AsBool ? 1f : 0f;
                    case DialogueValueType.Int:
                        return float.TryParse(_raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
                    case DialogueValueType.Float:
                        return float.TryParse(_raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float g) ? g : 0f;
                    default:
                        return float.TryParse(_raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float h) ? h : 0f;
                }
            }
        }

        public string AsString => _raw ?? "";

        public bool IsNumeric => Type == DialogueValueType.Int || Type == DialogueValueType.Float;

        public string Serialize() => AsString;

        public static DialogueValue Deserialize(string raw, DialogueValueType type) => Parse(raw, type);

        public int CompareTo(DialogueValue other)
        {
            if (IsNumeric || other.IsNumeric)
            {

                if (Type == DialogueValueType.Bool && other.IsNumeric) return AsInt.CompareTo(other.AsInt);
                if (other.Type == DialogueValueType.Bool && IsNumeric) return AsInt.CompareTo(other.AsInt);
                return AsFloat.CompareTo(other.AsFloat);
            }

            if (Type == DialogueValueType.Bool && other.Type == DialogueValueType.Bool)
                return AsBool.CompareTo(other.AsBool);

            return string.CompareOrdinal(AsString, other.AsString);
        }

        public bool Equals(DialogueValue other) => CompareTo(other) == 0;

        public override string ToString() => $"{AsString} ({TypeName(Type)})";
    }
}
