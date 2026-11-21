using System;
using System.Collections.Generic;
using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Une tenue de la garde-robe. Chaque tenue attire plus ou moins l'attention selon le quartier :
    /// la chemise propre passe inaperçue à Gombe mais détonne à Matonge, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "Outfit", menuName = "DMD/Monde/Outfit Data")]
    public class OutfitData : ScriptableObject
    {
        [Serializable]
        public struct DistrictModifier
        {
            public DistrictId district;
            [Tooltip("< 1 : se fond dans la foule ; > 1 : attire l'œil.")]
            [Min(0f)] public float heatMultiplier;
        }

        public string id = "Etudiant";
        public string displayName = "T-shirt blanc d'étudiant";
        [TextArea(2, 4)] public string description;
        [Tooltip("Modèle/matériau à activer sur le joueur (optionnel, pour plus tard).")]
        public GameObject visualPrefab;
        public List<DistrictModifier> modifiers = new();

        public float MultiplierFor(DistrictId district)
        {
            foreach (var m in modifiers)
                if (m.district == district) return m.heatMultiplier;
            return 1f;
        }
    }
}
