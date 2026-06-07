using _Game.Authoring;
using _Game.Components;
using _Game.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Gameplay.Abilities
{
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        private Ability[] _slots;
        
        private AbilityContext _ctx;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _slots = new Ability[BeaconLoadout.AbilitySlotCount];
            
            _ctx = new AbilityContext
            {
                EntityManager = em,
                BeaconQuery = em.CreateEntityQuery(typeof(BeaconStats), typeof(LocalTransform)),
                PrefabQuery = em.CreateEntityQuery(typeof(PrefabRefs)),
            };
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _ctx.BeaconQuery.Dispose();
                _ctx.PrefabQuery.Dispose();
            }

            Instance = null;
        }

        private void Update()
        {
            if (_slots == null || GameStateMachine.Instance.CurrentState != GameState.WaveRunning) return;

            foreach (Ability slot in _slots)
            {
                slot?.Tick(Time.deltaTime);
            }
        }
        
        public void SetupFromLoadout(BeaconLoadout loadout)
        {
            for (int i = 0; i < BeaconLoadout.AbilitySlotCount; i++)
            {
                _slots[i] = loadout.GetAbility(i) switch
                {
                    AbilityType.Flare => new FlareAbility(),
                    AbilityType.Stun => new StunAbility(),
                    _ => null,
                };
                
                _slots[i]?.Reset();
            }
        }

        public bool TryActivate(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return false;
            return _slots[slotIndex]?.TryUse(_ctx) ?? false;
        }

        public Ability GetSlot(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return null;
            return _slots[slotIndex];
        }

        public void Reset()
        {
            if (_slots == null) return;
            
            foreach (Ability slot in _slots)
            {
                slot?.Reset();
            }
        }
    }
}