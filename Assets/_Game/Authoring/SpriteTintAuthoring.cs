using _Game.Util;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace _Game.Authoring
{
    /// <summary>
    /// Per-entity tint for Entities Graphics quads. Replaces SpriteRenderer.color.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class SpriteTintAuthoring : MonoBehaviour
    {
        public Color color = Color.white;

        private class Baker : Baker<SpriteTintAuthoring>
        {
            public override void Bake(SpriteTintAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = ShaderColor.From(authoring.color) });
            }
        }
    }
}