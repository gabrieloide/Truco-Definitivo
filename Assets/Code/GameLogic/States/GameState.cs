namespace Code.GameLogic.States
{
    public abstract class GameState
    {
        protected GameStateMachine StateMachine;

        public virtual void Initialize(GameStateMachine stateMachine)
        {
            StateMachine = stateMachine;
        }

        public abstract void EnterState();
        public abstract void UpdateState();
        public abstract void ExitState();
    }
}
