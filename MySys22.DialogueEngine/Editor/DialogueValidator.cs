using System;
using System.IO;
using System.Text;
using UnityEditor;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{
    public static class DialogueValidator
    {
        [MenuItem("MySys22/Dialogue/Validate Project", false, 30)]
        public static void ValidateProject()
        {
            DialogueProjectValidator.Report report = ValidateInternal(null, out string summary);
            EditorUtility.DisplayDialog(report.HasErrors ? "Validation failed" : "Validation", summary, "OK");
        }

        public static DialogueProjectValidator.Report ValidateInternal(string graphPath, out string summary)
        {
            DialogueProject project = DialogueProjectLoader.Load(graphPath);
            ILineProvider provider = YamlLineProvider.Instance;

            DialogueProjectValidator.Report report =
                DialogueProjectValidator.Validate(project.Graph, provider, project.Language);

            var builder = new StringBuilder();
            builder.AppendLine($"Root:     {DialoguePaths.Root}");
            builder.AppendLine($"Graph:    {(project.Graph == null ? "-" : Path.GetFileName(project.GraphPath))}");
            builder.AppendLine($"Language: {project.Language}");
            builder.AppendLine();
            builder.Append(report.ToText());

            summary = builder.ToString();
            return report;
        }
    }
}
