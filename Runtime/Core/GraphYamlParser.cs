using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using UnityEngine.Scripting;

namespace MySys22.DialogueEngine.Core
{
    [Preserve]
    public static class GraphYamlParser
    {

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)

            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .Build();

        public static GraphData LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Graph YAML not found: {filePath}");

            try
            {
                string yamlContent = File.ReadAllText(filePath);
                var result = Deserializer.Deserialize<GraphData>(yamlContent);
                DialogueLogger.Log($"Graph loaded: {result.GraphId}, nodes: {result.Nodes.Count}");
                return result;
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                DialogueLogger.LogError("302", "YAML parsing failed", ex.Message);
                throw;
            }
        }

        public static async Task<GraphData> LoadFromStreamingAssetsAsync(string relativePath)
        {
            if (DialogueStreamingAssets.TryReadTextDirect(relativePath, out string direct))
            {
                var parsed = Deserializer.Deserialize<GraphData>(direct);
                DialogueLogger.Log($"Graph loaded: {parsed.GraphId}, nodes: {parsed.Nodes.Count}");
                return parsed;
            }

            string url = DialogueStreamingAssets.UrlFor(relativePath);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var asyncOp = request.SendWebRequest();
                var tcs = new TaskCompletionSource<bool>();
                asyncOp.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DialogueLogger.LogError("317", "Graph load failed", $"Error: {request.error}, URL: {url}");
                    throw new Exception($"Graph load error: {request.error}");
                }

                string yamlContent = request.downloadHandler.text;
                var result = Deserializer.Deserialize<GraphData>(yamlContent);
                DialogueLogger.Log($"Graph loaded (async): {result.GraphId}, nodes: {result.Nodes.Count}");
                return result;
            }
        }

        public static bool TryLoadFromFile(string filePath, out GraphData graph, out string error)
        {
            graph = null;
            error = null;
            try
            {
                graph = LoadFromFile(filePath);
                return graph != null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void SaveToFile(GraphData data, string filePath)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                string yaml = Serializer.Serialize(data);
                File.WriteAllText(filePath, yaml);
                DialogueLogger.Log($"Graph saved: {filePath}");
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("316", "Save failed", ex.Message);
                throw;
            }
        }

        public static string SerializeToString(GraphData data)
        {
            return Serializer.Serialize(data);
        }
    }
}
