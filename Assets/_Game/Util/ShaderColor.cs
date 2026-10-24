using Unity.Mathematics;
using UnityEngine;

namespace _Game.Util
{
    public static class ShaderColor
    {
        /// <summary>
        /// Converts a Color to the raw float4 a material property override expects.
        /// </summary>
        public static float4 From(Color color)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            return new float4(c.r, c.g, c.b, c.a);
        }
    }
}