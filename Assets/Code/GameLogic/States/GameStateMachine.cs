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
                _currentState.ExitState();
            }

            _currentState = newState;

            if (_currentState != null)
            {
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

        private void OnDestroy()
        {
            if (_currentState != null)
            {
                _currentState.ExitState();
            }
        }
    }
}
