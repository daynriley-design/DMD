using System;
using System.Collections.Generic;
using System.Text;
using DMD.Economy;
using UnityEngine;

namespace DMD.Quests
{
    public enum QuestState
    {
        Verrouillee, // prérequis ou preuves manquants
        Disponible,  // peut être lancée
        Active,
        Terminee,
        Echouee
    }

    /// <summary>
    /// Suit l'avancement des missions (QuestData). Les données restent dans les assets ;
    /// l'état vit ici, en mémoire (pas d'écriture dans les ScriptableObjects pendant le jeu).
    ///  • Une mission se termine seule quand tous ses objectifs non optionnels sont faits.
    ///  • Le mur d'enquête de la planque appelle TryUnlockFromEvidenceBoard() : si les preuves requises
    ///    sont possédées (sur soi ou au coffre), la mission suivante devient disponible.
    /// Pilotable depuis Ink : #QUEST:Start:M2_1  #QUEST:Objective:M2_1:acheter_torche  #QUEST:Complete:M2_1
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [SerializeField] private QuestData[] quests = new QuestData[0];
        [SerializeField] private CurrencyWallet wallet;
        [Tooltip("Mission lancée automatiquement au début (ex. M2_1).")]
        [SerializeField] private QuestData autoStartQuest;

        private readonly Dictionary<QuestData, QuestState> states = new();
        private readonly Dictionary<QuestData, HashSet<string>> doneObjectives = new();

        /// <summary>(mission, ancien état, nouvel état)</summary>
        public event Action<QuestData, QuestState, QuestState> OnQuestStateChanged;
        /// <summary>(mission, identifiant de l'objectif)</summary>
        public event Action<QuestData, string> OnObjectiveCompleted;

        public IReadOnlyList<QuestData> Quests => quests;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            foreach (var q in quests)
            {
                if (q == null || states.ContainsKey(q)) continue;
                states[q] = QuestState.Verrouillee;
                doneObjectives[q] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            RefreshAvailability();
        }

