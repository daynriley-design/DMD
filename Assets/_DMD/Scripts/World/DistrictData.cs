using UnityEngine;

namespace DMD.World
{
    public enum DistrictId
    {
        Limete,        // zone tampon : planque, petits boulots, SIM, tenues
        MatongeKalamu, // marchés, ngandas, cybercafés, cambistes
        Gombe          // ministères, banques, checkpoints
    }

    public enum RiskLevel { Faible, Moyen, Eleve }

    /// <summary>Un quartier de Kinshasa chargé comme scène additive (Zone_Limete, Zone_Matonge, Zone_Gombe).</summary>
    [CreateAssetMenu(fileName = "District", menuName = "DMD/Monde/District Data")]
    public class DistrictData : ScriptableObject
    {
        public DistrictId id;
        public string displayName = "Limete";
        [Tooltip("Nom exact de la scène additive (doit être dans la Scene List du build).")]
        public string sceneName = "Zone_Limete";
        public RiskLevel risk = RiskLevel.Faible;

        [Tooltip("Multiplie la chaleur des témoins dans ce quartier (Gombe surveille plus que Limete).")]
        [Min(0f)] public float heatMultiplier = 1f;

        [Tooltip("Repères visuels pour s'orienter sans GPS (Échangeur, Rond-Point Victoire...).")]
        public string[] landmarks = new string[0];

        [TextArea(2, 5)] public string ambiance;
        [TextArea(2, 5)] public string gameplayUse;
    }
}
