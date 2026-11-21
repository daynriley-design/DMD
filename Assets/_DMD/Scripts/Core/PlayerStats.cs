using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DMD.Core
{
    /// <summary>
    /// Variation d'une stat. Permet d'appliquer un choix de dialogue en une fois
    /// (ex. « Utiliser le nom du père » : +5 Influence, -5 Voix) avant d'évaluer les seuils.
    /// </summary>
    [Serializable]
    public struct StatDelta
    {
        public StatType stat;
        public int delta;

        public StatDelta(StatType stat, int delta)
        {
            this.stat = stat;
            this.delta = delta;
        }
    }

    /// <summary>
    /// Gère les 4 stats (Rue, Savoir, Influence, Voix), le rôle courant et les seuils :
    ///  • Régression : rôle non-Civil dont la stat principale passe sous 30 → Civil + choc -20.
    ///  • Transcendance : une stat cible atteint 60+ → mission de bascule disponible (le rôle ne change pas seul).
    ///  • Effondrement : Civil avec les 4 stats sous 10 → fin prématurée.
    /// À placer sur le GameObject du joueur. Les seuils viennent d'un PlayerStatsConfig (ScriptableObject).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Seuils et règles. Si vide, les valeurs par défaut du GDD sont utilisées.")]
        [SerializeField] private PlayerStatsConfig config;
        [SerializeField] private PlayerRole startingRole = PlayerRole.Civil;

        [Header("État actuel (lecture seule : passer par ModifyStat)")]
        [SerializeField] private PlayerRole currentRole = PlayerRole.Civil;
        [SerializeField] private int rue;
        [SerializeField] private int savoir;
        [SerializeField] private int influence;
        [SerializeField] private int voix;

        [Header("Debug")]
        [SerializeField] private bool logChanges = true;

        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        private PlayerStatsConfig runtimeDefaultConfig;
        private readonly HashSet<StatType> unlockedTranscendences = new();
        private bool hasCollapsed;

        // ─── Événements (pour l'UI, les missions, Ink...) ────────────────
        /// <summary>(stat, ancienne valeur, nouvelle valeur, raison)</summary>
        public event Action<StatType, int, int, string> OnStatChanged;
        /// <summary>(ancien rôle, nouveau rôle, raison)</summary>
        public event Action<PlayerRole, PlayerRole, string> OnRoleChanged;
        /// <summary>(rôle perdu, stat principale passée sous le seuil)</summary>
        public event Action<PlayerRole, StatType> OnRoleDemoted;
        /// <summary>(stat au-dessus du seuil, rôle ciblé par la mission de bascule)</summary>
        public event Action<StatType, PlayerRole> OnTranscendenceUnlocked;
        public event Action OnCivilCollapse;

        public PlayerRole CurrentRole => currentRole;
        public PlayerStatsConfig Config => GetConfig();
        public bool HasCollapsed => hasCollapsed;

        /// <summary>Vrai si une mission de bascule est actuellement ouverte grâce à cette stat.</summary>
        public bool IsTranscendenceUnlocked(StatType stat) => unlockedTranscendences.Contains(stat);

        private void Awake()
        {
            ResetStats();
        }

        // ─── API publique ─────────────────────────────────────────────────

        public int GetStat(StatType type) => type switch
        {
            StatType.Rue => rue,
            StatType.Savoir => savoir,
            StatType.Influence => influence,
            StatType.Voix => voix,
            _ => 0
        };

        /// <summary>Modifie une stat (bornée 0–100), notifie l'UI et vérifie les seuils.</summary>
        public void ModifyStat(StatType type, int delta, string reason)
        {
            if (ApplyDelta(type, delta, reason))
                EvaluateThresholds();
        }

        /// <summary>
        /// Applique plusieurs variations d'un coup PUIS vérifie les seuils une seule fois.
        /// Exemple : ModifyStats("Payer discrètement", new StatDelta(StatType.Voix, 5), new StatDelta(StatType.Savoir, 3));
        /// </summary>
        public void ModifyStats(string reason, params StatDelta[] deltas)
        {
            if (deltas == null) return;

            bool changed = false;
            foreach (var d in deltas)
                changed |= ApplyDelta(d.stat, d.delta, reason);

            if (changed) EvaluateThresholds();
        }

        /// <summary>
        /// Change le rôle (à appeler à la réussite d'une mission spéciale de bascule).
        /// Les seuils sont réévalués immédiatement.
        /// </summary>
        public void SetRole(PlayerRole newRole, string reason)
        {
            if (newRole == currentRole) return;

            var oldRole = currentRole;
            currentRole = newRole;
            unlockedTranscendences.Clear(); // les missions de bascule se recalculent pour le nouveau rôle

            if (logChanges) Debug.Log($"[PlayerStats] Rôle : {oldRole} → {newRole} — {reason}", this);
            OnRoleChanged?.Invoke(oldRole, newRole, reason);
            GameEvents.RaiseRoleChanged(oldRole, newRole, reason);

            EvaluateThresholds();
        }

        /// <summary>Remet toutes les stats à la valeur de départ et le rôle initial (sans événement).</summary>
        public void ResetStats()
        {
            var cfg = GetConfig();
            int start = Mathf.Clamp(cfg.startValue, cfg.minValue, cfg.maxValue);
            foreach (var stat in AllStats) SetRaw(stat, start);

            currentRole = startingRole;
            unlockedTranscendences.Clear();
            hasCollapsed = false;
        }

        // ─── Logique interne ──────────────────────────────────────────────

        /// <summary>Applique la variation bornée. Renvoie vrai si la valeur a réellement changé.</summary>
        private bool ApplyDelta(StatType type, int delta, string reason)
        {
            if (delta == 0) return false;

            var cfg = GetConfig();
            int oldValue = GetStat(type);
            int newValue = Mathf.Clamp(oldValue + delta, cfg.minValue, cfg.maxValue);
            if (newValue == oldValue) return false;

            SetRaw(type, newValue);

            if (logChanges)
                Debug.Log($"[PlayerStats] {type} {oldValue} → {newValue} ({delta:+#;-#}) — {reason}", this);

            OnStatChanged?.Invoke(type, oldValue, newValue, reason);
            GameEvents.RaiseStatChanged(type, oldValue, newValue, reason);
            return true;
        }

        private void EvaluateThresholds()
        {
            var cfg = GetConfig();

            // 1. Régression : la stat principale du rôle passe sous le seuil.
            if (currentRole != PlayerRole.Civil
                && cfg.TryGetPrimaryStat(currentRole, out var primary)
                && GetStat(primary) < cfg.regressionThreshold)
            {
                Demote(primary); // réévalue elle-même transcendance et effondrement
                return;
            }

            // 2. Transcendance : une stat cible atteint le seuil (déclenché une fois par franchissement).
            foreach (var stat in AllStats)
            {
                if (!cfg.TryGetTranscendenceRole(stat, out var targetRole)) continue;

                if (GetStat(stat) < cfg.transcendenceThreshold)
                {
                    unlockedTranscendences.Remove(stat); // pourra se redéclencher si la stat remonte
                    continue;
                }

                if (targetRole == currentRole || !unlockedTranscendences.Add(stat)) continue;

                if (logChanges)
                    Debug.Log($"[PlayerStats] Transcendance disponible : {stat} ≥ {cfg.transcendenceThreshold} → mission de bascule vers {targetRole}", this);
                OnTranscendenceUnlocked?.Invoke(stat, targetRole);
                GameEvents.RaiseTranscendenceUnlocked(stat, targetRole);
            }

            // 3. Effondrement : Civil et les 4 stats sous le seuil.
            if (currentRole == PlayerRole.Civil && !hasCollapsed && AllStatsBelow(cfg.collapseThreshold))
            {
                hasCollapsed = true;
                Debug.LogWarning($"[PlayerStats] Effondrement du Civil : toutes les stats < {cfg.collapseThreshold}. Fin prématurée.", this);
                OnCivilCollapse?.Invoke();
                GameEvents.RaiseCivilCollapse();
            }
        }

        private void Demote(StatType failingStat)
        {
            var cfg = GetConfig();
            var lostRole = currentRole;
            currentRole = PlayerRole.Civil;
            unlockedTranscendences.Clear();

            string reason = $"Régression : {failingStat} < {cfg.regressionThreshold}";
            Debug.LogWarning($"[PlayerStats] {lostRole} → Civil ({reason})", this);

            OnRoleChanged?.Invoke(lostRole, PlayerRole.Civil, reason);
            GameEvents.RaiseRoleChanged(lostRole, PlayerRole.Civil, reason);
            OnRoleDemoted?.Invoke(lostRole, failingStat);
            GameEvents.RaiseRoleDemoted(lostRole, failingStat);

            // Malus choc
            string shockReason = $"Choc de régression ({lostRole} → Civil)";
            if (cfg.demotionShockAppliesToAllStats)
            {
                foreach (var stat in AllStats) ApplyDelta(stat, cfg.demotionShock, shockReason);
            }
            else
            {
                ApplyDelta(failingStat, cfg.demotionShock, shockReason);
            }

            // Désormais Civil : pas de nouvelle régression possible, mais l'effondrement peut tomber.
            EvaluateThresholds();
        }

        private bool AllStatsBelow(int threshold)
        {
            foreach (var stat in AllStats)
                if (GetStat(stat) >= threshold) return false;
            return true;
        }

        private void SetRaw(StatType type, int value)
        {
            switch (type)
            {
                case StatType.Rue: rue = value; break;
                case StatType.Savoir: savoir = value; break;
                case StatType.Influence: influence = value; break;
                case StatType.Voix: voix = value; break;
            }
        }

        private PlayerStatsConfig GetConfig()
        {
            if (config != null) return config;

            if (runtimeDefaultConfig == null)
            {
                runtimeDefaultConfig = ScriptableObject.CreateInstance<PlayerStatsConfig>();
                runtimeDefaultConfig.name = "PlayerStatsConfig (défauts)";
                runtimeDefaultConfig.hideFlags = HideFlags.DontSave;
                Debug.LogWarning("[PlayerStats] Aucun PlayerStatsConfig assigné : valeurs par défaut utilisées.", this);
            }
            return runtimeDefaultConfig;
        }

        private void OnDestroy()
        {
            if (runtimeDefaultConfig != null) Destroy(runtimeDefaultConfig);
        }

        // ─── Tests rapides dans l'Inspecteur (clic droit sur le composant / menu ⋮) ───

        [ContextMenu("DMD Test/+15 Rue")] private void TestRuePlus() => TestModify(StatType.Rue, 15);
        [ContextMenu("DMD Test/-15 Rue")] private void TestRueMinus() => TestModify(StatType.Rue, -15);
        [ContextMenu("DMD Test/+15 Savoir")] private void TestSavoirPlus() => TestModify(StatType.Savoir, 15);
        [ContextMenu("DMD Test/-15 Savoir")] private void TestSavoirMinus() => TestModify(StatType.Savoir, -15);
        [ContextMenu("DMD Test/+15 Influence")] private void TestInfluencePlus() => TestModify(StatType.Influence, 15);
        [ContextMenu("DMD Test/-15 Influence")] private void TestInfluenceMinus() => TestModify(StatType.Influence, -15);
        [ContextMenu("DMD Test/+15 Voix")] private void TestVoixPlus() => TestModify(StatType.Voix, 15);
        [ContextMenu("DMD Test/-15 Voix")] private void TestVoixMinus() => TestModify(StatType.Voix, -15);

        [ContextMenu("DMD Test/Rôle → Juge")] private void TestRoleJuge() => TestRole(PlayerRole.Juge);
        [ContextMenu("DMD Test/Rôle → Caïd")] private void TestRoleCaid() => TestRole(PlayerRole.Caid);
        [ContextMenu("DMD Test/Rôle → Musicien")] private void TestRoleMusicien() => TestRole(PlayerRole.Musicien);
        [ContextMenu("DMD Test/Rôle → Civil")] private void TestRoleCivil() => TestRole(PlayerRole.Civil);

        [ContextMenu("DMD Test/Toutes les stats à 5 (test effondrement)")]
        private void TestCollapse()
        {
            if (!CheckPlaying()) return;
            var deltas = new StatDelta[AllStats.Length];
            for (int i = 0; i < AllStats.Length; i++)
                deltas[i] = new StatDelta(AllStats[i], 5 - GetStat(AllStats[i]));
            ModifyStats("Test Inspecteur : effondrement", deltas);
        }

        [ContextMenu("DMD Test/Reset")]
        private void TestReset()
        {
            if (!CheckPlaying()) return;
            ResetStats();
            Debug.Log("[PlayerStats] Reset.", this);
        }

        [ContextMenu("DMD Test/Afficher l'état dans la console")]
        private void TestLogState()
        {
            var sb = new StringBuilder($"[PlayerStats] Rôle : {currentRole}");
            foreach (var stat in AllStats) sb.Append($" | {stat} {GetStat(stat)}");
            Debug.Log(sb.ToString(), this);
        }

        private void TestModify(StatType type, int delta)
        {
            if (CheckPlaying()) ModifyStat(type, delta, "Test Inspecteur");
        }

        private void TestRole(PlayerRole role)
        {
            if (CheckPlaying()) SetRole(role, "Test Inspecteur");
        }

        private bool CheckPlaying()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[PlayerStats] Lance le mode Play pour utiliser les tests.", this);
            return false;
        }
    }
}
