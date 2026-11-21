using System;
using System.Collections.Generic;
using UnityEngine;

namespace DMD.Core
{
    /// <summary>Les 4 statistiques dynamiques du joueur.</summary>
    public enum StatType
    {
        Rue,        // milieu, débrouille
        Savoir,     // enquête, dossiers
        Influence,  // État, réseaux
        Voix        // peuple, médias, culture
    }

    /// <summary>Les 4 rôles jouables de l'Acte I.</summary>
    public enum PlayerRole
    {
        Civil,      // matrice par défaut
        Juge,
        Caid,
        Musicien
    }

    /// <summary>
    /// Règles et seuils du système de stats, éditables sans toucher au code.
    /// Création : clic droit dans le Project > Create > DMD > Player Stats Config.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsConfig", menuName = "DMD/Player Stats Config")]
    public class PlayerStatsConfig : ScriptableObject
    {
        [Serializable]
        public struct RolePrimaryStat
        {
            public PlayerRole role;
            public StatType primaryStat;
        }

        [Serializable]
        public struct TranscendenceTarget
        {
            [Tooltip("Stat qui doit atteindre le seuil de transcendance.")]
            public StatType stat;
            [Tooltip("Rôle vers lequel cette stat ouvre une mission de bascule.")]
            public PlayerRole targetRole;
        }

        [Header("Bornes")]
        public int minValue = 0;
        public int maxValue = 100;
        public int startValue = 20;

        [Header("Régression (rôle non-Civil)")]
        [Tooltip("Si la stat principale du rôle passe SOUS cette valeur, retour au statut Civil.")]
        public int regressionThreshold = 30;
        [Tooltip("Malus appliqué au moment de la régression (valeur négative).")]
        public int demotionShock = -20;
        [Tooltip("Faux : le choc ne frappe que la stat principale perdue. Vrai : il frappe les 4 stats.")]
        public bool demotionShockAppliesToAllStats = false;

        [Header("Transcendance")]
        [Tooltip("Seuil (inclus) à partir duquel une mission de bascule devient disponible.")]
        public int transcendenceThreshold = 60;

        [Header("Fin prématurée (Civil)")]
        [Tooltip("Si le joueur est Civil et que TOUTES ses stats sont sous cette valeur : effondrement.")]
        public int collapseThreshold = 10;

        [Header("Correspondances")]
        public List<RolePrimaryStat> primaryStats = new()
        {
            new RolePrimaryStat { role = PlayerRole.Juge,     primaryStat = StatType.Savoir },
            new RolePrimaryStat { role = PlayerRole.Caid,     primaryStat = StatType.Rue },
            new RolePrimaryStat { role = PlayerRole.Musicien, primaryStat = StatType.Voix },
        };

        // Influence n'ouvre aucun rôle en V1 : elle est réservée à Ministre/Président (Actes II/III).
        public List<TranscendenceTarget> transcendenceTargets = new()
        {
            new TranscendenceTarget { stat = StatType.Savoir, targetRole = PlayerRole.Juge },
            new TranscendenceTarget { stat = StatType.Rue,    targetRole = PlayerRole.Caid },
            new TranscendenceTarget { stat = StatType.Voix,   targetRole = PlayerRole.Musicien },
        };

        public bool TryGetPrimaryStat(PlayerRole role, out StatType stat)
        {
            foreach (var entry in primaryStats)
            {
                if (entry.role == role) { stat = entry.primaryStat; return true; }
            }
            stat = default;
            return false;
        }

        public bool TryGetTranscendenceRole(StatType stat, out PlayerRole role)
        {
            foreach (var entry in transcendenceTargets)
            {
                if (entry.stat == stat) { role = entry.targetRole; return true; }
            }
            role = default;
            return false;
        }

        private void OnValidate()
        {
            if (maxValue <= minValue) maxValue = minValue + 1;
            startValue = Mathf.Clamp(startValue, minValue, maxValue);
            if (demotionShock > 0) demotionShock = -demotionShock;
        }
    }
}
