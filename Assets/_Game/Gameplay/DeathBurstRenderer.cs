using System.Collections.Generic;
using _Game.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Game.Gameplay
{
    /// <summary>
    /// Draws a burst of neon fragments plus a short shockwave ring where an enemy dies, in the enemy's own color.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class DeathBurstRenderer : MonoBehaviour
    {
        private const int MaxFragments = 16;

        [SerializeField] 
        private Shader shader;

        [SerializeField] 
        private int capacity = 4096;

        [Header("Fragments")]
        
        [SerializeField, Range(1, MaxFragments)] 
        private int fragments = 7;
        
        [SerializeField] 
        private float lifetime = 0.45f;
        
        [SerializeField] 
        private float minSpeed = 2f;
        
        [SerializeField] 
        private float maxSpeed = 5.5f;
        
        [SerializeField] 
        private float fragmentLength = 0.9f;
        
        [SerializeField] 
        private float maxSpin = 720f;
        
        [SerializeField] 
        private float lineWidth = 0.045f;
        
        [SerializeField, Range(0f, 1f)]
        private float coreWidth = 0.35f;
        
        [SerializeField]
        private float glowRadius = 0.07f;
        
        [SerializeField] 
        private float intensity = 1.5f;

        [Header("Ring")]
        
        [SerializeField]
        private bool ring = true;
        
        [SerializeField] 
        private float ringLifetime = 0.16f;
        
        [SerializeField] 
        private float ringGrowth = 2.2f;
        
        [SerializeField]
        private float ringIntensity = 1.2f;

        [SerializeField] 
        private float z = -0.1f;

        private static readonly int BurstsId = Shader.PropertyToID("_Bursts");
        private static readonly int FirstBurstId = Shader.PropertyToID("_FirstBurst");
        private static readonly int CapacityId = Shader.PropertyToID("_Capacity");
        private static readonly int InstancesPerBurstId = Shader.PropertyToID("_InstancesPerBurst");
        private static readonly int LifetimeId = Shader.PropertyToID("_Lifetime");
        private static readonly int MinSpeedId = Shader.PropertyToID("_MinSpeed");
        private static readonly int MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
        private static readonly int FragmentLengthId = Shader.PropertyToID("_FragmentLength");
        private static readonly int MaxSpinId = Shader.PropertyToID("_MaxSpin");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int CoreWidthId = Shader.PropertyToID("_CoreWidth");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RingLifetimeId = Shader.PropertyToID("_RingLifetime");
        private static readonly int RingGrowthId = Shader.PropertyToID("_RingGrowth");
        private static readonly int RingIntensityId = Shader.PropertyToID("_RingIntensity");
        private static readonly int ZId = Shader.PropertyToID("_Z");

        private GraphicsBuffer _buffer;
        private Material _material;
        private RenderParams _renderParams;

        private int _instancesPerBurst;

        private float _visibleTime;

        private readonly Queue<(float time, int count)> _uploads = new();
        private int _live;
        private int _head;
        private float _clock = float.NegativeInfinity;

        private void OnEnable()
        {
            if (shader == null)
            {
                Debug.LogError("[DeathBurstRenderer] No shader assigned.", this);
                enabled = false;
                return;
            }

            capacity = Mathf.Max(1, capacity);

            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, UnsafeUtility.SizeOf<DeathBurstData>());
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetBuffer(BurstsId, _buffer);
            _renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };

            _uploads.Clear();
            _live = 0;
            _head = 0;
            _clock = float.NegativeInfinity;

            ApplySettings();

            EventBus.OnDeathBurstsBatch += OnDeathBurstsBatch;
        }

        private void OnDisable()
        {
            EventBus.OnDeathBurstsBatch -= OnDeathBurstsBatch;

            _buffer?.Release();
            _buffer = null;
            if (_material != null) Destroy(_material);
        }

        private void OnValidate()
        {
            if (_material != null) ApplySettings();
        }

        private void ApplySettings()
        {
            _instancesPerBurst = 1 + Mathf.Clamp(fragments, 1, MaxFragments);

            float fragmentLifetime = Mathf.Max(0.01f, lifetime);
            float ringTime = ring ? Mathf.Max(0.01f, ringLifetime) : 0f;
            _visibleTime = Mathf.Max(fragmentLifetime, ringTime);

            _material.SetFloat(CapacityId, capacity);
            _material.SetFloat(InstancesPerBurstId, _instancesPerBurst);
            _material.SetFloat(LifetimeId, fragmentLifetime);
            _material.SetFloat(MinSpeedId, minSpeed);
            _material.SetFloat(MaxSpeedId, Mathf.Max(minSpeed, maxSpeed));
            _material.SetFloat(FragmentLengthId, fragmentLength);
            _material.SetFloat(MaxSpinId, maxSpin * Mathf.Deg2Rad);
            _material.SetFloat(LineWidthId, lineWidth);
            _material.SetFloat(CoreWidthId, coreWidth);
            _material.SetFloat(GlowRadiusId, Mathf.Max(0.001f, glowRadius));
            _material.SetFloat(IntensityId, intensity);
            _material.SetFloat(RingLifetimeId, ringTime);
            _material.SetFloat(RingGrowthId, ringGrowth);
            _material.SetFloat(RingIntensityId, ringIntensity);
            _material.SetFloat(ZId, z);
        }

        /// <summary>
        /// Copies this frame's bursts into the ring buffer, overwriting the oldest.
        /// </summary>
        private void OnDeathBurstsBatch(NativeArray<DeathBurstData> batch, int count)
        {
            float now = Tick();

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
            _uploads.Enqueue((now, count));
            _live += count;
        }

        private void LateUpdate()
        {
            Tick();

            int live = Mathf.Min(_live, capacity);
            if (live == 0) return;

            _material.SetFloat(FirstBurstId, (_head - live + capacity) % capacity);
            Graphics.RenderPrimitives(_renderParams, MeshTopology.Triangles, 6, live * _instancesPerBurst);
        }

        private float Tick()
        {
            float now = WorldTime();

            if (now < _clock)
            {
                _uploads.Clear();
                _live = 0;
            }

            _clock = now;

            while (_uploads.Count > 0 && now - _uploads.Peek().time >= _visibleTime)
            {
                _live -= _uploads.Dequeue().count;
            }

            return now;
        }

        private static float WorldTime()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return 0f;
            return (float)world.Time.ElapsedTime;
        }
    }
}
