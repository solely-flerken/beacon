using Unity.Entities;
using UnityEngine;

namespace _Game.Systems
{
    /// <summary>
    /// Publishes ECS state to shaders as global shader values, once per frame:
    /// - _GameTime: the world's elapsed time, the clock for timestamps written by systems (HitFlashTime in Neon.shader, HitSparkData.StartTime in HitSpark.shader).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ShaderGlobalsSystem : ISystem
    {
        private static readonly int GameTimeId = Shader.PropertyToID("_GameTime");

        public void OnUpdate(ref SystemState state)
        {
            Shader.SetGlobalFloat(GameTimeId, (float)SystemAPI.Time.ElapsedTime);
        }
    }
}
