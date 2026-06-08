using UnityEngine;

namespace _Game.Config
{
    [CreateAssetMenu(menuName = "LastBeacon/Beacon Config", fileName = "BeaconConfig")]
    public class BeaconConfig : ScriptableObject
    {
        [Header("Stats")] 
        public float maxHp = 100f;
        public float arrivalRadius = 1.2f;

        [Header("Movement")]
        public float moveSpeed = 10f;

        [Header("Visual")]
        public Color fullHpColor = Color.cyan;
        public Color lowHpColor = Color.red;
    }
}