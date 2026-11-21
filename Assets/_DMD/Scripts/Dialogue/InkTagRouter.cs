using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DMD.Core;
using DMD.Economy;
using DMD.Heat;
using DMD.Quests;
using DMD.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DMD.Dialogue
{
    /// <summary>
    /// Traduit les tags Ink en actions de jeu. Ne dépend PAS d'Ink : il reçoit une simple liste de chaînes
    /// (story.currentTags), ce qui permet aussi de le tester sans dialogue.
    ///
    ///   #STAT:Influence:+5        → PlayerStats (tous les #STAT d'une même ligne sont appliqués ensemble)
    ///   #HEAT:Kinshasa:+45        → HeatManager.AddHeatToCity
    ///   #FLAG:Add:Visage          → allume l'icône dans la ville active
    ///   #FLAG:Clear:Tenue         → l'efface
    ///   #ITEM:Add:Arrete_Minier   → GameEvents.ItemAdded     #ITEM:Remove:Enveloppe_Scellee → retire de l'inventaire
    ///   #EXCHANGE:Chantal:SellUsd:10  (BuyUsd, SellWorn) → change chez un cambiste (CambisteExchange)
    ///   #EVIDENCE:Board             → mur d'enquête : débloque les missions dont on possède les preuves
    ///   #LOC:Parking_Loyola       → GameEvents.LocationChanged
    ///   #SFX:Phone_Ring           → joue le son correspondant (liste « Sons » ci-dessous)
    ///   #UI:BankNotification:"…"  → GameEvents.NarrativeUi (le HUD l'affiche)
    ///   #SCENE:Load:02_Planque    → chargé à la FIN du dialogue (FlushPendingScene)
    ///   — Chapitre 2 —
    ///   #CASH:CDF:+5000  #CASH:USD:-50  #CASH:USDUSE:+10   → portefeuille (USDUSE = billets usés)
    ///   (Pour un choix PAYANT, mettre plutôt #COST:CDF:25000 dans le choix : géré par DialogueRunner.)
    ///   #QUEST:Start:M2_1  #QUEST:Objective:M2_1:acheter_torche  #QUEST:Complete:M2_1  #QUEST:Fail:M2_4
    ///   #TIME:Advance:3  (heures)   #TIME:Until:19  (se reposer jusqu'à 19h)
    ///   #OUTFIT:ChemiseGombe        → change de tenue
    ///   #PHONE:DestroySmartphone  #PHONE:BuyBurner  #PHONE:GiveBurner  #PHONE:BuySim
    ///   #SAFEHOUSE:Unlock  #SAFEHOUSE:PayRent   → la planque (coffre, repos, loyer)
    ///   #ZONE:Gombe:Arrivee         → charge un quartier (scène additive) à la fin du dialogue
    /// Majuscules et accents sont ignorés (« Caméra » = « Camera »).
    /// </summary>
    public class InkTagRouter : MonoBehaviour
    {
        [Serializable]
        public struct SfxEntry
        {
            public string id;
            public AudioClip clip;
        }

        [Header("Références (trouvées automatiquement si vides)")]
        [SerializeField] private PlayerStats playerStats;
        [Tooltip("Portefeuille du joueur (tags #CASH).")]
        [SerializeField] private CurrencyWallet wallet;
        [Tooltip("Quartiers connus (tags #ZONE).")]
        [SerializeField] private DistrictData[] districts = Array.Empty<DistrictData>();

        [Header("Sons")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private SfxEntry[] sounds = Array.Empty<SfxEntry>();

        [Header("Debug")]
        [SerializeField] private bool logTags = true;

        private string currentLocation = "";
        private string pendingScene;
        private DistrictData pendingZone;
        private string pendingZoneSpawn;

        public string CurrentLocation => currentLocation;
        public string PendingScene => pendingScene;

        /// <summary>Applique tous les tags d'une ligne Ink.</summary>
        public void Process(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0) return;

            var statDeltas = new List<StatDelta>();
            foreach (var rawTag in tags)
            {
                if (string.IsNullOrWhiteSpace(rawTag)) continue;
                string tag = rawTag.Trim();
                if (logTags) Debug.Log($"[InkTag] #{tag}", this);

                string[] parts = tag.Split(new[] { ':' }, 3);
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                switch (Normalize(parts[0]))
                {
                    case "STAT": HandleStat(parts, statDeltas, tag); break;
                    case "HEAT": HandleHeat(parts, tag); break;
                    case "FLAG": HandleFlag(parts, tag); break;
                    case "ITEM": HandleItem(parts, tag); break;
                    case "LOC": HandleLocation(parts, tag); break;
                    case "SFX": HandleSfx(parts, tag); break;
                    case "UI": HandleUi(parts, tag); break;
                    case "SCENE": HandleScene(parts, tag); break;
                    case "CASH": HandleCash(parts, tag); break;
                    case "QUEST": HandleQuest(tag); break;
                    case "TIME": HandleTime(parts, tag); break;
                    case "OUTFIT": HandleOutfit(parts, tag); break;
                    case "PHONE": HandlePhone(parts, tag); break;
                    case "ZONE": HandleZone(parts, tag); break;
                    case "SAFEHOUSE": HandleSafehouse(parts, tag); break;
                    case "EXCHANGE": HandleExchange(tag); break;
                    case "EVIDENCE": HandleEvidence(parts, tag); break;
                    default: Warn($"tag inconnu « #{tag} »"); break;
                }
            }

            if (statDeltas.Count > 0)
            {
                var stats = FindStats();
                if (stats != null)
                {
                    string reason = string.IsNullOrEmpty(currentLocation) ? "Dialogue" : $"Dialogue — {currentLocation}";
                    stats.ModifyStats(reason, statDeltas.ToArray());
                }
                else Warn("aucun PlayerStats dans la scène : tags #STAT ignorés.");
            }
        }

        /// <summary>À appeler quand le dialogue se termine : charge la scène demandée par #SCENE:Load.</summary>
        public void FlushPendingScene()
        {
            if (pendingZone != null)
            {
                var zone = pendingZone;
                pendingZone = null;
                if (ZoneLoader.Instance != null) ZoneLoader.Instance.LoadZone(zone, pendingZoneSpawn);
                else Warn("aucun ZoneLoader dans la scène : #ZONE ignoré.");
            }

            if (string.IsNullOrEmpty(pendingScene)) return;

            string scene = pendingScene;
            pendingScene = null;

            if (Application.CanStreamedLevelBeLoaded(scene))
            {
                SceneManager.LoadScene(scene);
            }
            else
            {
                Warn($"la scène « {scene} » n'existe pas ou n'est pas dans File > Build Profiles > Scene List. Chargement ignoré.");
            }
        }

        // ─── Gestion de chaque tag ────────────────────────────────────────

        private void HandleStat(string[] p, List<StatDelta> deltas, string tag)
        {
            if (p.Length < 3 || !TryParseEnum(p[1], out StatType stat) || !TryParseInt(p[2], out int delta))
            {
                Warn($"format attendu #STAT:<Rue|Savoir|Influence|Voix>:<+/-n>, reçu « #{tag} »");
                return;
            }
            deltas.Add(new StatDelta(stat, delta));
        }

        private void HandleHeat(string[] p, string tag)
        {
            if (p.Length < 3 || !TryParseEnum(p[1], out CityId city) || !TryParseFloat(p[2], out float amount))
            {
                Warn($"format attendu #HEAT:<Ville>:<+/-n>, reçu « #{tag} »");
                return;
            }
            if (HeatManager.Instance == null) { Warn("aucun HeatManager dans la scène : #HEAT ignoré."); return; }
            HeatManager.Instance.AddHeatToCity(city, amount);
        }

        private void HandleFlag(string[] p, string tag)
        {
            if (p.Length < 3 || !TryParseEnum(p[2], out IdentityFlag flag) || flag == IdentityFlag.None)
            {
                Warn($"format attendu #FLAG:<Add|Clear>:<Visage|Tenue|Vehicule|Arme|Camera|Complice>, reçu « #{tag} »");
                return;
            }
            var heat = HeatManager.Instance;
            if (heat == null) { Warn("aucun HeatManager dans la scène : #FLAG ignoré."); return; }

            switch (Normalize(p[1]))
            {
                case "ADD": heat.AddHeat(0f, flag); break;
                case "CLEAR": heat.ClearIdentityFlag(flag); break;
                default: Warn($"action de #FLAG inconnue « {p[1]} » (Add ou Clear)"); break;
            }
        }

        private void HandleItem(string[] p, string tag)
        {
            string action = p.Length >= 2 ? Normalize(p[1]) : "";
            if (p.Length < 3 || p[2].Length == 0 || (action != "ADD" && action != "REMOVE"))
            {
                Warn($"format attendu #ITEM:<Add|Remove>:<Id>, reçu « #{tag} »");
                return;
            }

            if (action == "ADD")
            {
                GameEvents.RaiseItemAdded(p[2]);
            }
            else if (PlayerInventory.Instance == null || !PlayerInventory.Instance.Remove(p[2]))
            {
                Warn($"« {p[2]} » n'est pas dans l'inventaire : #ITEM:Remove ignoré.");
            }
        }

        private void HandleEvidence(string[] p, string tag)
        {
            if (p.Length < 2 || Normalize(p[1]) != "BOARD") { Warn($"format attendu #EVIDENCE:Board, reçu « #{tag} »"); return; }
            if (QuestManager.Instance == null) { Warn("aucun QuestManager dans la scène : #EVIDENCE ignoré."); return; }

            var unlocked = QuestManager.Instance.TryUnlockFromEvidenceBoard();
            if (unlocked.Count == 0)
                GameEvents.RaiseNarrativeUi("Enquete", "Rien de nouveau à relier sur le mur.");
        }

        private void HandleExchange(string tag)
        {
            // #EXCHANGE:Chantal:SellUsd:10 → 4 morceaux
            string[] p = tag.Split(':');
            for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();
            if (p.Length < 4 || !TryParseInt(p[3], out int usd) || usd <= 0)
            {
                Warn($"format attendu #EXCHANGE:<IdCambiste>:<SellUsd|BuyUsd|SellWorn>:<dollars>, reçu « #{tag} »");
                return;
            }

            var cambiste = CambisteExchange.Find(p[1]);
            if (cambiste == null) { Warn($"cambiste « {p[1]} » introuvable dans les scènes chargées."); return; }

            switch (Normalize(p[2]))
            {
                case "SELLUSD": cambiste.SellUsd(usd); break;
                case "BUYUSD": cambiste.BuyUsd(usd); break;
                case "SELLWORN": cambiste.SellWornUsd(usd); break;
                default: Warn($"opération de change inconnue « {p[2]} »"); break;
            }
        }

        private void HandleLocation(string[] p, string tag)
        {
            if (p.Length < 2 || p[1].Length == 0) { Warn($"format attendu #LOC:<Lieu>, reçu « #{tag} »"); return; }
            currentLocation = p[1];
            GameEvents.RaiseLocationChanged(currentLocation);
        }

        private void HandleSfx(string[] p, string tag)
        {
            if (p.Length < 2) { Warn($"format attendu #SFX:<Id>, reçu « #{tag} »"); return; }

            string id = Normalize(p[1]);
            foreach (var entry in sounds)
            {
                if (entry.clip == null || Normalize(entry.id) != id) continue;
                if (sfxSource != null) sfxSource.PlayOneShot(entry.clip);
                else AudioSource.PlayClipAtPoint(entry.clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                return;
            }
            if (logTags) Debug.Log($"[InkTag] Son « {p[1]} » pas encore assigné dans InkTagRouter > Sons.", this);
        }

        private void HandleUi(string[] p, string tag)
        {
            if (p.Length < 2) { Warn($"format attendu #UI:<Type>:\"texte\", reçu « #{tag} »"); return; }
            string text = p.Length >= 3 ? p[2].Trim().Trim('"', '“', '”') : "";
            GameEvents.RaiseNarrativeUi(p[1], text);
        }

        private void HandleScene(string[] p, string tag)
        {
            if (p.Length < 3 || Normalize(p[1]) != "LOAD" || p[2].Length == 0)
            {
                Warn($"format attendu #SCENE:Load:<NomDeScène>, reçu « #{tag} »");
                return;
            }
            pendingScene = p[2];
        }

        // ─── Chapitre 2 ───────────────────────────────────────────────────

        private void HandleCash(string[] p, string tag)
        {
            if (p.Length < 3 || !TryParseInt(p[2], out int amount))
            {
                Warn($"format attendu #CASH:<CDF|USD|USDUSE>:<+/-n>, reçu « #{tag} »");
                return;
            }
            if (wallet == null) { Warn("aucun CurrencyWallet assigné sur InkTagRouter : #CASH ignoré."); return; }

            string kind = Normalize(p[1]);
            bool worn = kind == "USDUSE";
            Currency currency = kind == "CDF" ? Currency.CDF : Currency.USD;
            if (kind != "CDF" && kind != "USD" && !worn) { Warn($"devise inconnue « {p[1]} »"); return; }

            string reason = string.IsNullOrEmpty(currentLocation) ? "Dialogue" : $"Dialogue — {currentLocation}";
            if (amount >= 0) wallet.Add(currency, amount, reason, worn);
            else if (worn) wallet.RemoveWorn(-amount);
            else wallet.TrySpend(currency, -amount, reason);
        }

        private void HandleQuest(string tag)
        {
            // #QUEST:Objective:M2_1:acheter_torche → 4 morceaux
            string[] p = tag.Split(':');
            for (int i = 0; i < p.Length; i++) p[i] = p[i].Trim();

            var quests = QuestManager.Instance;
            if (quests == null) { Warn("aucun QuestManager dans la scène : #QUEST ignoré."); return; }
            if (p.Length < 3) { Warn($"format attendu #QUEST:<Start|Objective|Complete|Fail>:<Id>[:objectif], reçu « #{tag} »"); return; }

            switch (Normalize(p[1]))
            {
                case "START": quests.StartQuest(p[2]); break;
                case "COMPLETE": quests.CompleteQuest(p[2]); break;
                case "FAIL": quests.FailQuest(quests.Find(p[2])); break;
                case "OBJECTIVE":
                    if (p.Length < 4) Warn($"objectif manquant dans « #{tag} »");
                    else quests.CompleteObjective(p[2], p[3]);
                    break;
                default: Warn($"action de #QUEST inconnue « {p[1]} »"); break;
            }
        }

        private void HandleTime(string[] p, string tag)
        {
            if (p.Length < 3 || !TryParseFloat(p[2], out float hours))
            {
                Warn($"format attendu #TIME:<Advance|Until>:<heures>, reçu « #{tag} »");
                return;
            }
            var clock = GameClock.Instance;
            if (clock == null) { Warn("aucun GameClock dans la scène : #TIME ignoré."); return; }

            switch (Normalize(p[1]))
            {
                case "ADVANCE": clock.AdvanceHours(hours); break;
                case "UNTIL": clock.RestUntil(hours); break;
                default: Warn($"action de #TIME inconnue « {p[1]} »"); break;
            }
        }

        private void HandleOutfit(string[] p, string tag)
        {
            if (p.Length < 2) { Warn($"format attendu #OUTFIT:<IdTenue>, reçu « #{tag} »"); return; }
            if (DistrictManager.Instance == null) { Warn("aucun DistrictManager dans la scène : #OUTFIT ignoré."); return; }
            DistrictManager.Instance.ChangeOutfit(p[1]);
        }

        private void HandlePhone(string[] p, string tag)
        {
            var phone = PlayerPhone.Instance;
            if (phone == null) { Warn("aucun PlayerPhone dans la scène : #PHONE ignoré."); return; }
            if (p.Length < 2) { Warn($"format attendu #PHONE:<DestroySmartphone|BuyBurner|BuySim|SwapSim>, reçu « #{tag} »"); return; }

            switch (Normalize(p[1]))
            {
                case "DESTROYSMARTPHONE": phone.DestroySmartphone(); break;
                case "BUYBURNER": phone.BuyBurner(); break;
                case "GIVEBURNER": phone.GiveBurnerWithSim(); break; // prix déjà réglé par un #CASH
                case "BUYSIM": phone.BuySim(); break;
                case "SWAPSIM": phone.SwapSim(); break;
                default: Warn($"action de #PHONE inconnue « {p[1]} »"); break;
            }
        }

        private void HandleSafehouse(string[] p, string tag)
        {
            var safehouse = Safehouse.Instance;
            if (safehouse == null) { Warn("aucune Safehouse dans la scène : #SAFEHOUSE ignoré."); return; }
            if (p.Length < 2) { Warn($"format attendu #SAFEHOUSE:<Unlock|PayRent>, reçu « #{tag} »"); return; }

            switch (Normalize(p[1]))
            {
                case "UNLOCK": safehouse.Unlock(); break;
                case "PAYRENT": safehouse.PayRent(); break;
                default: Warn($"action de #SAFEHOUSE inconnue « {p[1]} »"); break;
            }
        }

        private void HandleZone(string[] p, string tag)
        {
            if (p.Length < 2 || !TryParseEnum(p[1], out DistrictId id))
            {
                Warn($"format attendu #ZONE:<Limete|MatongeKalamu|Gombe>[:PointArrivee], reçu « #{tag} »");
                return;
            }
            foreach (var d in districts)
            {
                if (d == null || d.id != id) continue;
                pendingZone = d;
                pendingZoneSpawn = p.Length >= 3 ? p[2] : "Arrivee";
                return;
            }
            Warn($"quartier {id} absent de la liste « Districts » d'InkTagRouter.");
        }

        // ─── Utilitaires ──────────────────────────────────────────────────

        private PlayerStats FindStats()
        {
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
            return playerStats;
        }

        private static bool TryParseEnum<T>(string value, out T result) where T : struct, Enum
        {
            if (Enum.TryParse(value, true, out result)) return true;

            string wanted = Normalize(value);
            foreach (T candidate in Enum.GetValues(typeof(T)))
            {
                if (Normalize(candidate.ToString()) == wanted) { result = candidate; return true; }
            }
            return false;
        }

        private static bool TryParseInt(string s, out int value) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static bool TryParseFloat(string s, out float value) =>
            float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>Majuscules, sans accents ni espaces : « Caméra » → « CAMERA ».</summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || char.IsWhiteSpace(c)) continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        private void Warn(string message) => Debug.LogWarning($"[InkTag] {message}", this);
    }
}
