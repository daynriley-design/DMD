using System.Collections.Generic;
using DMD.Core;
using DMD.Heat;
using UnityEngine;

namespace DMD.UI
{
    /// <summary>
    /// Overlay de DEBUG (IMGUI, aucun Canvas) — complète le vrai HUD (HudController) pendant la greybox.
    ///  • Sous le bloc ville/rôle : les 4 stats en barres (le trait blanc = seuil de régression du rôle).
    ///  • Bas-gauche : journal détaillé (variations avec leur raison, régression, transcendance, arrestation...).
    /// Dans le jeu final, les stats vivent dans le menu téléphone : désactiver ce composant pour la démo.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private bool showHud = true;
        [SerializeField, Range(0.5f, 3f)] private float uiScale = 1f;
        [SerializeField, Min(0.5f)] private float messageDuration = 4f;
        [SerializeField, Min(1)] private int maxMessages = 6;

        private struct HudMessage
        {
            public string text;
            public Color color;
            public float expiresAt;
        }

        private static readonly StatType[] AllStats = (StatType[])System.Enum.GetValues(typeof(StatType));
        private static readonly string[] FlagLabels = { "VISAGE", "TENUE", "VÉHIC.", "ARME", "CAMÉRA", "COMPL." };

        private static readonly Color Panel = new(0f, 0f, 0f, 0.6f);
        private static readonly Color BarBack = new(1f, 1f, 1f, 0.15f);
        private static readonly Color Calme = new(0.3f, 0.85f, 0.4f);
        private static readonly Color Recherche = new(1f, 0.65f, 0.1f);
        private static readonly Color Traque = new(0.95f, 0.2f, 0.2f);
        private static readonly Color FlagOff = new(1f, 1f, 1f, 0.25f);

        private readonly List<HudMessage> messages = new();
        private GUIStyle label;
        private GUIStyle title;
        private GUIStyle small;

        private void Start()
        {
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
        }

        private void OnEnable()
        {
            GameEvents.StatChanged += HandleStatChanged;
            GameEvents.RoleChanged += HandleRoleChanged;
            GameEvents.TranscendenceUnlocked += HandleTranscendence;
            GameEvents.CivilCollapse += HandleCollapse;
            GameEvents.HeatStateChanged += HandleHeatState;
            GameEvents.ArrestTriggered += HandleArrest;
            GameEvents.CityChanged += HandleCity;
        }

        private void OnDisable()
        {
            GameEvents.StatChanged -= HandleStatChanged;
            GameEvents.RoleChanged -= HandleRoleChanged;
            GameEvents.TranscendenceUnlocked -= HandleTranscendence;
            GameEvents.CivilCollapse -= HandleCollapse;
            GameEvents.HeatStateChanged -= HandleHeatState;
            GameEvents.ArrestTriggered -= HandleArrest;
            GameEvents.CityChanged -= HandleCity;
        }

        // ─── Réactions aux événements ─────────────────────────────────────

        private void HandleStatChanged(StatType stat, int oldV, int newV, string reason)
        {
            int d = newV - oldV;
            Push($"{(d > 0 ? "+" : "")}{d} {stat}  ({reason})", d > 0 ? Calme : Recherche);
        }

        private void HandleRoleChanged(PlayerRole oldR, PlayerRole newR, string reason) =>
            Push($"Rôle : {oldR} → {newR}  ({reason})", Color.cyan);

        private void HandleTranscendence(StatType stat, PlayerRole target)
        {
            int threshold = playerStats != null ? playerStats.Config.transcendenceThreshold : 60;
            Push($"Mission de bascule disponible : {target} ({stat} ≥ {threshold})", Color.yellow);
        }

        private void HandleCollapse() =>
            Push("EFFONDREMENT : toutes les stats sont au plus bas. Fin prématurée.", Traque);

        private void HandleHeatState(CityId city, HeatState oldS, HeatState newS) =>
            Push($"{city} : {StateLabel(oldS)} → {StateLabel(newS)}", StateColor(newS));

        private void HandleArrest(CityId city, PrisonId prison) =>
            Push($"ARRESTATION à {city} → {(prison == PrisonId.Aucune ? "pas de prison ici" : prison.ToString())}", Traque);

        private void HandleCity(CityId oldC, CityId newC) => Push($"Arrivée : {newC}", Color.white);

        private void Push(string text, Color color)
        {
            messages.Add(new HudMessage { text = text, color = color, expiresAt = Time.unscaledTime + messageDuration });
            while (messages.Count > maxMessages) messages.RemoveAt(0);
        }

