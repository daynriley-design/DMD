using System;
using UnityEngine;

namespace DMD.Core
{
    /// <summary>
    /// Identité du joueur choisie à l'écran titre : le prénom parmi les 9 options du GDD.
    /// Sauvegardé dans PlayerPrefs pour survivre aux changements de scène et aux relances.
    /// </summary>
    public static class PlayerProfile
    {
        public static readonly string[] FirstNames =
        {
            "Obed", "Mike", "Juan", "Prince", "Luba", "Kunde", "Elia", "Bogonda", "Wissa"
        };

        private const string Key = "DMD.FirstName";

        public static event Action<string> FirstNameChanged;

        /// <summary>Prénom choisi, ou chaîne vide si aucun choix n'a été fait.</summary>
        public static string FirstName => PlayerPrefs.GetString(Key, "");

        public static bool HasFirstName => FirstName.Length > 0;

        public static void SetFirstName(string firstName)
        {
            if (Array.IndexOf(FirstNames, firstName) < 0)
            {
                Debug.LogWarning($"[PlayerProfile] « {firstName} » ne fait pas partie des 9 prénoms du GDD.");
                return;
            }

            PlayerPrefs.SetString(Key, firstName);
            PlayerPrefs.Save();
            FirstNameChanged?.Invoke(firstName);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => FirstNameChanged = null;
    }
}
