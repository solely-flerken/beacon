using System;
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

        [Header("Type")]
        public EnemyType typeId;

        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<EnemyTag>(entity);
                AddComponent<TargetableTag>(entity);
                
                AddComponent(entity, new EnemySpeed { BaseValue = authoring.speed, Value = authoring.speed });
                AddComponent(entity, new EnemyHealth { BaseMax = authoring.maxHealth, Max = authoring.maxHealth, Current = authoring.maxHealth });
                AddComponent(entity, new ContactDamage { BaseValue = authoring.contactDamage, Value = authoring.contactDamage });
               
                AddComponent(entity, new EnemyReward { ExpValue = authoring.exp });
                AddComponent(entity, new EnemySpeedVariation { Value = authoring.speedVariation });

                AddComponent<EnemyTypeRef>(entity);
                
                // Individual enemy modifiers. Used for elites.
                AddBuffer<StatModifier>(entity);

                AddComponent<EnemyStatUpdateRequired>(entity);

                // Per-type enemy tag
                switch (authoring.typeId)
                {
                    case EnemyType.Basic:
                        AddComponent<BasicEnemyTag>(entity);
                        break;
                    case EnemyType.Fast:
                        AddComponent<FastEnemyTag>(entity);
                        break;
                    case EnemyType.Tank:
                        AddComponent<TankEnemyTag>(entity);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
