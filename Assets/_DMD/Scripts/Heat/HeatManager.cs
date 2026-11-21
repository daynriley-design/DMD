using System;
using System.Collections.Generic;
using System.Text;
using DMD.Core;
using UnityEngine;

namespace DMD.Heat
{
    /// <summary>
    /// Jauge de chaleur locale par ville (inspirée de GTA).
    ///  • Ville active : mise à jour en temps réel (AddHeat / ClearIdentityFlag).
    ///  • Villes quittées : aucune simulation continue ; refroidissement passif via ApplyPassiveCooldown.
    ///  • Menu « Photo » : GetAllCitiesSnapshot() renvoie une copie figée des villes visitées.
    ///  • À 100 dans la ville active : OnArrestTriggered (Kinshasa → Makala, Lagos → Prison de Lagos).
    /// Rappel GDD : n'appeler AddHeat que pour une action VUE ou ENTENDUE (voir WitnessDetector).
    /// Un seul HeatManager dans le jeu (singleton, persiste entre les scènes).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // prêt avant les témoins et le HUD
    public class HeatManager : MonoBehaviour
    {
        public static HeatManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private CityId startingCity = CityId.Kinshasa;
        [Tooltip("Points de chaleur perdus par jour écoulé dans une ville quittée.")]
        [SerializeField, Min(0f)] private float cooldownPerDay = 5f;
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("État (visible pour le debug)")]
        [SerializeField] private CityId currentCity;
        [SerializeField] private List<CityHeatData> cities = new();

        [Header("Debug")]
        [SerializeField] private bool logChanges = true;

        private static readonly CityId[] AllCities = (CityId[])Enum.GetValues(typeof(CityId));
        private readonly Dictionary<CityId, CityHeatData> lookup = new();

        // ─── Événements ───────────────────────────────────────────────────
        /// <summary>(ville, ancienne chaleur, nouvelle chaleur)</summary>
        public event Action<CityId, float, float> OnHeatChanged;
        /// <summary>(ville, ancien état, nouvel état)</summary>
        public event Action<CityId, HeatState, HeatState> OnHeatStateChanged;
        /// <summary>(ville, icônes actives)</summary>
        public event Action<CityId, IdentityFlag> OnIdentityFlagsChanged;
        /// <summary>(ancienne ville, nouvelle ville)</summary>
        public event Action<CityId, CityId> OnCityChanged;
        /// <summary>Jauge à 100 dans la ville active. Utiliser GetPrisonFor(city) pour la destination.</summary>
        public event Action<CityId> OnArrestTriggered;

        public CityId CurrentCity => currentCity;
        public float CurrentHeat => GetData(currentCity).heatValue;
        public HeatState CurrentState => GetData(currentCity).State;
        public IdentityFlag CurrentFlags => GetData(currentCity).activeFlags;

