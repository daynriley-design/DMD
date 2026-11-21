using System;
using DMD.Core;
using DMD.Heat;
using UnityEngine;

namespace DMD.World
{
    public enum DayPhase
    {
        Matin,   // 06h – 11h
        Journee, // 11h – 18h
        Soir,    // 18h – 21h (tombée de la nuit : ngandas, embouteillages)
        Nuit     // 21h – 06h
    }

    /// <summary>
    /// Horloge du jeu : jour + heure. Le temps avance en continu (option) et se saute en se reposant à la planque.
    ///  • Événements statiques HourTick / DayPassed / PhaseChanged (les systèmes s'abonnent sans référence).
    ///  • Chaque jour écoulé refroidit les villes quittées (HeatManager.ApplyPassiveCooldownToAllInactive).
    ///  • L'horloge se fige pendant les dialogues.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        /// <summary>(jour, heure entière 0–23)</summary>
        public static event Action<int, int> HourTick;
        /// <summary>(nouveau jour)</summary>
        public static event Action<int> DayPassed;
        public static event Action<DayPhase> PhaseChanged;

        [Header("Départ")]
        [SerializeField, Min(1)] private int startDay = 1;
        [SerializeField, Range(0f, 23.99f)] private float startHour = 7f;

        [Header("Écoulement")]
        [Tooltip("Secondes réelles pour 1 heure de jeu. 0 = le temps n'avance qu'en se reposant.")]
        [SerializeField, Min(0f)] private float realSecondsPerGameHour = 60f;
        [SerializeField] private bool pauseDuringDialogue = true;

        [Header("État (debug)")]
        [SerializeField] private int day;
        [SerializeField] private float hour;

        private bool inDialogue;

        public int Day => day;
        public float Hour => hour;
        public DayPhase Phase => PhaseFor(hour);
        public bool IsNight => hour >= 19f || hour < 6f;
        public string TimeLabel => $"Jour {day} — {Mathf.FloorToInt(hour):00}h{Mathf.FloorToInt(hour % 1f * 60f):00}";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            day = startDay;
            hour = startHour;
        }

        private void OnEnable()
        {
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.DialogueEnded += OnDialogueEnded;
        }

        private void OnDisable()
        {
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.DialogueEnded -= OnDialogueEnded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            HourTick = null;
            DayPassed = null;
            PhaseChanged = null;
        }

        private void Update()
        {
            if (realSecondsPerGameHour <= 0f || (pauseDuringDialogue && inDialogue)) return;
            AdvanceHours(Time.deltaTime / realSecondsPerGameHour);
        }

        /// <summary>Fait avancer le temps (les heures et jours franchis déclenchent leurs événements).</summary>
        public void AdvanceHours(float hours)
        {
            if (hours <= 0f) return;

            float remaining = hours;
            while (remaining > 0f)
            {
                float toNextHour = Mathf.Floor(hour) + 1f - hour;
                if (remaining < toNextHour)
                {
                    hour += remaining;
                    return;
                }

                var oldPhase = Phase;
                remaining -= toNextHour;
                hour = Mathf.Floor(hour) + 1f;

                if (hour >= 24f)
                {
                    hour = 0f;
                    day++;
                    if (HeatManager.Instance != null) HeatManager.Instance.ApplyPassiveCooldownToAllInactive(1);
                    DayPassed?.Invoke(day);
                }

                HourTick?.Invoke(day, Mathf.FloorToInt(hour));
                if (Phase != oldPhase) PhaseChanged?.Invoke(Phase);
            }
        }

        /// <summary>Se reposer jusqu'à une heure donnée (ex. 19 = tombée de la nuit). Passe au lendemain si besoin.</summary>
        public void RestUntil(float targetHour)
        {
            targetHour = Mathf.Repeat(targetHour, 24f);
            float delta = targetHour - hour;
            if (delta <= 0f) delta += 24f;
            AdvanceHours(delta);
            Debug.Log($"[Horloge] Repos jusqu'à {TimeLabel}.", this);
        }

        public static DayPhase PhaseFor(float h)
        {
            if (h >= 6f && h < 11f) return DayPhase.Matin;
            if (h >= 11f && h < 18f) return DayPhase.Journee;
            if (h >= 18f && h < 21f) return DayPhase.Soir;
            return DayPhase.Nuit;
        }

        /// <summary>Vrai si l'heure est dans la plage [from, to) — gère les plages qui passent minuit (21 → 4).</summary>
        public static bool IsWithin(float h, float from, float to) =>
            from <= to ? h >= from && h < to : h >= from || h < to;

        private void OnDialogueStarted() => inDialogue = true;
        private void OnDialogueEnded() => inDialogue = false;

        [ContextMenu("DMD Test/Avancer de 6 heures")] private void TestSixHours() => AdvanceHours(6f);
        [ContextMenu("DMD Test/Se reposer jusqu'à 19h")] private void TestRestEvening() => RestUntil(19f);
        [ContextMenu("DMD Test/Passer 1 jour")] private void TestOneDay() => AdvanceHours(24f);
    }
}
