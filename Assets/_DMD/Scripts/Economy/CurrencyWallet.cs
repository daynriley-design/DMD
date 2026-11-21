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

        // Les taux de change appartiennent à chaque cambiste (voir CambisteExchange) :
        // Mama Chantal ne fait pas les mêmes prix qu'un changeur de Gombe.

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

        /// <summary>Vendre des dollars à un cambiste : reçoit usd × cdfPerUsd francs (arrondi inférieur).</summary>
        public bool TrySellUsd(int usd, float cdfPerUsd, bool wornBills, string reason)
        {
            EnsureInitialized();
            if (usd <= 0) return false;

            if ((wornBills ? wornUSD : cashUSD) < usd)
            {
                OnPaymentRefused?.Invoke(Currency.USD, usd, wornBills ? "Pas assez de billets usés." : "Pas assez de dollars sur toi.");
                return false;
            }

            if (wornBills) SetWorn(wornUSD - usd);
            else SetCash(Currency.USD, cashUSD - usd, reason);
            SetCash(Currency.CDF, cashCDF + Mathf.FloorToInt(usd * cdfPerUsd), reason);
            return true;
        }

        /// <summary>Acheter des dollars propres à un cambiste : coûte usd × cdfPerUsd francs (arrondi supérieur).</summary>
        public bool TryBuyUsd(int usd, float cdfPerUsd, string reason)
        {
            EnsureInitialized();
            if (usd <= 0) return false;

            int cost = Mathf.CeilToInt(usd * cdfPerUsd);
            if (cashCDF < cost)
            {
                OnPaymentRefused?.Invoke(Currency.CDF, cost, $"Il te faut {Format(Currency.CDF, cost)} pour {usd} $.");
                return false;
            }

            SetCash(Currency.CDF, cashCDF - cost, reason);
            SetCash(Currency.USD, cashUSD + usd, reason);
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
