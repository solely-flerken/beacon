using System;
using System.Collections.Generic;
using System.Linq;
using _Game.Components;
using _Game.Core;
using Unity.Entities;
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
        public EnemyType type;
        public List<EnemyTierEntry> tiers = new();
    }

    // Flat internal representation consumed by WaveGenerator
    public struct EnemyTypeEntry
    {
        public EnemyType Type;
        public EnemyTier Tier;
        public int Cost;
        public int Weight;
        public int StartWave;
    }

    public struct WaveGeneratorConfig
    {
        public int BudgetBase;
        public int BudgetPerWave;
        public EnemyTypeEntry[] EnemyTypes;
    }

    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        public int CurrentWave { get; private set; }
        public bool IsWaveActive { get; private set; }

        [Header("Wave Budget")]
        [SerializeField] private int budgetBase = 30;
        [SerializeField] private int budgetPerWave = 5;
        [SerializeField] private List<EnemyTypeDefinition> enemyTypeDefinitions = new();

        private EntityManager _entityManager;
        private EntityQuery _enemyQuery;

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

            EventBus.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            EventBus.OnGameStateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState from, GameState to)
        {
            if (to == GameState.WaveRunning)
            {
                StartNextWave();
            }
            else if (to == GameState.RunEnd)
            {
                IsWaveActive = false;
            }
        }
        
        private void Update()
        {
            if (!IsWaveActive) return;
            if (!_enemyQuery.IsEmpty) return;
            
            WaveCleared();
        }

        public void ResetWaves()
        {
            CurrentWave = 0;
            IsWaveActive = false;
        }
        
        private void StartNextWave()
        {
            CurrentWave++;
            
            Debug.Log($"[WaveManager] Wave {CurrentWave} started.");

            WaveGeneratorConfig config = new()
            {
                BudgetBase = budgetBase,
                BudgetPerWave = budgetPerWave,
                EnemyTypes = FlattenEnemyTypes(),
            };

            WaveDefinition definition = WaveGenerator.Generate(CurrentWave, config);
            SpawnWave(definition);

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

        private void SpawnWave(WaveDefinition definition)
        {
            foreach (SpawnGroup group in definition.SpawnGroups)
            {
                EnemySpawner.SpawnWave(_entityManager, group);
            }
        }

        private void WaveCleared()
        {
            IsWaveActive = false;
            EventBus.FireWaveCleared(CurrentWave);
            Debug.Log($"[WaveManager] Wave {CurrentWave} cleared.");

            GameStateMachine.Instance.TransitionTo(GameState.WaveEnd);

            Entity e = _entityManager.CreateEntity();
            _entityManager.AddComponent<WaveEndTag>(e);
        }
    }
}
