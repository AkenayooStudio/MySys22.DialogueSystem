using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Core.SIMD
{
    public static class SimdOptimizer
    {
        public static bool Enabled { get; set; } = true;

        public static NativeList<int> FilterNodesSimd(
            GraphData graph,
            string targetType,
            string targetSpeaker = null,
            Vector2? searchCenter = null,
            float searchRadius = -1f,
            Allocator allocator = Allocator.TempJob)
        {
            if (!Enabled || !SimdHelper.UseSimd || graph == null || graph.Nodes.Count == 0)
                return FilterNodesScalar(graph, targetType, targetSpeaker, searchCenter, searchRadius, allocator);

            try
            {
                var nodeTypes = new NativeArray<int>(graph.Nodes.Count, Allocator.TempJob);
                var speakerHashes = new NativeArray<int>(graph.Nodes.Count, Allocator.TempJob);
                var positions = new NativeArray<float2>(graph.Nodes.Count, Allocator.TempJob);

                int typeCode = targetType switch
                {
                    "dialogue" => 0,
                    "choice" => 1,
                    "function" => 2,
                    "end" => 3,
                    _ => -1
                };

                int speakerHash = string.IsNullOrEmpty(targetSpeaker) ? 0 : targetSpeaker.GetHashCode();
                float2 center = searchCenter.HasValue ? new float2(searchCenter.Value.x, searchCenter.Value.y) : float2.zero;

                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    var node = graph.Nodes[i];
                    nodeTypes[i] = node.Type switch
                    {
                        "dialogue" => 0,
                        "choice" => 1,
                        "function" => 2,
                        "end" => 3,
                        _ => -1
                    };
                    speakerHashes[i] = string.IsNullOrEmpty(node.Speaker) ? 0 : node.Speaker.GetHashCode();
                    positions[i] = new float2(node.Position?.X ?? 0, node.Position?.Y ?? 0);
                }

                var matchFlags = new NativeArray<int>(graph.Nodes.Count, Allocator.TempJob);

                var job = new NodeSearchSimdJob
                {
                    NodeTypes = nodeTypes,
                    SpeakerHashes = speakerHashes,
                    Positions = positions,
                    TargetType = typeCode,
                    TargetSpeakerHash = speakerHash,
                    SearchCenter = center,
                    SearchRadius = searchRadius,
                    MatchFlags = matchFlags
                };
                var handle = job.Schedule(graph.Nodes.Count, 64);
                handle.Complete();

                var resultIndices = new NativeList<int>(allocator);
                for (int i = 0; i < matchFlags.Length; i++)
                    if (matchFlags[i] == 1) resultIndices.Add(i);

                nodeTypes.Dispose();
                speakerHashes.Dispose();
                positions.Dispose();
                matchFlags.Dispose();

                return resultIndices;
            }
            catch (Exception ex)
            {
                DialogueLogger.LogError("314", "SIMD filter failed, using scalar fallback", ex.Message);
                return FilterNodesScalar(graph, targetType, targetSpeaker, searchCenter, searchRadius, allocator);
            }
        }

        private static NativeList<int> FilterNodesScalar(
            GraphData graph,
            string targetType,
            string targetSpeaker = null,
            Vector2? searchCenter = null,
            float searchRadius = -1f,
            Allocator allocator = Allocator.TempJob)
        {
            var result = new NativeList<int>(allocator);
            if (graph == null) return result;

            float2 center = searchCenter.HasValue ? new float2(searchCenter.Value.x, searchCenter.Value.y) : float2.zero;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                bool typeMatch = node.Type == targetType;
                bool speakerMatch = targetSpeaker == null || node.Speaker == targetSpeaker;
                bool distMatch = searchRadius <= 0 ||
                    math.distance(new float2(node.Position?.X ?? 0, node.Position?.Y ?? 0), center) <= searchRadius;

                if (typeMatch && speakerMatch && distMatch)
                    result.Add(i);
            }
            return result;
        }
    }
}
