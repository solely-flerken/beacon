using _Game.Config;
using _Game.Core;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Gameplay
{
    public class BeaconVisualController : MonoBehaviour
    {
        [SerializeField] private BeaconConfig config;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private void OnEnable()
        {
            EventBus.OnBeaconHit += OnBeaconHit;
        }

        private void OnDisable()
        {
            EventBus.OnBeaconHit -= OnBeaconHit;
        }

        private void OnBeaconHit(float current, float max)
        {
            if (spriteRenderer == null || config == null) return;
            spriteRenderer.color = Color.Lerp(config.lowHpColor, config.fullHpColor, math.saturate(current / max));
        }
    }
}