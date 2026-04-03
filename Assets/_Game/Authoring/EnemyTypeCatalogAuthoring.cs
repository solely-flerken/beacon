using System.Collections.Generic;
using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    /// <summary>
    /// Place this on a single GameObject in the SubScene.
    /// Add one entry per enemy prefab
    /// The baker creates a live type entity per entry (queryable, holds StatModifier buffer).
    /// </summary>
    public class EnemyTypeCatalogAuthoring : MonoBehaviour
    {
        public List<GameObject> prefabs = new();

        private class Baker : Baker<EnemyTypeCatalogAuthoring>
        {
            public override void Bake(EnemyTypeCatalogAuthoring authoring)
            {
                Entity catalogEntity = GetEntity(TransformUsageFlags.None);
                DynamicBuffer<EnemyTypeCatalogEntry> buffer = AddBuffer<EnemyTypeCatalogEntry>(catalogEntity);

                foreach (GameObject prefab in authoring.prefabs)
                {
                    DependsOn(prefab);

                    EnemyType typeId = GetComponent<EnemyAuthoring>(prefab).typeId;

                    Entity typeEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: $"EnemyType.{typeId}");
                    AddComponent<EnemyTypeTag>(typeEntity);
                    AddBuffer<StatModifier>(typeEntity);

                    buffer.Add(new EnemyTypeCatalogEntry
                    {
                        TypeId = typeId,
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                        TypeEntity = typeEntity,
                    });
                }
            }
        }
    }
}
