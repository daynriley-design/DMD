using DMD.Quests;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DMD.Dialogue
{
    /// <summary>
    /// Lance un knot Ink quand le joueur entre dans la zone (Collider « Is Trigger »),
    /// automatiquement ou en appuyant sur [E]. Exemples Mission 2.1 :
    ///   canal          → knot "m2_1_canal",   [E] Casser et jeter le téléphone
    ///   présentoir     → knot "m2_1_vendeur", [E] Parler à Bébé-Cash
    ///   portail vert   → knot "m2_1_planque", [E] Frapper au portail vert
    /// Conditions optionnelles liées aux missions : la zone ne réagit que si la mission est active,
    /// qu'un objectif précédent est fait et que l'objectif de cette scène ne l'est pas encore.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DialogueTrigger : MonoBehaviour
    {
        [Header("Dialogue")]
        [Tooltip("Fichier Ink compilé (.json). Vide = celui du DialogueRunner.")]
        [SerializeField] private TextAsset inkJson;
        [SerializeField] private string knot = "m2_1_canal";

        [Header("Déclenchement")]
        [SerializeField] private bool requireInteractKey = true;
        [SerializeField] private string prompt = "[E] Interagir";
        [SerializeField] private bool oneShot = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Conditions de mission (optionnelles)")]
        [Tooltip("Ex. M2_1. Vide = aucune condition.")]
        [SerializeField] private string questId = "";
        [Tooltip("Objectif qui doit déjà être fait (ex. detruire_smartphone).")]
        [SerializeField] private string requiresObjective = "";
        [Tooltip("Objectif accompli par cette scène : la zone se désactive une fois qu'il est fait.")]
        [SerializeField] private string completesObjective = "";

        private bool playerInside;
        private bool used;
        private DialogueRunner runner;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            playerInside = true;
            if (!requireInteractKey) TryStart();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag)) playerInside = false;
        }

        private void Update()
        {
            if (playerInside && requireInteractKey && InteractPressed()) TryStart();
        }

        private bool CanTrigger()
        {
            if (used && oneShot) return false;
            if (string.IsNullOrEmpty(questId)) return true;

            var quests = QuestManager.Instance;
            if (quests == null) return true; // pas de système de quêtes : on laisse passer (tests)

            var quest = quests.Find(questId);
            if (quest == null || quests.GetState(quest) != QuestState.Active) return false;
            if (!string.IsNullOrEmpty(requiresObjective) && !quests.IsObjectiveDone(quest, requiresObjective)) return false;
            if (!string.IsNullOrEmpty(completesObjective) && quests.IsObjectiveDone(quest, completesObjective)) return false;
            return true;
        }

        private void TryStart()
        {
            if (!CanTrigger()) return;
            if (runner == null) runner = FindAnyObjectByType<DialogueRunner>();
            if (runner == null)
            {
                Debug.LogWarning("[DialogueTrigger] Aucun DialogueRunner dans les scènes chargées.", this);
                return;
            }
            if (runner.IsRunning) return;

            used = true;
            runner.StartDialogue(inkJson != null ? inkJson : runner.DefaultInkJson, knot);
        }

        private void OnGUI()
        {
            if (!playerInside || !requireInteractKey || runner != null && runner.IsRunning || !CanTrigger()) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.85f, 0.45f);
            var size = new Vector2(Mathf.Max(260f, prompt.Length * 11f), 44f);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.62f, size.x, size.y), prompt, style);
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider>();
            if (box != null) Gizmos.DrawCube(box.center, box.size);
        }
    }
}
