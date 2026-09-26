using System;
using UnityEditor;
using UnityEngine;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{

    public static class DialogueDigestTools
    {
        [MenuItem("MySys22/Dialogue/Export Graph Digest", false, 32)]
        public static void ExportDigest()
        {
            try
            {
                GraphDigest digest = DialogueGraphDigest.Export();
                EditorUtility.DisplayDialog("Graph digest",
                    $"Written to:\n{DialoguePaths.EngineDigest}\n\n" +
                    $"Graphs: {(digest.graphs == null ? 0 : digest.graphs.Length)}\n" +
                    $"Preview language: {digest.previewLanguage}\n" +
                    $"Characters: {(digest.characters == null ? 0 : digest.characters.Length)}",
                    "OK");
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("322", "Digest export failed", ex.Message);
                EditorUtility.DisplayDialog("Graph digest", "Export failed:\n" + ex.Message, "OK");
            }
        }

        [MenuItem("MySys22/Dialogue/Reveal Graph Digest", false, 33)]
        public static void RevealDigest()
        {
            if (!System.IO.File.Exists(DialoguePaths.EngineDigest))
            {
                DialogueLogger.LogWarning("No digest yet. Run MySys22/Dialogue/Export Graph Digest.");
                return;
            }
            EditorUtility.RevealInFinder(DialoguePaths.EngineDigest);
        }

        internal static void ExportQuietly()
        {
            try
            {
                DialogueGraphDigest.Export();
            }
            catch (Exception ex)
            {
                DialogueLogger.LogWarning("Digest export skipped: " + ex.Message);
            }
        }
    }
}
