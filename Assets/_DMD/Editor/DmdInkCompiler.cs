#if INK_EDITOR || INK_RUNTIME
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DMD.EditorTools
{
    /// <summary>
    /// Menu DMD > Compiler tous les dialogues Ink : compile chaque .ink de Assets/_DMD/Dialogues en .json
    /// et affiche les erreurs Ink dans la console (numéro de ligne compris).
    /// Utilisable aussi sans fenêtre (vérification automatique) :
    ///   Unity.exe -batchmode -projectPath D:\DMD -executeMethod DMD.EditorTools.DmdInkCompiler.CompileAll
    /// </summary>
    internal static class DmdInkCompiler
    {
        private const string DialoguesFolder = "Assets/_DMD/Dialogues";

        [MenuItem("DMD/Compiler tous les dialogues Ink", priority = 80)]
        public static void CompileAll()
        {
            int totalErrors = 0;
            int compiled = 0;

            foreach (string path in Directory.GetFiles(DialoguesFolder, "*.ink", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                var messages = new List<string>();
                int fileErrors = 0;

                var options = new Ink.Compiler.Options
                {
                    sourceFilename = assetPath,
                    countAllVisits = true,
                    errorHandler = (message, type) =>
                    {
                        if (type == Ink.ErrorType.Error) fileErrors++;
                        messages.Add($"{type} : {message}");
                    }
                };

                Ink.Runtime.Story story = null;
                try
                {
                    story = new Ink.Compiler(File.ReadAllText(assetPath), options).Compile();
                }
                catch (System.Exception e)
                {
                    fileErrors++;
                    messages.Add($"Exception : {e.Message}");
                }

                foreach (string m in messages)
                {
                    if (m.StartsWith("Error") || m.StartsWith("Exception")) Debug.LogError($"[DMD][Ink] {assetPath} — {m}");
                    else Debug.LogWarning($"[DMD][Ink] {assetPath} — {m}");
                }

                if (story != null && fileErrors == 0)
                {
                    File.WriteAllText(Path.ChangeExtension(assetPath, ".json"), story.ToJson());
                    compiled++;
                    Debug.Log($"[DMD][Ink] OK : {assetPath}");
                }
                totalErrors += fileErrors;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[DMD][Ink] Terminé : {compiled} fichier(s) compilé(s), {totalErrors} erreur(s).");

            if (Application.isBatchMode) EditorApplication.Exit(totalErrors > 0 ? 1 : 0);
        }
    }
}
#endif
