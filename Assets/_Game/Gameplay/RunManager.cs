using System;
using _Game.Authoring;
using _Game.Components;
using _Game.Config;
using _Game.Core;
using _Game.Gameplay.Abilities;
using _Game.Gameplay.Waves;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Gameplay
{
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [SerializeField] private RunConfig runConfig;
        [SerializeField] private BeaconSlotProvider slotProvider;

        public BeaconLoadout Loadout { get; } = new();

        private EntityManager _entityManager;
        
        private EntityQuery _prefabQuery;
        private EntityQuery _weaponCatalogQuery;
        private EntityQuery _globalModQuery;

        private PrefabRefs _prefabRefs;
        private Entity _beaconEntity;
        private Entity[] _weaponEntities = Array.Empty<Entity>();

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
            _globalModQuery = _entityManager.CreateEntityQuery(typeof(GlobalModSingleton), typeof(GlobalStatModifier));

            if (_prefabQuery.IsEmpty)
            {
                Debug.LogError("[RunManager] PrefabRefs not found. Ensure SpawnerAuthoring is in the subscene and the prefabs are assigned.");
                return;
            }

            _prefabRefs = _prefabQuery.GetSingleton<PrefabRefs>();

            _weaponEntities = new Entity[slotProvider.WeaponSlotCount];

            WarmupArchetypes();
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _prefabQuery.Dispose();
                _weaponCatalogQuery.Dispose();
                _globalModQuery.Dispose();
            }
        }

        public void StartRun()
        {
            SpawnBeacon();
            SpawnWeapons();
            InitializeAbilities();
            GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
        }

        public void Cleanup()
        {
            DestroyEntity(_weaponEntities);
            DestroyEntity(ref _beaconEntity);

            RemoveRunScopedGlobalModifiers();

            Loadout.Reset();
            slotProvider.ClearWeaponEntities();
            AbilityManager.Instance?.Reset();

            WaveManager.Instance.ResetWaves();
            CardDraftManager.Instance.ResetHistory();
            ExpManager.Instance.Reset();
            RunStatsManager.Instance.ResetStats();
        }

        /// <summary>
        /// Removes run-scoped global modifiers (e.g. enemy cards). Baked authoring entries are kept.
        /// </summary>
        private void RemoveRunScopedGlobalModifiers()
        {
            if (_globalModQuery.IsEmpty)
            {
                Debug.LogWarning("[RunManager] GlobalModSingleton not found, global modifiers won't be reset between runs.");
                return;
            }

            DynamicBuffer<GlobalStatModifier> buffer = _entityManager.GetBuffer<GlobalStatModifier>(_globalModQuery.GetSingletonEntity());
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (!buffer[i].RunScoped) continue;
                
                buffer.RemoveAt(i);
            }
        }

        private void SpawnBeacon()
        {
            _beaconEntity = _entityManager.Instantiate(_prefabRefs.Beacon);
        }

        private void SpawnWeapons()
        {
            Loadout.Initialize(slotProvider.WeaponSlotCount);
            
            int weaponsToAssign = runConfig.startingWeapons.Length;
            if (weaponsToAssign > slotProvider.WeaponSlotCount)
            {
                Debug.LogWarning($"[RunManager] RunConfig defines {weaponsToAssign} weapons but only {slotProvider.WeaponSlotCount} slots are available. Extra weapons will be ignored.");
                weaponsToAssign = slotProvider.WeaponSlotCount;
            }

            for (int i = 0; i < weaponsToAssign; i++)
            {
                Loadout.SetWeapon(i, runConfig.startingWeapons[i]);
            }

            if (_weaponCatalogQuery.IsEmpty)
            {
                Debug.LogWarning("[RunManager] Weapon catalog not found, cannot spawn weapons.");
                return;
            }

            using NativeArray<WeaponCatalogEntry> catalog = _entityManager
                .GetBuffer<WeaponCatalogEntry>(_weaponCatalogQuery.GetSingletonEntity(), true)
                .ToNativeArray(Allocator.Temp);

            for (int i = 0; i < Loadout.SlotCount; i++)
            {
                WeaponType? type = Loadout.GetWeapon(i);
                if (type == null) continue;

                if (!slotProvider.TryGetSlot(i, out Transform slotTransform)) continue;
                _weaponEntities[i] = SpawnWeapon(type.Value, catalog, slotTransform.localPosition, _beaconEntity);
                slotProvider.SetWeaponEntity(i, _weaponEntities[i]);
            }
        }

        private void InitializeAbilities()
        {
            int count = Mathf.Min(runConfig.startingAbilities.Length, BeaconLoadout.AbilitySlotCount);
            for (int i = 0; i < count; i++)
            {
                Loadout.SetAbility(i, runConfig.startingAbilities[i]);
            }

            AbilityManager.Instance?.SetupFromLoadout(Loadout);
        }

        private Entity SpawnWeapon(WeaponType type, NativeArray<WeaponCatalogEntry> catalog, float3 position, Entity owner)
        {
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].Type != type) continue;

                Entity entity = _entityManager.Instantiate(catalog[i].Prefab);
                _entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
                _entityManager.AddComponentData(entity, new Parent { Value = owner });
                _entityManager.SetComponentData(entity, new WeaponOwner { Value = owner });

                Debug.Log($"[RunManager] Spawned weapon {type}.");
                return entity;
            }

            Debug.LogWarning($"[RunManager] Weapon type {type} not found in catalog.");
            return Entity.Null;
        }

        private void DestroyEntity(ref Entity entity)
        {
            if (entity != Entity.Null && _entityManager.Exists(entity))
            {
                _entityManager.DestroyEntity(entity);
            }

            entity = Entity.Null;
        }
        
        private void DestroyEntity(Entity[] entities)
        {
            foreach (Entity entity in entities)
            {
                if (entity != Entity.Null && _entityManager.Exists(entity))
                {
                    _entityManager.DestroyEntity(entity);
                }
            }

            Array.Clear(entities, 0, entities.Length);
        }

        private void WarmupArchetypes()
        {
            _entityManager.DestroyEntity(_entityManager.Instantiate(_prefabRefs.Beacon));
        }
    }
}