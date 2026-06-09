using System;
using _Game.Authoring;
using _Game.Cards;
using _Game.Components;
using _Game.Core;
using _Game.Gameplay.Abilities;
using _Game.Gameplay.Waves;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Game.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private RunManager runManager;
        [SerializeField] private AbilityManager abilityManager;

        private EntityManager _entityManager;
        private EntityQuery _enemyCountQuery;
        private EntityQuery _projectileCountQuery;

        private SimulationSystemGroup _simGroup;

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _enemyCountQuery = _entityManager.CreateEntityQuery(typeof(EnemyTag));
            _projectileCountQuery = _entityManager.CreateEntityQuery(typeof(ProjectileTag));

            _simGroup = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystemGroup>();
        }

        private void Start()
        {
            // Warm up archetypes so spawns don't lead to stutter
            using EntityQuery prefabQuery = _entityManager.CreateEntityQuery(typeof(PrefabRefs));
            if (!prefabQuery.IsEmpty)
            {
                PrefabRefs prefabs = prefabQuery.GetSingleton<PrefabRefs>();
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.Projectile));
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.Flare));
                _entityManager.DestroyEntity(_entityManager.Instantiate(prefabs.ExpOrb));
            }

            // Warm up enemy archetypes from catalog
            using EntityQuery catalogQuery = _entityManager.CreateEntityQuery(typeof(EnemyTypeCatalogEntry));
            if (!catalogQuery.IsEmpty)
            {
                DynamicBuffer<EnemyTypeCatalogEntry> catalogBuffer = _entityManager.GetBuffer<EnemyTypeCatalogEntry>(catalogQuery.GetSingletonEntity());

                using NativeArray<EnemyTypeCatalogEntry> catalogData = catalogBuffer.ToNativeArray(Allocator.Temp);
                for (int i = 0; i < catalogData.Length; i++)
                {
                    Entity instance = _entityManager.Instantiate(catalogData[i].Prefab);
                    _entityManager.DestroyEntity(instance);
                }
            }

            SetSimulation(false);

            EventBus.OnBeaconDestroyed += OnBeaconDestroyed;
            EventBus.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            EventBus.ClearAllSubscriptions();

            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _enemyCountQuery.Dispose();
                _projectileCountQuery.Dispose();
            }
        }

        private void Update()
        {
            GameState state = GameStateMachine.Instance.CurrentState;

            switch (state)
            {
                case GameState.None:
                    GameStateMachine.Instance.TransitionTo(GameState.MainMenu);
                    break;
                
                case GameState.MainMenu:
                    if (Keyboard.current.anyKey.wasPressedThisFrame)
                        runManager.StartRun();
                    break;

                case GameState.WaveRunning:
                    if (Keyboard.current.qKey.wasPressedThisFrame)
                        abilityManager?.TryActivate(0);
                    if (Keyboard.current.eKey.wasPressedThisFrame)
                        abilityManager?.TryActivate(1);
                    if (Keyboard.current.pKey.wasPressedThisFrame)
                        GameStateMachine.Instance.TransitionTo(GameState.Paused);
                    if (Keyboard.current.mKey.wasPressedThisFrame)
                        ApplyTestModifier();
                    if (Keyboard.current.bKey.wasPressedThisFrame)
                        ApplyTestBeaconModifier();
                    if (Keyboard.current.nKey.wasPressedThisFrame)
                        ApplyTestEnemyModifier(RoleMask.Basic);
                    if (Keyboard.current.tabKey.wasPressedThisFrame)
                        CardDraftManager.Instance.ToggleDrawer();
                    if (Keyboard.current.lKey.wasPressedThisFrame)
                        EventBus.FireExpCollected(1000);
                    break;

                case GameState.WaveEnd:
                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                        GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
                    if (Keyboard.current.tabKey.wasPressedThisFrame)
                        CardDraftManager.Instance.ToggleDrawer();
                    break;

                case GameState.Paused:
                    if (Keyboard.current.pKey.wasPressedThisFrame)
                        GameStateMachine.Instance.TransitionTo(GameState.WaveRunning);
                    break;
                
                case GameState.RunEnd:
                    if (Keyboard.current.rKey.wasPressedThisFrame)
                        runManager.StartRun();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnGameStateChanged(GameState from, GameState to)
        {
            SetSimulation(to is GameState.WaveRunning or GameState.WaveEnd);
        }

        private void OnBeaconDestroyed()
        {
            runManager.Cleanup();
            GameStateMachine.Instance.TransitionTo(GameState.RunEnd);
        }

        private void SetSimulation(bool enable)
        {
            _simGroup.Enabled = enable;
        }

        private void ApplyTestModifier()
        {
            CardData cardData = CardLibrary.RapidFire;
            CardModifierApplier.ApplyCard(cardData, _entityManager);
            Debug.Log("[GameManager] Applied Global 2x Fire Rate Modifier to Beacon.");
        }
        
        private void ApplyTestBeaconModifier()
        {
            CardData cardData = CardLibrary.ReinforcedArmour;
            CardModifierApplier.ApplyCard(cardData, _entityManager);
            Debug.Log("[GameManager] Applied Beacon +50% MaxHp modifier.");
        }

        private void ApplyTestEnemyModifier(RoleMask type)
        {
            CardData cardData = CardLibrary.SlowBasicEnemies;
            CardModifierApplier.ApplyCard(cardData, _entityManager);
            Debug.Log($"[GameManager] Applied 0.5x Speed modifier to EnemyType.{type}.");
        }

        private void OnGUI()
        {
            int enemyCount = _enemyCountQuery.CalculateEntityCount();
            int projectileCount = _projectileCountQuery.CalculateEntityCount();
            int waveNumber = WaveManager.Instance.CurrentWave;
            GUI.Box(new Rect(10, 10, 150, 25), $"Enemies: {enemyCount}");
            GUI.Box(new Rect(10, 40, 150, 25), $"Projectiles: {projectileCount}");
            GUI.Box(new Rect(10, 70, 150, 25), $"Wave {waveNumber}");
            
            if (ExpManager.Instance != null)
            {
                int level = ExpManager.Instance.CurrentLevel;
                float progress = ExpManager.Instance.LevelProgress;
                float totalExp = ExpManager.Instance.TotalExp;

                GUI.Box(new Rect(10, 100, 150, 25), $"Level: {level} - Exp: {(int)totalExp}");

                // Progress bar
                GUI.Box(new Rect(10, 130, 150, 16), "");
                if (progress > 0) GUI.Box(new Rect(10, 130, 150 * progress, 16), "");
            }

            // Stats
            if (RunStatsManager.Instance)
            {
                int y = 200;
                foreach (Entity e in RunStatsManager.Instance.WeaponStats.Keys)
                {
                    RunStatsManager.WeaponRecord r = RunStatsManager.Instance.WeaponStats[e];
                    GUI.Box(new Rect(10, y, 150, 25), $"{e.Index} {r.Raw:0}");
                    y += 30;
                }
            }

            // Ability slots
            if (AbilityManager.Instance)
            {
                const float slotW = 120f;
                const float slotH = 25f;
                const float gap = 5f;
                const float totalW = BeaconLoadout.AbilitySlotCount * slotW + (BeaconLoadout.AbilitySlotCount - 1) * gap;
                float startX = (Screen.width - totalW) * 0.5f;
                float slotY = Screen.height - 65;

                for (int i = 0; i < BeaconLoadout.AbilitySlotCount; i++)
                {
                    Ability slot = AbilityManager.Instance.GetSlot(i);
                    if (slot == null) continue;

                    string label = slot.Charges > 0
                        ? $"Ability {i} - {slot.Charges}/{slot.EffectiveMaxCharges}"
                        : $"Ability {i} - {slot.RechargeTimer:0.0}s";

                    GUI.Box(new Rect(startX + i * (slotW + gap), slotY, slotW, slotH), label);
                }
            }

            // Controls hint
            string hint = GameStateMachine.Instance?.CurrentState switch
            {
                GameState.MainMenu => "Any key to start",
                GameState.WaveRunning => "Ability1(Q), Ability2(E), Pause(P), Cards(Tab), Weapon Mod(M), Beacon Mod(B), Enemy Mod(N), Grant Exp(L)",
                GameState.WaveEnd => "Next wave(Spc), Cards(Tab)",
                GameState.RunEnd => "Restart(R)",
                GameState.Paused => "Unpause(P)",
                _ => ""
            };
            GUI.Box(new Rect(10, Screen.height - 35, 400, 25), hint);

            GUI.Box(new Rect(10, Screen.height - 65, 400, 25), $"State: {GameStateMachine.Instance?.CurrentState.ToString()}");
        }
    }
}