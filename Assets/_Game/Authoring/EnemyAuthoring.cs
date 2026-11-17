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
        public Faction faction = Faction.Enemy;
        public FactionMask targetFactions = FactionMask.Player;
        public RoleMask role = RoleMask.Basic;
        
        [Header("Stats")] 
        public float speed = 4f;
        [Range(0f, 1f)] public float speedVariation = 0.15f;
        [Tooltip("How fast the movement direction eases toward the flow field direction (per second). Higher = snappier.")]
        [Min(0.1f)] public float steeringResponsiveness = 8f;

        public float maxHealth = 10f;
        public float armor = 0f;
        public float contactDamage = 1f;
        [Range(0, 100)] public int armorPenetration;
        [Range(0, 100)] public int critChance;
        [Min(100)] public int critDamage = 150;

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
                    Tags = Tags.Unit | authoring.role.ToTags() | Tags.Normal
                });

                // Side
                AddSharedComponent(entity, new UnitFaction { Value = authoring.faction });
                AddComponent(entity, new Hostility { Targets = authoring.targetFactions });

                // Basic enemy stats
                AddComponent(entity, new Speed { BaseValue = authoring.speed, Value = authoring.speed });
                AddComponent(entity, new EnemySpeedVariation { Value = authoring.speedVariation });
                AddComponent(entity, new Steering { Responsiveness = authoring.steeringResponsiveness });
                AddComponent(entity, new EnemyReward { BaseExpValue = authoring.exp, ExpValue = authoring.exp });
                AddComponent(entity, new ContactDamage { BaseValue = authoring.contactDamage, Value = authoring.contactDamage });
                AddComponent(entity, new Health { BaseMax = authoring.maxHealth, Max = authoring.maxHealth, Current = authoring.maxHealth });
                AddComponent(entity, new Armor { BaseValue = authoring.armor, Value = authoring.armor });
                
                // Attack stats
                AddComponent(entity, new AttackStats
                {
                    BaseArmorPenetration = authoring.armorPenetration,
                    ArmorPenetration = authoring.armorPenetration,
                    BaseCritChance = authoring.critChance,
                    CritChance = authoring.critChance,
                    BaseCritDamage = authoring.critDamage,
                    CritDamage = authoring.critDamage,
                });

                // Reference to the enemy type 
                AddComponent<EnemyTypeRef>(entity);
                
                // Damage
                AddComponent<EnemyIncomingDamage>(entity);
                AddBuffer<HitCooldown>(entity);
                AddComponent<Despawn>(entity);
                SetComponentEnabled<Despawn>(entity, false);
                AddComponent(entity, new HitFlashTime { Value = HitFlashTime.Never });

                // Individual enemy modifiers
                AddBuffer<StatModifier>(entity);
                AddComponent<Uninitialized>(entity);
                AddComponent<EnemyStatUpdateRequired>(entity);
                SetComponentEnabled<EnemyStatUpdateRequired>(entity, false);
            }
        }
    }
}
