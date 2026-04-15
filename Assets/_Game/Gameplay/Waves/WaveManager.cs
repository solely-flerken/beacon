using _Game.Components;
using _Game.Core;
using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay.Waves
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        public int CurrentWave { get; private set; }
        public bool IsWaveActive { get; private set; }

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
            
            WaveDefinition definition = WaveGenerator.Generate(CurrentWave);
            SpawnWave(definition);

            IsWaveActive = true;
            EventBus.FireWaveStarted(CurrentWave);
        }

        private void SpawnWave(WaveDefinition definition)
        {
            foreach (SpawnGroup group in definition.SpawnGroups)
            {
                EnemySpawner.SpawnWave(_entityManager, group.Type, group.Count);
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
