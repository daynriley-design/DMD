using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Active des objets seulement pendant une plage horaire (nganda ouvert le soir, barrage de nuit, cible
    /// présente à la tombée de la nuit...). Placer sur un objet parent TOUJOURS actif et lister les enfants à piloter.
    /// </summary>
    public class TimeWindowGate : MonoBehaviour
    {
        [Tooltip("Heure d'ouverture (incluse). Ex. 19 pour la tombée de la nuit.")]
        [SerializeField, Range(0f, 24f)] private float fromHour = 19f;
        [Tooltip("Heure de fermeture (exclue). Ex. 4 : la plage passe minuit.")]
        [SerializeField, Range(0f, 24f)] private float toHour = 4f;
        [SerializeField] private GameObject[] targets = new GameObject[0];

        private void OnEnable()
        {
            GameClock.HourTick += HandleHourTick;
            Refresh();
        }

        private void OnDisable() => GameClock.HourTick -= HandleHourTick;

        private void HandleHourTick(int day, int hour) => Refresh();

        public void Refresh()
        {
            float h = GameClock.Instance != null ? GameClock.Instance.Hour : 12f;
            bool open = GameClock.IsWithin(h, fromHour, toHour);
            foreach (var target in targets)
                if (target != null && target != gameObject && target.activeSelf != open) target.SetActive(open);
        }
    }
}
