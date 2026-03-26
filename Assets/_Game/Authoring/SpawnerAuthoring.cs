using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public GameObject projectilePrefab;
        public GameObject flarePrefab;

        private class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                DependsOn(authoring.enemyPrefab);
                DependsOn(authoring.projectilePrefab);
                DependsOn(authoring.flarePrefab);

                Entity entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new PrefabRefs
                {
                    Enemy = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                    Projectile = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                    Flare = GetEntity(authoring.flarePrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }

    public struct PrefabRefs : IComponentData
    {
        public Entity Enemy;
        public Entity Projectile;
        public Entity Flare;
    }
}