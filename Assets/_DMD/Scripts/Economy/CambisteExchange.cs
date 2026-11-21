using System.Collections.Generic;
using DMD.Core;
using UnityEngine;

namespace DMD.Economy
{
    /// <summary>
    /// Un cambiste de rue (ex. Mama Chantal au Rond-Point Victoire) : ses taux du jour et ce qu'il accepte.
    /// L'interface du change est un dialogue Ink (knot « chantal_change ») dont les choix envoient
    ///   #EXCHANGE:Chantal:SellUsd:10   → le joueur vend 10 $ (reçoit 10 × taux d'achat en FC)
    ///   #EXCHANGE:Chantal:BuyUsd:10    → le joueur achète 10 $ propres (paie 10 × taux de vente en FC)
    ///   #EXCHANGE:Chantal:SellWorn:10  → billets usés : refusés par Mama Chantal, repris ailleurs avec décote
    /// Les refus (pas assez d'argent, vieux billets) remontent au HUD via le portefeuille.
    /// </summary>
    public class CambisteExchange : MonoBehaviour
    {
        private static readonly List<CambisteExchange> All = new();

        [SerializeField] private string id = "Chantal";
        [SerializeField] private string displayName = "Mama Chantal";
        [SerializeField] private CurrencyWallet wallet;

        [Header("Taux du jour (Kinshasa)")]
        [Tooltip("Le joueur VEND 1 $ → reçoit ce nombre de FC.")]
        [SerializeField, Min(1)] private int buyUsdRateCDF = 2850;
        [Tooltip("Le joueur ACHÈTE 1 $ propre → paie ce nombre de FC.")]
        [SerializeField, Min(1)] private int sellUsdRateCDF = 2900;

        [Header("Billets usés")]
        [Tooltip("« Je prends pas les vieux billets de 2006, ni ceux qui sont déchirés. »")]
        [SerializeField] private bool acceptsWornBills = false;
        [SerializeField, Range(0f, 1f)] private float wornValueRatio = 0.7f;

        public string Id => id;
        public string DisplayName => displayName;
        public int BuyUsdRate => buyUsdRateCDF;
        public int SellUsdRate => sellUsdRateCDF;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static CambisteExchange Find(string cambisteId) =>
            All.Find(c => string.Equals(c.id, cambisteId, System.StringComparison.OrdinalIgnoreCase));

        public bool SellUsd(int usd) =>
            wallet != null && wallet.TrySellUsd(usd, buyUsdRateCDF, false, $"Change chez {displayName}");

        public bool BuyUsd(int usd) =>
            wallet != null && wallet.TryBuyUsd(usd, sellUsdRateCDF, $"Change chez {displayName}");

        public bool SellWornUsd(int usd)
        {
            if (wallet == null) return false;
            if (!acceptsWornBills)
            {
                GameEvents.RaiseNarrativeUi("Refus", $"{displayName} refuse les billets usés ou déchirés.");
                return false;
            }
            return wallet.TrySellUsd(usd, buyUsdRateCDF * wornValueRatio, true, $"Change (billets usés) chez {displayName}");
        }
    }
}
