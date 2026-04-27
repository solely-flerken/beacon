using _Game.Authoring;
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
        private Entity _beaconEntity;
        private PrefabRefs _prefabRefs;
        private EntityQuery _prefabQuery;

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
            GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
        }

        public void Cleanup()
        {
            // Destroy beacon
            if (_beaconEntity != Entity.Null && _entityManager.Exists(_beaconEntity))
            {
                _entityManager.DestroyEntity(_beaconEntity);
                _beaconEntity = Entity.Null;
            }

            WaveManager.Instance.ResetWaves();
            CardDraftManager.Instance.ResetHistory();
            ExpManager.Instance.Reset();
        }

        private void SpawnBeacon()
        {
            _beaconEntity = _entityManager.Instantiate(_prefabRefs.Beacon);
        }

        private void WarmupArchetypes()
        {
            _entityManager.DestroyEntity(_entityManager.Instantiate(_prefabRefs.Beacon));
        }
    }
}