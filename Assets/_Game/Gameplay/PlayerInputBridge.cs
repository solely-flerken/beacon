using _Game.Components;
using _Game.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Game.Gameplay
{
    /// <summary>
    /// Writes the here defined input actions to the player entity each frame for PlayerMovementSystem to consume.
    /// </summary>
    public class PlayerInputBridge : MonoBehaviour
    {
        private EntityManager _entityManager;
        private EntityQuery _beaconQuery;
        
        private InputAction _moveAction;

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _beaconQuery = _entityManager.CreateEntityQuery(typeof(PlayerTag), typeof(PlayerMoveInput));

            _moveAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Move") : null;
            if (_moveAction == null)
            {
                Debug.LogError("[PlayerInputBridge] 'Player/Move' action not found. Assign the project-wide Input Actions asset.");
            }
            else if (!_moveAction.enabled)
            {
                _moveAction.Enable();
            }
        }

        private void Update()
        {
            if (_beaconQuery.IsEmptyIgnoreFilter || _moveAction == null) return;

            bool waveRunning = GameStateMachine.Instance && GameStateMachine.Instance.CurrentState == GameState.WaveRunning;

            Vector2 velocity = waveRunning ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _beaconQuery.SetSingleton(new PlayerMoveInput { Move = new float2(velocity.x, velocity.y) });
        }
    }
}