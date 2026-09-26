using System;
using System.Collections.Generic;
using System.Text;

namespace MySys22.DialogueEngine.Core
{

    public static class DialogueProjectValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Infos = new List<string>();
            public int NodesChecked;
            public int LinesChecked;

            public bool HasErrors => Errors.Count > 0;
            public bool IsClean => Errors.Count == 0 && Warnings.Count == 0;

            public string ToText()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Nodes checked: {NodesChecked}, lines checked: {LinesChecked}");
                foreach (string e in Errors) sb.AppendLine("ERROR   " + e);
                foreach (string w in Warnings) sb.AppendLine("WARNING " + w);
                foreach (string i in Infos) sb.AppendLine("INFO    " + i);
                if (IsClean) sb.AppendLine("Project is valid.");
                return sb.ToString();
            }
        }

        public static Report Validate(GraphData graph, ILineProvider lineProvider, string language = null)
        {
            var report = new Report();

            if (graph == null)
            {
                report.Errors.Add("No graph loaded.");
                return report;
            }

            if (graph.Nodes == null || graph.Nodes.Count == 0)
            {
                report.Errors.Add($"Graph '{graph.GraphId}' has no nodes.");
                return report;
            }

            var byId = new Dictionary<string, NodeData>();
            var referenced = new HashSet<string>();

            foreach (NodeData node in graph.Nodes)
            {
                report.NodesChecked++;

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    report.Errors.Add($"A '{node.Type}' node has no id.");
                    continue;
                }
                if (byId.ContainsKey(node.Id))
                {
                    report.Errors.Add($"Duplicate node id '{node.Id}'.");
                    continue;
                }
                byId[node.Id] = node;

                if (!string.IsNullOrWhiteSpace(node.Condition) &&
                    node.Type != "dialogue" && node.Type != "function" && node.Type != "condition")
                {
                    report.Warnings.Add(
                        $"Node '{node.Id}' ({node.Type}) has a condition, but only dialogue, function and " +
                        "condition nodes evaluate it.");
                }
                if (string.IsNullOrWhiteSpace(node.Condition) is false &&
                    (node.Type == "dialogue" || node.Type == "function"))
                {
                    ValidateCondition(report, node.Condition, $"{node.Type} node '{node.Id}'");
                }

                switch (node.Type)
                {
                    case "dialogue":
                        if (string.IsNullOrWhiteSpace(node.Speaker))
                            report.Errors.Add($"Dialogue node '{node.Id}' has no speaker (CID).");
                        if (node.StartDid == null || node.EndDid == null)
                            report.Errors.Add($"Dialogue node '{node.Id}' is missing its DID range.");
                        else if (node.EndDid < node.StartDid)
                            report.Errors.Add($"Dialogue node '{node.Id}' has EndDid < StartDid " +
                                             $"({node.EndDid} < {node.StartDid}).");
                        break;

                    case "choice":
                        if (node.Choices == null || node.Choices.Count == 0)
                            report.Errors.Add($"Choice node '{node.Id}' has no options.");
                        else
                            ValidateChoices(report, node);
                        break;

                    case "function":
                        if (string.IsNullOrWhiteSpace(node.Action))
                            report.Errors.Add($"Function node '{node.Id}' has no action.");
                        else
                            ValidateActionParameters(report, node.Action, node.Params,
                                $"function node '{node.Id}'", node.Wait);
                        if (node.Conditions != null)
                        {
                            foreach (string conditionId in node.Conditions)
                            {
                                NodeData condition = graph.Nodes.Find(n => n.Id == conditionId);
                                if (condition == null)
                                {
                                    report.Errors.Add(
                                        $"Function node '{node.Id}' references condition '{conditionId}' which does not exist.");
                                }
                                else if (condition.Type != "condition")
                                {
                                    report.Errors.Add(
                                        $"Function node '{node.Id}' references '{conditionId}', which is a " +
                                        $"{condition.Type} node and not a condition.");
                                }
                            }
                        }
                        break;

                    case "condition":
                        if (string.IsNullOrWhiteSpace(node.OnTrue) && string.IsNullOrWhiteSpace(node.OnFalse))
                        {
                            report.Warnings.Add(
                                $"Condition node '{node.Id}' has no connected output: neither True nor False " +
                                "goes anywhere.");
                        }
                        if (string.IsNullOrWhiteSpace(node.OnTrue))
                            report.Warnings.Add($"Condition node '{node.Id}' has no True target.");
                        if (string.IsNullOrWhiteSpace(node.OnFalse))
                            report.Warnings.Add($"Condition node '{node.Id}' has no False target.");
                        if (!string.IsNullOrWhiteSpace(node.Condition))
                            ValidateCondition(report, node.Condition, $"condition node '{node.Id}'");
                        break;

                    case "jump":
                        if (string.IsNullOrWhiteSpace(node.Graph) && string.IsNullOrWhiteSpace(node.Next))
                            report.Errors.Add($"Jump node '{node.Id}' has neither a target graph nor a target node.");
                        else if (!string.IsNullOrWhiteSpace(node.Graph))
                        {
                            if (DialogueProjectLoader.FindGraphFileByName(node.Graph) == null &&
                                DialogueProjectLoader.FindGraphById(node.Graph) == null)
                            {
                                report.Errors.Add(
                                    $"Jump node '{node.Id}' targets graph '{node.Graph}' which does not exist in " +
                                    "StreamingAssets/" + DialoguePaths.GraphsFolderName + ".");
                            }
                            if (node.Return && string.IsNullOrWhiteSpace(node.Next))
                                report.Warnings.Add(
                                    $"Jump node '{node.Id}' declares return: true but no return node; " +
                                    "the dialogue will simply continue in the target graph.");
                        }
                        break;

                    case "end":
                        break;

                    default:
                        report.Errors.Add($"Node '{node.Id}' has unknown type '{node.Type}'.");
                        break;
                }
            }

            foreach (NodeData node in graph.Nodes)
            {
                CheckAction(report, node.Action, $"function node '{node.Id}'");
                CheckAction(report, node.OnComplete, $"dialogue node '{node.Id}' (on_complete)");
                CheckAction(report, node.OnParallel, $"dialogue node '{node.Id}' (on_parallel)");

                if (node.Type == "condition")
                {
                    CheckConditionTarget(report, byId, referenced, node.Id, node.OnTrue, "True");
                    CheckConditionTarget(report, byId, referenced, node.Id, node.OnFalse, "False");

                    if (!string.IsNullOrWhiteSpace(node.Next))
                    {
                        referenced.Add(node.Next);
                        if (!byId.ContainsKey(node.Next))
                            report.Errors.Add($"Condition node '{node.Id}' points to missing node '{node.Next}'.");
                    }
                }
                else if (node.Type == "jump")
                {
                    if (string.IsNullOrWhiteSpace(node.Graph) && !string.IsNullOrWhiteSpace(node.Next))
                    {
                        referenced.Add(node.Next);
                        if (!byId.ContainsKey(node.Next))
                            report.Errors.Add($"Jump node '{node.Id}' points to missing node '{node.Next}'.");
                    }
                }
                else if (node.Type == "dialogue" || node.Type == "function")
                {
                    if (string.IsNullOrWhiteSpace(node.Next))
                    {
                        report.Warnings.Add($"Node '{node.Id}' has no outgoing link; the dialogue ends there.");
                    }
                    else
                    {
                        referenced.Add(node.Next);
                        if (!byId.ContainsKey(node.Next))
                            report.Errors.Add($"Node '{node.Id}' points to missing node '{node.Next}'.");
                    }
                }
                else if (node.Type == "choice" && node.Choices != null)
                {
                    foreach (ChoiceData choice in node.Choices)
                    {
                        if (string.IsNullOrWhiteSpace(choice.Next))
                        {
                            report.Warnings.Add($"Choice '{choice.Text}' in node '{node.Id}' has no target.");
                            continue;
                        }
                        referenced.Add(choice.Next);
                        if (!byId.ContainsKey(choice.Next))
                            report.Errors.Add(
                                $"Choice '{choice.Text}' in node '{node.Id}' points to missing node '{choice.Next}'.");
                    }
                }
            }

            string startId = graph.StartNode;
            if (string.IsNullOrWhiteSpace(startId))
            {
                foreach (NodeData node in graph.Nodes)
                {
                    if (node.IsStart) { startId = node.Id; break; }
                }
                if (string.IsNullOrWhiteSpace(startId))
                    report.Warnings.Add("The graph has no start node; the first node will be used.");
            }
            else if (!byId.ContainsKey(startId))
            {
                report.Errors.Add($"Start node '{startId}' does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(startId))
            {
                var reachable = new HashSet<string> { startId };
                var queue = new Queue<string>();
                queue.Enqueue(startId);

                void Enqueue(string id)
                {
                    if (string.IsNullOrWhiteSpace(id)) return;
                    if (reachable.Add(id)) queue.Enqueue(id);
                }

                while (queue.Count > 0)
                {
                    NodeData node;
                    if (!byId.TryGetValue(queue.Dequeue(), out node)) continue;
                    if (node.Type != "jump" || string.IsNullOrWhiteSpace(node.Graph)) Enqueue(node.Next);
                    if (node.Choices != null)
                        foreach (ChoiceData choice in node.Choices) Enqueue(choice.Next);
                }

                foreach (NodeData node in graph.Nodes)
                {
                    if (!reachable.Contains(node.Id))
                        report.Warnings.Add($"Node '{node.Id}' is unreachable from the start node.");
                }
            }

            if (lineProvider == null)
            {
                report.Warnings.Add("No line provider supplied; line resolution was not checked.");
            }
            else
            {
                string lang = language ?? LanguageManager.CurrentLanguage;
                report.Infos.Add($"Line database: {lang}");

                foreach (NodeData node in graph.Nodes)
                {
                    if (node.Type == "dialogue")
                    {
                        if (string.IsNullOrWhiteSpace(node.Speaker)) continue;
                        if (node.StartDid == null || node.EndDid == null) continue;

                        for (int did = node.StartDid.Value; did <= node.EndDid.Value; did++)
                        {
                            report.LinesChecked++;
                            if (!HasLine(lineProvider, node.Speaker, did))
                            {
                                report.Warnings.Add(
                                    $"Missing line: character '{node.Speaker}' ({CharacterRegistry.DisplayName(node.Speaker)}) " +
                                    $"DID {did} required by node '{node.Id}'.");
                            }
                        }
                    }
                    else if (node.Type == "choice" && node.Choices != null)
                    {
                        string speaker = string.IsNullOrWhiteSpace(node.Speaker)
                            ? DialogueGraphPlayer.DefaultChoiceSpeaker
                            : node.Speaker;

                        foreach (ChoiceData choice in node.Choices)
                        {
                            if (choice == null || choice.TextDid <= 0) continue;
                            report.LinesChecked++;
                            if (!HasLine(lineProvider, speaker, choice.TextDid))
                            {
                                report.Warnings.Add(
                                    $"Missing choice text: speaker '{speaker}' " +
                                    $"({CharacterRegistry.DisplayName(speaker)}) DID {choice.TextDid} " +
                                    $"required by choice '{choice.Text}' in node '{node.Id}'.");
                            }
                        }
                    }
                }
            }

            return report;
        }

        private static void ValidateCondition(Report report, string expression, string context)
        {
            if (string.IsNullOrWhiteSpace(expression)) return;

            if (!DialogueExpression.TryValidate(expression, out string error, out List<string> variables))
            {
                report.Errors.Add($"Invalid condition on {context}: {error} ({expression})");
                return;
            }

            if (!DialogueVariableCatalog.IsLoaded) return;

            foreach (string name in variables)
            {
                if (DialogueExpression.IsKeyword(name)) continue;
                if (name.StartsWith("quest.", StringComparison.Ordinal)) continue;
                if (DialogueVariableCatalog.Find(name) != null) continue;

                report.Infos.Add(
                    $"Condition on {context} uses '{name}', which is not declared in Variables/variables.yaml.");
            }
        }

        private static bool HasLine(ILineProvider provider, string speaker, int did)
        {
            if (provider is YamlLineProvider yaml) return yaml.HasLine(speaker, did);

            string text = provider.GetLine(speaker, did);
            return !string.IsNullOrEmpty(text) && !text.StartsWith("[MISSING");
        }

        private static void CheckAction(Report report, string actionId, string context)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return;
            if (ActionRegistry.IsRegistered(actionId)) return;

            report.Infos.Add($"Action '{actionId}' referenced by {context} is not registered yet. " +
                             "Register it with ActionRegistry.RegisterAction before it is executed.");
        }

        private static void ValidateActionParameters(Report report, string actionId,
            IReadOnlyDictionary<string, string> parameters, string context, bool wait)
        {
            ActionRegistry.ActionInfo info = ActionRegistry.GetInfo(actionId);
            if (info == null) return;

            if (info.RequiredParameters != null)
            {
                foreach (string key in info.RequiredParameters)
                {
                    string value = null;
                    bool present = parameters != null && parameters.TryGetValue(key, out value);
                    if (!present || string.IsNullOrWhiteSpace(value))
                    {
                        report.Errors.Add(
                            $"Action '{actionId}' requires parameter '{key}' ({context}). " +
                            $"Add it to the node params.");
                    }
                }
            }

            if (info.OptionalParameters != null && parameters != null)
            {
                foreach (var pair in parameters)
                {
                    if (Contains(info.RequiredParameters, pair.Key)) continue;
                    if (Contains(info.OptionalParameters, pair.Key)) continue;
                    report.Warnings.Add(
                        $"Action '{actionId}' does not declare parameter '{pair.Key}' " +
                        $"({context}). It will be ignored by the action.");
                }
            }

            if (info.WaitsForCompletion && !wait && !string.IsNullOrEmpty(info.Description))
            {
                report.Warnings.Add(
                    $"Action '{actionId}' is declared as long running; set wait: true on {context} " +
                    "if the dialogue must resume only when it finishes.");
            }
        }

        private static void ValidateChoices(Report report, NodeData node)
        {
            string speaker = string.IsNullOrWhiteSpace(node.Speaker)
                ? DialogueGraphPlayer.DefaultChoiceSpeaker
                : node.Speaker;

            foreach (ChoiceData choice in node.Choices)
            {
                if (choice == null) continue;

                if (string.IsNullOrWhiteSpace(choice.Text) && choice.TextDid <= 0)
                {
                    report.Errors.Add(
                        $"Choice in node '{node.Id}' has neither text nor text_did.");
                }

                if (choice.TextDid > 0 && string.IsNullOrWhiteSpace(node.Speaker))
                {
                    report.Infos.Add(
                        $"Choice '{choice.Text}' in node '{node.Id}' resolves text_did {choice.TextDid} " +
                        $"through '{DialogueGraphPlayer.DefaultChoiceSpeaker}'; set a speaker on the node " +
                        "to keep it readable.");
                }

                if (!string.IsNullOrWhiteSpace(choice.Condition))
                    ValidateCondition(report, choice.Condition, $"choice '{choice.Text}' in node '{node.Id}'");

                if (!string.IsNullOrEmpty(choice.OnChosen))
                    CheckAction(report, choice.OnChosen, $"choice '{choice.Text}' in node '{node.Id}'");
            }

            bool anyUngated = false;
            foreach (ChoiceData choice in node.Choices)
            {
                if (choice != null && string.IsNullOrWhiteSpace(choice.Condition)) { anyUngated = true; break; }
            }
            if (!anyUngated)
            {
                report.Warnings.Add(
                    $"Choice node '{node.Id}': every option has a condition, the dialogue can dead-end. " +
                    "Leave at least one option unguarded.");
            }
        }

        private static void CheckConditionTarget(Report report, Dictionary<string, NodeData> byId,
            HashSet<string> referenced, string conditionId, string target, string branch)
        {
            if (string.IsNullOrWhiteSpace(target)) return;

            referenced.Add(target);
            if (!byId.ContainsKey(target))
                report.Errors.Add($"Condition node '{conditionId}' ({branch}) points to missing node '{target}'.");
        }

        private static bool Contains(string[] array, string value)
        {
            if (array == null) return false;
            foreach (string item in array)
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static Report ValidateAndLog(GraphData graph, ILineProvider provider, string language = null)
        {
            Report report = Validate(graph, provider, language);
            if (report.HasErrors)
                DialogueLogger.LogError("330", "Dialogue project validation failed", "\n" + report.ToText());
            else if (!report.IsClean)
                DialogueLogger.LogWarning("Dialogue project validation warnings:\n" + report.ToText());
            else
                DialogueLogger.Log("Dialogue project validation passed.\n" + report.ToText());
            return report;
        }
    }
}