        // ─── Dessin ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!showHud) return;
            EnsureStyles();

            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            // La jauge de chaleur est affichée par HudController (haut-droite) : ici, seulement les stats.
            if (HeatManager.Instance == null) DrawHeatPanel(16f, 16f);
            DrawStatsPanel(16f, 110f);
            DrawMessages(16f, Screen.height / uiScale - 16f);

            GUI.matrix = previousMatrix;
        }

        private float DrawHeatPanel(float x, float y)
        {
            const float w = 320f, h = 92f;
            Fill(new Rect(x, y, w, h), Panel);

            var manager = HeatManager.Instance;
            if (manager == null)
            {
                GUI.Label(new Rect(x + 10, y + 8, w - 20, 22), "HeatManager absent de la scène", label);
                return y + h;
            }

            HeatState state = manager.CurrentState;
            float heat = manager.CurrentHeat;
            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), $"{manager.CurrentCity}  —  {StateLabel(state)}", title);
            DrawBar(new Rect(x + 10, y + 32, w - 70, 14), heat / HeatRules.Max, StateColor(state));
            GUI.Label(new Rect(x + w - 56, y + 28, 50, 22), $"{heat:0}", label);

            IdentityFlag flags = manager.CurrentFlags;
            float cellW = (w - 20) / HeatRules.AllFlags.Length;
            for (int i = 0; i < HeatRules.AllFlags.Length; i++)
            {
                bool on = (flags & HeatRules.AllFlags[i]) != 0;
                var r = new Rect(x + 10 + i * cellW, y + 56, cellW - 4, 26);
                Fill(r, on ? Traque : BarBack);
                GUI.color = on ? Color.white : FlagOff;
                GUI.Label(r, FlagLabels[i], small);
                GUI.color = Color.white;
            }
            return y + h;
        }

        private void DrawStatsPanel(float x, float y)
        {
            const float w = 320f, rowH = 22f;
            float h = 34f + AllStats.Length * rowH;
            Fill(new Rect(x, y, w, h), Panel);

            if (playerStats == null)
            {
                GUI.Label(new Rect(x + 10, y + 8, w - 20, 22), "PlayerStats introuvable", label);
                return;
            }

            var cfg = playerStats.Config;
            bool hasPrimary = cfg.TryGetPrimaryStat(playerStats.CurrentRole, out StatType primary);
            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), $"Rôle : {playerStats.CurrentRole}", title);

            float range = Mathf.Max(1, cfg.maxValue - cfg.minValue);
            for (int i = 0; i < AllStats.Length; i++)
            {
                StatType stat = AllStats[i];
                int value = playerStats.GetStat(stat);
                float rowY = y + 32 + i * rowH;
                bool isPrimary = hasPrimary && stat == primary;

                GUI.Label(new Rect(x + 10, rowY - 3, 80, 20), isPrimary ? $"{stat} ★" : stat.ToString(), label);
                var bar = new Rect(x + 95, rowY, w - 150, 12);
                DrawBar(bar, (value - cfg.minValue) / range, isPrimary ? Color.cyan : new Color(0.75f, 0.75f, 0.85f));
                if (isPrimary)
                {
                    float tx = bar.x + bar.width * ((cfg.regressionThreshold - cfg.minValue) / range);
                    Fill(new Rect(tx - 1, bar.y - 2, 2, bar.height + 4), Color.white);
                }
                GUI.Label(new Rect(x + w - 50, rowY - 3, 44, 20), value.ToString(), label);
            }
        }

        private void DrawMessages(float x, float bottom)
        {
            float now = Time.unscaledTime;
            messages.RemoveAll(m => m.expiresAt <= now);

            float y = bottom - messages.Count * 24f;
            foreach (var m in messages)
            {
                float alpha = Mathf.Clamp01((m.expiresAt - now) / 0.5f); // fondu sur la dernière demi-seconde
                Fill(new Rect(x, y, 520, 22), new Color(0, 0, 0, 0.55f * alpha));
                GUI.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
                GUI.Label(new Rect(x + 8, y + 1, 510, 20), m.text, label);
                GUI.color = Color.white;
                y += 24f;
            }
        }

        // ─── Utilitaires ──────────────────────────────────────────────────

        private static void DrawBar(Rect r, float t, Color color)
        {
            Fill(r, BarBack);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), color);
        }

        private static void Fill(Rect r, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string StateLabel(HeatState s) => s switch
        {
            HeatState.Calme => "Calme",
            HeatState.Recherche => "Recherché",
            _ => "Traqué"
        };

        private static Color StateColor(HeatState s) => s switch
        {
            HeatState.Calme => Calme,
            HeatState.Recherche => Recherche,
            _ => Traque
        };

        private void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            title = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