        private void Start()
        {
            if (autoStartQuest != null) StartQuest(autoStartQuest);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // ─── Lecture ──────────────────────────────────────────────────────

        public QuestData Find(string questId)
        {
            foreach (var q in quests)
                if (q != null && string.Equals(q.questId, questId, StringComparison.OrdinalIgnoreCase)) return q;
            return null;
        }

        public QuestState GetState(QuestData quest) =>
            quest != null && states.TryGetValue(quest, out var s) ? s : QuestState.Verrouillee;

        public bool IsObjectiveDone(QuestData quest, string objectiveId) =>
            quest != null && doneObjectives.TryGetValue(quest, out var set) && set.Contains(objectiveId);

        /// <summary>Première mission active (pour l'affichage de l'objectif courant).</summary>
        public QuestData ActiveQuest
        {
            get
            {
                foreach (var q in quests)
                    if (q != null && GetState(q) == QuestState.Active) return q;
                return null;
            }
        }

        /// <summary>
        /// Premier objectif OBLIGATOIRE non fait (texte du HUD / du téléphone). Les objectifs optionnels
        /// (ex. « couper le disjoncteur », une approche parmi d'autres) ne sont pas affichés comme objectif courant.
        /// </summary>
        public QuestData.Objective CurrentObjective(QuestData quest)
        {
            if (quest == null) return null;
            foreach (var o in quest.objectives)
                if (!o.optional && !IsObjectiveDone(quest, o.id)) return o;
            return null;
        }

        // ─── Actions ──────────────────────────────────────────────────────

        public bool StartQuest(string questId) => StartQuest(Find(questId));

        public bool StartQuest(QuestData quest)
        {
            if (quest == null || !states.ContainsKey(quest)) return Warn($"mission inconnue « {quest?.questId} »");

            RefreshAvailability();
            var state = GetState(quest);
            if (state == QuestState.Active) return true;
            if (state != QuestState.Disponible)
                return Warn($"{quest.questId} n'est pas disponible ({state}). {MissingRequirements(quest)}");

            SetState(quest, QuestState.Active);
            return true;
        }

        public bool CompleteObjective(string questId, string objectiveId) => CompleteObjective(Find(questId), objectiveId);

        public bool CompleteObjective(QuestData quest, string objectiveId)
        {
            if (quest == null) return Warn("mission introuvable");
            if (GetState(quest) != QuestState.Active) return Warn($"{quest.questId} n'est pas active : objectif « {objectiveId} » ignoré.");
            if (quest.FindObjective(objectiveId) == null) return Warn($"objectif « {objectiveId} » absent de {quest.questId}.");
            if (!doneObjectives[quest].Add(objectiveId)) return true;

            Debug.Log($"[Quêtes] {quest.questId} — objectif fait : {objectiveId}", this);
            OnObjectiveCompleted?.Invoke(quest, objectiveId);

            if (AllRequiredDone(quest)) CompleteQuest(quest);
            return true;
        }

        public bool CompleteQuest(string questId) => CompleteQuest(Find(questId));

        public bool CompleteQuest(QuestData quest)
        {
            if (quest == null || GetState(quest) == QuestState.Terminee) return false;

            SetState(quest, QuestState.Terminee);
            if (wallet != null)
            {
                wallet.Add(Currency.CDF, quest.rewardCDF, $"Récompense {quest.questId}");
                wallet.Add(Currency.USD, quest.rewardUSD, $"Récompense {quest.questId}");
            }
            RefreshAvailability();
            return true;
        }

        public void FailQuest(QuestData quest)
        {
            if (quest != null && GetState(quest) == QuestState.Active) SetState(quest, QuestState.Echouee);
        }

        /// <summary>Mur d'enquête : vérifie les preuves et rend disponibles les missions débloquées. Renvoie celles qui l'ont été.</summary>
        public List<QuestData> TryUnlockFromEvidenceBoard()
        {
            var unlocked = new List<QuestData>();
            foreach (var q in quests)
            {
                if (q == null || GetState(q) != QuestState.Verrouillee) continue;
                if (PrerequisitesDone(q) && EvidenceOwned(q))
                {
                    SetState(q, QuestState.Disponible);
                    unlocked.Add(q);
                }
            }
            if (unlocked.Count == 0) Debug.Log("[Quêtes] Mur d'enquête : rien de nouveau à relier.", this);
            return unlocked;
        }

        // ─── Interne ──────────────────────────────────────────────────────

        /// <summary>Les missions sans preuve requise se débloquent dès que leurs prérequis sont terminés.
        /// Celles qui exigent des preuves attendent le mur d'enquête.</summary>
        private void RefreshAvailability()
        {
            foreach (var q in quests)
            {
                if (q == null || GetState(q) != QuestState.Verrouillee) continue;
                if (PrerequisitesDone(q) && q.requiredEvidence.Count == 0) SetState(q, QuestState.Disponible);
            }
        }

        private bool PrerequisitesDone(QuestData q)
        {
            foreach (var p in q.prerequisites)
                if (p != null && GetState(p) != QuestState.Terminee) return false;
            return true;
        }

        private static bool EvidenceOwned(QuestData q)
        {
            foreach (var item in q.requiredEvidence)
                if (!Safehouse.Owns(item)) return false;
            return true;
        }

        private bool AllRequiredDone(QuestData q)
        {
            foreach (var o in q.objectives)
                if (!o.optional && !doneObjectives[q].Contains(o.id)) return false;
            return true;
        }

        private string MissingRequirements(QuestData q)
        {
            var sb = new StringBuilder();
            foreach (var p in q.prerequisites)
                if (p != null && GetState(p) != QuestState.Terminee) sb.Append($"Terminer {p.questId}. ");
            foreach (var e in q.requiredEvidence)
                if (!Safehouse.Owns(e)) sb.Append($"Preuve manquante : {e}. ");
            return sb.ToString();
        }

        private void SetState(QuestData q, QuestState newState)
        {
            var old = GetState(q);
            if (old == newState) return;
            states[q] = newState;
            Debug.Log($"[Quêtes] {q.questId} « {q.title} » : {old} → {newState}", this);
            OnQuestStateChanged?.Invoke(q, old, newState);
        }

        private bool Warn(string message)
        {
            Debug.LogWarning($"[Quêtes] {message}", this);
            return false;
        }

        [ContextMenu("DMD Test/Afficher l'état des missions")]
        private void TestLog()
        {
            var sb = new StringBuilder("[Quêtes]");
            foreach (var q in quests)
            {
                if (q == null) continue;
                sb.Append($"\n  • {q.questId} « {q.title} » : {GetState(q)}");
                foreach (var o in q.objectives)
                    sb.Append($"\n      [{(IsObjectiveDone(q, o.id) ? "x" : " ")}] {o.id}{(o.optional ? " (optionnel)" : "")}");
            }
            Debug.Log(sb.ToString(), this);
        }

        [ContextMenu("DMD Test/Valider l'objectif courant")]
        private void TestCompleteCurrent()
        {
            var q = ActiveQuest;
            var o = CurrentObjective(q);
            if (o != null) CompleteObjective(q, o.id);
        }

        [ContextMenu("DMD Test/Mur d'enquête")]
        private void TestEvidenceBoard() => TryUnlockFromEvidenceBoard();
    }
}
