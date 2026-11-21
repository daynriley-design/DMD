using System;
using DMD.Economy;
using DMD.Heat;
using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Téléphone du joueur au Chapitre 2.
    ///  • Le smartphone personnel est traçable : tant qu'il n'est pas détruit, chaque heure de jeu
    ///    passée à Kinshasa fait monter la chaleur (Mission 2.1 « Zéro Trace »).
    ///  • Le téléphone « torche » (à touches) + cartes SIM jetables : pas de GPS, juste appels et SMS.
    ///    Chaque SIM a une durée de vie ; au-delà, elle devient elle aussi repérable.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPhone : MonoBehaviour
    {
        public static PlayerPhone Instance { get; private set; }

        [SerializeField] private CurrencyWallet wallet;

        [Header("Smartphone traçable")]
        [SerializeField] private bool startsWithSmartphone = true;
        [Tooltip("Chaleur ajoutée à Kinshasa par heure de jeu tant que le smartphone est allumé.")]
        [SerializeField] private float smartphoneHeatPerHour = 2f;

        [Header("Téléphone torche & SIM (prix en CDF)")]
        [SerializeField, Min(0)] private int burnerPriceCDF = 25000;
        [SerializeField, Min(0)] private int simPriceCDF = 2000;
        [Tooltip("Jours d'utilisation d'une SIM avant qu'elle devienne repérable.")]
        [SerializeField, Min(1)] private int simSafeDays = 3;

        [Header("État (debug)")]
        [SerializeField] private bool hasSmartphone;
        [SerializeField] private bool hasBurner;
        [SerializeField] private int spareSims;
        [SerializeField] private int activeSimDaysUsed;
        [SerializeField] private bool simActive;

        public event Action OnPhoneStateChanged;

        public bool HasSmartphone => hasSmartphone;
        public bool HasBurner => hasBurner;
        public bool SimActive => simActive;
        public int SpareSims => spareSims;
        public bool SimIsBurnt => simActive && activeSimDaysUsed >= simSafeDays;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            hasSmartphone = startsWithSmartphone;
        }

        private void OnEnable()
        {
            GameClock.HourTick += HandleHourTick;
            GameClock.DayPassed += HandleDayPassed;
        }

        private void OnDisable()
        {
            GameClock.HourTick -= HandleHourTick;
            GameClock.DayPassed -= HandleDayPassed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>Jeter le smartphone dans un canal ou un terrain vague.</summary>
        public void DestroySmartphone()
        {
            if (!hasSmartphone) return;
            hasSmartphone = false;
            Debug.Log("[Téléphone] Smartphone détruit : plus de traçage.", this);
            OnPhoneStateChanged?.Invoke();
        }

        public bool BuyBurner()
        {
            if (hasBurner) return true;
            if (wallet == null || !wallet.TrySpend(Currency.CDF, burnerPriceCDF, "Téléphone torche")) return false;
            hasBurner = true;
            OnPhoneStateChanged?.Invoke();
            return true;
        }

        /// <summary>Reçoit un téléphone torche avec une SIM déjà activée (prix négocié et payé dans le dialogue).</summary>
        public void GiveBurnerWithSim()
        {
            hasBurner = true;
            simActive = true;
            activeSimDaysUsed = 0;
            Debug.Log("[Téléphone] Téléphone torche + SIM jetable activée.", this);
            OnPhoneStateChanged?.Invoke();
        }

        public bool BuySim()
        {
            if (wallet == null || !wallet.TrySpend(Currency.CDF, simPriceCDF, "Carte SIM jetable")) return false;
            spareSims++;
            if (!simActive) SwapSim();
            OnPhoneStateChanged?.Invoke();
            return true;
        }

        /// <summary>Jette la SIM active et en insère une neuve.</summary>
        public bool SwapSim()
        {
            if (spareSims <= 0) return false;
            spareSims--;
            simActive = true;
            activeSimDaysUsed = 0;
            OnPhoneStateChanged?.Invoke();
            return true;
        }

        /// <summary>Peut-on appeler/recevoir un SMS d'un contact ?</summary>
        public bool CanCommunicate => hasBurner && simActive;

        private void HandleHourTick(int day, int hour)
        {
            var heat = HeatManager.Instance;
            if (heat == null || heat.CurrentCity != CityId.Kinshasa) return;

            if (hasSmartphone) heat.AddHeat(smartphoneHeatPerHour);
            else if (SimIsBurnt) heat.AddHeat(smartphoneHeatPerHour * 0.5f);
        }

        private void HandleDayPassed(int day)
        {
            if (!simActive) return;
            activeSimDaysUsed++;
            if (activeSimDaysUsed == simSafeDays)
                Debug.LogWarning("[Téléphone] La SIM a trop servi : elle devient repérable. Changes-en.", this);
        }

        [ContextMenu("DMD Test/Détruire le smartphone")] private void TestDestroy() => DestroySmartphone();
        [ContextMenu("DMD Test/Acheter téléphone torche + SIM")] private void TestBuy() { BuyBurner(); BuySim(); }
    }
}
