using System.Collections.Generic;
using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class WeaponTypeCatalogAuthoring : MonoBehaviour
    {
        public List<GameObject> prefabs = new();

        private class Baker : Baker<WeaponTypeCatalogAuthoring>
        {
            public override void Bake(WeaponTypeCatalogAuthoring authoring)
            {
                Entity catalogEntity = GetEntity(TransformUsageFlags.None);
                DynamicBuffer<WeaponCatalogEntry> buffer = AddBuffer<WeaponCatalogEntry>(catalogEntity);

                foreach (GameObject prefab in authoring.prefabs)
                {
                    DependsOn(prefab);

                    WeaponBaseAuthoring auth = GetComponent<WeaponBaseAuthoring>(prefab);

                    buffer.Add(new WeaponCatalogEntry
                    {
                        Type = auth.weaponType,
                        Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    });
                }
            }
        }
    }
}
