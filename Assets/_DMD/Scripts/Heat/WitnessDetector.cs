using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DMD.Heat
{
    /// <summary>
    /// Témoin « Vu vs Non vu » pour la greybox. À placer sur un cube PNJ, un policier ou une caméra.
    ///  • Champ de vision = distance + angle autour de l'axe avant (flèche bleue Z de l'objet).
    ///  • Un Physics.Raycast vérifie qu'aucun mur ne cache le joueur.
    ///  • Si le joueur est vu ET commet une action risquée (touche F = sortir une arme, ou vitesse
    ///    excessive), le témoin appelle HeatManager.Instance.AddHeat(...).
    ///  • Mur entre les deux, joueur trop loin ou témoin de dos : rien ne se passe.
    /// Gizmo : cône vert = ne voit pas le joueur, rouge = le voit.
    /// Prérequis : le joueur porte le tag "Player" et un Collider (le CharacterController des Starter Assets suffit).
    /// </summary>
    public class WitnessDetector : MonoBehaviour
    {
        public enum WitnessKind { Pnj, Policier, Camera }

        [Header("Cible")]
        [Tooltip("Laisser vide : le joueur est trouvé via son tag.")]
        [SerializeField] private Transform player;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("Hauteur visée sur le joueur (≈ torse), depuis ses pieds.")]
        [SerializeField] private float playerTargetHeight = 1.0f;

        [Header("Vision")]
        [SerializeField] private WitnessKind kind = WitnessKind.Pnj;
        [SerializeField, Min(0.1f)] private float viewDistance = 12f;
        [Tooltip("Ouverture totale du cône, en degrés.")]
        [SerializeField, Range(1f, 360f)] private float viewAngle = 110f;
        [Tooltip("Hauteur des yeux depuis le pivot de l'objet.")]
        [SerializeField] private float eyeHeight = 0.8f;
        [Tooltip("Calques qui bloquent la vue (murs) ET celui du joueur.")]
        [SerializeField] private LayerMask visionMask = ~0;

        [Header("Signalement")]
        [SerializeField] private float heatOnWeapon = 15f;
        [SerializeField] private float heatOnSpeeding = 8f;
        [Tooltip("Vitesse du joueur (m/s) considérée comme excessive. 12 m/s ≈ 43 km/h.")]
        [SerializeField] private float speedThreshold = 12f;
        [Tooltip("Délai minimal entre deux signalements de ce témoin (évite d'ajouter de la chaleur à chaque image).")]
        [SerializeField, Min(0f)] private float reportCooldown = 3f;

        [Header("Repérage à vue (agents en civil, filatures)")]
        [Tooltip("Le témoin te repère simplement en te voyant assez longtemps, sans action risquée.")]
        [SerializeField] private bool spotOnSight = false;
        [Tooltip("Ne réagit que si le joueur porte cet objet (ex. Enveloppe_Scellee). Vide = toujours.")]
        [SerializeField] private string suspiciousItem = "";
        [Tooltip("Distance de repérage (plus courte que la vision : de loin, tu n'es qu'un passant).")]
        [SerializeField, Min(0.1f)] private float spotRange = 10f;
        [Tooltip("Secondes à découvert dans la zone de repérage avant d'être interpellé.")]
        [SerializeField, Min(0.1f)] private float timeToSpot = 1.5f;
        [SerializeField] private float heatOnSpot = 10f;
        [Tooltip("Secondes hors de sa vue pour qu'il te perde (casser la ligne de vue dans la foule).")]
        [SerializeField, Min(0f)] private float loseTrackTime = 10f;
        [SerializeField, TextArea(1, 3)] private string spotBark = "Hé ! Le petit en t-shirt blanc ! Viens ici une seconde.";
        [SerializeField] private string lostBark = "Ils t'ont perdu de vue.";

        [Header("Debug")]
        [SerializeField] private bool logReports = true;

        private float awareness;      // 0 → 1 : jauge d'alerte du témoin
        private bool tracking;        // repéré : il te suit des yeux
        private float unseenTimer;

        /// <summary>Jauge d'alerte (0–1) du repérage à vue.</summary>
        public float Awareness => awareness;
        public bool IsTracking => tracking;

        private static readonly System.Collections.Generic.List<WitnessDetector> Active = new();

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// Monte la jauge d'alerte de tous les témoins en « repérage à vue » (ex. tag Ink #SUSPICION:30
        /// quand le joueur s'est montré nerveux à l'accueil). Ne touche que les témoins actuellement
        /// chargés (le 4ème étage fait partie de Zone_Gombe) ; plafonnée sous 100 % pour laisser une chance.
        /// </summary>
        public static void AddSuspicionToAll(float amount01)
        {
            foreach (var w in Active)
                if (w.spotOnSight && !w.tracking) w.awareness = Mathf.Clamp(w.awareness + amount01, 0f, 0.95f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private const float TeleportSpeed = 80f; // au-delà : téléportation / respawn, ignoré

        private bool canSeePlayer;
        private float lastReportTime = float.NegativeInfinity;
        private Vector3 lastPlayerPosition;
        private bool hasLastPosition;
        private float playerSpeed;

        public bool CanSeePlayer => canSeePlayer;

        private void Start()
        {
            TryFindPlayer();
        }

        private void Update()
        {
            if (player == null && !TryFindPlayer())
            {
                canSeePlayer = false;
                return;
            }

            UpdatePlayerSpeed();
            canSeePlayer = CheckLineOfSight();
            if (spotOnSight) UpdateSpotting();
            if (!canSeePlayer || Time.time - lastReportTime < reportCooldown) return;

            if (WeaponKeyPressed())
                Report(heatOnWeapon, IdentityFlag.Arme | IdentityFlag.Visage, "arme sortie");
            else if (playerSpeed > speedThreshold)
                Report(heatOnSpeeding, IdentityFlag.Vehicule | IdentityFlag.Visage, $"vitesse excessive ({playerSpeed * 3.6f:0} km/h)");
        }

        /// <summary>Le joueur est-il dans le cône ET sans obstacle entre les yeux du témoin et lui ?</summary>
        private bool CheckLineOfSight()
        {
            Vector3 eye = EyePosition;
            Vector3 toTarget = PlayerTargetPosition - eye;
            float distance = toTarget.magnitude;

            if (distance > viewDistance || distance < 0.001f) return false;
            if (Vector3.Angle(transform.forward, toTarget) > viewAngle * 0.5f) return false;

            // Le premier objet touché doit être le joueur (ou un de ses enfants). Sinon : un mur cache la vue.
            if (Physics.Raycast(eye, toTarget / distance, out RaycastHit hit, distance, visionMask, QueryTriggerInteraction.Ignore))
                return hit.transform == player || hit.transform.IsChildOf(player);

            return true; // rien touché : vue dégagée (joueur sans collider sur ce calque)
        }

        /// <summary>
        /// Repérage à vue : la jauge monte quand tu es visible et proche (avec l'objet suspect),
        /// redescend sinon. Pleine → interpellation + chaleur. Une fois repéré, il faut rester
        /// loseTrackTime secondes hors de sa vue pour qu'il te perde.
        /// </summary>
        private void UpdateSpotting()
        {
            bool carrying = string.IsNullOrEmpty(suspiciousItem)
                            || (DMD.Economy.PlayerInventory.Instance != null && DMD.Economy.PlayerInventory.Instance.Has(suspiciousItem));
            bool exposed = canSeePlayer && carrying &&
                           Vector3.Distance(EyePosition, PlayerTargetPosition) <= spotRange;

            if (!tracking)
            {
                float rate = exposed ? 1f / timeToSpot : -0.5f / timeToSpot;
                awareness = Mathf.Clamp01(awareness + rate * Time.deltaTime);
                if (awareness < 1f) return;

                tracking = true;
                unseenTimer = 0f;
                if (!string.IsNullOrEmpty(spotBark)) DMD.Core.GameEvents.RaiseNarrativeUi("Bark", spotBark);
                Report(heatOnSpot, IdentityFlag.Visage | IdentityFlag.Tenue, "repérage à vue");
                return;
            }

            unseenTimer = canSeePlayer ? 0f : unseenTimer + Time.deltaTime;
            if (unseenTimer < loseTrackTime) return;

            tracking = false;
            awareness = 0f;
            if (!string.IsNullOrEmpty(lostBark)) DMD.Core.GameEvents.RaiseNarrativeUi("Bark", lostBark);
            if (logReports) Debug.Log($"[Witness] {name} t'a perdu de vue.", this);
        }

        private void Report(float heat, IdentityFlag flags, string action)
        {
            var manager = HeatManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning($"[Witness] {name} a vu « {action} » mais aucun HeatManager n'est dans la scène.", this);
                lastReportTime = Time.time;
                return;
            }

            if (kind == WitnessKind.Camera) flags |= IdentityFlag.Camera;

            // Vigilance du quartier × effet de la tenue (Gombe en t-shirt de rue = très repérable).
            var district = DMD.World.DistrictManager.Instance;
            if (district != null) heat *= district.HeatMultiplier;

            lastReportTime = Time.time;
            if (logReports) Debug.Log($"[Witness] {name} ({kind}) a vu : {action} → +{heat:0.#} chaleur ({HeatRules.FlagsToString(flags)})", this);
            manager.AddHeat(heat, flags);
        }

        private void UpdatePlayerSpeed()
        {
            Vector3 position = player.position;
            if (hasLastPosition && Time.deltaTime > 0f)
            {
                float speed = (position - lastPlayerPosition).magnitude / Time.deltaTime;
                playerSpeed = speed > TeleportSpeed ? 0f : speed;
            }
            lastPlayerPosition = position;
            hasLastPosition = true;
        }

        private static bool WeaponKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        private bool TryFindPlayer()
        {
            if (string.IsNullOrEmpty(playerTag)) return false;
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go == null) return false;

            player = go.transform;
            hasLastPosition = false;
            return true;
        }

        private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        private Vector3 PlayerTargetPosition => player.position + Vector3.up * playerTargetHeight;

        // ─── Debug visuel ─────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Vector3 eye = EyePosition;
            Gizmos.color = tracking || canSeePlayer && !spotOnSight ? Color.red
                         : awareness > 0f ? Color.yellow
                         : Color.green;

            // Cône vu de dessus (arc horizontal + deux bords)
            const int segments = 24;
            float half = viewAngle * 0.5f;
            Vector3 forward = transform.forward * viewDistance;
            Vector3 previous = eye + Quaternion.AngleAxis(-half, Vector3.up) * forward;
            Gizmos.DrawLine(eye, previous);
            for (int i = 1; i <= segments; i++)
            {
                float angle = -half + viewAngle * i / segments;
                Vector3 point = eye + Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawLine(eye, previous);
            Gizmos.DrawLine(eye, eye + forward); // axe du regard

            // Ligne vers le joueur quand il est vu
            if (canSeePlayer && player != null)
                Gizmos.DrawLine(eye, PlayerTargetPosition);
        }
    }
}
