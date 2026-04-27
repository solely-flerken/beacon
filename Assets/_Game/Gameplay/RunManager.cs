using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using _Game.Gameplay.Waves;
using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    public class RunManager : MonoBehaviour
    {
        private static RunManager Instance { get; set; }

        private EntityManager _entityManager;
        
        private EntityQuery _prefabQuery;
        private EntityQuery _weaponCatalogQuery;
        
        private PrefabRefs _prefabRefs;
        private Entity _beaconEntity;
        private Entity _weaponEntity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        private void Start()
        {
            _prefabQuery = _entityManager.CreateEntityQuery(typeof(PrefabRefs));
            _weaponCatalogQuery = _entityManager.CreateEntityQuery(typeof(WeaponCatalogEntry));

            if (_prefabQuery.IsEmpty)
            {
                Debug.LogError("[RunManager] PrefabRefs not found. Ensure SpawnerAuthoring is in the subscene and the prefabs are assigned.");
                return;
            }

            _prefabRefs = _prefabQuery.GetSingleton<PrefabRefs>();

            WarmupArchetypes();
        }

        public void StartRun()
        {
            SpawnBeacon();
            SpawnWeapon(WeaponType.Basic);
            GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
        }

        public void Cleanup()
        {
            DestroyEntity(ref _beaconEntity);
            DestroyEntity(ref _weaponEntity);

            WaveManager.Instance.ResetWaves();
            CardDraftManager.Instance.ResetHistory();
            ExpManager.Instance.Reset();
        }

        private void SpawnBeacon()
        {
            _beaconEntity = _entityManager.Instantiate(_prefabRefs.Beacon);
        }

        private void SpawnWeapon(WeaponType type)
        {
            if (_weaponCatalogQuery.IsEmpty)
            {
                Debug.LogWarning($"[RunManager] Weapon catalog not found, cannot spawn {type}.");
                return;
            }

            DynamicBuffer<WeaponCatalogEntry> catalog = _entityManager.GetBuffer<WeaponCatalogEntry>(_weaponCatalogQuery.GetSingletonEntity());

            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].Type != type) continue;
                
                _weaponEntity = _entityManager.Instantiate(catalog[i].Prefab);
                Debug.Log($"[RunManager] Spawned weapon {type}.");
                return;
            }

            Debug.LogWarning($"[RunManager] Weapon type {type} not found in catalog.");
        }

        private void DestroyEntity(ref Entity entity)
        {
            if (entity != Entity.Null && _entityManager.Exists(entity))
            {
                _entityManager.DestroyEntity(entity);
            }

            entity = Entity.Null;
        }

        private void WarmupArchetypes()
        {
            _entityManager.DestroyEntity(_entityManager.Instantiate(_prefabRefs.Beacon));
        }
    }
}