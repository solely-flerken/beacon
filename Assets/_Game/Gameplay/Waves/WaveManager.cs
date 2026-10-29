using System;
using System.Collections.Generic;
using System.Linq;
using _Game.Components;
using _Game.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Gameplay.Waves
{
    [Serializable]
    public struct EnemyTierEntry
    {
        public EnemyTier tier;
        [Min(1)] public int cost;
        [Min(0)] public int weight;
        [Min(1)] public int startWave;
    }

    [Serializable]
    public class EnemyTypeDefinition
    {
        public RoleMask type;
        public List<EnemyTierEntry> tiers = new();
    }

    // Flat internal representation consumed by WaveGenerator
    public struct EnemyTypeEntry
    {
        public RoleMask Type;
        public EnemyTier Tier;
        public int Cost;
        public int Weight;
        public int StartWave;
    }

    public struct WaveGeneratorConfig
    {
        public int BudgetBase;
        public int BudgetPerWave;
        public float SpawnDurationBase;
        public float SpawnDurationPerWave;
        public float PulseInterval;
        public float IntensityRamp;
        public EnemyTypeEntry[] EnemyTypes;
    }

    public enum WavePhase
    {
        None,
        Spawning, // Pulses spawn on schedule
        Grace, // No more spawns, remaining enemies can still be killed
    }

    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        public int CurrentWave { get; private set; }
        public bool IsWaveActive { get; private set; }
        public float WaveElapsed { get; private set; }
        public WavePhase Phase { get; private set; }
        public int SpawnedCount { get; private set; }
        public int TotalEnemies => _wave?.TotalEnemies ?? 0;

        public float PhaseTimeRemaining => Phase switch
        {
            WavePhase.Spawning => Mathf.Max(0f, _wave.SpawnDuration - _spawnClock),
            WavePhase.Grace => Mathf.Max(0f, _graceRemaining),
            _ => 0f
        };

        [Header("Wave Budget")]
        [SerializeField] private int budgetBase = 30;
        [SerializeField] private int budgetPerWave = 5;
        [SerializeField] private List<EnemyTypeDefinition> enemyTypeDefinitions = new();

        [Header("Wave Timing")]
        [Tooltip("Length of the spawn phase on wave 1, in seconds.")]
        [SerializeField, Min(1f)] private float spawnDurationBase = 30f;
        [Tooltip("Seconds added to the spawn phase per wave after the first.")]
        [SerializeField, Min(0f)] private float spawnDurationPerWave = 2f;
        [Tooltip("Seconds between pulses.")]
        [SerializeField, Min(0.1f)] private float pulseInterval = 2f;
        [Tooltip("Size of the last pulse relative to the first.")]
        [SerializeField, Min(0.1f)] private float intensityRamp = 2f;
        [Tooltip("Seconds after the spawn phase to kill remaining enemies before they despawn.")]
        [SerializeField, Min(0f)] private float gracePeriod = 8f;
        [Tooltip("Width of the spawn ring arc each pulse spawns on, in degrees.")]
        [SerializeField, Range(1f, 360f)] private float packArcDegrees = 60f;

        private EntityManager _entityManager;
        private EntityQuery _enemyQuery;
        private EntityQuery _despawnQuery;
        private EntityQuery _spawnQueueQuery;
        private EntityQuery _waveModQuery;

        private WaveDefinition _wave;
        private int _nextPulse;
        private float _spawnClock;
        private float _graceRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _enemyQuery = _entityManager.CreateEntityQuery(typeof(EnemyTag));
            _despawnQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<EnemyTag>().WithPresent<Despawn>().Build(_entityManager);
            _spawnQueueQuery = _entityManager.CreateEntityQuery(typeof(EnemySpawnRequest));
            _waveModQuery = _entityManager.CreateEntityQuery(typeof(GlobalModSingleton), typeof(WaveStatModifier));

            EventBus.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            EventBus.OnGameStateChanged -= OnGameStateChanged;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _enemyQuery.Dispose();
                _despawnQuery.Dispose();
                _spawnQueueQuery.Dispose();
                _waveModQuery.Dispose();
            }
        }

        private void OnGameStateChanged(GameState from, GameState to)
        {
            if (to == GameState.WaveRunning && from is GameState.WaveEnd or GameState.MainMenu or GameState.RunEnd)
            {
                StartNextWave();
            }
            else if (to == GameState.RunEnd)
            {
                IsWaveActive = false;
                Phase = WavePhase.None;
                ClearSpawnQueue();
            }
        }
        
        private void Update()
        {
            if (!GameStateMachine.Instance.IsWaveRunning) return;
            if (!IsWaveActive) return;

            float dt = Time.deltaTime;
            WaveElapsed += dt;

            switch (Phase)
            {
                case WavePhase.Spawning:
                    TickSpawning(dt);
                    break;
                case WavePhase.Grace:
                    TickGrace(dt);
                    break;
            }
        }

        private void TickSpawning(float dt)
        {
            _spawnClock += dt;

            // Field cleared early: pull the next pulse (or the end of the spawn phase) forward
            if (_enemyQuery.IsEmpty && !HasPendingSpawns())
            {
                float nextTime = _nextPulse < _wave.Pulses.Count ? _wave.Pulses[_nextPulse].Time : _wave.SpawnDuration;
                _spawnClock = Mathf.Max(_spawnClock, nextTime);
            }

            while (_nextPulse < _wave.Pulses.Count && _wave.Pulses[_nextPulse].Time <= _spawnClock)
            {
                SpawnPulse(_wave.Pulses[_nextPulse]);
                _nextPulse++;
            }

            if (_nextPulse < _wave.Pulses.Count || _spawnClock < _wave.SpawnDuration) return;

            Phase = WavePhase.Grace;
            _graceRemaining = gracePeriod;
            
            Debug.Log($"[WaveManager] Wave {CurrentWave} spawning finished, {gracePeriod}s grace period.");
        }

        private void TickGrace(float dt)
        {
            _graceRemaining -= dt;

            if (!_enemyQuery.IsEmpty || HasPendingSpawns())
            {
                if (_graceRemaining > 0f) return;
                
                DespawnRemainingEnemies();
            }

            WaveCleared();
        }

        public void ResetWaves()
        {
            CurrentWave = 0;
            IsWaveActive = false;
            WaveElapsed = 0f;
            Phase = WavePhase.None;
            SpawnedCount = 0;
            _wave = null;

            ClearWaveModifiers();
            ClearSpawnQueue();
        }
        
        private void StartNextWave()
        {
            CurrentWave++;
            WaveElapsed = 0f;

            WaveGeneratorConfig config = new()
            {
                BudgetBase = budgetBase,
                BudgetPerWave = budgetPerWave,
                SpawnDurationBase = spawnDurationBase,
                SpawnDurationPerWave = spawnDurationPerWave,
                PulseInterval = pulseInterval,
                IntensityRamp = intensityRamp,
                EnemyTypes = FlattenEnemyTypes(),
            };

            _wave = WaveGenerator.Generate(CurrentWave, config);
            _nextPulse = 0;
            _spawnClock = 0f;
            SpawnedCount = 0;
            Phase = WavePhase.Spawning;

            Debug.Log($"[WaveManager] Wave {CurrentWave} started: {_wave.TotalEnemies} enemies in {_wave.Pulses.Count} pulses over {_wave.SpawnDuration}s.");

            IsWaveActive = true;
            EventBus.FireWaveStarted(CurrentWave);
        }

        private EnemyTypeEntry[] FlattenEnemyTypes()
        {
            IEnumerable<EnemyTypeEntry> enemyTypeEntry = from enemyTypeDefinition in enemyTypeDefinitions
                from enemyTierDefinition in enemyTypeDefinition.tiers
                select new EnemyTypeEntry
                {
                    Type = enemyTypeDefinition.type,
                    Tier = enemyTierDefinition.tier,
                    Cost = enemyTierDefinition.cost,
                    Weight = enemyTierDefinition.weight,
                    StartWave = enemyTierDefinition.startWave,
                };

            return enemyTypeEntry.ToArray();
        }

        private void SpawnPulse(SpawnPulse pulse)
        {
            if (_spawnQueueQuery.IsEmpty)
            {
                Debug.LogError("[WaveManager] Enemy spawn queue not found.");
                return;
            }

            DynamicBuffer<EnemySpawnRequest> queue = _spawnQueueQuery.GetSingletonBuffer<EnemySpawnRequest>();
            float arcWidth = math.radians(packArcDegrees);

            // EnemySpawnSystem instantiates these over the next frames, capped per frame
            foreach (SpawnGroup group in pulse.Groups)
            {
                queue.Add(new EnemySpawnRequest
                {
                    Type = group.Type,
                    Tier = group.Tier,
                    Count = group.Count,
                    ArcCenter = pulse.Angle,
                    ArcWidth = arcWidth,
                });
                SpawnedCount += group.Count;
            }
        }

        private bool HasPendingSpawns()
        {
            return !_spawnQueueQuery.IsEmpty && !_spawnQueueQuery.GetSingletonBuffer<EnemySpawnRequest>(isReadOnly: true).IsEmpty;
        }

        private void ClearSpawnQueue()
        {
            if (_spawnQueueQuery.IsEmpty) return;
            _spawnQueueQuery.GetSingletonBuffer<EnemySpawnRequest>().Clear();
        }

        private void DespawnRemainingEnemies()
        {
            ClearSpawnQueue();

            int count = _despawnQuery.CalculateEntityCount();
            _entityManager.SetComponentEnabled<Despawn>(_despawnQuery, true);
            Debug.Log($"[WaveManager] Grace period over, despawned {count} enemies.");
        }

        private void WaveCleared()
        {
            IsWaveActive = false;
            WaveElapsed = 0f;
            Phase = WavePhase.None;

            ClearWaveModifiers();

            EventBus.FireWaveCleared(CurrentWave);
            Debug.Log($"[WaveManager] Wave {CurrentWave} cleared.");

            GameStateMachine.Instance.TransitionTo(GameState.WaveEnd);

            Entity e = _entityManager.CreateEntity();
            _entityManager.AddComponent<WaveEndTag>(e);
        }

        private void ClearWaveModifiers()
        {
            if (_waveModQuery.IsEmpty)
            {
                Debug.LogWarning("[WaveManager] GlobalModSingleton not found, wave modifiers not cleared.");
                return;
            }

            _entityManager.GetBuffer<WaveStatModifier>(_waveModQuery.GetSingletonEntity()).Clear();
        }
    }
}
