using System;
using Unity.Collections;

namespace _Game.Core
{
    /// <summary>
    /// Central event bus. No direct references between systems.
    ///
    /// Batch events - high-volume, fired once per frame from DOTSBridge.
    /// Individual events - low-volume, fired directly by systems.
    ///
    /// Batch event listeners: NativeArray is only valid during the callback. Do not store it.
    /// </summary>
    public static class EventBus
    {
        #region Batch Events

        public static event Action<NativeArray<DeathEvent>, int> OnEnemiesDiedBatch;
        public static event Action<NativeArray<HitEvent>, int> OnEnemiesHitBatch;
        public static event Action<NativeArray<ChainArcVisualData>, int> OnChainArcsBatch;
        public static event Action<NativeArray<HitSparkData>, int> OnHitSparksBatch;
        public static event Action<NativeArray<DeathBurstData>, int> OnDeathBurstsBatch;

        #endregion
        
        #region Individual Events

        // Wave
        public static event Action<int> OnWaveStarted;
        public static event Action<int> OnWaveCleared;

        // Progression
        public static event Action<float> OnExpCollected;
        public static event Action<int, float, float> OnExpChanged; // level, current, required
        public static event Action<int> OnLevelUp;

        // Beacon
        public static event Action<float, float, float> OnBeaconHit; // damage, currentHp, maxHp
        public static event Action<float, float> OnBeaconHealthChanged; // currentHp, maxHp
        public static event Action OnBeaconDestroyed;

        // Card draft
        public static event Action<int> OnCardDrafted;

        // Game state
        public static event Action<GameState, GameState> OnGameStateChanged;
        public static event Action<RunResult> OnRunEnded;

        #endregion

        // Fire
        public static void FireEnemiesDiedBatch(NativeArray<DeathEvent> batch, int count) => OnEnemiesDiedBatch?.Invoke(batch, count);
        public static void FireEnemiesHitBatch(NativeArray<HitEvent> batch, int count) => OnEnemiesHitBatch?.Invoke(batch, count);
        public static void FireChainArcsBatch(NativeArray<ChainArcVisualData> batch, int count) => OnChainArcsBatch?.Invoke(batch, count);
        public static void FireHitSparksBatch(NativeArray<HitSparkData> batch, int count) => OnHitSparksBatch?.Invoke(batch, count);
        public static void FireDeathBurstsBatch(NativeArray<DeathBurstData> batch, int count) => OnDeathBurstsBatch?.Invoke(batch, count);

        public static void FireWaveStarted(int wave) => OnWaveStarted?.Invoke(wave);
        public static void FireWaveCleared(int wave) => OnWaveCleared?.Invoke(wave);

        public static void FireExpCollected(float amount) => OnExpCollected?.Invoke(amount);
        public static void FireExpChanged(int level, float current, float max) => OnExpChanged?.Invoke(level, current, max);
        public static void FireLevelUp(int level) => OnLevelUp?.Invoke(level);

        public static void FireBeaconHit(float damage, float hp, float max) => OnBeaconHit?.Invoke(damage, hp, max);
        public static void FireBeaconHealthChanged(float currentHp, float maxHp) => OnBeaconHealthChanged?.Invoke(currentHp, maxHp);
        public static void FireBeaconDestroyed() => OnBeaconDestroyed?.Invoke();

        public static void FireCardDrafted(int cardId) => OnCardDrafted?.Invoke(cardId);

        public static void FireGameStateChanged(GameState from, GameState to) => OnGameStateChanged?.Invoke(from, to);
        public static void FireRunEnded(RunResult r) => OnRunEnded?.Invoke(r);

        public static void ClearAllSubscriptions()
        {
            OnEnemiesDiedBatch = null;
            OnEnemiesHitBatch = null;
            OnChainArcsBatch = null;
            OnHitSparksBatch = null;
            OnDeathBurstsBatch = null;
            OnWaveStarted = null;
            OnWaveCleared = null;
            OnExpCollected = null;
            OnExpChanged = null;
            OnLevelUp = null;
            OnBeaconHit = null;
            OnBeaconHealthChanged = null;
            OnBeaconDestroyed = null;
            OnCardDrafted = null;
            OnGameStateChanged = null;
            OnRunEnded = null;
        }
    }
}
