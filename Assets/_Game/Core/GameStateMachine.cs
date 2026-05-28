using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Game.Core
{
    public enum GameState
    {
        None,
        MainMenu,
        Paused,
        WaveRunning,
        WaveEnd,
        RunEnd
    }

    public class GameStateMachine : MonoBehaviour
    {
        public static GameStateMachine Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.None;
        public GameState PreviousState { get; private set; } = GameState.None;

        /// <summary>
        /// Simulation runs in these states.
        /// </summary>
        public bool IsSimulationRunning => CurrentState == GameState.WaveRunning;
        
        private static readonly Dictionary<GameState, GameState[]> ValidTransitions = new()
        {
            { GameState.None, new[] { GameState.MainMenu } },
            { GameState.MainMenu, new[] { GameState.WaveRunning } },
            { GameState.WaveRunning, new[] { GameState.WaveEnd, GameState.RunEnd, GameState.Paused } },
            { GameState.Paused, new[] { GameState.WaveRunning } },
            { GameState.WaveEnd, new[] { GameState.WaveRunning, GameState.RunEnd } }, // TODO: WaveEnd -> RunEnd: Needed? Yes: Player might quit in this state | No: No enemies exist here = No death possible
            { GameState.RunEnd, new[] { GameState.None, GameState.WaveRunning } },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Request a state transition.
        /// </summary>
        public bool TransitionTo(GameState next)
        {
            if (!IsValidTransition(CurrentState, next))
            {
                Debug.LogError($"[GameStateMachine] Invalid transition: {CurrentState} -> {next}");
                return false;
            }

            PreviousState = CurrentState;
            CurrentState = next;

            Debug.Log($"[GameStateMachine] {PreviousState} -> {CurrentState}");
            EventBus.FireGameStateChanged(PreviousState, CurrentState);
            return true;
        }
        
        public bool TransitionToPrevious()
        {
            return TransitionTo(PreviousState);
        }
        
        /// <summary>
        /// Force a state without validation.
        /// </summary>
        public void ForceState(GameState state)
        {
            GameState previous = CurrentState;
            CurrentState = state;
            
            Debug.LogWarning($"[GameStateMachine] Forced state: {state} (was {CurrentState})");
            EventBus.FireGameStateChanged(previous, CurrentState);
        }

        private static bool IsValidTransition(GameState from, GameState to)
        {
            return ValidTransitions.TryGetValue(from, out GameState[] allowed) && allowed.Any(validNextState => validNextState == to);
        }
    }
}