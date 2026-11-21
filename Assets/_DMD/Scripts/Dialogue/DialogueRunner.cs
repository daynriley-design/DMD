using System.Collections.Generic;
using System.Text.RegularExpressions;
using DMD.Core;
using DMD.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if DMD_INK
using Ink.Runtime;
#endif

namespace DMD.Dialogue
{
    /// <summary>
    /// Lit une histoire Ink (fichier .json compilé automatiquement à partir du .ink) et l'affiche :
    ///  • nom du personnage (« AGENT : "..." » → AGENT) + texte ;
    ///  • choix en boutons (clic ou touches 1 à 9) ;
    ///  • Espace / Entrée / clic pour avancer ;
    ///  • chaque ligne passe ses tags à InkTagRouter (#STAT, #HEAT, #FLAG...).
    /// Pendant le dialogue, les composants listés dans « Désactiver pendant le dialogue »
    /// (ex. ThirdPersonController, PlayerInput) sont coupés et le curseur est libéré.
    ///
    /// Ink est requis : le symbole DMD_INK est ajouté automatiquement dès que le paquet Ink est installé
    /// (voir Editor/InkDefine). Sans Ink, ce script compile mais affiche une erreur au lancement.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [Header("Histoire")]
        [Tooltip("Le fichier .json généré par Ink à côté du .ink (ex. Ch01_LeNomDuPere.json).")]
        [SerializeField] private TextAsset inkJson;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Knot de départ au lancement automatique (vide = début du fichier). Ex. m2_1_appel.")]
        [SerializeField] private string startKnot = "";

        [Header("Interface")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private RectTransform choicesRoot;
        [Tooltip("Bouton modèle (désactivé) cloné pour chaque choix.")]
        [SerializeField] private Button choiceTemplate;

        [Header("Systèmes")]
        [SerializeField] private InkTagRouter tagRouter;
        [Tooltip("Portefeuille du joueur : prix des choix (#COST), boutons grisés si l'argent manque.")]
        [SerializeField] private CurrencyWallet wallet;
        [Tooltip("Composants coupés pendant le dialogue (contrôleur du joueur, caméra...).")]
        [SerializeField] private Behaviour[] disableDuringDialogue = new Behaviour[0];
        [Tooltip("Nom affiché pour « TOI » si aucun prénom n'a été choisi à l'écran titre.")]
        [SerializeField] private string playerDisplayName = "TOI";

        // « AGENT : "texte" » → nom en majuscules (2 à 24 caractères) puis « : »
        private static readonly Regex SpeakerPattern =
            new(@"^([A-ZÀ-ÖØ-Þ0-9' \-]{2,24})\s*:\s*(.+)$", RegexOptions.Compiled);

        private readonly List<Button> choiceButtons = new();
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;
        private int startedFrame;
        private bool waitingForChoice;

#if DMD_INK
        private Story story;
        private TextAsset storyJson; // fichier de l'histoire en mémoire : ses variables Ink sont conservées d'un knot à l'autre
#endif

        public bool IsRunning { get; private set; }

        /// <summary>Fichier Ink par défaut (utilisé par les DialogueTrigger sans fichier propre).</summary>
        public TextAsset DefaultInkJson => inkJson;

        private void Awake()
        {
            if (tagRouter == null) tagRouter = FindAnyObjectByType<InkTagRouter>();
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (playOnStart) StartDialogue();
        }

        private void Update()
        {
            if (!IsRunning || Time.frameCount == startedFrame) return;

            if (waitingForChoice)
            {
                for (int i = 0; i < choiceButtons.Count && i < 9; i++)
                    if (DigitPressed(i)) { Choose(i); return; }
                return;
            }

            if (AdvancePressed()) Advance();
        }

        // ─── API publique ─────────────────────────────────────────────────

        public void StartDialogue() => StartDialogue(inkJson, startKnot);

        public void StartDialogue(TextAsset json) => StartDialogue(json, null);

        /// <summary>
        /// Lance un dialogue, éventuellement à partir d'un knot précis (ex. "m2_1_canal").
        /// Si le même fichier Ink est déjà chargé, l'histoire est réutilisée : les variables
        /// (ex. a_negocie) restent valables d'une scène de mission à l'autre.
        /// </summary>
        public void StartDialogue(TextAsset json, string knot)
        {
#if DMD_INK
            if (IsRunning) return;
            if (json == null)
            {
                Debug.LogError("[DialogueRunner] Aucun fichier Ink (.json) assigné.", this);
                return;
            }

            if (story == null || storyJson != json)
            {
                story = new Story(json.text);
                storyJson = json;
                story.onError += (message, type) =>
                {
                    if (type == Ink.ErrorType.Error) Debug.LogError($"[Ink] {message}", this);
                    else Debug.LogWarning($"[Ink] {message}", this);
                };
            }

            // Le prénom choisi à l'écran titre est disponible dans Ink via {prenom}.
            if (PlayerProfile.HasFirstName && story.variablesState.GlobalVariableExistsWithName("prenom"))
                story.variablesState["prenom"] = PlayerProfile.FirstName;

            if (!string.IsNullOrEmpty(knot))
            {
                try { story.ChoosePathString(knot); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DialogueRunner] Knot « {knot} » introuvable dans {json.name} : {e.Message}", this);
                    return;
                }
            }

            IsRunning = true;
            startedFrame = Time.frameCount;
            SetPlayerControl(false);
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            GameEvents.RaiseDialogueStarted();
            ShowNextLine();
#else
            Debug.LogError("[DialogueRunner] Ink n'est pas installé. Package Manager > + > Add package from git URL : " +
                           "https://github.com/inkle/ink-unity-integration.git#upm", this);
#endif
        }

