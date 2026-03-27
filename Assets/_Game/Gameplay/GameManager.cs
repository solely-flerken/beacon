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

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _enemyCountQuery = _entityManager.CreateEntityQuery(typeof(EnemyTag));
            _projectileCountQuery = _entityManager.CreateEntityQuery(typeof(ProjectileTag));
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
            
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
            }

            Debug.Log("[GameManager] Press Space to spawn a wave.");
        }

        private void OnDestroy()
        {
            EventBus.ClearAllSubscriptions();
        }

        private void Update()
        {
            if (GameStateMachine.Instance.CurrentState != GameState.WaveRunning) return;
            
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                EnemySpawner.SpawnWave(_entityManager, 200);
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                SpawnFlare();
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                TriggerWaveEnd();
            }
        }
    
        private void TriggerWaveEnd()
        {
            Entity e = _entityManager.CreateEntity();
            _entityManager.AddComponent<WaveEndTag>(e);
            Debug.Log("[GameManager] Wave ended.");
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
            GUI.Box(new Rect(10, 10, 150, 25), $"    Enemies: {enemyCount}");
            GUI.Box(new Rect(10, 40, 150, 25), $"Projectiles: {projectileCount}");
        }
    }
}