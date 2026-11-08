using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Smoothly follows the player-beacon entity. Attach to the main camera and set a non-zero
    /// smoothTime for an eased follow. Thin specialization of <see cref="PlayerEntityFollower"/>.
    /// Kept as a named component so the camera can grow camera-specific behavior later without touching the shared follow mechanics.
    /// </summary>
    public sealed class CameraFollow : PlayerEntityFollower
    {
        private CameraShake _cameraShake;

        protected override Vector3 DisplayOffset => _cameraShake ? _cameraShake.Offset : Vector3.zero;

        private void Awake()
        {
            _cameraShake = GetComponent<CameraShake>();
        }
    }
}
