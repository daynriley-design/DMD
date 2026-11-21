using UnityEngine;

namespace DMD.World
{
    /// <summary>
    /// Fait apparaître / disparaître un objet en boucle : la secrétaire du 4ème étage qui part
    /// à l'imprimante puis revient, un garde qui fait sa ronde, un guetteur qui va fumer...
    /// Placer sur un parent TOUJOURS actif ; « target » est l'objet piloté (avec son WitnessDetector).
    /// </summary>
    public class CycleToggle : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [Tooltip("Secondes pendant lesquelles l'objet est présent.")]
        [SerializeField, Min(0.1f)] private float presentSeconds = 12f;
        [Tooltip("Secondes d'absence (la fenêtre pour passer).")]
        [SerializeField, Min(0.1f)] private float absentSeconds = 8f;
        [SerializeField] private bool startPresent = true;

        private float timer;
        private bool present;

        private void OnEnable()
        {
            present = startPresent;
            timer = 0f;
            Apply();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < (present ? presentSeconds : absentSeconds)) return;
            timer = 0f;
            present = !present;
            Apply();
        }

        private void Apply()
        {
            if (target != null && target != gameObject && target.activeSelf != present) target.SetActive(present);
        }
    }
}
