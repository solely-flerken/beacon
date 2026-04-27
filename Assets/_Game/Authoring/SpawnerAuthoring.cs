using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject beaconPrefab;
        public GameObject projectilePrefab;
        public GameObject flarePrefab;
        public GameObject expOrbPrefab;

        private class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                DependsOn(authoring.beaconPrefab);
                DependsOn(authoring.projectilePrefab);
                DependsOn(authoring.flarePrefab);
                DependsOn(authoring.expOrbPrefab);

                Entity entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new PrefabRefs
                {
                    Beacon = GetEntity(authoring.beaconPrefab, TransformUsageFlags.Dynamic),
                    Projectile = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                    Flare = GetEntity(authoring.flarePrefab, TransformUsageFlags.Dynamic),
                    ExpOrb = GetEntity(authoring.expOrbPrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }

    public struct PrefabRefs : IComponentData
    {
        public Entity Beacon;
        public Entity Projectile;
        public Entity Flare;
        public Entity ExpOrb;
    }
}