using System;
using UnityEngine;

namespace DMD.Economy
{
    public enum Currency
    {
        CDF, // Francs congolais : petites dépenses (taxi, wewa, nourriture, crédit)
        USD  // Dollars : pots-de-vin, faux papiers, informateurs sérieux
    }

    /// <summary>
    /// Argent liquide SUR SOI (sans compte bancaire). Asset partagé entre la logique et l'UI.
    ///  • CDF pour le quotidien, USD pour les grosses transactions.
    ///  • Les USD sont séparés en billets propres et billets usés : les PNJ refusent les billets usés,
    ///    seul un cambiste les reprend (avec décote).
    ///  • L'argent du coffre de la planque n'est PAS ici (voir Safehouse) : en cas d'arrestation,
    ///    seul cet argent-là est perdu.
    /// L'état d'exécution n'est pas sauvegardé dans l'asset : il repart des valeurs de départ à chaque partie.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencyWallet", menuName = "DMD/Économie/Currency Wallet")]
    public class CurrencyWallet : ScriptableObject
    {
        [Header("Argent de départ (début du Chapitre 2)")]
        [Min(0)] public int startCDF = 15000;
        [Min(0)] public int startUSD = 20;
        [Min(0)] public int startWornUSD = 10;

        [Header("Change de rue (cambistes de Victoire)")]
        [Tooltip("Taux de rue : nombre de CDF pour 1 USD.")]
        [Min(1)] public int cdfPerUsd = 2850;
        [Tooltip("Valeur d'un billet usé par rapport à un billet propre.")]
        [Range(0f, 1f)] public float wornUsdValueRatio = 0.7f;
        [Tooltip("Commission du cambiste sur chaque change.")]
        [Range(0f, 0.2f)] public float cambistCommission = 0.03f;

        [NonSerialized] private int cashCDF;
        [NonSerialized] private int cashUSD;
        [NonSerialized] private int wornUSD;
        [NonSerialized] private bool initialized;

        /// <summary>(devise, ancien montant, nouveau montant, raison). Pour USD : billets propres seulement.</summary>
        public event Action<Currency, int, int, string> OnCashChanged;
        /// <summary>Billets usés modifiés : (ancien, nouveau).</summary>
        public event Action<int, int> OnWornUsdChanged;
        /// <summary>Paiement refusé : (devise, montant, raison lisible).</summary>
        public event Action<Currency, int, string> OnPaymentRefused;

        public int CashCDF { get { EnsureInitialized(); return cashCDF; } }
        public int CashUSD { get { EnsureInitialized(); return cashUSD; } }
        public int WornUSD { get { EnsureInitialized(); return wornUSD; } }

        public int Get(Currency currency) => currency == Currency.CDF ? CashCDF : CashUSD;

        public void ResetToStart()
        {
            cashCDF = startCDF;
            cashUSD = startUSD;
            wornUSD = startWornUSD;
            initialized = true;
        }

        public bool CanAfford(Currency currency, int amount) => amount <= Get(currency);

        /// <summary>Dépense. En USD, seuls les billets propres sont acceptés.</summary>
        public bool TrySpend(Currency currency, int amount, string reason)
        {
            EnsureInitialized();
            if (amount <= 0) return true;

            if (!CanAfford(currency, amount))
            {
                string why = currency == Currency.USD && CashUSD + WornUSD >= amount
                    ? "Billets trop usés : refusés. Passe chez un cambiste."
                    : $"Pas assez de {Symbol(currency)}.";
                OnPaymentRefused?.Invoke(currency, amount, why);
                return false;
            }

            SetCash(currency, Get(currency) - amount, reason);
            return true;
        }

        public void Add(Currency currency, int amount, string reason, bool wornBills = false)
        {
            EnsureInitialized();
            if (amount <= 0) return;

            if (currency == Currency.USD && wornBills) SetWorn(wornUSD + amount);
            else SetCash(currency, Get(currency) + amount, reason);
        }

