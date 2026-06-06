using Unity.Entities;

namespace _Game.Gameplay.Abilities
{
    public struct AbilityContext
    {
        public EntityManager EntityManager;
        public EntityQuery BeaconQuery;
        public EntityQuery PrefabQuery;
    }
}