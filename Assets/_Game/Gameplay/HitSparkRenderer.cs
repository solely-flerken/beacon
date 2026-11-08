using System;
using System.Collections.Generic;
using _Game.Components;
using _Game.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Game.Gameplay
{
    /// <summary>
    /// TODO
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HitSparkRenderer : MonoBehaviour
    {
        private enum Spray
        {
            /// <summary>
            /// Back toward the source, off the side that was hit.
            /// </summary>
            Recoil,

            /// <summary>
            /// Away from the source, out the far side.
            /// </summary>
            Burst,
        }

        [Serializable]
        private struct WeaponStyle
        {
            public WeaponType weapon;

            [ColorUsage(false, true)]
            public Color color;

            [Range(0, MaxSparksPerHit)]
            public int sparks;

            public Spray spray;

            [Range(0f, 180f)] 
            public float spread;

            [Min(0f)] 
            public float speedMult;
        }

        private const int MaxSparksPerHit = 8;
        private const int MaxStyles = 8;

        [SerializeField] private Shader shader;

        // Hits kept in the ring buffer. When more hits are alive, the oldest vanish early.
        [SerializeField] private int capacity = 8192;

        [Header("Spark")]
        [SerializeField] private float lifetime = 0.22f;
        [SerializeField] private float minSpeed = 3f;
        [SerializeField] private float maxSpeed = 9f;
        [SerializeField] private float stretch = 0.035f; // Streak length at launch speed, in seconds of travel.
        [SerializeField] private float width = 0.03f;
        [SerializeField] private float glowRadius = 0.06f;
        [SerializeField] private float z = -0.1f;

        [Header("Per Weapon")] 
        [SerializeField]
        private WeaponStyle[] styles =
        {
            new()
            {
                weapon = WeaponType.Basic, color = new Color(0f, 1.6f, 1.6f), sparks = 5, spray = Spray.Recoil,
                spread = 55f, speedMult = 1f
            },
            new()
            {
                weapon = WeaponType.Scatter, color = new Color(0f, 1.6f, 1.6f), sparks = 3, spray = Spray.Recoil,
                spread = 55f, speedMult = 1f
            },
            new()
            {
                weapon = WeaponType.Beam, color = new Color(0.45f, 0.7f, 2.5f), sparks = 3, spray = Spray.Recoil,
                spread = 80f, speedMult = 0.7f
            },
            new()
            {
                weapon = WeaponType.Mortar, color = new Color(2f, 1.2f, 0.2f), sparks = 4, spray = Spray.Burst,
                spread = 35f, speedMult = 1.3f
            },
        };

        private static readonly int HitsId = Shader.PropertyToID("_Hits");
        private static readonly int StyleColorsId = Shader.PropertyToID("_StyleColors");
        private static readonly int StyleMotionId = Shader.PropertyToID("_StyleMotion");
        private static readonly int FirstHitId = Shader.PropertyToID("_FirstHit");
        private static readonly int CapacityId = Shader.PropertyToID("_Capacity");
        private static readonly int LifetimeId = Shader.PropertyToID("_Lifetime");
        private static readonly int SparksPerHitId = Shader.PropertyToID("_SparksPerHit");
        private static readonly int MinSpeedId = Shader.PropertyToID("_MinSpeed");
        private static readonly int MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
        private static readonly int StretchId = Shader.PropertyToID("_Stretch");
        private static readonly int WidthId = Shader.PropertyToID("_Width");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int ZId = Shader.PropertyToID("_Z");

        private GraphicsBuffer _buffer;
        private Material _material;
        private RenderParams _renderParams;

        private readonly Vector4[] _styleColors = new Vector4[MaxStyles];
        private readonly Vector4[] _styleMotion = new Vector4[MaxStyles];

        // Instances drawn per hit: the most sparks any style has
        private int _sparksPerHit;

        // Hits uploaded per frame, oldest first, to know how many are still alive
        private readonly Queue<(float time, int count)> _uploads = new();
        private int _live;
        private int _head;
        private float _clock = float.NegativeInfinity;

        private void OnEnable()
        {
            if (shader == null)
            {
                Debug.LogError("[HitSparkRenderer] No shader assigned.", this);
                enabled = false;
                return;
            }

            capacity = Mathf.Max(1, capacity);

            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, UnsafeUtility.SizeOf<HitSparkData>());
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetBuffer(HitsId, _buffer);
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

            EventBus.OnHitSparksBatch += OnHitSparksBatch;
        }

        private void OnDisable()
        {
            EventBus.OnHitSparksBatch -= OnHitSparksBatch;

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
            Array.Clear(_styleColors, 0, MaxStyles);
            Array.Clear(_styleMotion, 0, MaxStyles);

            _sparksPerHit = 1;
            foreach (WeaponStyle style in styles)
            {
                int index = (int)style.weapon;
                if (index < 0 || index >= MaxStyles) continue;

                int sparks = Mathf.Clamp(style.sparks, 0, MaxSparksPerHit);
                _styleColors[index] = new Vector4(style.color.r, style.color.g, style.color.b, sparks);
                _styleMotion[index] = new Vector4(style.spray == Spray.Burst ? 1f : -1f, style.spread * Mathf.Deg2Rad, style.speedMult, 0f);
                _sparksPerHit = Mathf.Max(_sparksPerHit, sparks);
            }

            _material.SetVectorArray(StyleColorsId, _styleColors);
            _material.SetVectorArray(StyleMotionId, _styleMotion);
            _material.SetFloat(CapacityId, capacity);
            _material.SetFloat(SparksPerHitId, _sparksPerHit);
            _material.SetFloat(LifetimeId, lifetime);
            _material.SetFloat(MinSpeedId, minSpeed);
            _material.SetFloat(MaxSpeedId, Mathf.Max(minSpeed, maxSpeed));
            _material.SetFloat(StretchId, stretch);
            _material.SetFloat(WidthId, width);
            _material.SetFloat(GlowRadiusId, glowRadius);
            _material.SetFloat(ZId, z);
        }

        /// <summary>
        /// Copies this frame's hits into the ring buffer, overwriting the oldest.
        /// </summary>
        private void OnHitSparksBatch(NativeArray<HitSparkData> batch, int count)
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

            _material.SetFloat(FirstHitId, (_head - live + capacity) % capacity);
            Graphics.RenderPrimitives(_renderParams, MeshTopology.Triangles, 6, live * _sparksPerHit);
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

            while (_uploads.Count > 0 && now - _uploads.Peek().time >= lifetime)
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
