using System;

namespace Code.Core
{
    public static class GameEventManager
    {
        // Eventos de la Interfaz de Usuario (UI -> Lógica)
        public static event Action<string> OnAnnounceButtonClicked;
        public static event Action OnAcceptButtonClicked;
        public static event Action OnDeclineButtonClicked;
        public static event Action OnMoreButtonClicked;

        // Métodos para emitir eventos desde la UI
        public static void EmitAnnounceButtonClicked(string announceType)
        {
            OnAnnounceButtonClicked?.Invoke(announceType);
        }

        public static void EmitAcceptButtonClicked()
        {
            OnAcceptButtonClicked?.Invoke();
        }

        public static void EmitDeclineButtonClicked()
        {
            OnDeclineButtonClicked?.Invoke();
        }

        public static void EmitMoreButtonClicked()
        {
            OnMoreButtonClicked?.Invoke();
        }
    }
}
