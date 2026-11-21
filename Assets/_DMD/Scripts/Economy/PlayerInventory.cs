using System;
using System.Collections.Generic;
using DMD.Core;
using UnityEngine;

namespace DMD.Economy
{
    /// <summary>
    /// Objets portés par le joueur (identifiants texte : "Arrete_Minier_Goma", "Cle_USB_Journaliste"...).
    /// Reçoit automatiquement les tags Ink #ITEM:Add:... via GameEvents.ItemAdded.
    /// Les objets « compromettants » sont confisqués en cas d'arrestation : mieux vaut les laisser au coffre.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        public static PlayerInventory Instance { get; private set; }

        [Tooltip("Objets confisqués si le joueur est arrêté en les portant.")]
        [SerializeField] private List<string> compromisingItems = new()
        {
            "Arrete_Minier_Goma", "Cle_USB_Journaliste", "Photo_Manifeste", "Faux_Badge", "Echantillon_Minerai"
        };

        [Header("État (debug)")]
        [SerializeField] private List<string> items = new();

        public event Action<string> OnItemAdded;
        public event Action<string> OnItemRemoved;

        public IReadOnlyList<string> Items => items;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable() => GameEvents.ItemAdded += Add;
        private void OnDisable() => GameEvents.ItemAdded -= Add;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public bool Has(string itemId) => items.Contains(itemId);

        public bool IsCompromising(string itemId) => compromisingItems.Contains(itemId);

        public void Add(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || items.Contains(itemId)) return;
            items.Add(itemId);
            Debug.Log($"[Inventaire] + {itemId}", this);
            OnItemAdded?.Invoke(itemId);
        }

        public bool Remove(string itemId)
        {
            if (!items.Remove(itemId)) return false;
            Debug.Log($"[Inventaire] - {itemId}", this);
            OnItemRemoved?.Invoke(itemId);
            return true;
        }

        /// <summary>Retire et renvoie tous les objets compromettants portés (confiscation).</summary>
        public List<string> ConfiscateCompromising()
        {
            var seized = items.FindAll(IsCompromising);
            foreach (var id in seized) Remove(id);
            return seized;
        }
    }
}
