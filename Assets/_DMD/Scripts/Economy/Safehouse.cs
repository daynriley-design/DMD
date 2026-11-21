using System;
using System.Collections.Generic;
using DMD.Core;
using DMD.Heat;
using DMD.World;
using UnityEngine;

namespace DMD.Economy
{
    /// <summary>
    /// La planque de Limete (annexe en parpaings près du Boulevard Lumumba) — état persistant.
    ///  • Coffre : argent liquide (CDF / USD propres / USD usés) et objets compromettants, à l'abri des arrestations.
    ///  • Loyer de survie : tous les X jours, payer pour garder la planque sûre et le voisinage discret.
    ///    Impayé au-delà du délai de grâce → planque compromise (+chaleur à Kinshasa, icône Complice).
    ///  • Arrestation : l'argent sur soi et les objets compromettants portés sont perdus ; le coffre est intact.
    /// Vit dans la scène Persistent_Core. Les objets 3D de la planque (coffre, lit, garde-robe, mur d'enquête)
    /// dans Zone_Limete appellent Safehouse.Instance.
    /// </summary>
    [DisallowMultipleComponent]
    public class Safehouse : MonoBehaviour
    {
        public static Safehouse Instance { get; private set; }

        [Header("Références")]
        [SerializeField] private CurrencyWallet wallet;

        [Header("Accès")]
        [Tooltip("Faux : la planque s'ouvre à la fin de la Mission 2.1 (#SAFEHOUSE:Unlock dans Ink).")]
        [SerializeField] private bool startsUnlocked = false;

        [Header("Loyer de survie (la dette au gardien du quartier : 50 $ tous les 3 jours)")]
        [SerializeField] private Currency rentCurrency = Currency.USD;
        [SerializeField, Min(0)] private int rentAmount = 50;
        [SerializeField, Min(1)] private int rentPeriodDays = 3;
        [Tooltip("Jours de retard tolérés avant que la planque soit compromise.")]
        [SerializeField, Min(0)] private int graceDays = 1;
        [Tooltip("Chaleur ajoutée à Kinshasa quand le voisinage parle.")]
        [SerializeField] private float compromisedHeat = 15f;

        [Header("Coffre (debug)")]
        [SerializeField] private int stashCDF;
        [SerializeField] private int stashUSD;
        [SerializeField] private int stashWornUSD;
        [SerializeField] private List<string> stashItems = new();

        [Header("État (debug)")]
        [SerializeField] private bool unlocked;
        [SerializeField] private int nextRentDay;
        [SerializeField] private bool compromised;

        public event Action OnStashChanged;
        /// <summary>(montant, devise, jour d'échéance)</summary>
        public event Action<int, Currency, int> OnRentDue;
        public event Action<int> OnRentPaid; // prochain jour d'échéance
        public event Action OnCompromised;

        public event Action OnUnlocked;

        public bool IsUnlocked => unlocked;
        public bool IsCompromised => compromised;
        public int NextRentDay => nextRentDay;
        public int StashCDF => stashCDF;
        public int StashUSD => stashUSD;
        public int StashWornUSD => stashWornUSD;
        public IReadOnlyList<string> StashItems => stashItems;
        public int RentAmount => rentAmount;
        public Currency RentCurrency => rentCurrency;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (startsUnlocked) Unlock();
        }

        /// <summary>
        /// Ouvre la planque (sauvegarde, repos, coffre) et lance le compte à rebours du loyer :
        /// premier paiement dû dans rentPeriodDays jours.
        /// </summary>
        public void Unlock()
        {
            if (unlocked) return;
            unlocked = true;
            int today = GameClock.Instance != null ? GameClock.Instance.Day : 1;
            nextRentDay = today + rentPeriodDays;
            Debug.Log($"[Planque] Débloquée. Premier loyer ({CurrencyWallet.Format(rentCurrency, rentAmount)}) dû au jour {nextRentDay}.", this);
            OnUnlocked?.Invoke();
        }

        private void OnEnable()
        {
            GameEvents.ArrestTriggered += HandleArrest;
            GameClock.DayPassed += HandleDayPassed;
        }

