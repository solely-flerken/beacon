using System.Collections.Generic;
using _Game.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
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

                    EnemyAuthoring auth = GetComponent<EnemyAuthoring>(prefab);

                    Entity typeEntity = CreateAdditionalEntity(TransformUsageFlags.None, entityName: $"EnemyType.{auth.type}");
                    AddComponent<EnemyTypeTag>(typeEntity);
                    
                    BlobAssetReference<EnemyTypeBlob> blobRef = BuildBlob(auth);
                    AddBlobAsset(ref blobRef, out _);
                    AddComponent(typeEntity, new EnemyTypeBlobRef { Value = blobRef });

                    buffer.Add(new EnemyTypeCatalogEntry
                    {
                        TypeId = auth.type,
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                        TypeEntity = typeEntity,
                    });
                }
            }

            private static BlobAssetReference<EnemyTypeBlob> BuildBlob(EnemyAuthoring auth)
            {
                using BlobBuilder builder = new(Allocator.Temp);
                ref EnemyTypeBlob root = ref builder.ConstructRoot<EnemyTypeBlob>();
                BlobBuilderArray<TieredStatModifier> blobArray = builder.Allocate(ref root.ConfigModifiers, auth.tierModifiers.Count);

                for (int i = 0; i < auth.tierModifiers.Count; i++)
                {
                    blobArray[i] = new TieredStatModifier
                    {
                        Tier = auth.tierModifiers[i].tier,
                        Modifier = new StatModifier
                        {
                            Stat = auth.tierModifiers[i].stat,
                            Type = auth.tierModifiers[i].operation,
                            Value = auth.tierModifiers[i].value
                        }
                    };
                }

                return builder.CreateBlobAssetReference<EnemyTypeBlob>(Allocator.Persistent);
            }
        }
    }
}
