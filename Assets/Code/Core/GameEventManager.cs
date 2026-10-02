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

        // Eventos Fuertemente Tipados (UI -> Lógica)
        public static event Action<AnnounceState> OnAnnounceStateClicked;
        public static event Action<Code.Domain.ResponseType> OnResponseButtonClicked;

        // Métodos para emitir eventos desde la UI
        public static void EmitAnnounceButtonClicked(string announceType)
        {
            OnAnnounceButtonClicked?.Invoke(announceType);
            if (Enum.TryParse<AnnounceState>(announceType, true, out var parsed))
            {
                OnAnnounceStateClicked?.Invoke(parsed);
            }
        }

        public static void EmitAnnounceStateClicked(AnnounceState state)
        {
            OnAnnounceStateClicked?.Invoke(state);
            OnAnnounceButtonClicked?.Invoke(state.ToString());
        }

        public static void EmitAcceptButtonClicked()
        {
            OnAcceptButtonClicked?.Invoke();
            OnResponseButtonClicked?.Invoke(Code.Domain.ResponseType.Quiero);
        }

        public static void EmitDeclineButtonClicked()
        {
            OnDeclineButtonClicked?.Invoke();
            OnResponseButtonClicked?.Invoke(Code.Domain.ResponseType.NoQuiero);
        }

        public static void EmitMoreButtonClicked()
        {
            OnMoreButtonClicked?.Invoke();
            OnResponseButtonClicked?.Invoke(Code.Domain.ResponseType.Mas);
        }
    }
}
