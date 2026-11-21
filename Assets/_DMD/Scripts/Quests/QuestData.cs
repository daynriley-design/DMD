using System;
using System.Collections.Generic;
using DMD.World;
using UnityEngine;

namespace DMD.Quests
{
    /// <summary>
    /// Une mission (données uniquement — l'avancement est suivi par QuestManager).
    /// Exemple : M2_1 « Zéro Trace », objectifs « rejoindre_planque », « detruire_smartphone », « acheter_torche ».
    /// </summary>
    [CreateAssetMenu(fileName = "Quest", menuName = "DMD/Quêtes/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Serializable]
        public class Objective
        {
            [Tooltip("Identifiant utilisé par le code et Ink (#QUEST:Objective:M2_1:acheter_torche).")]
            public string id;
            [TextArea(1, 3)] public string description;
            public bool optional;
        }

        [Header("Identité")]
        public string questId = "M2_1";
        public string title = "Zéro Trace";
        public int chapter = 2;
        public DistrictId district = DistrictId.Limete;

        [Header("Texte")]
        [TextArea(2, 6)] public string summary;
        [TextArea(2, 4)] public string stakes;

        [Header("Déroulé")]
        public List<Objective> objectives = new();

        [Header("Déblocage")]
        [Tooltip("Missions à terminer avant celle-ci.")]
        public List<QuestData> prerequisites = new();
        [Tooltip("Preuves à relier sur le mur d'enquête de la planque (identifiants d'objets).")]
        public List<string> requiredEvidence = new();

        [Header("Récompense")]
        [Min(0)] public int rewardCDF;
        [Min(0)] public int rewardUSD;

        public Objective FindObjective(string objectiveId) =>
            objectives.Find(o => string.Equals(o.id, objectiveId, StringComparison.OrdinalIgnoreCase));
    }
}
