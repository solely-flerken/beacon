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

        [SerializeField] private float expPerLevel = 100f;

        public float TotalExp { get; private set; }
        public int CurrentLevel { get; private set; } = 1;
        
        public float ExpPerLevel => expPerLevel;

        /// <summary>
        /// Exp progress toward next level, 0-1.
        /// </summary>
        public float LevelProgress => TotalExp % expPerLevel / expPerLevel;

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
            EventBus.OnExpCollected += OnExpCollected;
        }

        private void OnDisable()
        {
            EventBus.OnExpCollected -= OnExpCollected;
        }

        private void OnExpCollected(float amount)
        {
            AddExp(amount);
        }

        private void AddExp(float amount)
        {
            TotalExp += amount;

            int newLevel = DeriveLevel(TotalExp);
            EventBus.FireExpChanged(CurrentLevel, TotalExp % expPerLevel, expPerLevel);

            if (newLevel <= CurrentLevel) return;

            CurrentLevel = newLevel;
            EventBus.FireLevelUp(CurrentLevel);
        }

        private int DeriveLevel(float exp)
        {
            return (int)(exp / expPerLevel) + 1;   
        }
    }
}