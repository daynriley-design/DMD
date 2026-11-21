using System.Collections;
using System.Collections.Generic;
using DMD.Core;
using DMD.Economy;
using DMD.Heat;
using DMD.Quests;
using DMD.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DMD.UI
{
    /// <summary>
    /// HUD principal (uGUI + TextMeshPro), 100 % piloté par événements : rien n'est recalculé dans Update().
    ///  • Haut-gauche : ville active + rôle (CIVIL, JUGE, CAÏD, MUSICIEN).
    ///  • Haut-droite : jauge de chaleur (Image Filled) colorée selon l'état + 6 icônes d'identification.
    ///      Calme     → blanc ; le bloc passe à 30 % d'opacité après 5 s de calme (CanvasGroup).
    ///      Recherché → orange ; la barre pulse légèrement.
    ///      Traqué    → rouge ; bordure rouge + alerte sonore discrète.
    ///  • Bas-droite : notifications « +10 SAVOIR » qui montent puis disparaissent (2,5 s, Coroutine).
    /// Construction automatique : menu DMD > Créer le HUD dans la scène.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Contexte (haut-gauche)")]
        [SerializeField] private TMP_Text cityText;
        [SerializeField] private TMP_Text roleText;

        [Header("Chapitre 2 (optionnels)")]
        [Tooltip("Argent sur soi : « 15 000 FC · 20 $ (+10 $ usés) ».")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text clockText;
        [Tooltip("Objectif en cours de la mission active.")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private CurrencyWallet wallet;

        [Header("Bloc chaleur (haut-droite)")]
        [SerializeField] private CanvasGroup heatBlock;
        [Tooltip("Image en mode Filled / Horizontal.")]
        [SerializeField] private Image heatFill;
        [SerializeField] private TMP_Text heatStateText;
        [Tooltip("Bordure rouge affichée en Traqué.")]
        [SerializeField] private Image traqueBorder;
        [Tooltip("Ordre : Visage, Tenue, Véhicule, Arme, Caméra, Complice.")]
        [SerializeField] private Image[] identityIcons = new Image[6];

        [Header("Notifications de stats (bas-droite)")]
        [SerializeField] private RectTransform notificationRoot;
        [Tooltip("Texte modèle (désactivé) cloné pour chaque notification.")]
        [SerializeField] private TMP_Text notificationTemplate;
        [SerializeField, Min(0.5f)] private float notificationDuration = 2.5f;
        [SerializeField] private float notificationRise = 50f;
        [SerializeField] private float notificationSpacing = 34f;
        [SerializeField, Min(1)] private int maxNotifications = 5;

        [Header("Couleurs")]
        [SerializeField] private Color calmeColor = new(0.92f, 0.92f, 0.92f);
        [SerializeField] private Color rechercheColor = new(1f, 0.62f, 0.1f);
        [SerializeField] private Color traqueColor = new(0.78f, 0.05f, 0.05f);
        [SerializeField] private Color positiveStatColor = new(0.55f, 0.95f, 0.6f);
        [SerializeField] private Color negativeStatColor = new(1f, 0.5f, 0.45f);
        [SerializeField, Range(0f, 1f)] private float inactiveIconAlpha = 0.2f;

        [Header("Comportement")]
        [SerializeField, Min(0f)] private float calmFadeDelay = 5f;
        [SerializeField, Range(0f, 1f)] private float calmBlockAlpha = 0.3f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.6f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.6f;

        [Header("Audio (optionnel)")]
        [SerializeField] private AudioSource alertSource;
        [SerializeField] private AudioClip traqueAlertClip;

        [Header("Références (trouvées automatiquement si vides)")]
        [SerializeField] private PlayerStats playerStats;

        private class Notification
        {
            public TMP_Text text;
            public float offset;
            public Coroutine routine;
        }

        private readonly List<Notification> notifications = new();
        private HeatManager heat;
        private HeatState displayedState;
        private Coroutine pulseRoutine;
        private Coroutine calmRoutine;
        private Coroutine fadeRoutine;
        private TMP_Text[] iconLabels;
        private Sprite runtimeSprite;

        // ─── Cycle de vie ─────────────────────────────────────────────────
        // Abonnement dans Start (tous les Awake sont passés : HeatManager.Instance existe).

        private void Start()
        {
            heat = HeatManager.Instance;
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();

            EnsureFillSprite();
            CacheIconLabels();
            if (notificationTemplate != null) notificationTemplate.gameObject.SetActive(false);
            if (traqueBorder != null) traqueBorder.enabled = false;

            if (heat != null)
            {
                heat.OnHeatChanged += HandleHeatChanged;
                heat.OnHeatStateChanged += HandleHeatStateChanged;
                heat.OnIdentityFlagsChanged += HandleFlagsChanged;
                heat.OnCityChanged += HandleCityChanged;
            }
            else Debug.LogWarning("[HUD] Aucun HeatManager dans la scène.", this);

            if (playerStats != null)
            {
                playerStats.OnStatChanged += HandleStatChanged;
                playerStats.OnRoleChanged += HandleRoleChanged;
            }
            else Debug.LogWarning("[HUD] Aucun PlayerStats dans la scène.", this);

            GameEvents.NarrativeUi += HandleNarrativeUi;
            GameEvents.ArrestTriggered += HandleArrest;

            SubscribeChapter2();
            RefreshAll();
        }

        // ─── Chapitre 2 : argent, heure, quartier, objectif ───────────────

        private DistrictManager districts;
        private QuestManager questManager;

        private void SubscribeChapter2()
        {
            if (wallet != null)
            {
                wallet.OnCashChanged += HandleCashChanged;
                wallet.OnWornUsdChanged += HandleWornChanged;
                wallet.OnPaymentRefused += HandlePaymentRefused;
            }

            GameClock.HourTick += HandleHourTick;

            districts = DistrictManager.Instance;
            if (districts != null) districts.OnDistrictChanged += HandleDistrictChanged;

            questManager = QuestManager.Instance;
            if (questManager != null)
            {
                questManager.OnQuestStateChanged += HandleQuestState;
                questManager.OnObjectiveCompleted += HandleObjectiveCompleted;
            }
        }

        private void UnsubscribeChapter2()
        {
            if (wallet != null)
            {
                wallet.OnCashChanged -= HandleCashChanged;
                wallet.OnWornUsdChanged -= HandleWornChanged;
                wallet.OnPaymentRefused -= HandlePaymentRefused;
            }
            GameClock.HourTick -= HandleHourTick;
            if (districts != null) districts.OnDistrictChanged -= HandleDistrictChanged;
            if (questManager != null)
            {
                questManager.OnQuestStateChanged -= HandleQuestState;
                questManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
            }
        }

        private void HandleCashChanged(Currency currency, int oldValue, int newValue, string reason)
        {
            RefreshMoney();
            int delta = newValue - oldValue;
            SpawnNotification($"{(delta > 0 ? "+" : "-")}{CurrencyWallet.Format(currency, Mathf.Abs(delta))}",
                              delta > 0 ? positiveStatColor : negativeStatColor);
        }

        private void HandleWornChanged(int oldValue, int newValue) => RefreshMoney();

        private void HandlePaymentRefused(Currency currency, int amount, string why) => SpawnNotification(why, negativeStatColor);

        private void HandleHourTick(int day, int hour) => RefreshClock();

        private void HandleDistrictChanged(DistrictData district) => RefreshCityLine();

        private void HandleQuestState(QuestData quest, QuestState oldState, QuestState newState)
        {
            if (newState == QuestState.Active) SpawnNotification($"MISSION : {quest.title.ToUpperInvariant()}", rechercheColor);
            else if (newState == QuestState.Terminee) SpawnNotification($"MISSION TERMINÉE : {quest.title}", positiveStatColor);
            else if (newState == QuestState.Disponible && oldState == QuestState.Verrouillee)
                SpawnNotification($"Nouvelle piste : {quest.title}", calmeColor);
            RefreshObjective();
        }

        private void HandleObjectiveCompleted(QuestData quest, string objectiveId) => RefreshObjective();

        private void RefreshMoney()
        {
            if (moneyText == null || wallet == null) return;
            string worn = wallet.WornUSD > 0 ? $" <alpha=#88>(+{wallet.WornUSD} $ usés)" : "";
            moneyText.text = $"{CurrencyWallet.Format(Currency.CDF, wallet.CashCDF)}  ·  {wallet.CashUSD} ${worn}";
        }

        private void RefreshClock()
        {
            if (clockText != null && GameClock.Instance != null) clockText.text = GameClock.Instance.TimeLabel;
        }

        private void RefreshCityLine()
        {
            if (cityText == null || heat == null) return;
            var district = DistrictManager.Instance != null ? DistrictManager.Instance.CurrentDistrict : null;
            cityText.text = district != null && heat.CurrentCity == CityId.Kinshasa
                ? $"{CityLabel(heat.CurrentCity)}  <size=70%>· {district.displayName.ToUpperInvariant()}</size>"
                : CityLabel(heat.CurrentCity);
        }

        private void RefreshObjective()
        {
            if (objectiveText == null) return;
            var quests = QuestManager.Instance;
            var quest = quests != null ? quests.ActiveQuest : null;
            var objective = quests != null ? quests.CurrentObjective(quest) : null;
            objectiveText.text = objective != null ? $"<b>{quest.title}</b> — {objective.description}" : "";
        }

        private void OnDestroy()
        {
            if (heat != null)
            {
                heat.OnHeatChanged -= HandleHeatChanged;
                heat.OnHeatStateChanged -= HandleHeatStateChanged;
                heat.OnIdentityFlagsChanged -= HandleFlagsChanged;
                heat.OnCityChanged -= HandleCityChanged;
            }
            if (playerStats != null)
            {
                playerStats.OnStatChanged -= HandleStatChanged;
                playerStats.OnRoleChanged -= HandleRoleChanged;
            }
            GameEvents.NarrativeUi -= HandleNarrativeUi;
            GameEvents.ArrestTriggered -= HandleArrest;
            UnsubscribeChapter2();

            if (runtimeSprite != null) Destroy(runtimeSprite);
        }

        // ─── Réactions aux événements ─────────────────────────────────────

        private void HandleHeatChanged(CityId city, float oldValue, float newValue)
        {
            if (city != heat.CurrentCity) return; // la jauge n'affiche que la ville active
            SetFill(newValue);
            WakeHeatBlock();
        }

        private void HandleHeatStateChanged(CityId city, HeatState oldState, HeatState newState)
        {
            if (city != heat.CurrentCity) return;
            ApplyState(newState, playAlert: newState == HeatState.Traque && oldState != HeatState.Traque);
        }

        private void HandleFlagsChanged(CityId city, IdentityFlag flags)
        {
            if (city != heat.CurrentCity) return;
            UpdateIcons(flags);
            WakeHeatBlock();
        }

        private void HandleCityChanged(CityId oldCity, CityId newCity) => RefreshAll();

        private void HandleRoleChanged(PlayerRole oldRole, PlayerRole newRole, string reason) => SetRole(newRole);

        private void HandleStatChanged(StatType stat, int oldValue, int newValue, string reason)
        {
            int delta = newValue - oldValue;
            SpawnNotification($"{(delta > 0 ? "+" : "")}{delta} {StatLabel(stat)}",
                              delta > 0 ? positiveStatColor : negativeStatColor);
        }

        private void HandleNarrativeUi(string kind, string text)
        {
            if (!string.IsNullOrEmpty(text)) SpawnNotification(text, negativeStatColor);
        }

        private void HandleArrest(CityId city, PrisonId prison) =>
            SpawnNotification(prison == PrisonId.Aucune ? "ARRESTATION" : $"ARRESTATION → {PrisonLabel(prison)}", traqueColor);

        // ─── Mise à jour de l'affichage ───────────────────────────────────

        private void RefreshAll()
        {
            if (playerStats != null) SetRole(playerStats.CurrentRole);
            RefreshMoney();
            RefreshClock();
            RefreshObjective();
            if (heat == null) return;

            RefreshCityLine();
            SetFill(heat.CurrentHeat);
            ApplyState(heat.CurrentState, playAlert: false);
            UpdateIcons(heat.CurrentFlags);
            WakeHeatBlock();
        }

        private void SetRole(PlayerRole role)
        {
            if (roleText != null) roleText.text = $"Rôle : {RoleLabel(role)}";
        }

        private void SetFill(float value)
        {
            if (heatFill != null) heatFill.fillAmount = Mathf.Clamp01(value / HeatRules.Max);
        }

        private void ApplyState(HeatState state, bool playAlert)
        {
            displayedState = state;
            Color color = StateColor(state);

            if (heatFill != null) heatFill.color = color;
            if (heatStateText != null)
            {
                heatStateText.text = StateLabel(state);
                heatStateText.color = color;
            }
            if (traqueBorder != null) traqueBorder.enabled = state == HeatState.Traque;

            // Pulsation uniquement en Recherché
            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
            if (state == HeatState.Recherche) pulseRoutine = StartCoroutine(Pulse());

            if (playAlert && alertSource != null && traqueAlertClip != null)
                alertSource.PlayOneShot(traqueAlertClip);

            if (heat != null) UpdateIcons(heat.CurrentFlags); // la teinte des icônes suit l'état
            WakeHeatBlock();
        }

        private void UpdateIcons(IdentityFlag flags)
        {
            Color activeColor = displayedState == HeatState.Traque ? traqueColor : rechercheColor;

            for (int i = 0; i < identityIcons.Length && i < HeatRules.AllFlags.Length; i++)
            {
                Image icon = identityIcons[i];
                if (icon == null) continue;

                bool active = (flags & HeatRules.AllFlags[i]) != 0;
                Color c = active ? activeColor : Color.white;
                c.a = active ? 1f : inactiveIconAlpha;
                icon.color = c;

                if (iconLabels != null && iconLabels[i] != null)
                    iconLabels[i].alpha = active ? 1f : inactiveIconAlpha + 0.15f;
            }
        }

        /// <summary>Remet le bloc chaleur à pleine opacité ; en Calme, relance le minuteur de transparence.</summary>
        private void WakeHeatBlock()
        {
            if (heatBlock == null) return;

            if (calmRoutine != null) { StopCoroutine(calmRoutine); calmRoutine = null; }
            FadeHeatBlock(1f);

            if (displayedState == HeatState.Calme && isActiveAndEnabled)
                calmRoutine = StartCoroutine(FadeAfterCalm());
        }

        private void FadeHeatBlock(float target)
        {
            if (heatBlock == null || !isActiveAndEnabled) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeTo(target));
        }

        // ─── Coroutines ───────────────────────────────────────────────────

        private IEnumerator FadeAfterCalm()
        {
            yield return new WaitForSecondsRealtime(calmFadeDelay);
            FadeHeatBlock(calmBlockAlpha);
            calmRoutine = null;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = heatBlock.alpha;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                heatBlock.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
            heatBlock.alpha = target;
            fadeRoutine = null;
        }

        private IEnumerator Pulse()
        {
            while (true)
            {
                if (heatFill != null)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
                    Color c = heatFill.color;
                    c.a = Mathf.Lerp(pulseMinAlpha, 1f, k);
                    heatFill.color = c;
                }
                yield return null;
            }
        }

        private void SpawnNotification(string message, Color color)
        {
            if (notificationTemplate == null || notificationRoot == null || !isActiveAndEnabled) return;

            // Les notifications déjà affichées remontent d'un cran.
            foreach (var n in notifications) n.offset += notificationSpacing;

            while (notifications.Count >= maxNotifications) RemoveNotification(notifications[0]);

            TMP_Text text = Instantiate(notificationTemplate, notificationRoot);
            text.gameObject.SetActive(true);
            text.text = message;
            text.color = color;

            var notification = new Notification { text = text, offset = 0f };
            notifications.Add(notification);
            notification.routine = StartCoroutine(AnimateNotification(notification));
        }

        private IEnumerator AnimateNotification(Notification n)
        {
            RectTransform rt = n.text.rectTransform;
            Vector2 basePos = notificationTemplate.rectTransform.anchoredPosition;

            for (float t = 0f; t < notificationDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / notificationDuration;
                rt.anchoredPosition = basePos + new Vector2(0f, n.offset + notificationRise * k);
                n.text.alpha = k < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, k); // fondu sur les 30 % finaux
                yield return null;
            }

            n.routine = null;
            RemoveNotification(n);
        }

        private void RemoveNotification(Notification n)
        {
            if (n.routine != null) StopCoroutine(n.routine);
            if (n.text != null) Destroy(n.text.gameObject);
            notifications.Remove(n);
        }

        // ─── Utilitaires ──────────────────────────────────────────────────

        /// <summary>Une Image Filled a besoin d'un sprite : on en crée un blanc si aucun n'est assigné.</summary>
        private void EnsureFillSprite()
        {
            if (heatFill == null || heatFill.sprite != null) return;

            Texture2D tex = Texture2D.whiteTexture;
            runtimeSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            heatFill.sprite = runtimeSprite;
            heatFill.type = Image.Type.Filled;
            heatFill.fillMethod = Image.FillMethod.Horizontal;
            heatFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void CacheIconLabels()
        {
            iconLabels = new TMP_Text[identityIcons.Length];
            for (int i = 0; i < identityIcons.Length; i++)
                if (identityIcons[i] != null) iconLabels[i] = identityIcons[i].GetComponentInChildren<TMP_Text>();
        }

        private Color StateColor(HeatState s) => s switch
        {
            HeatState.Calme => calmeColor,
            HeatState.Recherche => rechercheColor,
            _ => traqueColor
        };

        private static string StateLabel(HeatState s) => s switch
        {
            HeatState.Calme => "CALME",
            HeatState.Recherche => "RECHERCHÉ",
            _ => "TRAQUÉ"
        };

        private static string RoleLabel(PlayerRole r) => r switch
        {
            PlayerRole.Civil => "CIVIL",
            PlayerRole.Juge => "JUGE",
            PlayerRole.Caid => "CAÏD",
            PlayerRole.Musicien => "MUSICIEN",
            _ => r.ToString().ToUpperInvariant()
        };

        private static string StatLabel(StatType s) => s switch
        {
            StatType.Rue => "RUE",
            StatType.Savoir => "SAVOIR",
            StatType.Influence => "INFLUENCE",
            StatType.Voix => "VOIX",
            _ => s.ToString().ToUpperInvariant()
        };

        private static string CityLabel(CityId c) => c switch
        {
            CityId.Kinshasa => "KINSHASA",
            CityId.EstRDC => "EST DE LA RDC",
            CityId.Conakry => "CONAKRY",
            CityId.Lagos => "LAGOS",
            CityId.Johannesburg => "JOHANNESBURG",
            CityId.Dakar => "DAKAR",
            CityId.Marrakech => "MARRAKECH",
            CityId.LeCaire => "LE CAIRE",
            _ => c.ToString().ToUpperInvariant()
        };

        private static string PrisonLabel(PrisonId p) => p switch
        {
            PrisonId.Makala => "MAKALA",
            PrisonId.PrisonLagos => "PRISON DE LAGOS",
            _ => ""
        };
    }
}
