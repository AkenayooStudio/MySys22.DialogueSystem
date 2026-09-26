#if UNITY_BURST
using Unity.Burst;
#endif
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MySys22.DialogueEngine.Core.SIMD
{
#if UNITY_BURST
    [BurstCompile]
#endif
    public struct NodeSearchSimdJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> NodeTypes;
        [ReadOnly] public NativeArray<int> SpeakerHashes;
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public int TargetType;
        [ReadOnly] public int TargetSpeakerHash;
        [ReadOnly] public float2 SearchCenter;
        [ReadOnly] public float SearchRadius;
        [WriteOnly] public NativeArray<int> MatchFlags;

        public void Execute(int index)
        {
            bool typeMatch = NodeTypes[index] == TargetType;
            bool speakerMatch = TargetSpeakerHash == 0 || SpeakerHashes[index] == TargetSpeakerHash;
            bool distanceMatch = SearchRadius <= 0 ||
                                 math.distance(Positions[index], SearchCenter) <= SearchRadius;

            MatchFlags[index] = (typeMatch && speakerMatch && distanceMatch) ? 1 : 0;
        }
    }
}
