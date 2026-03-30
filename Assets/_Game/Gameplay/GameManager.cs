using System;
using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Game.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        private EntityManager _entityManager;
        private EntityQuery _enemyCountQuery;
        private EntityQuery _projectileCountQuery;

        private SimulationSystemGroup _simGroup;

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _enemyCountQuery = _entityManager.CreateEntityQuery(typeof(EnemyTag));
            _projectileCountQuery = _entityManager.CreateEntityQuery(typeof(ProjectileTag));

            _simGroup = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystemGroup>();
        }

        private void Start()
        {
            // Warm up archetypes so spawns don't lead to stutter
            using EntityQuery prefabQuery = _entityManager.CreateEntityQuery(typeof(PrefabRefs));
            if (!prefabQuery.IsEmpty)
            {
                PrefabRefs prefabs = prefabQuery.GetSingleton<PrefabRefs>();
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.Enemy));
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.Projectile));
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.Flare));
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.ExpOrb));
            }

            SetSimulation(false);

            EventBus.OnBeaconDestroyed += OnBeaconDestroyed;
            EventBus.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            EventBus.ClearAllSubscriptions();
        }

        private void Update()
        {
            GameState state = GameStateMachine.Instance.CurrentState;

            switch (state)
            {
                case GameState.None:
                    GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
                    break;
                
                case GameState.MainMenu:
                    if (Keyboard.current.anyKey.wasPressedThisFrame)
                        EnterWaveRunning();
                    break;

                case GameState.WaveRunning:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        EnemySpawner.SpawnWave(_entityManager, 200);
                    if (Keyboard.current.fKey.wasPressedThisFrame)
                        SpawnFlare();
                    if (Keyboard.current.eKey.wasPressedThisFrame)
                        TriggerWaveEnd();
                    if (Keyboard.current.pKey.wasPressedThisFrame)
                        GameStateMachine.Instance.TransitionTo(GameState.Paused);
                    break;

                case GameState.WaveEnd:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        EnterWaveRunning();
                    break;

                case GameState.Paused:
                    if (Keyboard.current.pKey.wasPressedThisFrame)
                        GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
                    break;
                
                case GameState.CardDraft:
                    // Card draft
                    break;

                case GameState.RunEnd:
                    if (Keyboard.current.rKey.wasPressedThisFrame)
                        RestartRun();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EnterWaveRunning()
        {
            GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
            SetSimulation(true);
        }

        private void TriggerWaveEnd()
        {
            GameStateMachine.Instance.TransitionTo(GameState.WaveEnd);
            
            Entity e = _entityManager.CreateEntity();
            _entityManager.AddComponent<WaveEndTag>(e);
            Debug.Log("[GameManager] Wave ended.");
        }

        private void OnGameStateChanged(GameState from, GameState to)
        {
            SetSimulation(to is GameState.WaveRunning or GameState.WaveEnd);
        }

        private void OnBeaconDestroyed()
        {
            GameStateMachine.Instance.TransitionTo(GameState.RunEnd);
            SetSimulation(false);
        }

        private void SetSimulation(bool enable)
        {
            _simGroup.Enabled = enable;
        }

        private void RestartRun()
        {
            using EntityQuery beaconQuery = _entityManager.CreateEntityQuery(typeof(BeaconStats));
            if (!beaconQuery.IsEmpty)
            {
                Entity beacon = beaconQuery.GetSingletonEntity();
                
                BeaconStats stats = _entityManager.GetComponentData<BeaconStats>(beacon);
                stats.CurrentHp = stats.MaxHp;
                stats.IsAlive = true;
                _entityManager.SetComponentData(beacon, stats);

                // TODO: Reset weapon timers, modifiers, etc.
                
                EventBus.FireBeaconHit(stats.MaxHp, stats.MaxHp);
            }
            
            GameStateMachine.Instance.TransitionTo(GameState.None);
        }
        
        private void SpawnFlare()
        {
            using EntityQuery prefabQuery = _entityManager.CreateEntityQuery(typeof(PrefabRefs));
            if (prefabQuery.IsEmpty) return;

            PrefabRefs prefabs = prefabQuery.GetSingleton<PrefabRefs>();

            // Calculate Spawn Position
            float2 beaconPos = float2.zero; // TODO: Get beaconPosition here.
            float2 randomOffset = (float2)UnityEngine.Random.insideUnitCircle * 10f;
            float3 spawnPos = new(beaconPos + randomOffset, 0f);

            Entity flare = _entityManager.Instantiate(prefabs.Flare);

            LocalTransform existingTransform = _entityManager.GetComponentData<LocalTransform>(flare);
            _entityManager.SetComponentData(flare, LocalTransform.FromPositionRotationScale(
                spawnPos,
                quaternion.identity,
                existingTransform.Scale
            ));

            Debug.Log("Flare Deployed via Prefab!");
        }

        private void OnGUI()
        {
            int enemyCount = _enemyCountQuery.CalculateEntityCount();
            int projectileCount = _projectileCountQuery.CalculateEntityCount();
            GUI.Box(new Rect(10, 10, 150, 25), $"Enemies: {enemyCount}");
            GUI.Box(new Rect(10, 40, 150, 25), $"Projectiles: {projectileCount}");

            if (ExpManager.Instance != null)
            {
                int level = ExpManager.Instance.CurrentLevel;
                float progress = ExpManager.Instance.LevelProgress;
                float totalExp = ExpManager.Instance.TotalExp;

                GUI.Box(new Rect(10, 70, 150, 25), $"Level: {level} - Exp: {(int)totalExp}");

                // Progress bar
                GUI.Box(new Rect(10, 100, 150, 16), "");
                if (progress > 0) GUI.Box(new Rect(10, 100, 150 * progress, 16), "");
            }

            // Controls hint
            string hint = GameStateMachine.Instance?.CurrentState switch
            {
                GameState.MainMenu => "Any key to start",
                GameState.WaveRunning => "Enemies (space), End wave ('E'), Flare ('F'), Pause ('P')",
                GameState.WaveEnd => "Start next wave (space)",
                GameState.RunEnd => "Restart ('R')",
                GameState.Paused => "Unpause ('P')",
                _ => ""
            };
            GUI.Box(new Rect(10, Screen.height - 35, 400, 25), hint);

            GUI.Box(new Rect(10, Screen.height - 65, 400, 25), $"State: {GameStateMachine.Instance?.CurrentState.ToString()}");
        }
    }
}