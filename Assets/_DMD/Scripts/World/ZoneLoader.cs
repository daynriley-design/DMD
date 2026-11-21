using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DMD.World
{
    /// <summary>
    /// Chargement des quartiers en scènes additives (SceneManager.LoadSceneAsync).
    /// La scène Persistent_Core (joueur, UI, argent, horloge, audio...) reste chargée en permanence ;
    /// un seul quartier (Zone_Limete, Zone_Matonge, Zone_Gombe) est chargé à la fois.
    /// À l'arrivée, le joueur est placé sur le ZoneSpawnPoint demandé.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneLoader : MonoBehaviour
    {
        public static ZoneLoader Instance { get; private set; }

        [SerializeField] private DistrictData startingDistrict;
        [SerializeField] private string startingSpawnId = "Planque";
        [SerializeField] private string playerTag = "Player";

        [Header("État (debug)")]
        [SerializeField] private string loadedSceneName;

        public event Action<DistrictData> OnZoneLoadStarted;
        public event Action<DistrictData> OnZoneLoaded;

        public bool IsLoading { get; private set; }
        public DistrictData CurrentDistrict { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            if (startingDistrict != null) LoadZone(startingDistrict, startingSpawnId);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public void LoadZone(DistrictData district, string spawnId)
        {
            if (district == null || IsLoading) return;

            if (!Application.CanStreamedLevelBeLoaded(district.sceneName))
            {
                Debug.LogError($"[Zones] La scène « {district.sceneName} » n'est pas dans File > Build Profiles > Scene List.", this);
                return;
            }
            StartCoroutine(LoadRoutine(district, spawnId));
        }

        private IEnumerator LoadRoutine(DistrictData district, string spawnId)
        {
            IsLoading = true;
            OnZoneLoadStarted?.Invoke(district);

            // Le joueur est mis en pause pendant le chargement : sinon il tombe dans le vide
            // entre le déchargement de l'ancien quartier et l'arrivée du nouveau.
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) player.SetActive(false);

            // Si le quartier est déjà chargé (ex. lancé depuis l'éditeur avec la zone ouverte), on le garde.
            Scene already = SceneManager.GetSceneByName(district.sceneName);
            if (!already.isLoaded)
            {
                if (!string.IsNullOrEmpty(loadedSceneName) && SceneManager.GetSceneByName(loadedSceneName).isLoaded)
                    yield return SceneManager.UnloadSceneAsync(loadedSceneName);

                yield return SceneManager.LoadSceneAsync(district.sceneName, LoadSceneMode.Additive);
            }

            Scene zone = SceneManager.GetSceneByName(district.sceneName);
            SceneManager.SetActiveScene(zone); // éclairage et objets instanciés vont dans le quartier
            loadedSceneName = district.sceneName;
            CurrentDistrict = district;

            if (player != null)
            {
                PlacePlayer(player, zone, spawnId);
                player.SetActive(true);
            }
            if (DistrictManager.Instance != null) DistrictManager.Instance.SetDistrict(district);

            IsLoading = false;
            Debug.Log($"[Zones] {district.displayName} chargé.", this);
            OnZoneLoaded?.Invoke(district);
        }

        private void PlacePlayer(GameObject player, Scene zone, string spawnId)
        {
            ZoneSpawnPoint target = null;
            foreach (var root in zone.GetRootGameObjects())
            {
                foreach (var spawn in root.GetComponentsInChildren<ZoneSpawnPoint>(true))
                {
                    if (target == null) target = spawn; // repli : premier point trouvé
                    if (spawn.Id == spawnId) { target = spawn; break; }
                }
            }
            if (target == null)
            {
                Debug.LogWarning($"[Zones] Aucun ZoneSpawnPoint dans {zone.name} : le joueur reste où il est.", this);
                return;
            }

            // Le CharacterController écrase la position s'il reste actif pendant la téléportation.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
