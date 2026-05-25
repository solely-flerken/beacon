using System;
using System.Collections.Generic;
using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class GlobalModAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct GlobalModEntry
        {
            public StatType stat;
            public ModifierType operation;
            public float value;

            [Header("Who does this apply to?")] public TargetMask target;
            public FactionMask faction;
            public RoleMask role;
            public TierMask tier;
        }

        public List<GlobalModEntry> globalModifiers = new();

        private class Baker : Baker<GlobalModAuthoring>
        {
            public override void Bake(GlobalModAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent<GlobalModSingleton>(entity);
                DynamicBuffer<GlobalStatModifier> buffer = AddBuffer<GlobalStatModifier>(entity);

                foreach (GlobalModEntry modifier in authoring.globalModifiers)
                {
                    buffer.Add(new GlobalStatModifier
                    {
                        Stat = modifier.stat,
                        Type = modifier.operation,
                        Value = modifier.value,

                        Mask = new Classification
                        {
                            Target = modifier.target,
                            Faction = modifier.faction,
                            Role = modifier.role,
                            Tier = modifier.tier
                        }
                    });
                }
                
                AddBuffer<WaveStatModifier>(entity);
            }
        }
    }
}