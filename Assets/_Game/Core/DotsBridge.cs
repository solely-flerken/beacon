using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// Bridge between ECS simulation and managed C# events.
    ///
    /// The queues are statics so ECS systems can write to them without an Instance reference.
    /// Listeners on batch events must process the NativeArray immediately - do not store it.
    /// </summary>
    public class DotsBridge : MonoBehaviour
    {
        [Header("Buffer Sizes")] 
        [SerializeField] private int deathBufferSize = 8192;
        [SerializeField] private int hitBufferSize = 4096;
        [SerializeField] private int arcBufferSize = 1024;
        [SerializeField] private int maxSparkHitsPerFrame = 256;

        /// <summary>
        /// Written by EnemyDeathSystem on enemy death.
        /// </summary>
        public static NativeQueue<DeathEvent> DeathQueue;

        /// <summary>
        /// Written by DamageAndDeathSystem for every weapon hit that lands on a living target.
        /// </summary>
        public static NativeQueue<HitEvent> HitQueue;

        /// <summary>
        /// Written by BeaconCollectExpSystem with the exp value of every orb that reaches the beacon.
        /// </summary>
        public static NativeQueue<float> ExpCollectedQueue;

        /// <summary>
        /// Written by ChainLightningSystem to visualize chain lightning.
        /// </summary>
        public static NativeQueue<ChainArcVisualData> ArcQueue;

        /// <summary>
        /// Written by DamageAndDeathSystem. Already in the GPU layout, so it is fired as one batch straight.
        /// </summary>
        public static NativeList<HitSparkData> SparkList;

        private NativeArray<DeathEvent> _deathBatch;
        private NativeArray<HitEvent> _hitBatch;
        private NativeArray<ChainArcVisualData> _arcBatch;

        public static JobHandle BridgeDependencies;

        private static readonly Action<NativeArray<DeathEvent>, int> FireEnemiesDiedBatch = EventBus.FireEnemiesDiedBatch;
        private static readonly Action<NativeArray<HitEvent>, int> FireEnemiesHitBatch = EventBus.FireEnemiesHitBatch;
        private static readonly Action<NativeArray<ChainArcVisualData>, int> FireChainArcsBatch = EventBus.FireChainArcsBatch;

        private void Awake()
        {
            DeathQueue = new NativeQueue<DeathEvent>(Allocator.Persistent);
            HitQueue = new NativeQueue<HitEvent>(Allocator.Persistent);
            ExpCollectedQueue = new NativeQueue<float>(Allocator.Persistent);
            ArcQueue = new NativeQueue<ChainArcVisualData>(Allocator.Persistent);
            SparkList = new NativeList<HitSparkData>(maxSparkHitsPerFrame, Allocator.Persistent);
            _deathBatch = new NativeArray<DeathEvent>(deathBufferSize, Allocator.Persistent);
            _hitBatch = new NativeArray<HitEvent>(hitBufferSize, Allocator.Persistent);
            _arcBatch = new NativeArray<ChainArcVisualData>(arcBufferSize, Allocator.Persistent);
        }

        private void LateUpdate()
        {
            BridgeDependencies.Complete();
            
            DrainQueue(DeathQueue, _deathBatch, FireEnemiesDiedBatch);
            DrainQueue(HitQueue, _hitBatch, FireEnemiesHitBatch);
            DrainQueue(ArcQueue, _arcBatch, FireChainArcsBatch);

            if (SparkList.Length > 0)
            {
                EventBus.FireHitSparksBatch(SparkList.AsArray(), SparkList.Length);
                SparkList.Clear();
            }

            while (ExpCollectedQueue.TryDequeue(out float exp))
            {
                EventBus.FireExpCollected(exp);
            }

            BridgeDependencies = default; 
        }
        
        private void OnDestroy()
        {
            if (DeathQueue.IsCreated) DeathQueue.Dispose();
            if (HitQueue.IsCreated) HitQueue.Dispose();
            if (ExpCollectedQueue.IsCreated) ExpCollectedQueue.Dispose();
            if (ArcQueue.IsCreated) ArcQueue.Dispose();
            if (SparkList.IsCreated) SparkList.Dispose();
            if (_deathBatch.IsCreated) _deathBatch.Dispose();
            if (_hitBatch.IsCreated) _hitBatch.Dispose();
            if (_arcBatch.IsCreated) _arcBatch.Dispose();
        }
        
        private static void DrainQueue<T>(NativeQueue<T> queue, NativeArray<T> batch, Action<NativeArray<T>, int> fireEvent) where T : unmanaged
        {
            int count = 0;
            while (queue.TryDequeue(out T e))
            {
                if (count >= batch.Length)
                {
                    fireEvent(batch, count);
                    count = 0;
                }
                batch[count++] = e;
            }
            
            if (count > 0) fireEvent(batch, count);
        }
    }
}