        private void OnDisable()
        {
            GameEvents.ArrestTriggered -= HandleArrest;
            GameClock.DayPassed -= HandleDayPassed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // ─── Coffre : argent ──────────────────────────────────────────────

        public bool Deposit(Currency currency, int amount, bool wornBills = false)
        {
            if (!unlocked || wallet == null || amount <= 0) return false;

            if (currency == Currency.USD && wornBills)
            {
                if (wallet.RemoveWorn(amount) < amount) return false;
                stashWornUSD += amount;
            }
            else
            {
                if (wallet.Remove(currency, amount, "Dépôt au coffre") < amount) return false;
                if (currency == Currency.CDF) stashCDF += amount; else stashUSD += amount;
            }

            OnStashChanged?.Invoke();
            return true;
        }

        public bool Withdraw(Currency currency, int amount, bool wornBills = false)
        {
            if (wallet == null || amount <= 0) return false;

            if (currency == Currency.CDF)
            {
                if (stashCDF < amount) return false;
                stashCDF -= amount;
            }
            else if (wornBills)
            {
                if (stashWornUSD < amount) return false;
                stashWornUSD -= amount;
            }
            else
            {
                if (stashUSD < amount) return false;
                stashUSD -= amount;
            }

            wallet.Add(currency, amount, "Retrait du coffre", wornBills);
            OnStashChanged?.Invoke();
            return true;
        }

        // ─── Coffre : objets ──────────────────────────────────────────────

        public bool DepositItem(string itemId)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || !inventory.Remove(itemId)) return false;
            stashItems.Add(itemId);
            OnStashChanged?.Invoke();
            return true;
        }

        public bool WithdrawItem(string itemId)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || !stashItems.Remove(itemId)) return false;
            inventory.Add(itemId);
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>L'objet est-il en possession du joueur (sur lui ou au coffre) ? Utilisé par le mur d'enquête.</summary>
        public static bool Owns(string itemId) =>
            (PlayerInventory.Instance != null && PlayerInventory.Instance.Has(itemId))
            || (Instance != null && Instance.stashItems.Contains(itemId));

        // ─── Loyer ────────────────────────────────────────────────────────

        public bool PayRent()
        {
            if (!unlocked) return false;
            if (wallet == null || !wallet.TrySpend(rentCurrency, rentAmount, "Loyer de la planque")) return false;

            int today = GameClock.Instance != null ? GameClock.Instance.Day : nextRentDay;
            nextRentDay = Mathf.Max(nextRentDay, today) + rentPeriodDays;
            compromised = false;
            Debug.Log($"[Planque] Loyer payé. Prochaine échéance : jour {nextRentDay}.", this);
            OnRentPaid?.Invoke(nextRentDay);
            return true;
        }

        private void HandleDayPassed(int day)
        {
            if (!unlocked || compromised) return;

            if (day == nextRentDay)
            {
                Debug.Log($"[Planque] Loyer dû aujourd'hui : {CurrencyWallet.Format(rentCurrency, rentAmount)}.", this);
                OnRentDue?.Invoke(rentAmount, rentCurrency, nextRentDay);
            }
            else if (day > nextRentDay + graceDays)
            {
                compromised = true;
                Debug.LogWarning("[Planque] Loyer impayé : le voisinage parle. Planque compromise.", this);
                if (HeatManager.Instance != null)
                    HeatManager.Instance.AddHeatToCity(CityId.Kinshasa, compromisedHeat, IdentityFlag.Complice);
                OnCompromised?.Invoke();
            }
        }

        // ─── Arrestation ──────────────────────────────────────────────────

        private void HandleArrest(CityId city, PrisonId prison)
        {
            if (wallet != null)
            {
                var lost = wallet.LoseEverything("Arrestation : argent confisqué");
                Debug.LogWarning($"[Planque] Arrestation : perdu {lost.cdf} FC, {lost.usd} $ propres, {lost.worn} $ usés. Le coffre est intact.", this);
            }
            var seized = PlayerInventory.Instance != null ? PlayerInventory.Instance.ConfiscateCompromising() : new List<string>();
            if (seized.Count > 0) Debug.LogWarning($"[Planque] Objets confisqués : {string.Join(", ", seized)}", this);
        }

        // ─── Tests ────────────────────────────────────────────────────────

        [ContextMenu("DMD Test/Débloquer la planque")] private void TestUnlock() => Unlock();
        [ContextMenu("DMD Test/Déposer 5000 FC")] private void TestDepositCdf() => Deposit(Currency.CDF, 5000);
        [ContextMenu("DMD Test/Retirer 5000 FC")] private void TestWithdrawCdf() => Withdraw(Currency.CDF, 5000);
        [ContextMenu("DMD Test/Payer le loyer")] private void TestPayRent() => PayRent();
    }
}