        // ─── Cycle de vie ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Cas normal en changeant de chapitre : celui de la scène précédente persiste et garde la chaleur.
                Debug.Log("[HeatManager] Un HeatManager persistant existe déjà : celui de cette scène est retiré.", this);
                bool aloneOnObject = GetComponents<Component>().Length <= 2; // Transform + HeatManager
                if (aloneOnObject) Destroy(gameObject);
                else Destroy(this); // ne pas emporter les autres systèmes posés sur le même objet
                return;
            }

            Instance = this;
            if (persistAcrossScenes && GetComponents<Component>().Length > 2)
            {
                Debug.LogWarning("[HeatManager] Pour persister entre les scènes, HeatManager doit être seul sur son objet " +
                                 "(sinon les autres composants persisteraient avec lui). Persistance désactivée.", this);
            }
            else if (persistAcrossScenes)
            {
                transform.SetParent(null); // DontDestroyOnLoad exige un objet racine
                DontDestroyOnLoad(gameObject);
            }

            BuildLookup();
            currentCity = startingCity;
            GetData(currentCity).visited = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // ─── API temps réel (ville active) ────────────────────────────────

        /// <summary>
        /// Ajoute de la chaleur à la ville active et allume les icônes observées.
        /// Exemple : AddHeat(15f, IdentityFlag.Arme | IdentityFlag.Visage).
        /// Une valeur négative fait baisser la jauge (ex. pot-de-vin).
        /// </summary>
        public void AddHeat(float amount, IdentityFlag observedFlags = IdentityFlag.None)
        {
            var data = GetData(currentCity);

            // Icônes d'abord : les abonnés à l'arrestation voient l'état complet.
            if (observedFlags != IdentityFlag.None)
                SetFlags(data, data.activeFlags | observedFlags);

            if (!Mathf.Approximately(amount, 0f))
                SetHeat(data, data.heatValue + amount);
        }

        /// <summary>
        /// Ajoute de la chaleur à une ville précise (événement narratif, ex. tag Ink #HEAT:Kinshasa:+45).
        /// Si c'est la ville active, équivaut à AddHeat. Sinon la ville est marquée comme visitée.
        /// </summary>
        public void AddHeatToCity(CityId city, float amount, IdentityFlag observedFlags = IdentityFlag.None)
        {
            if (city == currentCity)
            {
                AddHeat(amount, observedFlags);
                return;
            }

            var data = GetData(city);
            data.visited = true;
            if (observedFlags != IdentityFlag.None) SetFlags(data, data.activeFlags | observedFlags);
            if (!Mathf.Approximately(amount, 0f)) SetHeat(data, data.heatValue + amount);
        }

        /// <summary>
        /// Efface une ou plusieurs icônes dans la ville active
        /// (changer de tenue → Tenue, repeindre → Vehicule, ranger l'arme → Arme...).
        /// </summary>
        public void ClearIdentityFlag(IdentityFlag flag)
        {
            var data = GetData(currentCity);
            SetFlags(data, data.activeFlags & ~flag);
        }

        /// <summary>Le joueur arrive dans une autre ville : elle devient la ville active.</summary>
        public void SetCurrentCity(CityId city)
        {
            if (city == currentCity) return;

            var oldCity = currentCity;
            currentCity = city;
            var data = GetData(city);
            data.visited = true;

            if (logChanges) Debug.Log($"[HeatManager] Ville : {oldCity} → {city} ({data.heatValue:0}/100, {data.State})", this);
            OnCityChanged?.Invoke(oldCity, city);
            GameEvents.RaiseCityChanged(oldCity, city);

            CheckArrest(data); // revenir dans une ville où l'on est à 100 déclenche l'arrestation
        }

        // ─── Menu « Photo » ───────────────────────────────────────────────

        /// <summary>Copie figée de toutes les villes visitées, à appeler à l'ouverture du menu.</summary>
        public IReadOnlyList<CityHeatSnapshot> GetAllCitiesSnapshot()
        {
            var list = new List<CityHeatSnapshot>();
            foreach (var city in AllCities)
            {
                var data = GetData(city);
                if (data.visited) list.Add(data.ToSnapshot(city == currentCity));
            }
            return list;
        }

        // ─── Villes quittées ──────────────────────────────────────────────

        /// <summary>Baisse la chaleur d'une ville quittée de cooldownPerDay (5) points par jour écoulé.</summary>
        public void ApplyPassiveCooldown(CityId city, int daysPassed)
        {
            if (daysPassed <= 0) return;
            if (city == currentCity)
            {
                Debug.LogWarning($"[HeatManager] {city} est la ville active : pas de refroidissement passif.", this);
                return;
            }

            var data = GetData(city);
            if (!data.visited || data.heatValue <= HeatRules.Min) return;

            SetHeat(data, data.heatValue - cooldownPerDay * daysPassed);
        }

        /// <summary>Applique le refroidissement passif à toutes les villes visitées sauf la ville active.</summary>
        public void ApplyPassiveCooldownToAllInactive(int daysPassed)
        {
            foreach (var city in AllCities)
                if (city != currentCity) ApplyPassiveCooldown(city, daysPassed);
        }

        // ─── Arrestation ──────────────────────────────────────────────────

        public static PrisonId GetPrisonFor(CityId city) => city switch
        {
            CityId.Kinshasa => PrisonId.Makala,
            CityId.Lagos => PrisonId.PrisonLagos,
            _ => PrisonId.Aucune
        };

        /// <summary>À appeler une fois l'arrestation traitée (entrée en prison, fuite réussie...).</summary>
        public void ResolveArrest(CityId city, float heatAfter = 0f, bool clearFlags = true)
        {
            var data = GetData(city);
            data.arrestPending = false;
            if (clearFlags) SetFlags(data, IdentityFlag.None);
            SetHeat(data, heatAfter);
        }

        // ─── Lecture ──────────────────────────────────────────────────────

        public float GetHeat(CityId city) => GetData(city).heatValue;
        public HeatState GetState(CityId city) => GetData(city).State;
        public IdentityFlag GetFlags(CityId city) => GetData(city).activeFlags;
        public bool IsVisited(CityId city) => GetData(city).visited;

        // ─── Interne ──────────────────────────────────────────────────────

        private CityHeatData GetData(CityId city)
        {
            if (lookup.Count == 0) BuildLookup();
            return lookup[city];
        }

        /// <summary>Une entrée par ville, en gardant les valeurs réglées à la main dans l'Inspecteur.</summary>
        private void BuildLookup()
        {
            lookup.Clear();
            foreach (var data in cities)
                if (data != null && !lookup.ContainsKey(data.city)) lookup.Add(data.city, data);

            cities.Clear();
            foreach (var city in AllCities)
            {
                if (!lookup.TryGetValue(city, out var data))
                {
                    data = new CityHeatData(city);
                    lookup.Add(city, data);
                }
                data.heatValue = HeatRules.Clamp(data.heatValue);
                cities.Add(data);
            }
        }

        private void SetHeat(CityHeatData data, float value)
        {
            float oldValue = data.heatValue;
            float newValue = HeatRules.Clamp(value);

            if (!Mathf.Approximately(oldValue, newValue))
            {
                var oldState = HeatRules.GetState(oldValue);
                data.heatValue = newValue;
                var newState = data.State;

                if (logChanges) Debug.Log($"[HeatManager] {data.city} : {oldValue:0} → {newValue:0} ({newState})", this);
                OnHeatChanged?.Invoke(data.city, oldValue, newValue);
                GameEvents.RaiseHeatChanged(data.city, oldValue, newValue);

                if (oldState != newState)
                {
                    OnHeatStateChanged?.Invoke(data.city, oldState, newState);
                    GameEvents.RaiseHeatStateChanged(data.city, oldState, newState);
                }

                if (newValue < HeatRules.Max) data.arrestPending = false;
            }

            CheckArrest(data);
        }

        private void SetFlags(CityHeatData data, IdentityFlag flags)
        {
            if (data.activeFlags == flags) return;
            data.activeFlags = flags;

            if (logChanges) Debug.Log($"[HeatManager] {data.city} icônes : {HeatRules.FlagsToString(flags)}", this);
            OnIdentityFlagsChanged?.Invoke(data.city, flags);
            GameEvents.RaiseIdentityFlagsChanged(data.city, flags);
        }

        private void CheckArrest(CityHeatData data)
        {
            if (data.city != currentCity || data.arrestPending || data.heatValue < HeatRules.Max) return;

            data.arrestPending = true;
            var prison = GetPrisonFor(data.city);
            Debug.LogWarning($"[HeatManager] ARRESTATION à {data.city} → {prison}", this);
            OnArrestTriggered?.Invoke(data.city);
            GameEvents.RaiseArrestTriggered(data.city, prison);
        }

        // ─── Tests rapides dans l'Inspecteur ──────────────────────────────

        [ContextMenu("DMD Test/Crime vu avec arme (+25 : Arme, Visage, Tenue)")]
        private void TestArmedCrime()
        {
            if (CheckPlaying()) AddHeat(25f, IdentityFlag.Arme | IdentityFlag.Visage | IdentityFlag.Tenue);
        }

        [ContextMenu("DMD Test/Délit mineur vu (+2 : Visage)")]
        private void TestMinorOffense()
        {
            if (CheckPlaying()) AddHeat(2f, IdentityFlag.Visage);
        }

        [ContextMenu("DMD Test/Changement de tenue (efface Tenue)")]
        private void TestChangeOutfit()
        {
            if (CheckPlaying()) ClearIdentityFlag(IdentityFlag.Tenue);
        }

        [ContextMenu("DMD Test/Ranger l'arme (efface Arme)")]
        private void TestHolster()
        {
            if (CheckPlaying()) ClearIdentityFlag(IdentityFlag.Arme);
        }

        [ContextMenu("DMD Test/Changer de ville (suivante)")]
        private void TestNextCity()
        {
            if (!CheckPlaying()) return;
            int next = ((int)currentCity + 1) % AllCities.Length;
            SetCurrentCity(AllCities[next]);
        }

        [ContextMenu("DMD Test/Passer 3 jours (refroidit les villes quittées)")]
        private void TestPassDays()
        {
            if (CheckPlaying()) ApplyPassiveCooldownToAllInactive(3);
        }

        [ContextMenu("DMD Test/Ouvrir le menu Photo (console)")]
        private void TestSnapshot()
        {
            if (!CheckPlaying()) return;
            var sb = new StringBuilder("[HeatManager] Menu Photo :");
            foreach (var snap in GetAllCitiesSnapshot()) sb.Append("\n  • ").Append(snap);
            Debug.Log(sb.ToString(), this);
        }

        private bool CheckPlaying()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[HeatManager] Lance le mode Play pour utiliser les tests.", this);
            return false;
        }
    }
}
