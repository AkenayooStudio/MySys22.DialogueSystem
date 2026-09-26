using System.Collections.Generic;
using YamlDotNet.Serialization;
using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    [Preserve]
    public class GraphData
    {
        [YamlMember(Alias = "graphId")]
        public string GraphId { get; set; }

        [YamlMember(Alias = "startNode")]
        public string StartNode { get; set; }

        [YamlMember(Alias = "nodes")]
        public List<NodeData> Nodes { get; set; } = new List<NodeData>();
    }

    [Preserve]
    public class NodeData
    {
        [YamlMember(Alias = "id")]
        public string Id { get; set; }

        [YamlMember(Alias = "type")]
        public string Type { get; set; }

        [YamlMember(Alias = "speaker")]
        public string Speaker { get; set; }

        [YamlMember(Alias = "start_did")]
        public int? StartDid { get; set; }

        [YamlMember(Alias = "end_did")]
        public int? EndDid { get; set; }

        [YamlMember(Alias = "on_parallel")]
        public string OnParallel { get; set; }

        [YamlMember(Alias = "on_complete")]
        public string OnComplete { get; set; }

        [YamlMember(Alias = "action")]
        public string Action { get; set; }

        [YamlMember(Alias = "wait")]
        public bool Wait { get; set; }

        [YamlMember(Alias = "params")]
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();

        [YamlMember(Alias = "next")]
        public string Next { get; set; }

        [YamlMember(Alias = "graph")]
        public string Graph { get; set; }

        [YamlMember(Alias = "return")]
        public bool Return { get; set; }

        [YamlMember(Alias = "condition")]
        public string Condition { get; set; }

        [YamlMember(Alias = "else")]
        public string Else { get; set; }

        [YamlMember(Alias = "conditions")]
        public List<string> Conditions { get; set; } = new List<string>();

        [YamlMember(Alias = "option")]
        public string Option { get; set; }

        [YamlMember(Alias = "on_true")]
        public string OnTrue { get; set; }

        [YamlMember(Alias = "on_false")]
        public string OnFalse { get; set; }

        [YamlMember(Alias = "choices")]
        public List<ChoiceData> Choices { get; set; } = new List<ChoiceData>();

        [YamlMember(Alias = "position")]
        public PositionData Position { get; set; }

        [YamlMember(Alias = "isStart")]
        public bool IsStart { get; set; }

        [YamlMember(Alias = "audio")]
        public string Audio { get; set; }

        [YamlMember(Alias = "audio_loop")]
        public bool AudioLoop { get; set; }

        [YamlMember(Alias = "voice")]
        public bool Voice { get; set; }
    }

    [Preserve]
    public class ChoiceData
    {

        [YamlMember(Alias = "text")]
        public string Text { get; set; }

        [YamlMember(Alias = "text_did")]
        public int TextDid { get; set; }

        [YamlMember(Alias = "next")]
        public string Next { get; set; }

        [YamlMember(Alias = "portGuid")]
        public string PortGuid { get; set; }

        [YamlMember(Alias = "onChosen")]
        public string OnChosen { get; set; }

        [YamlMember(Alias = "condition")]
        public string Condition { get; set; }
    }

    [Preserve]
    public class PositionData
    {
        [YamlMember(Alias = "x")]
        public float X { get; set; }

        [YamlMember(Alias = "y")]
        public float Y { get; set; }
    }
}
