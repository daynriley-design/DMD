using System;
using System.Text;
using UnityEngine;

namespace DMD.Heat
{
    /// <summary>Les 8 lieux de l'Acte I.</summary>
    public enum CityId
    {
        Kinshasa,
        EstRDC,
        Conakry,
        Lagos,
        Johannesburg,
        Dakar,
        Marrakech,
        LeCaire
    }

    /// <summary>États de la jauge : Calme (0–30), Recherché (31–70), Traqué (71–100).</summary>
    public enum HeatState
    {
        Calme,
        Recherche,
        Traque
    }

    /// <summary>Les 6 icônes d'identification (combinables : Arme | Visage).</summary>
    [Flags]
    public enum IdentityFlag
    {
        None = 0,
        Visage = 1 << 0,
        Tenue = 1 << 1,
        Vehicule = 1 << 2,
        Arme = 1 << 3,
        Camera = 1 << 4,
        Complice = 1 << 5
    }

    /// <summary>Destination en cas d'arrestation.</summary>
    public enum PrisonId
    {
        Aucune,      // ville sans prison jouable en V1 (résistance / transfert à concevoir)
        Makala,      // Kinshasa
        PrisonLagos  // Lagos
    }

    /// <summary>Constantes et règles pures de la jauge (aucun état).</summary>
    public static class HeatRules
    {
        public const float Min = 0f;
        public const float Max = 100f;
        public const float CalmeMax = 30f;
        public const float RechercheMax = 70f;

        public static readonly IdentityFlag[] AllFlags =
        {
            IdentityFlag.Visage, IdentityFlag.Tenue, IdentityFlag.Vehicule,
            IdentityFlag.Arme, IdentityFlag.Camera, IdentityFlag.Complice
        };

        public static float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public static HeatState GetState(float heat)
        {
            if (heat <= CalmeMax) return HeatState.Calme;
            if (heat <= RechercheMax) return HeatState.Recherche;
            return HeatState.Traque;
        }

        public static string FlagsToString(IdentityFlag flags)
        {
            if (flags == IdentityFlag.None) return "aucune icône";
            var sb = new StringBuilder();
            foreach (var f in AllFlags)
            {
                if ((flags & f) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(f);
            }
            return sb.ToString();
        }
    }

    /// <summary>État vivant d'une ville (modifié uniquement par HeatManager).</summary>
    [Serializable]
    public class CityHeatData
    {
        public CityId city;
        [Range(0f, 100f)] public float heatValue;
        public IdentityFlag activeFlags;
        public bool visited;

        /// <summary>Verrou : l'arrestation ne se déclenche qu'une fois tant que la jauge reste à 100.</summary>
        [NonSerialized] public bool arrestPending;

        public CityHeatData() { }
        public CityHeatData(CityId city) { this.city = city; }

        public HeatState State => HeatRules.GetState(heatValue);

        public CityHeatSnapshot ToSnapshot(bool isCurrent) =>
            new CityHeatSnapshot(city, heatValue, State, activeFlags, isCurrent);
    }

    /// <summary>Copie figée d'une ville, pour le menu « Photo ». Modifier la ville ne la change pas.</summary>
    public readonly struct CityHeatSnapshot
    {
        public readonly CityId City;
        public readonly float Heat;
        public readonly HeatState State;
        public readonly IdentityFlag Flags;
        public readonly bool IsCurrentCity;

        public CityHeatSnapshot(CityId city, float heat, HeatState state, IdentityFlag flags, bool isCurrentCity)
        {
            City = city;
            Heat = heat;
            State = state;
            Flags = flags;
            IsCurrentCity = isCurrentCity;
        }

        public override string ToString() =>
            $"{City}{(IsCurrentCity ? " (actuelle)" : "")} : {Heat:0}/100 — {State} — {HeatRules.FlagsToString(Flags)}";
    }
}
