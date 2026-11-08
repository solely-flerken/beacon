using System.Collections.Generic;
using _Game.Shared;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Game.Gameplay
{
    /// <summary>
    /// Draws all beams in one procedural draw call.
    /// </summary>
    public class BeamVisualController : MonoBehaviour
    {
        [SerializeField]
        private Shader shader;

        [Header("Beam")]
        
        [SerializeField] 
        private float glowRadius = 0.25f;
        
        [SerializeField, ColorUsage(false, true)] 
        private Color color = new(0.45f, 0.7f, 2.5f);
        
        [SerializeField, Range(0f, 1f)] 
        private float tipFade = 0.15f;

        [Header("Flow")]
        
        [SerializeField]
        private float flowSpeed = 14f;
        [SerializeField]
        private float flowFrequency = 0.6f;
        [SerializeField, Range(0f, 1f)]
        private float flowAmount = 0.35f;

        [Header("Filaments")]
        
        [SerializeField, Range(0f, 1f)]
        private float filamentStrength = 0.6f;
        
        [SerializeField]
        private float filamentWidth = 0.015f;
        
        [SerializeField]
        private float jaggedness = 0.12f;

        [SerializeField]
        private float noiseFrequency = 2f;

        [SerializeField]
        private float flickerRate = 24f;

        [SerializeField]
        private float pulseDecay = 0.04f;

        [SerializeField]
        private float pulseBrightness = 1f;

        [SerializeField]
        private float pulseWidth = 0.6f;

        [Header("Muzzle")]
        
        [SerializeField]
        private float muzzleRadius = 0.25f;

        [SerializeField] 
        private float z = -0.1f;

        private static readonly int BeamsId = Shader.PropertyToID("_Beams");
        private static readonly int GlowRadiusId = Shader.PropertyToID("_GlowRadius");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int FlowFrequencyId = Shader.PropertyToID("_FlowFrequency");
        private static readonly int FlowAmountId = Shader.PropertyToID("_FlowAmount");
        private static readonly int FilamentStrengthId = Shader.PropertyToID("_FilamentStrength");
        private static readonly int FilamentWidthId = Shader.PropertyToID("_FilamentWidth");
        private static readonly int JaggednessId = Shader.PropertyToID("_Jaggedness");
        private static readonly int NoiseFrequencyId = Shader.PropertyToID("_NoiseFrequency");
        private static readonly int FlickerRateId = Shader.PropertyToID("_FlickerRate");
        private static readonly int PulseDecayId = Shader.PropertyToID("_PulseDecay");
        private static readonly int PulseBrightnessId = Shader.PropertyToID("_PulseBrightness");
        private static readonly int PulseWidthId = Shader.PropertyToID("_PulseWidth");
        private static readonly int MuzzleRadiusId = Shader.PropertyToID("_MuzzleRadius");
        private static readonly int TipFadeId = Shader.PropertyToID("_TipFade");
        private static readonly int ZId = Shader.PropertyToID("_Z");

        private GraphicsBuffer _buffer;
        private Material _material;

        private void OnEnable()
        {
            BeamVisualShared.Beams.Clear();

            if (shader == null)
            {
                Debug.LogError("[BeamVisualController] No shader assigned.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            EnsureCapacity(16);
        }

        private void OnDisable()
        {
            _buffer?.Release();
            _buffer = null;
            if (_material != null) Destroy(_material);
        }

        private void LateUpdate()
        {
            List<BeamVisualData> beams = BeamVisualShared.Beams;
            if (beams.Count == 0) return;

            EnsureCapacity(beams.Count);
            
            _buffer.SetData(beams);

            _material.SetFloat(GlowRadiusId, glowRadius);
            _material.SetColor(ColorId, color);
            _material.SetFloat(FlowSpeedId, flowSpeed);
            _material.SetFloat(FlowFrequencyId, flowFrequency);
            _material.SetFloat(FlowAmountId, flowAmount);
            _material.SetFloat(FilamentStrengthId, filamentStrength);
            _material.SetFloat(FilamentWidthId, filamentWidth);
            _material.SetFloat(JaggednessId, jaggedness);
            _material.SetFloat(NoiseFrequencyId, noiseFrequency);
            _material.SetFloat(FlickerRateId, flickerRate);
            _material.SetFloat(PulseDecayId, pulseDecay);
            _material.SetFloat(PulseBrightnessId, pulseBrightness);
            _material.SetFloat(PulseWidthId, pulseWidth);
            _material.SetFloat(MuzzleRadiusId, muzzleRadius);
            _material.SetFloat(TipFadeId, tipFade);
            _material.SetFloat(ZId, z);

            RenderParams rp = new(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };

            Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, beams.Count);
        }

        /// <summary>
        /// Grows the buffer (doubling) when more beams are alive than it holds.
        /// </summary>
        private void EnsureCapacity(int count)
        {
            if (_buffer != null && _buffer.count >= count) return;

            int capacity = Mathf.Max(count, _buffer != null ? _buffer.count * 2 : 16);
            _buffer?.Release();
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, UnsafeUtility.SizeOf<BeamVisualData>());
            _material.SetBuffer(BeamsId, _buffer);
        }
    }
}
