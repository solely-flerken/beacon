using System;
using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using Unity.Collections;
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
                    if (Keyboard.current.mKey.wasPressedThisFrame)
                        ApplyTestModifier();
                    if (Keyboard.current.bKey.wasPressedThisFrame)
                        ApplyTestBeaconModifier();
                    if (Keyboard.current.nKey.wasPressedThisFrame)
                        ApplyTestEnemyModifier();
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

        private void ApplyTestModifier()
        {
            using EntityQuery beaconQuery = _entityManager.CreateEntityQuery(typeof(BeaconStats), typeof(GlobalStatModifier));

            if (beaconQuery.IsEmpty) return;
           
            Entity beacon = beaconQuery.GetSingletonEntity();
            DynamicBuffer<GlobalStatModifier> globalBuffer = _entityManager.GetBuffer<GlobalStatModifier>(beacon);

            globalBuffer.Add(new GlobalStatModifier
            {
                Stat = StatType.FireRate,
                Type = ModifierType.Multiplicative,
                Value = 2f
            });

            Debug.Log("[GameManager] Applied Global 2x Fire Rate Modifier to Beacon.");
            
            // Mark all weapons as "Dirty"
            EntityQueryDesc weaponQueryDesc = new()
            {
                All = new[] { ComponentType.ReadOnly<WeaponStatUpdateRequired>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };

            using EntityQuery weaponQuery = _entityManager.CreateEntityQuery(weaponQueryDesc);
            using NativeArray<Entity> weapons = weaponQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity weapon in weapons)
            {
                _entityManager.SetComponentEnabled<WeaponStatUpdateRequired>(weapon, true);
            }
        }
        
        private void ApplyTestBeaconModifier()
        {
            using EntityQuery beaconQuery = _entityManager.CreateEntityQuery(typeof(BeaconStats), typeof(StatModifier));

            if (beaconQuery.IsEmpty) return;

            Entity beacon = beaconQuery.GetSingletonEntity();
            DynamicBuffer<StatModifier> localBuffer = _entityManager.GetBuffer<StatModifier>(beacon);

            localBuffer.Add(new StatModifier
            {
                Stat = StatType.MaxHp,
                Type = ModifierType.Multiplicative,
                Value = 1.5f
            });

            _entityManager.SetComponentEnabled<BeaconStatUpdateRequired>(beacon, true);

            Debug.Log("[GameManager] Applied Beacon +50% MaxHp modifier.");
        }

        private void ApplyTestEnemyModifier()
        {
            EntityQueryDesc enemyQueryDesc = new()
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EnemyTag>(),
                    ComponentType.ReadOnly<StatModifier>(),
                    ComponentType.ReadOnly<EnemyStatUpdateRequired>()
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };

            using EntityQuery enemyQuery = _entityManager.CreateEntityQuery(enemyQueryDesc);
            using NativeArray<Entity> enemies = enemyQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity enemy in enemies)
            {
                DynamicBuffer<StatModifier> localBuffer = _entityManager.GetBuffer<StatModifier>(enemy);
                localBuffer.Add(new StatModifier
                {
                    Stat = StatType.EnemySpeed,
                    Type = ModifierType.Multiplicative,
                    Value = 2f
                });

                _entityManager.SetComponentEnabled<EnemyStatUpdateRequired>(enemy, true);
            }

            Debug.Log($"[GameManager] Applied Enemy 2x Speed modifier to {enemies.Length} enemies.");
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
                GameState.WaveRunning => "Enemies(spc), End wave(E), Flare(F), Pause(P), Weapon Mod(M), Beacon Mod(B), Enemy Mod(N)",
                GameState.WaveEnd => "Start next wave(spc)",
                GameState.RunEnd => "Restart(R)",
                GameState.Paused => "Unpause(P)",
                _ => ""
            };
            GUI.Box(new Rect(10, Screen.height - 35, 400, 25), hint);

            GUI.Box(new Rect(10, Screen.height - 65, 400, 25), $"State: {GameStateMachine.Instance?.CurrentState.ToString()}");
        }
    }
}