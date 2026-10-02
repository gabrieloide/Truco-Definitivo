using System;
using Code.Cards;

namespace Code.Domain
{
    /// <summary>
    /// Tipos de respuestas ante un canto (Envido, Truco, Flor, etc.)
    /// </summary>
    public enum ResponseType : byte
    {
        Quiero = 0,
        NoQuiero = 1,
        Mas = 2
    }

    /// <summary>
    /// Identificador fuertemente tipado de equipo
    /// </summary>
    public enum TeamId : byte
    {
        Team1 = 0,
        Team2 = 1,
        None = 255
    }

    /// <summary>
    /// Resultado de una baza
    /// </summary>
    public enum TrickResult : byte
    {
        Team1Wins = 0,
        Team2Wins = 1,
        Tie = 2
    }

    /// <summary>
    /// Fases principales de una mano de Truco
    /// </summary>
    public enum HandPhase : byte
    {
        Dealing,
        PlayingCards,
        AnnouncementPending,
        HandResolution,
        MatchFinished
    }
}

// Mantener AnnounceState en el namespace global para compatibilidad total con el código existente
public enum AnnounceState
{
    Envido,
    Truco,
    Flor,
    ALey,
    None
}
