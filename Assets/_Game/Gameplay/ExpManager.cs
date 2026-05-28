using _Game.Core;
using UnityEngine;

namespace _Game.Gameplay
{
    /// <summary>
    /// Accumulates exp and derives level from total exp.
    /// </summary>
    public class ExpManager : MonoBehaviour
    {
        public static ExpManager Instance { get; private set; }

        [SerializeField] private float baseExpCost = 70f;
        [SerializeField] private float growthFactor = 1.18f;

        public float TotalExp { get; private set; }
        public int CurrentLevel { get; private set; } = 1;

        /// <summary>
        /// Exp progress toward next level, 0–1.
        /// </summary>
        public float LevelProgress => (TotalExp - ExpToLevel(CurrentLevel)) / CostToNextLevel(CurrentLevel);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.OnExpCollected += AddExp;
        }

        private void OnDisable()
        {
            EventBus.OnExpCollected -= AddExp;
        }

        private void AddExp(float amount)
        {
            TotalExp += amount;

            int newLevel = DeriveLevel(TotalExp);

            while (CurrentLevel < newLevel)
            {
                CurrentLevel++;
                EventBus.FireLevelUp(CurrentLevel);
            }

            EventBus.FireExpChanged(CurrentLevel, TotalExp - ExpToLevel(CurrentLevel), CostToNextLevel(CurrentLevel));
        }

        public void Reset()
        {
            TotalExp = 0f;
            CurrentLevel = 1;
            EventBus.FireExpChanged(1, 0f, CostToNextLevel(1));
        }

        private int DeriveLevel(float exp)
        {
            int level = 1;

            while (exp >= ExpToLevel(level + 1))
            {
                level++;
            }

            return level;
        }

        /// <summary>
        /// Cumulative exp required to reach `level`
        /// (i.e. sum of costs for levels 1..level-1).
        /// </summary>
        private float ExpToLevel(int level)
        {
            if (level <= 1) return 0f;
            
            return baseExpCost * (Mathf.Pow(growthFactor, level - 1) - 1f) / (growthFactor - 1f);
        }

        /// <summary>
        /// Cost to go from `level` to `level + 1`: baseExpCost * growthFactor^(level-1).
        /// </summary>
        private float CostToNextLevel(int level)
        {
            return baseExpCost * Mathf.Pow(growthFactor, level - 1);
        }
    }
}