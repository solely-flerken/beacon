using _Game.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Game.Gameplay
{
    /// <summary>
    /// Draws all chain lightning arcs in one procedural draw call.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ChainLightningRenderer : MonoBehaviour
    {
        [SerializeField] 
        private Shader shader;

        [SerializeField]
        private int capacity = 8192;

        [Header("Bolt")]
        
        [SerializeField] 
        private float lifetime = 0.18f;

        [SerializeField]
        private float coreWidth = 0.04f;

        [SerializeField]
        private float glowRadius = 0.2f;

        [SerializeField]
        private float jaggedness = 0.4f;

        [SerializeField]
        private float noiseFrequency = 1.5f;

        [SerializeField]
        private float flickerRate = 24f;

        [SerializeField, ColorUsage(false, true)]
        private Color color = new(0.45f, 0.7f, 2.5f);

        [Header("Branch")] 
        
        [SerializeField, Range(0f, 1f)]
        private float branchStrength = 0.6f;

        [SerializeField]
        private float branchOffset = 0.6f;

        [Header("Impact")]
        
        [SerializeField]
        private float flashRadius = 0.3f;

        [SerializeField]
        private float z = -0.1f;

        private static readonly int ArcsId = Shader.PropertyToID("_Arcs");
        private static readonly int NowId = Shader.PropertyToID("_Now");
        private static readonly int LifetimeId = Shader.PropertyToID("_Lifetime");
        private static readonly int CoreWidthId = Shader.PropertyToID("_CoreWidth");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int JaggednessId = Shader.PropertyToID("_Jaggedness");
        private static readonly int NoiseFrequencyId = Shader.PropertyToID("_NoiseFrequency");
        private static readonly int FlickerRateId = Shader.PropertyToID("_FlickerRate");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BranchStrengthId = Shader.PropertyToID("_BranchStrength");
        private static readonly int BranchOffsetId = Shader.PropertyToID("_BranchOffset");
        private static readonly int FlashRadiusId = Shader.PropertyToID("_FlashRadius");
        private static readonly int ZId = Shader.PropertyToID("_Z");

        private GraphicsBuffer _buffer;
        private Material _material;

        private int _head;
        private int _used;
        private float _lastArcTime = float.NegativeInfinity;

        private void OnEnable()
        {
            if (shader == null)
            {
                Debug.LogError("[ChainLightningRenderer] No shader assigned.", this);
                enabled = false;
                return;
            }

            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, UnsafeUtility.SizeOf<ChainArcVisualData>());
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetBuffer(ArcsId, _buffer);
            _head = 0;
            _used = 0;

            EventBus.OnChainArcsBatch += OnChainArcsBatch;
        }

        private void OnDisable()
        {
            EventBus.OnChainArcsBatch -= OnChainArcsBatch;

            _buffer?.Release();
            _buffer = null;
            if (_material != null) Destroy(_material);
        }

        private void OnChainArcsBatch(NativeArray<ChainArcVisualData> batch, int count)
        {
            int start = 0;
            if (count > capacity)
            {
                start = count - capacity;
                count = capacity;
            }

            int first = Mathf.Min(count, capacity - _head);
            _buffer.SetData(batch, start, _head, first);
            if (count > first)
            {
                _buffer.SetData(batch, start + first, 0, count - first);
            }

            _head = (_head + count) % capacity;
            _used = Mathf.Min(_used + count, capacity);
            _lastArcTime = WorldTime();
        }

        private void LateUpdate()
        {
            float now = WorldTime();

            // Every arc has expired: skip the draw
            if (_used == 0 || now - _lastArcTime >= lifetime) return;

            _material.SetFloat(NowId, now);
            _material.SetFloat(LifetimeId, lifetime);
            _material.SetFloat(CoreWidthId, coreWidth);
            _material.SetFloat(GlowRadiusId, glowRadius);
            _material.SetFloat(JaggednessId, jaggedness);
            _material.SetFloat(NoiseFrequencyId, noiseFrequency);
            _material.SetFloat(FlickerRateId, flickerRate);
            _material.SetColor(ColorId, color);
            _material.SetFloat(BranchStrengthId, branchStrength);
            _material.SetFloat(BranchOffsetId, branchOffset);
            _material.SetFloat(FlashRadiusId, flashRadius);
            _material.SetFloat(ZId, z);

            RenderParams rp = new(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };

            Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, _used);
        }

        private static float WorldTime()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return 0f;
            return (float)world.Time.ElapsedTime;
        }
    }
}
