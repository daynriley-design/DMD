using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DMD.EditorTools
{
    /// <summary>
    /// Ajoute (ou retire) automatiquement le symbole DMD_INK selon que le paquet Ink est installé.
    /// DialogueRunner n'utilise Ink que si DMD_INK existe : le projet compile donc avec ou sans Ink.
    /// Ce script vit dans sa propre assembly (DMD.Editor.InkDefine) pour toujours compiler,
    /// même si le reste du projet a une erreur.
    /// </summary>
    [InitializeOnLoad]
    internal static class InkDefineSetter
    {
        private const string Define = "DMD_INK";

        static InkDefineSetter()
        {
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            bool inkInstalled = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => SafeHasType(a, "Ink.Runtime.Story"));

            var targets = new HashSet<NamedBuildTarget> { NamedBuildTarget.Standalone };
            try
            {
                var active = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                if (active != NamedBuildTarget.Unknown) targets.Add(active);
            }
            catch (ArgumentException) { /* plateforme sans cible nommée : Standalone suffit */ }

            foreach (var target in targets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                bool hasDefine = defines.Contains(Define);
                if (hasDefine == inkInstalled) continue;

                var updated = inkInstalled
                    ? defines.Append(Define).ToArray()
                    : defines.Where(d => d != Define).ToArray();
                PlayerSettings.SetScriptingDefineSymbols(target, updated);
                Debug.Log($"[DMD] Ink {(inkInstalled ? "détecté : symbole DMD_INK ajouté" : "absent : symbole DMD_INK retiré")} ({target.TargetName}).");
            }
        }

        private static bool SafeHasType(System.Reflection.Assembly assembly, string typeName)
        {
            try { return assembly.GetType(typeName, false) != null; }
            catch { return false; }
        }
    }
}
