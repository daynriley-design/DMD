using System.Collections;
using System.Collections.Generic;
using DMD.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DMD.UI
{
    /// <summary>
    /// Écran titre de DMD en 3 étapes :
    ///   1. « Appuie sur une touche » (clignote) ;
    ///   2. Menu : Nouvelle partie / Quitter ;
    ///   3. Choix du prénom parmi les 9 du GDD, puis chargement du Chapitre 1.
    /// Construction automatique : menu DMD > Créer l'écran titre (nouvelle scène).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Étapes")]
        [SerializeField] private GameObject pressAnyKeyStep;
        [SerializeField] private GameObject mainMenuStep;
        [SerializeField] private GameObject namePickerStep;

        [Header("Étape 1")]
        [SerializeField] private TMP_Text pressAnyKeyText;
        [SerializeField] private float blinkSpeed = 2.5f;

        [Header("Étape 2")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button quitButton;

        [Header("Étape 3")]
        [SerializeField] private RectTransform namesGrid;
        [Tooltip("Bouton modèle (désactivé) cloné pour chacun des 9 prénoms.")]
        [SerializeField] private Button nameTemplate;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        [Header("Suite")]
        [SerializeField] private string firstSceneName = "01_Loyola_Greybox";
        [SerializeField] private Color selectedNameColor = new(1f, 0.72f, 0.25f);

        private readonly List<(Button button, TMP_Text label, string name)> nameButtons = new();
        private string chosenName;
        private Coroutine blinkRoutine;
        private bool waitingForAnyKey;
        private int stepShownFrame;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (newGameButton != null) newGameButton.onClick.AddListener(() => ShowStep(namePickerStep));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (backButton != null) backButton.onClick.AddListener(() => ShowStep(mainMenuStep));

            BuildNameButtons();
            Select(PlayerProfile.HasFirstName ? PlayerProfile.FirstName : null);
            ShowStep(pressAnyKeyStep);
        }

        private void Update()
        {
            if (Time.frameCount == stepShownFrame) return;

            if (waitingForAnyKey && AnyKeyPressed())
                ShowStep(mainMenuStep);
            else if (namePickerStep != null && namePickerStep.activeSelf && EscapePressed())
                ShowStep(mainMenuStep);
        }

        // ─── Étapes ───────────────────────────────────────────────────────

        private void ShowStep(GameObject step)
        {
            if (pressAnyKeyStep != null) pressAnyKeyStep.SetActive(step == pressAnyKeyStep);
            if (mainMenuStep != null) mainMenuStep.SetActive(step == mainMenuStep);
            if (namePickerStep != null) namePickerStep.SetActive(step == namePickerStep);

            waitingForAnyKey = step == pressAnyKeyStep;
            stepShownFrame = Time.frameCount;

            if (blinkRoutine != null) { StopCoroutine(blinkRoutine); blinkRoutine = null; }
            if (waitingForAnyKey && pressAnyKeyText != null) blinkRoutine = StartCoroutine(Blink());
        }

        private void BuildNameButtons()
        {
            if (nameTemplate == null || namesGrid == null) return;
            nameTemplate.gameObject.SetActive(false);

            foreach (string firstName in PlayerProfile.FirstNames)
            {
                Button button = Instantiate(nameTemplate, namesGrid);
                button.gameObject.SetActive(true);
                button.name = $"Prenom_{firstName}";
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = firstName.ToUpperInvariant();

                string captured = firstName;
                button.onClick.AddListener(() => Select(captured));
                nameButtons.Add((button, label, firstName));
            }
        }

        private void Select(string firstName)
        {
            chosenName = firstName;
            foreach (var (_, label, name) in nameButtons)
                if (label != null) label.color = name == chosenName ? selectedNameColor : Color.white;

            if (selectedNameText != null)
                selectedNameText.text = string.IsNullOrEmpty(chosenName) ? "Choisis ton prénom" : $"Tu es {chosenName}.";
            if (startButton != null) startButton.interactable = !string.IsNullOrEmpty(chosenName);
        }

        private void StartGame()
        {
            if (string.IsNullOrEmpty(chosenName)) return;
            PlayerProfile.SetFirstName(chosenName);

            if (Application.CanStreamedLevelBeLoaded(firstSceneName))
            {
                SceneManager.LoadScene(firstSceneName);
            }
            else
            {
                Debug.LogWarning($"[TitleScreen] La scène « {firstSceneName} » n'est pas dans File > Build Profiles > Scene List.", this);
                if (selectedNameText != null) selectedNameText.text = $"Scène « {firstSceneName} » introuvable (voir la console).";
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator Blink()
        {
            while (true)
            {
                pressAnyKeyText.alpha = Mathf.Lerp(0.25f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkSpeed));
                yield return null;
            }
        }

        // ─── Entrées ──────────────────────────────────────────────────────

        private static bool AnyKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                   || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
            return Input.anyKeyDown;
#endif
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
