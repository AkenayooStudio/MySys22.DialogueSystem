using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MySys22.DialogueEngine.Core
{

    [Serializable]
    public class GraphDigest
    {
        public int formatVersion = 1;
        public string generatedBy;
        public string generatedAt;
        public string previewLanguage;
        public string[] languages;
        public DigestCharacter[] characters;
        public DigestGraph[] graphs;
    }

    [Serializable]
    public class DigestCharacter
    {
        public string cid;
        public string name;
        public string language;
    }

    [Serializable]
    public class DigestGraph
    {
        public string graphId;
        public string file;
        public string startNode;
        public DigestNode[] nodes;

        public DigestUsage[] usage;
    }

    [Serializable]
    public class DigestNode
    {
        public string id;
        public string type;
        public string speaker;
        public int startDid;
        public int endDid;
        public string next;
        public string action;
        public bool wait;
        public string onComplete;
        public string onParallel;

        public string graph;

        public bool returns;

        public string condition;

        public string elseBranch;

        public string[] conditionVariables;

        public string option;

        public string[] conditions;

        public string onTrue;

        public string onFalse;
        public DigestChoice[] choices;
    }

    [Serializable]
    public class DigestChoice
    {
        public string text;
        public int textDid;
        public string speaker;
        public string next;
        public string portGuid;

        public string condition;
    }

    [Serializable]
    public class DigestUsage
    {
        public string speaker;
        public int did;
        public string nodeId;

        public string text;
        public bool missing;
    }

    public static class DialogueGraphDigest
    {
        public const int CurrentFormatVersion = 1;

        public static GraphDigest Build(string previewLanguage = null)
        {
            var manifest = DialogueProjectManifestLoader.LoadOrDefault();
            string language = DialoguePaths.NormalizeLanguage(previewLanguage)
                              ?? DialoguePaths.NormalizeLanguage(manifest.defaultLanguage)
                              ?? DialoguePaths.DefaultLanguage;

            var snapshot = YamlLineProvider.LoadLanguage(language);

            var digest = new GraphDigest
            {
                formatVersion = CurrentFormatVersion,
                generatedBy = $"MySys22.DialogueEngine {EngineVersion.Value}",
                generatedAt = DateTime.UtcNow.ToString("o"),
                previewLanguage = language,
                languages = CollectLanguages(manifest),
                characters = BuildCharacters(),
                graphs = BuildGraphs(snapshot)
            };

            return digest;
        }

        public static GraphDigest Export(string previewLanguage = null)
        {
            GraphDigest digest = Build(previewLanguage);

            DialoguePaths.EnsureLayout();
            string json = JsonUtility.ToJson(digest, true);
            File.WriteAllText(DialoguePaths.EngineDigest, json);

            int nodes = 0;
            int usage = 0;
            if (digest.graphs != null)
            {
                foreach (DigestGraph graph in digest.graphs)
                {
                    nodes += graph.nodes?.Length ?? 0;
                    usage += graph.usage?.Length ?? 0;
                }
            }

            DialogueLogger.Log($"Graph digest written: {DialoguePaths.EngineDigest} " +
                               $"({digest.graphs?.Length ?? 0} graph(s), {nodes} node(s), {usage} referenced line(s), " +
                               $"preview: {digest.previewLanguage})");
            return digest;
        }

        public static GraphDigest Read(string path = null)
        {
            string target = string.IsNullOrEmpty(path) ? DialoguePaths.EngineDigest : path;
            if (!File.Exists(target)) return null;

            try
            {
                return JsonUtility.FromJson<GraphDigest>(File.ReadAllText(target));
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("322", "Graph digest parse failed", $"{target}: {ex.Message}");
                return null;
            }
        }

        private static string[] CollectLanguages(DialogueProjectManifest manifest)
        {
            var languages = new List<string>();
            if (manifest?.languages != null)
            {
                foreach (string raw in manifest.languages)
                {
                    string code = DialoguePaths.NormalizeLanguage(raw);
                    if (code != null && !languages.Contains(code)) languages.Add(code);
                }
            }

            if (Directory.Exists(DialoguePaths.Dialogue))
            {
                foreach (string dir in Directory.GetDirectories(DialoguePaths.Dialogue))
                {
                    string code = DialoguePaths.NormalizeLanguage(Path.GetFileName(dir));
                    if (code != null && !languages.Contains(code)) languages.Add(code);
                }
            }
            return languages.ToArray();
        }

        private static DigestCharacter[] BuildCharacters()
        {
            CharacterRegistry.Load();

            var list = new List<DigestCharacter>();
            foreach (CharacterListEntry entry in CharacterRegistry.Entries)
            {
                list.Add(new DigestCharacter
                {
                    cid = entry.Cid,
                    name = entry.Name,
                    language = entry.Language
                });
            }
            return list.ToArray();
        }

        private static DigestGraph[] BuildGraphs(YamlLineProvider.Snapshot snapshot)
        {
            var list = new List<DigestGraph>();
            if (snapshot == null) return list.ToArray();

            foreach (string file in DialogueProjectLoader.ListGraphFiles())
            {
                if (!GraphYamlParser.TryLoadFromFile(file, out GraphData graph, out string error))
                {
                    DialogueLogger.LogError("318", "Digest: graph skipped", $"{file}: {error}");
                    continue;
                }
                list.Add(BuildGraph(graph, Path.GetFileName(file), snapshot));
            }
            return list.ToArray();
        }

        private static DigestGraph BuildGraph(GraphData graph, string fileName, YamlLineProvider.Snapshot snapshot)
        {
            var digest = new DigestGraph
            {
                graphId = graph.GraphId,
                file = fileName,
                startNode = graph.StartNode,
                nodes = new DigestNode[graph.Nodes.Count],
                usage = Array.Empty<DigestUsage>()
            };

            var usage = new List<DigestUsage>();

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                NodeData node = graph.Nodes[i];
                var entry = new DigestNode
                {
                    id = node.Id,
                    type = node.Type,
                    speaker = node.Speaker,
                    startDid = node.StartDid ?? 0,
                    endDid = node.EndDid ?? 0,
                    next = node.Next,
                    action = node.Action,
                    wait = node.Wait,
                    onComplete = node.OnComplete,
                    onParallel = node.OnParallel,
                    graph = node.Graph,
                    returns = node.Return,
                    condition = node.Condition,
                    elseBranch = node.Else,
                    conditionVariables = DialogueExpression.CollectVariables(node.Condition).ToArray(),
                    option = node.Option,
                    conditions = (node.Conditions ?? new List<string>()).ToArray(),
                    onTrue = node.OnTrue,
                    onFalse = node.OnFalse,
                    choices = BuildChoices(node)
                };
                digest.nodes[i] = entry;

                if (node.Type == "dialogue" && node.StartDid != null && node.EndDid != null)
                {
                    for (int did = node.StartDid.Value; did <= node.EndDid.Value; did++)
                        usage.Add(BuildUsage(node.Speaker, did, node.Id, snapshot));
                }

                if (node.Type == "choice" && node.Choices != null)
                {
                    foreach (ChoiceData choice in node.Choices)
                    {
                        if (choice.TextDid <= 0) continue;
                        string speaker = string.IsNullOrWhiteSpace(node.Speaker)
                            ? DialogueGraphPlayer.DefaultChoiceSpeaker
                            : node.Speaker;
                        usage.Add(BuildUsage(speaker, choice.TextDid, node.Id, snapshot));
                    }
                }
            }

            digest.usage = usage.ToArray();
            return digest;
        }

        private static DigestChoice[] BuildChoices(NodeData node)
        {
            if (node.Choices == null || node.Choices.Count == 0) return Array.Empty<DigestChoice>();

            var choices = new DigestChoice[node.Choices.Count];
            for (int i = 0; i < node.Choices.Count; i++)
            {
                ChoiceData choice = node.Choices[i];
                choices[i] = new DigestChoice
                {
                    text = choice.Text,
                    textDid = choice.TextDid,
                    speaker = string.IsNullOrWhiteSpace(node.Speaker)
                        ? DialogueGraphPlayer.DefaultChoiceSpeaker
                        : node.Speaker,
                    next = choice.Next,
                    portGuid = choice.PortGuid,
                    condition = choice.Condition
                };
            }
            return choices;
        }

        private static DigestUsage BuildUsage(string speaker, int did, string nodeId,
            YamlLineProvider.Snapshot snapshot)
        {
            bool found = snapshot.TryGet(speaker, did, out string text);
            return new DigestUsage
            {
                speaker = speaker,
                did = did,
                nodeId = nodeId,
                text = found ? text : "",
                missing = !found
            };
        }
    }

    public static class EngineVersion
    {
        public const string Value = "1.0";
    }
}
