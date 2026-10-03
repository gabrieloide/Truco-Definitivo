using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Code.Scripts.Audio;
using Code.Domain;

namespace Code.Core
{
    /// <summary>
    /// Servicio centralizado de audio para Truco Definitivo.
    /// Desacopla la lógica de juego de los IDs de sonido y del AudioManager.
    /// </summary>
    public static class TrucoAudioService
    {
        private static bool _initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var audioManagerInstance = AudioManager.Instance;
                if (audioManagerInstance == null) return;

                var fieldInfo = typeof(AudioManager).GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var database = (AudioDatabase)fieldInfo.GetValue(audioManagerInstance);
                    if (database == null)
                    {
                        var dbAsset = Resources.Load<AudioDatabase>("Audio/AudioDatabase");
                        if (dbAsset != null)
                        {
                            fieldInfo.SetValue(audioManagerInstance, dbAsset);
                            database = dbAsset;
                        }
                    }

                    if (database != null)
                    {
                        foreach (var audioData in database.audioDataList)
                        {
                            if (audioData.id == "backyard_truco" || audioData.id == "main_menu_truco")
                            {
                                audioData.loop = true;
                            }
                        }
                    }
                }

                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TrucoAudioService] Nota al inicializar audio: {ex.Message}");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                var audioMgr = AudioManager.Instance;
                if (audioMgr == null) return;

                if (scene.name == "GameScene")
                {
                    audioMgr.PlayMusic("backyard_truco", crossfade: true, duration: 1.5f);
                }
                else if (scene.name == "MainMenu" || scene.name == "LobbyScene")
                {
                    audioMgr.PlayMusic("main_menu_truco", crossfade: true, duration: 1.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TrucoAudioService] Error al cambiar música de escena: {ex.Message}");
            }
        }

        public static void PlaySFX(string soundId, float volumeMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(soundId)) return;
            try
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(soundId);
                }
            }
            catch
            {
                // Fallback silencioso si el clip o el AudioSource no están listos
            }
        }

        public static void PlayCardDeal() => PlaySFX("card_deal_swoosh");
        public static void PlayTurnAlert() => PlaySFX("turn_alert_ping");
        public static void PlayParda() => PlaySFX("parda_tie_dissonance");
        public static void PlayTrickWon() => PlaySFX("trick_won_coin");
        public static void PlayScoreChalk() => PlaySFX("score_add_chalk");
        public static void PlayBuenasFanfare() => PlaySFX("score_buenas_fanfare");
        public static void PlayQuieroPositive() => PlaySFX("confirm_quiero_positive");
        public static void PlayNoQuieroNegative() => PlaySFX("decline_noquiero_neg");

        public static void PlayMatchResult(bool isHumanVictory)
        {
            if (isHumanVictory)
                PlaySFX("match_victory_melody");
            else
                PlaySFX("match_defeat_sadness");
        }

        public static void PlayAnnounceSFX(AnnounceState state, int level = 1)
        {
            string sfxId = "";
            switch (state)
            {
                case AnnounceState.Envido:
                    sfxId = level switch
                    {
                        <= 1 => "canto_envido_wood",
                        2 => "canto_realenvido_wood",
                        _ => "canto_faltaenvido_sweep"
                    };
                    break;
                case AnnounceState.Truco:
                    sfxId = level switch
                    {
                        <= 1 => "canto_truco_warn",
                        2 => "canto_retruco_raise",
                        _ => "canto_valecuatro_siren"
                    };
                    break;
                case AnnounceState.Flor:
                    sfxId = level switch
                    {
                        <= 1 => "canto_flor_bell",
                        2 => "canto_contraflor_chime",
                        _ => "canto_contraflor_resto_blast"
                    };
                    break;
                case AnnounceState.ALey:
                    sfxId = "canto_aley_gong";
                    break;
            }

            if (!string.IsNullOrEmpty(sfxId))
            {
                PlaySFX(sfxId);
            }
        }
    }
}
