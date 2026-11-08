using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Drives this GameObject's transform from a linked ECS entity each frame, bridging the
    /// pure-ECS simulation to a managed visual. The entity stays authoritative for position;
    /// the view simply follows.
    ///
    /// Subclasses decide which entity to follow by implementing <see cref="TryResolveTarget"/>.
    /// </summary>
    public abstract class EntityTransformFollower : MonoBehaviour
    {
        [Tooltip("Seconds to ease toward the target. 0 = snap (no lag).")]
        [SerializeField] private float smoothTime;

        [Tooltip("World-space offset added to the followed entity's position.")]
        [SerializeField] private Vector3 offset;

        [Tooltip("Keep this GameObject's own Z. If false, takes the entity's Z.")]
        [SerializeField] private bool keepOwnZ = true;

        protected EntityManager EntityManager { get; private set; }

        private Vector3 _velocity;
        private bool _ready;

        private Vector3 _followPosition;

        /// <summary>
        /// Resolve the entity to follow this frame. Return false to hold position.
        /// </summary>
        protected abstract bool TryResolveTarget(out Entity entity);

        /// <summary>
        /// Hook for subclasses to build queries once the EntityManager is available.
        /// </summary>
        protected virtual void OnReady() { }

        /// <summary>
        /// Visual-only offset added on top of the followed position every frame.
        /// </summary>
        protected virtual Vector3 DisplayOffset => Vector3.zero;

        private void EnsureReady()
        {
            if (_ready) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (!world.IsCreated) return;

            EntityManager = world.EntityManager;
            _followPosition = transform.position;
            _ready = true;
            OnReady();
        }

        private void LateUpdate()
        {
            EnsureReady();
            if (!_ready) return;

            Follow();
            transform.position = _followPosition + DisplayOffset;
        }

        private void Follow()
        {
            if (!TryResolveTarget(out Entity entity)) return;
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<LocalTransform>(entity)) return;

            float3 entityPos = EntityManager.GetComponentData<LocalTransform>(entity).Position;

            Vector3 target = new Vector3(entityPos.x, entityPos.y, entityPos.z) + offset;
            if (keepOwnZ) target.z = _followPosition.z;

            _followPosition = smoothTime > 0f
                ? Vector3.SmoothDamp(_followPosition, target, ref _velocity, smoothTime)
                : target;
        }
    }
}
