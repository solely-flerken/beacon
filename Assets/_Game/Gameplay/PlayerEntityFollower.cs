using _Game.Components;
using Unity.Entities;
using Unity.Transforms;

namespace _Game.Gameplay
{
    /// <summary>
    /// Follows the player-beacon entity (the single <see cref="PlayerTag"/> entity).
    /// Put this on the beacon's visual GameObject. Self-resolves, so no runtime wiring is needed.
    /// </summary>
    public class PlayerEntityFollower : EntityTransformFollower
    {
        private EntityQuery _query;

        protected override void OnReady()
        {
            _query = EntityManager.CreateEntityQuery(typeof(PlayerTag), typeof(LocalTransform));
        }

        protected override bool TryResolveTarget(out Entity entity)
        {
            if (_query.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            entity = _query.GetSingletonEntity();
            return true;
        }
    }
}