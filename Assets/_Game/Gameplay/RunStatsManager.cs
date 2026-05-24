using System.Collections.Generic;
using System.Linq;
using _Game.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    public class RunStatsManager : MonoBehaviour
    {
        public struct WeaponRecord
        {
            public float Raw;
        }

        public static RunStatsManager Instance { get; private set; }

        private Dictionary<Entity, WeaponRecord> _weaponStats;

        public IReadOnlyDictionary<Entity, WeaponRecord> WeaponStats => _weaponStats;
        public int EnemiesKilled { get; private set; }

        public float TotalRawDamage
        {
            get { return _weaponStats.Values.Sum(r => r.Raw); }
        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _weaponStats = new Dictionary<Entity, WeaponRecord>();
        }

        private void OnEnable()
        {
            EventBus.OnEnemiesHitBatch += OnEnemiesHitBatch;
            EventBus.OnEnemiesDiedBatch += OnEnemiesDiedBatch;
        }

        private void OnDisable()
        {
            EventBus.OnEnemiesHitBatch -= OnEnemiesHitBatch;
            EventBus.OnEnemiesDiedBatch -= OnEnemiesDiedBatch;
        }

        public void ResetStats()
        {
            _weaponStats.Clear();
            EnemiesKilled = 0;
        }

        private void OnEnemiesHitBatch(NativeArray<HitEvent> batch, int count)
        {
            for (int i = 0; i < count; i++)
            {
                HitEvent hit = batch[i];
                if (hit.WeaponEntity == Entity.Null) continue;

                if (!_weaponStats.TryGetValue(hit.WeaponEntity, out WeaponRecord record))
                {
                    record = new WeaponRecord();
                }

                record.Raw += hit.Damage;
                _weaponStats[hit.WeaponEntity] = record;
            }
        }

        private void OnEnemiesDiedBatch(NativeArray<DeathEvent> batch, int count)
        {
            EnemiesKilled += count;
        }
    }
}
