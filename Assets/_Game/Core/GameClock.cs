using Unity.Entities;

namespace _Game.Core
{
    public static class GameClock
    {
        /// <summary>
        /// Seconds since the world started; 0 while no world exists.
        /// </summary>
        public static float Now
        {
            get
            {
                World world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) return 0f;
                return (float)world.Time.ElapsedTime;
            }
        }
    }
}
