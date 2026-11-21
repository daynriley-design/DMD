using DMD.Economy;
using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Passage vers un autre quartier : arrêt de taxi jaune, station de moto wewa, bout de ruelle...
    /// Zone de déclenchement (Collider en « Is Trigger ») : quand le joueur y entre, le trajet est payé en CDF
    /// puis le quartier cible est chargé. Sans argent, le joueur doit y aller à pied (autre transition gratuite).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTransition : MonoBehaviour
    {
        public enum Transport { APied, TaxiJaune, MotoWewa }

        [SerializeField] private DistrictData destination;
        [SerializeField] private string destinationSpawnId = "Arrivee";
        [SerializeField] private Transport transport = Transport.MotoWewa;
        [SerializeField, Min(0)] private int fareCDF = 1500;
        [SerializeField] private CurrencyWallet wallet;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("Délai avant de pouvoir réutiliser ce passage (évite un aller-retour immédiat).")]
        [SerializeField, Min(0f)] private float cooldown = 2f;
        [Tooltip("Passage fermé tant que cette mission n'est pas terminée (ex. M2_3 pour Gombe). Vide = toujours ouvert.")]
        [SerializeField] private string requiredQuestCompleted = "";
        [SerializeField] private string lockedMessage = "Rien à faire là-bas pour l'instant.";

        private float lastUse = float.NegativeInfinity;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag) || Time.time - lastUse < cooldown) return;
            if (destination == null || ZoneLoader.Instance == null || ZoneLoader.Instance.IsLoading) return;

            if (!string.IsNullOrEmpty(requiredQuestCompleted) && DMD.Quests.QuestManager.Instance != null)
            {
                var quests = DMD.Quests.QuestManager.Instance;
                var quest = quests.Find(requiredQuestCompleted);
                if (quest != null && quests.GetState(quest) != DMD.Quests.QuestState.Terminee)
                {
                    lastUse = Time.time; // évite de répéter le message à chaque frôlement
                    DMD.Core.GameEvents.RaiseNarrativeUi("Verrou", lockedMessage);
                    return;
                }
            }

            if (transport != Transport.APied && fareCDF > 0)
            {
                if (wallet == null)
                {
                    Debug.LogWarning($"[Transport] {name} : aucun CurrencyWallet assigné, trajet gratuit.", this);
                }
                else if (!wallet.TrySpend(Currency.CDF, fareCDF, $"{transport} vers {destination.displayName}"))
                {
                    Debug.Log($"[Transport] Pas assez de francs pour le {transport} ({CurrencyWallet.Format(Currency.CDF, fareCDF)}).", this);
                    return;
                }
            }

            lastUse = Time.time;
            ZoneLoader.Instance.LoadZone(destination, destinationSpawnId);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = transport == Transport.APied ? new Color(0.6f, 1f, 0.6f, 0.35f) : new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var box = GetComponent<BoxCollider>();
            if (box != null) Gizmos.DrawCube(box.center, box.size);
            else Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
    }
}
