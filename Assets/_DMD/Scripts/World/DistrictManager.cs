using System;
using DMD.Heat;
using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Sait dans quel quartier est le joueur et quelle tenue il porte, et en déduit la vigilance :
    ///   HeatMultiplier = vigilance du quartier × effet de la tenue dans ce quartier.
    /// Les témoins (WitnessDetector) multiplient la chaleur signalée par cette valeur.
    /// Changer de tenue (à la planque ou en boutique) efface aussi l'icône Tenue.
    /// </summary>
    [DisallowMultipleComponent]
    public class DistrictManager : MonoBehaviour
    {
        public static DistrictManager Instance { get; private set; }

        [SerializeField] private OutfitData startingOutfit;
        [SerializeField] private OutfitData[] wardrobe = new OutfitData[0];

        [Header("État (debug)")]
        [SerializeField] private DistrictData currentDistrict;
        [SerializeField] private OutfitData currentOutfit;

        public event Action<DistrictData> OnDistrictChanged;
        public event Action<OutfitData> OnOutfitChanged;

        public DistrictData CurrentDistrict => currentDistrict;
        public OutfitData CurrentOutfit => currentOutfit;
        public OutfitData[] Wardrobe => wardrobe;

        public float HeatMultiplier
        {
            get
            {
                float m = currentDistrict != null ? currentDistrict.heatMultiplier : 1f;
                if (currentOutfit != null && currentDistrict != null) m *= currentOutfit.MultiplierFor(currentDistrict.id);
                return m;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            currentOutfit = startingOutfit;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public void SetDistrict(DistrictData district)
        {
            if (district == currentDistrict) return;
            currentDistrict = district;
            Debug.Log($"[Quartier] {district?.displayName} (risque {district?.risk}, vigilance ×{HeatMultiplier:0.##})", this);
            OnDistrictChanged?.Invoke(district);
        }

        /// <summary>Change de tenue complète : la police ne reconnaît plus la tenue repérée.</summary>
        public void ChangeOutfit(OutfitData outfit)
        {
            if (outfit == null || outfit == currentOutfit) return;
            currentOutfit = outfit;
            if (HeatManager.Instance != null) HeatManager.Instance.ClearIdentityFlag(IdentityFlag.Tenue);
            Debug.Log($"[Garde-robe] {outfit.displayName} (vigilance ici ×{HeatMultiplier:0.##})", this);
            OnOutfitChanged?.Invoke(outfit);
        }

        public bool ChangeOutfit(string outfitId)
        {
            foreach (var outfit in wardrobe)
            {
                if (outfit != null && string.Equals(outfit.id, outfitId, StringComparison.OrdinalIgnoreCase))
                {
                    ChangeOutfit(outfit);
                    return true;
                }
            }
            Debug.LogWarning($"[Garde-robe] Tenue « {outfitId} » absente de la garde-robe.", this);
            return false;
        }

        [ContextMenu("DMD Test/Tenue suivante")]
        private void TestNextOutfit()
        {
            if (wardrobe.Length == 0) return;
            int i = Array.IndexOf(wardrobe, currentOutfit);
            ChangeOutfit(wardrobe[(i + 1) % wardrobe.Length]);
        }
    }
}
