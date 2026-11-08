using _Game.Config;
using _Game.Core;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Gameplay
{
    public class BeaconVisualController : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BeaconConfig config;
        [SerializeField] private MeshRenderer meshRenderer;

        private MaterialPropertyBlock _properties;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            SetHealthColor(1f);
        }

        private void OnEnable()
        {
            EventBus.OnBeaconHealthChanged += OnBeaconHealthChanged;
        }

        private void OnDisable()
        {
            EventBus.OnBeaconHealthChanged -= OnBeaconHealthChanged;
        }

        private void OnBeaconHealthChanged(float current, float max)
        {
            SetHealthColor(math.saturate(current / max));
        }

        private void SetHealthColor(float healthFraction)
        {
            if (meshRenderer == null || config == null) return;
            _properties.SetColor(BaseColor, Color.Lerp(config.lowHpColor, config.fullHpColor, healthFraction));
            meshRenderer.SetPropertyBlock(_properties);
        }
    }
}