        /// <summary>Lit une variable Ink (ex. "approche_fuite" → "Propre" ou "Sale").</summary>
        public object GetVariable(string name)
        {
#if DMD_INK
            return story?.variablesState[name];
#else
            return null;
#endif
        }

        // ─── Déroulé ──────────────────────────────────────────────────────

        private void Advance()
        {
#if DMD_INK
            if (story.canContinue) ShowNextLine();
            else if (story.currentChoices.Count == 0) EndDialogue();
#endif
        }

        private void ShowNextLine()
        {
#if DMD_INK
            // Saute les lignes vides (lignes de tags seuls) en appliquant quand même leurs tags.
            string line = "";
            while (story.canContinue)
            {
                line = story.Continue().Trim();
                tagRouter?.Process(story.currentTags);
                if (line.Length > 0) break;
            }

            if (line.Length > 0) DisplayLine(line);

            if (story.currentChoices.Count > 0)
            {
                ShowChoices();
            }
            else if (line.Length == 0 && !story.canContinue)
            {
                EndDialogue(); // plus rien à afficher
            }
            else
            {
                if (continueHint != null) continueHint.SetActive(true);
            }
#endif
        }

        private void ShowChoices()
        {
#if DMD_INK
            ClearChoices();
            waitingForChoice = true;
            if (continueHint != null) continueHint.SetActive(false);

            if (choiceTemplate == null || choicesRoot == null)
            {
                Debug.LogError("[DialogueRunner] choiceTemplate ou choicesRoot non assigné.", this);
                return;
            }

            for (int i = 0; i < story.currentChoices.Count; i++)
            {
                int index = i;
                var choice = story.currentChoices[i];
                string text = choice.text.Trim();
                bool affordable = true;

                // Choix payant : prix affiché, bouton grisé si l'argent sur soi ne suffit pas.
                if (TryGetCost(choice.tags, out Currency currency, out int amount))
                {
                    text += $"  ({CurrencyWallet.Format(currency, amount)})";
                    affordable = wallet == null || wallet.CanAfford(currency, amount);
                }

                Button button = Instantiate(choiceTemplate, choicesRoot);
                button.gameObject.SetActive(true);
                button.interactable = affordable;
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"{i + 1}. {text}";
                button.onClick.AddListener(() => Choose(index));
                choiceButtons.Add(button);
            }
#endif
        }

        /// <summary>
        /// Lit un tag de prix sur un choix Ink : « * [Payer sans discuter #COST:CDF:35000] ».
        /// Le paiement est fait au moment du choix (inutile d'ajouter un #CASH dans la suite).
        /// </summary>
        private static bool TryGetCost(IList<string> tags, out Currency currency, out int amount)
        {
            currency = Currency.CDF;
            amount = 0;
            if (tags == null) return false;

            foreach (string raw in tags)
            {
                string[] p = raw.Trim().Split(':');
                if (p.Length != 3 || !p[0].Trim().Equals("COST", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!System.Enum.TryParse(p[1].Trim(), true, out currency)) continue;
                if (!int.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Integer,
                                  System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0) continue;
                return true;
            }
            return false;
        }

        private void Choose(int index)
        {
#if DMD_INK
            if (!waitingForChoice || index < 0 || index >= story.currentChoices.Count) return;

            // Les touches 1–9 contournent l'état « grisé » des boutons : le paiement est donc revérifié ici.
            var picked = story.currentChoices[index];
            if (TryGetCost(picked.tags, out Currency currency, out int amount) && wallet != null &&
                !wallet.TrySpend(currency, amount, $"Dialogue — {picked.text.Trim()}"))
                return;

            waitingForChoice = false;
            startedFrame = Time.frameCount; // le clic qui choisit ne doit pas aussi faire avancer la ligne suivante
            ClearChoices();
            story.ChooseChoiceIndex(index);
            ShowNextLine();
#endif
        }

        private void EndDialogue()
        {
            IsRunning = false;
            waitingForChoice = false;
            ClearChoices();
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            SetPlayerControl(true);
            GameEvents.RaiseDialogueEnded();
            tagRouter?.FlushPendingScene();
        }

        // ─── Affichage ────────────────────────────────────────────────────

        private void DisplayLine(string line)
        {
            string speaker = "";
            string text = line;

            Match match = SpeakerPattern.Match(line);
            if (match.Success)
            {
                speaker = match.Groups[1].Value.Trim();
                text = match.Groups[2].Value.Trim();
                if (speaker == "TOI")
                    speaker = PlayerProfile.HasFirstName ? PlayerProfile.FirstName.ToUpperInvariant() : playerDisplayName;
            }

            if (speakerText != null)
            {
                speakerText.text = speaker;
                speakerText.gameObject.SetActive(speaker.Length > 0);
            }
            if (bodyText != null) bodyText.text = text;
        }

        private void ClearChoices()
        {
            foreach (var button in choiceButtons)
                if (button != null) Destroy(button.gameObject);
            choiceButtons.Clear();
        }

        private void SetPlayerControl(bool enabled)
        {
            foreach (var behaviour in disableDuringDialogue)
                if (behaviour != null) behaviour.enabled = enabled;

            if (!enabled)
            {
                previousLockState = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = previousLockState;
                Cursor.visible = previousCursorVisible;
            }
        }

        // ─── Entrées ──────────────────────────────────────────────────────

        private static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
        }

        private static bool DigitPressed(int index)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb[(Key)((int)Key.Digit1 + index)].wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha1 + index);
#endif
        }
    }
}
