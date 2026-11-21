using UnityEngine;

namespace DMD.World
{
    /// <summary>Point d'arrivée du joueur dans un quartier (ex. "Planque", "Arret_Wewa", "Taxi_Victoire").</summary>
    public class ZoneSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id = "Planque";
        public string Id => id;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward);
        }
    }
}