        /// <summary>Retire de l'argent sans condition (vol, perte). Renvoie le montant réellement retiré.</summary>
        public int Remove(Currency currency, int amount, string reason)
        {
            EnsureInitialized();
            int removed = Mathf.Clamp(amount, 0, Get(currency));
            if (removed > 0) SetCash(currency, Get(currency) - removed, reason);
            return removed;
        }

        /// <summary>Retire des billets usés (dépôt au coffre, vol). Renvoie le nombre réellement retiré.</summary>
        public int RemoveWorn(int amount)
        {
            EnsureInitialized();
            int removed = Mathf.Clamp(amount, 0, wornUSD);
            if (removed > 0) SetWorn(wornUSD - removed);
            return removed;
        }

        /// <summary>Cambiste : USD → CDF. Les billets usés sont repris avec décote.</summary>
        public bool TryExchangeUsdToCdf(int usd, bool useWornBills)
        {
            EnsureInitialized();
            if (usd <= 0) return false;

            int available = useWornBills ? wornUSD : cashUSD;
            if (available < usd)
            {
                OnPaymentRefused?.Invoke(Currency.USD, usd, "Pas assez de dollars à changer.");
                return false;
            }

            float value = usd * cdfPerUsd * (useWornBills ? wornUsdValueRatio : 1f) * (1f - cambistCommission);
            if (useWornBills) SetWorn(wornUSD - usd);
            else SetCash(Currency.USD, cashUSD - usd, "Change chez le cambiste");
            SetCash(Currency.CDF, cashCDF + Mathf.FloorToInt(value), "Change chez le cambiste");
            return true;
        }

        /// <summary>Cambiste : CDF → USD (billets propres).</summary>
        public bool TryExchangeCdfToUsd(int usdWanted)
        {
            EnsureInitialized();
            if (usdWanted <= 0) return false;

            int cost = Mathf.CeilToInt(usdWanted * cdfPerUsd * (1f + cambistCommission));
            if (!TrySpend(Currency.CDF, cost, "Change chez le cambiste")) return false;
            SetCash(Currency.USD, cashUSD + usdWanted, "Change chez le cambiste");
            return true;
        }

        /// <summary>Perd tout l'argent sur soi (arrestation, braquage). Renvoie (CDF, USD propres, USD usés) perdus.</summary>
        public (int cdf, int usd, int worn) LoseEverything(string reason)
        {
            EnsureInitialized();
            var lost = (cashCDF, cashUSD, wornUSD);
            SetCash(Currency.CDF, 0, reason);
            SetCash(Currency.USD, 0, reason);
            SetWorn(0);
            return lost;
        }

        public static string Symbol(Currency c) => c == Currency.CDF ? "FC" : "$";

        public static string Format(Currency c, int amount) =>
            c == Currency.CDF
                ? amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ') + " FC"
                : $"{amount} $";

        // ─── Interne ──────────────────────────────────────────────────────

        private void EnsureInitialized()
        {
            if (!initialized) ResetToStart();
        }

        private void SetCash(Currency currency, int value, string reason)
        {
            int old = currency == Currency.CDF ? cashCDF : cashUSD;
            value = Mathf.Max(0, value);
            if (old == value) return;

            if (currency == Currency.CDF) cashCDF = value;
            else cashUSD = value;
            OnCashChanged?.Invoke(currency, old, value, reason);
        }

        private void SetWorn(int value)
        {
            int old = wornUSD;
            wornUSD = Mathf.Max(0, value);
            if (old != wornUSD) OnWornUsdChanged?.Invoke(old, wornUSD);
        }

        /// <summary>Chaque lancement du Play mode repart d'un portefeuille neuf (même sans Domain Reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAllWallets()
        {
            foreach (var wallet in Resources.FindObjectsOfTypeAll<CurrencyWallet>())
            {
                wallet.initialized = false;
                wallet.OnCashChanged = null;
                wallet.OnWornUsdChanged = null;
                wallet.OnPaymentRefused = null;
            }
        }
    }
}
