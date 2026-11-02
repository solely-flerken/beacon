using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// One beam as the Beam shader reads it. Uploaded as-is, so the layout must match the shader's <c>Beam</c> struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BeamVisualData
    {
        public float2 Origin;
        public float2 End;
        public float Width;

        /// <summary>
        /// Seconds since the last damage pulse; the beam flares when it is near 0.
        /// </summary>
        public float SincePulse;

        /// <summary>
        /// Varies the noise per beam so beams don't flicker in sync.
        /// </summary>
        public uint Seed;
    }

    public static class BeamVisualShared
    {
        public static readonly List<BeamVisualData> Beams = new();
    }
}
