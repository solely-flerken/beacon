using System.Collections.Generic;
using Unity.Mathematics;

namespace _Game.Shared
{
    public struct BeamVisualData
    {
        public float3 Origin;
        public float3 End;
        public float Width;
    }

    public static class BeamVisualShared
    {
        public static readonly List<BeamVisualData> Beams = new();
    }
}
