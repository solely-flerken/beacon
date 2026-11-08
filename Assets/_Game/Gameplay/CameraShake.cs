using _Game.Core;
using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Trauma-based screen shake.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("Offset in world units at full trauma.")]
        [SerializeField] private float maxOffset = 0.35f;

        [Tooltip("How fast the shake moves.")]
        [SerializeField] private float frequency = 25f;

        [Tooltip("Trauma lost per second. 1 / decay = seconds from full trauma to still.")] 
        [SerializeField] private float traumaDecay = 2.5f;

        [Header("Triggers")] 
        [Tooltip("Trauma added per beacon hit, before scaling with damage.")]
        [SerializeField] private float beaconHitBaseTrauma = 0.2f;

        [Tooltip("Extra trauma per 1% of max HP lost in the hit.")]
        [SerializeField] private float beaconHitTraumaPerHpPercent = 0.03f;

        private float _trauma;
        private float _seed;

        /// <summary>
        /// The current shake offset. Zero when disabled or still.
        /// </summary>
        public Vector3 Offset { get; private set; }

        public void AddTrauma(float amount01)
        {
            if (!isActiveAndEnabled) return;
            _trauma = Mathf.Clamp01(_trauma + amount01);
        }

        private void Awake()
        {
            _seed = Random.value * 1000f;
        }

        private void OnEnable()
        {
            EventBus.OnBeaconHit += OnBeaconHit;
        }

        private void OnDisable()
        {
            EventBus.OnBeaconHit -= OnBeaconHit;
            
            _trauma = 0f;
            Offset = Vector3.zero;
        }
        
        private void OnBeaconHit(float damage, float current, float max)
        {
            float hpPercent = Mathf.InverseLerp(0f, max, damage) * 100f;
            AddTrauma(beaconHitBaseTrauma + hpPercent * beaconHitTraumaPerHpPercent);
        }
        
        private void Update()
        {
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.unscaledDeltaTime);

            if (_trauma <= 0f)
            {
                Offset = Vector3.zero;
                return;
            }

            float strength = _trauma * _trauma * maxOffset;
            float t = Time.unscaledTime * frequency;
            float x = Mathf.PerlinNoise(_seed, t) * 2f - 1f;
            float y = Mathf.PerlinNoise(_seed + 100f, t) * 2f - 1f;
            Offset = new Vector3(x, y, 0f) * strength;
        }
    }
}
