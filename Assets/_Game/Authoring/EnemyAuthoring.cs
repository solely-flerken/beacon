using System;
using System.Collections.Generic;
using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        [Header("Entity Classification")]
        public FactionMask faction = FactionMask.Enemy;
        public RoleMask role = RoleMask.Basic;
        
        [Header("Stats")] 
        public float speed = 4f;
        public float speedVariation = 0.5f;
        
        public float maxHealth = 10f;
        public float contactDamage = 1f;
        
        public float exp = 1f;
        
        [Header("Tier Overrides")]
        public List<TieredModifierEntry> tierModifiers = new();
        
        // [Header("Type")]
        // public EnemyType typeId;
    
        [Serializable]
        public struct TieredModifierEntry {
            public EnemyTier tier;
            public StatType stat;
            public ModifierType operation;
            public float value;
        }
        
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<EnemyTag>(entity);
                AddComponent<TargetableTag>(entity);

                // Classification
                AddComponent(entity, new Classification
                {
                    Target = TargetMask.Character,
                    Faction = authoring.faction,
                    Role = authoring.role,
                    Tier = TierMask.Normal
                });
                
                // Basic enemy stats
                AddComponent(entity, new EnemySpeed { BaseValue = authoring.speed, Value = authoring.speed });
                AddComponent(entity, new EnemySpeedVariation { Value = authoring.speedVariation });
                AddComponent(entity, new EnemyReward { ExpValue = authoring.exp });
                AddComponent(entity, new ContactDamage { BaseValue = authoring.contactDamage, Value = authoring.contactDamage });
                AddComponent(entity, new Health { BaseMax = authoring.maxHealth, Max = authoring.maxHealth, Current = authoring.maxHealth, IsAlive = true});
                
                // Reference to the enemy type 
                AddComponent<EnemyTypeRef>(entity);
                
                // Damage
                AddBuffer<DamageBufferElement>(entity);
                AddComponent<IsDead>(entity);
                SetComponentEnabled<IsDead>(entity, false);
                
                // Individual enemy modifiers
                AddBuffer<StatModifier>(entity);
                AddComponent<Uninitialized>(entity);
                AddComponent<EnemyStatUpdateRequired>(entity);
                SetComponentEnabled<EnemyStatUpdateRequired>(entity, false);

                // Per-type enemy tag
                switch (authoring.role)
                {
                    case RoleMask.Basic:
                        AddComponent<BasicEnemyTag>(entity);
                        break;
                    case RoleMask.Fast:
                        AddComponent<FastEnemyTag>(entity);
                        break;
                    case RoleMask.Tank:
                        AddComponent<TankEnemyTag>(entity);
                        break;
                    case RoleMask.Range:
                        AddComponent<RangeEnemyTag>(entity);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
