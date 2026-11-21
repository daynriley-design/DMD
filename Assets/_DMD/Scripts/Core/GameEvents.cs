using System;
using DMD.Heat;
using UnityEngine;

namespace DMD.Core
{
    /// <summary>
    /// Bus d'événements global : découple les systèmes (stats, chaleur, UI, missions, Ink...).
    /// Les systèmes émettent via Raise*, les autres s'abonnent sans se connaître.
    ///   Exemple : un témoin voit une arme → HeatManager.AddHeat → GameEvents.HeatChanged → le HUD se rafraîchit.
    /// Toujours se désabonner dans OnDisable/OnDestroy.
    /// </summary>
    public static class GameEvents
    {
        // ─── Stats & rôles ────────────────────────────────────────────────
        /// <summary>(stat, ancienne valeur, nouvelle valeur, raison)</summary>
        public static event Action<StatType, int, int, string> StatChanged;
        /// <summary>(ancien rôle, nouveau rôle, raison)</summary>
        public static event Action<PlayerRole, PlayerRole, string> RoleChanged;
        /// <summary>(rôle perdu, stat principale passée sous le seuil)</summary>
        public static event Action<PlayerRole, StatType> RoleDemoted;
        /// <summary>(stat au-dessus du seuil, rôle ciblé par la mission de bascule)</summary>
        public static event Action<StatType, PlayerRole> TranscendenceUnlocked;
        public static event Action CivilCollapse;

        // ─── Chaleur ──────────────────────────────────────────────────────
        /// <summary>(ville, ancienne chaleur, nouvelle chaleur)</summary>
        public static event Action<CityId, float, float> HeatChanged;
        /// <summary>(ville, ancien état, nouvel état)</summary>
        public static event Action<CityId, HeatState, HeatState> HeatStateChanged;
        /// <summary>(ville, icônes actives)</summary>
        public static event Action<CityId, IdentityFlag> IdentityFlagsChanged;
        /// <summary>(ancienne ville, nouvelle ville)</summary>
        public static event Action<CityId, CityId> CityChanged;
        /// <summary>(ville, prison de destination)</summary>
        public static event Action<CityId, PrisonId> ArrestTriggered;

        // ─── Narration (tags Ink) ─────────────────────────────────────────
        public static event Action DialogueStarted;
        public static event Action DialogueEnded;
        /// <summary>#LOC:Portail_Loyola → (identifiant du lieu)</summary>
        public static event Action<string> LocationChanged;
        /// <summary>#ITEM:Add:Arrete_Minier_Goma → (identifiant de l'objet)</summary>
        public static event Action<string> ItemAdded;
        /// <summary>#UI:BankNotification:"COMPTE BLOQUÉ" → (type, texte)</summary>
        public static event Action<string, string> NarrativeUi;

        public static void RaiseStatChanged(StatType s, int oldV, int newV, string reason) => StatChanged?.Invoke(s, oldV, newV, reason);
        public static void RaiseRoleChanged(PlayerRole oldR, PlayerRole newR, string reason) => RoleChanged?.Invoke(oldR, newR, reason);
        public static void RaiseRoleDemoted(PlayerRole lost, StatType failing) => RoleDemoted?.Invoke(lost, failing);
        public static void RaiseTranscendenceUnlocked(StatType s, PlayerRole target) => TranscendenceUnlocked?.Invoke(s, target);
        public static void RaiseCivilCollapse() => CivilCollapse?.Invoke();

        public static void RaiseHeatChanged(CityId c, float oldV, float newV) => HeatChanged?.Invoke(c, oldV, newV);
        public static void RaiseHeatStateChanged(CityId c, HeatState oldS, HeatState newS) => HeatStateChanged?.Invoke(c, oldS, newS);
        public static void RaiseIdentityFlagsChanged(CityId c, IdentityFlag flags) => IdentityFlagsChanged?.Invoke(c, flags);
        public static void RaiseCityChanged(CityId oldC, CityId newC) => CityChanged?.Invoke(oldC, newC);
        public static void RaiseArrestTriggered(CityId c, PrisonId prison) => ArrestTriggered?.Invoke(c, prison);

        public static void RaiseDialogueStarted() => DialogueStarted?.Invoke();
        public static void RaiseDialogueEnded() => DialogueEnded?.Invoke();
        public static void RaiseLocationChanged(string locationId) => LocationChanged?.Invoke(locationId);
        public static void RaiseItemAdded(string itemId) => ItemAdded?.Invoke(itemId);
        public static void RaiseNarrativeUi(string kind, string text) => NarrativeUi?.Invoke(kind, text);

        /// <summary>
        /// Vide les abonnements au lancement du Play mode, même si le "Domain Reload"
        /// est désactivé (Enter Play Mode Options) — évite les abonnés fantômes.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            StatChanged = null;
            RoleChanged = null;
            RoleDemoted = null;
            TranscendenceUnlocked = null;
            CivilCollapse = null;
            HeatChanged = null;
            HeatStateChanged = null;
            IdentityFlagsChanged = null;
            CityChanged = null;
            ArrestTriggered = null;
            DialogueStarted = null;
            DialogueEnded = null;
            LocationChanged = null;
            ItemAdded = null;
            NarrativeUi = null;
        }
    }
}
