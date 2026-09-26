using System.Collections.Generic;
using YamlDotNet.Serialization;
using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    [Preserve]
    public class LineDatabaseData
    {
        [YamlMember(Alias = "character")]
        public string Character { get; set; }

        [YamlMember(Alias = "language")]
        public string Language { get; set; }

        [YamlMember(Alias = "lines")]
        public List<LineEntry> Lines { get; set; } = new List<LineEntry>();
    }

    [Preserve]
    public class LineEntry
    {
        [YamlMember(Alias = "did")]
        public int Did { get; set; }

        [YamlMember(Alias = "text")]
        public string Text { get; set; }
    }
}
