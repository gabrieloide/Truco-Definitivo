using UnityEngine;

namespace Code.GameLogic.States
{
    public class GameStateMachine : MonoBehaviour
    {
        private GameState _currentState;

        // Propiedad pública para depuración o consulta
        public GameState CurrentState => _currentState;

        public void ChangeState(GameState newState)
        {
            if (_currentState != null)
            {
                Debug.Log($"[StateMachine] Saliendo de estado: {_currentState.GetType().Name}");
                _currentState.ExitState();
            }

            _currentState = newState;

            if (_currentState != null)
            {
                Debug.Log($"[StateMachine] Entrando a estado: {_currentState.GetType().Name}");
                _currentState.Initialize(this);
                _currentState.EnterState();
            }
        }

        private void Update()
        {
            if (_currentState != null)
            {
                _currentState.UpdateState();
            }
        }
    }
}
