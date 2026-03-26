using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        [Header("Stats")] 
        public float speed = 4f;
        public float speedVariation = 0.5f;
        
        public float maxHealth = 10f;
        public float contactDamage = 1f;
        
        public float exp = 1f;

        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<EnemyTag>(entity);
                AddComponent<TargetableTag>(entity);
                
                AddComponent(entity, new EnemySpeed { Value = authoring.speed });
                AddComponent(entity, new EnemyHealth { Current = authoring.maxHealth, Max = authoring.maxHealth });
                AddComponent(entity, new ContactDamage { Value = authoring.contactDamage });
                AddComponent(entity, new EnemyReward { ExpValue = authoring.exp });
                AddComponent(entity, new EnemySpeedVariation { Value = authoring.speedVariation });
            }
        }
    }
}