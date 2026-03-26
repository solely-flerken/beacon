using System;
using Unity.Collections;
using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// Bridge between ECS simulation and managed C# events.
    ///
    /// DeathQueue is a static so ECS systems can write to it without an Instance reference.
    /// Listeners on batch events must process the NativeArray immediately - do not store it.
    /// </summary>
    public class DotsBridge : MonoBehaviour
    {
        [Header("Buffer Sizes")] 
        [SerializeField] private int deathBufferSize = 8192;
        [SerializeField] private int hitBufferSize = 4096;

        /// <summary>
        /// Written by ProjectileSystem on enemy death.
        /// </summary>
        public static NativeQueue<DeathEvent> DeathQueue;

        /// <summary>
        /// Written by ProjectileSystem on enemy hit (damage but not death).
        /// </summary>
        public static NativeQueue<HitEvent> HitQueue;

        private NativeArray<DeathEvent> _deathBatch;
        private NativeArray<HitEvent> _hitBatch;

        private void Awake()
        {
            DeathQueue = new NativeQueue<DeathEvent>(Allocator.Persistent);
            HitQueue = new NativeQueue<HitEvent>(Allocator.Persistent);
            _deathBatch = new NativeArray<DeathEvent>(deathBufferSize, Allocator.Persistent);
            _hitBatch = new NativeArray<HitEvent>(hitBufferSize, Allocator.Persistent);
        }

        private void LateUpdate()
        {
            DrainQueue(DeathQueue, _deathBatch, EventBus.FireEnemiesDiedBatch);
            DrainQueue(HitQueue, _hitBatch, EventBus.FireEnemiesHitBatch);
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

        private void OnDestroy()
        {
            if (DeathQueue.IsCreated) DeathQueue.Dispose();
            if (HitQueue.IsCreated) HitQueue.Dispose();
            if (_deathBatch.IsCreated) _deathBatch.Dispose();
            if (_hitBatch.IsCreated) _hitBatch.Dispose();
        }
    }
}