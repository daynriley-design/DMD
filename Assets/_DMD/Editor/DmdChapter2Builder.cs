using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMD.Core;
using DMD.Dialogue;
using DMD.Economy;
using DMD.Quests;
using DMD.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DMD.EditorTools
{
    /// <summary>
    /// Menus « DMD > Chapitre 2 » :
    ///   1. Crée les données (ScriptableObjects) tirées du document de conception : portefeuille,
    ///      3 quartiers, 3 tenues, 4 missions avec leurs objectifs.
    ///   2. Crée la scène Persistent_Core (systèmes, joueur, HUD, dialogue) + Zone_Limete / Zone_Matonge /
    ///      Zone_Gombe en greybox, reliées par des arrêts de moto wewa, et les ajoute au build.
    /// Les assets et quartiers déjà existants ne sont jamais écrasés (ton travail est conservé).
    /// </summary>
    internal static partial class DmdSceneBuilder
    {
        private const string Ch2DataDir = "Assets/_DMD/ScriptableObjects/Chapitre2";
        private const string Ch2SceneDir = "Assets/_DMD/Scenes/Chapitre2";
        private const string CoreScenePath = Ch2SceneDir + "/Persistent_Core.unity";

        private class Ch2Data
        {
            public CurrencyWallet wallet;
            public DistrictData limete, matonge, gombe;
            public OutfitData etudiant, chemise, rue;
            public QuestData m21, m22, m23, m24;

            public DistrictData[] Districts => new[] { limete, matonge, gombe };
            public OutfitData[] Outfits => new[] { etudiant, chemise, rue };
            public QuestData[] Quests => new[] { m21, m22, m23, m24 };
        }

        // ─── Menus ────────────────────────────────────────────────────────

        [MenuItem("DMD/Chapitre 2/1. Créer les données (portefeuille, quartiers, tenues, missions)", priority = 60)]
        private static void CreateChapter2DataMenu()
        {
            var data = CreateChapter2Data();
            Selection.activeObject = data.wallet;
            EditorGUIUtility.PingObject(data.wallet);
            Debug.Log($"[DMD] Données du Chapitre 2 prêtes dans {Ch2DataDir}.");
        }

        [MenuItem("DMD/Chapitre 2/2. Créer Persistent_Core + les 3 quartiers", priority = 61)]
        private static void CreateChapter2ScenesMenu()
        {
            if (!CheckTmpResources()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(CoreScenePath) &&
                !EditorUtility.DisplayDialog("DMD", "Persistent_Core existe déjà. La remplacer ?\n(Les scènes de quartiers existantes ne seront pas touchées.)", "Remplacer", "Annuler"))
                return;

            var data = CreateChapter2Data();
            Directory.CreateDirectory(Ch2SceneDir);

            var zonePaths = data.Districts.Select(d => CreateZoneScene(d, data)).ToList();
            CreateCoreScene(data);
            AddChapter2ScenesToBuild(zonePaths);

            EditorSceneManager.OpenScene(CoreScenePath);
            Debug.Log("[DMD] Chapitre 2 prêt : ouvre Persistent_Core et appuie sur Play (Limete se charge automatiquement).");
        }

        // ─── Données ──────────────────────────────────────────────────────

        private static Ch2Data CreateChapter2Data()
        {
            Directory.CreateDirectory(Ch2DataDir);
            var d = new Ch2Data();

            d.wallet = CreateOrLoad<CurrencyWallet>("Portefeuille_Joueur", w =>
            {
                w.startCDF = 40000; // de quoi payer le téléphone torche (25 000 négocié / 35 000 sans discuter)
                w.startUSD = 20;
                w.startWornUSD = 10;
            });

            // Quartiers
            d.limete = CreateOrLoad<DistrictData>("Quartier_Limete", x =>
            {
                x.id = DistrictId.Limete;
                x.displayName = "Limete";
                x.sceneName = "Zone_Limete";
                x.risk = RiskLevel.Faible;
                x.heatMultiplier = 0.8f;
                x.landmarks = new[] { "Tour de l'Échangeur", "Boulevard Lumumba", "Garages", "Parcelles résidentielles" };
                x.ambiance = "Tour de l'Échangeur, Boulevard Lumumba, garages, parcelles résidentielles.";
                x.gameplayUse = "Planque, premiers petits boulots, achat de cartes SIM et changement de tenue. Zone tampon.";
            });
            d.matonge = CreateOrLoad<DistrictData>("Quartier_MatongeKalamu", x =>
            {
                x.id = DistrictId.MatongeKalamu;
                x.displayName = "Matonge / Kalamu";
                x.sceneName = "Zone_Matonge";
                x.risk = RiskLevel.Moyen;
                x.heatMultiplier = 1f;
                x.landmarks = new[] { "Rond-Point Victoire", "Marché", "Nganda", "Cybercafé", "Cambistes" };
                x.ambiance = "Rond-Point Victoire, marchés denses, ngandas, cybercafés, cambistes.";
                x.gameplayUse = "Change USD/CDF, recrutement d'informateurs, rumeurs, missions secondaires. Pickpockets et indics.";
            });
            d.gombe = CreateOrLoad<DistrictData>("Quartier_Gombe", x =>
            {
                x.id = DistrictId.Gombe;
                x.displayName = "Gombe";
                x.sceneName = "Zone_Gombe";
                x.risk = RiskLevel.Eleve;
                x.heatMultiplier = 1.5f;
                x.landmarks = new[] { "Ministères", "Banques", "Tours d'affaires", "Checkpoint policier" };
                x.ambiance = "Ministères, banques, tours d'affaires, checkpoints policiers.";
                x.gameplayUse = "Objectifs majeurs : dossiers, filatures de cadres liés aux minerais. Contrôles d'identité stricts.";
            });

            // Tenues : < 1 = se fond dans le décor, > 1 = attire l'œil
            d.etudiant = CreateOrLoad<OutfitData>("Tenue_Etudiant", o =>
            {
                o.id = "Etudiant";
                o.displayName = "T-shirt blanc d'étudiant";
                o.description = "La tenue du campus. Neutre à Limete et Matonge, un peu voyante à Gombe où ta photo circule.";
                o.modifiers = new() { Mod(DistrictId.Limete, 1f), Mod(DistrictId.MatongeKalamu, 1f), Mod(DistrictId.Gombe, 1.2f) };
            });
            d.chemise = CreateOrLoad<OutfitData>("Tenue_ChemiseGombe", o =>
            {
                o.id = "ChemiseGombe";
                o.displayName = "Chemise propre";
                o.description = "Pour approcher les bureaux de Gombe. Détonne dans les ruelles de Matonge.";
                o.modifiers = new() { Mod(DistrictId.Limete, 1.1f), Mod(DistrictId.MatongeKalamu, 1.2f), Mod(DistrictId.Gombe, 0.6f) };
            });
            d.rue = CreateOrLoad<OutfitData>("Tenue_TravailleurRue", o =>
            {
                o.id = "TravailleurRue";
                o.displayName = "Tenue de travailleur de rue";
                o.description = "Invisible dans la foule de Limete et Matonge. Suspecte près des ministères.";
                o.modifiers = new() { Mod(DistrictId.Limete, 0.7f), Mod(DistrictId.MatongeKalamu, 0.7f), Mod(DistrictId.Gombe, 1.4f) };
            });

            // Missions
            d.m21 = CreateOrLoad<QuestData>("Mission_2_1_ZeroTrace", q =>
            {
                q.questId = "M2_1";
                q.title = "Zéro Trace";
                q.district = DistrictId.Limete;
                q.summary = "Cédric prévient : ton smartphone est une balise. Le casser et le jeter dans le canal, acheter un téléphone torche à l'Échangeur, puis rejoindre la planque de son oncle, 12ème rue Limete.";
                q.stakes = "Introduit la discrétion et la navigation par repères visuels.";
                q.objectives = new()
                {
                    Obj("detruire_smartphone", "Détruire le smartphone dans le canal"),
                    Obj("acheter_torche", "Acheter un téléphone torche près de l'Échangeur"),
                    Obj("rejoindre_planque", "Rejoindre Cédric à la 12ème rue (portail vert rouillé)"),
                };
            });
            d.m22 = CreateOrLoad<QuestData>("Mission_2_2_BilletPropre", q =>
            {
                q.questId = "M2_2";
                q.title = "Billet Propre";
                q.district = DistrictId.MatongeKalamu;
                q.summary = "Faire une course discrète pour un cambiste : livrer une enveloppe scellée à travers la foule de Victoire sans être repéré par les agents en civil.";
                q.stakes = "Apprendre l'économie CDF/USD et les raccourcis piétons dans les ruelles.";
                q.prerequisites = new() { d.m21 };
                q.objectives = new()
                {
                    Obj("trouver_cambiste", "Trouver le cambiste du Rond-Point Victoire"),
                    Obj("livrer_enveloppe", "Livrer l'enveloppe scellée sans te faire repérer"),
                    Obj("payer_loyer", "Payer la première semaine de planque"),
                };
            });
            d.m23 = CreateOrLoad<QuestData>("Mission_2_3_OreilleDeMatonge", q =>
            {
                q.questId = "M2_3";
                q.title = "L'Oreille de Matonge";
                q.district = DistrictId.MatongeKalamu;
                q.summary = "Retrouver un ancien journaliste déchu dans un nganda, le soir. Il détient un nom lié au trafic de minerais. En échange : récupérer une clé USB confisquée dans un cybercafé surveillé.";
                q.stakes = "Premier vrai choix moral : soudoyer le gérant, voler la clé en infiltration ou faire diversion.";
                q.prerequisites = new() { d.m22 };
                q.objectives = new()
                {
                    Obj("trouver_journaliste", "Retrouver l'ancien journaliste au nganda (le soir)"),
                    Obj("recuperer_cle", "Récupérer la clé USB au cybercafé (soudoyer, voler ou faire diversion)"),
                    Obj("obtenir_nom", "Rendre la clé et obtenir le nom"),
                };
            });
            d.m24 = CreateOrLoad<QuestData>("Mission_2_4_LaLigneRouge", q =>
            {
                q.questId = "M2_4";
                q.title = "La Ligne Rouge";
                q.district = DistrictId.Gombe;
                q.summary = "Avec une tenue propre et un faux badge, approcher un immeuble de bureaux à Gombe et photographier un manifeste d'exportation. L'opération tourne mal : tu es reconnu sur les caméras.";
                q.stakes = "Fuite à pied puis en wewa dans les embouteillages du crépuscule jusqu'à Limete. Le complot remonte au sommet de l'État.";
                q.prerequisites = new() { d.m23 };
                q.requiredEvidence = new() { "Cle_USB_Journaliste", "Note_Nom_Madini" }; // à relier sur le mur d'enquête
                q.objectives = new()
                {
                    Obj("changer_tenue", "Enfiler la chemise propre et le faux badge"),
                    Obj("approcher_immeuble", "Approcher l'immeuble de bureaux de Gombe"),
                    Obj("photographier_manifeste", "Photographier le manifeste d'exportation"),
                    Obj("fuir_gombe", "Fuir à pied puis en wewa jusqu'à Limete"),
                };
            });

            AssetDatabase.SaveAssets();
            return d;
        }

        private static OutfitData.DistrictModifier Mod(DistrictId district, float multiplier) =>
            new() { district = district, heatMultiplier = multiplier };

        private static QuestData.Objective Obj(string id, string description) =>
            new() { id = id, description = description };

        private static T CreateOrLoad<T>(string name, Action<T> init) where T : ScriptableObject
        {
            string path = $"{Ch2DataDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ─── Quartiers (greybox) ──────────────────────────────────────────

        private static string CreateZoneScene(DistrictData district, Ch2Data data)
        {
            string path = $"{Ch2SceneDir}/{district.sceneName}.unity";
            if (File.Exists(path))
            {
                Debug.Log($"[DMD] {district.sceneName} existe déjà : conservée.");
                return path;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject($"{district.displayName} (greybox)");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Sol";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200 m × 200 m
            ground.isStatic = true;

            // Repères visuels (navigation sans GPS) : un bloc par repère, en cercle.
            var landmarksRoot = new GameObject("Reperes").transform;
            landmarksRoot.SetParent(root.transform);
            for (int i = 0; i < district.landmarks.Length; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Repere_" + Sanitize(district.landmarks[i]);
                cube.transform.SetParent(landmarksRoot);
                float height = 10f + (i % 3) * 8f;
                Vector3 dir = Quaternion.Euler(0f, i * 360f / district.landmarks.Length + 20f, 0f) * Vector3.forward;
                cube.transform.position = dir * 45f + Vector3.up * height * 0.5f;
                cube.transform.localScale = new Vector3(8f, height, 8f);
                cube.isStatic = true;
            }

            AddSpawn(root.transform, "Arrivee", new Vector3(0f, 0f, -10f));
            if (district.id == DistrictId.Limete)
            {
                AddSpawn(root.transform, "Planque", new Vector3(-24f, 0f, -24f));
                var annex = GameObject.CreatePrimitive(PrimitiveType.Cube);
                annex.name = "Planque_Annexe (parpaings)";
                annex.transform.SetParent(root.transform);
                annex.transform.position = new Vector3(-32f, 1.5f, -32f);
                annex.transform.localScale = new Vector3(6f, 3f, 5f);
                annex.isStatic = true;

                AddMission21Set(root.transform, district);
            }

            // Arrêts de moto wewa vers les autres quartiers
            var others = data.Districts.Where(o => o != district).ToArray();
            for (int j = 0; j < others.Length; j++)
            {
                var stop = new GameObject($"Wewa_vers_{others[j].sceneName}");
                stop.transform.SetParent(root.transform);
                stop.transform.position = new Vector3(j == 0 ? -70f : 70f, 1.5f, 70f);
                var box = stop.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(6f, 3f, 6f);
                var transition = stop.AddComponent<ZoneTransition>();
                Set(transition, "destination", others[j]);
                Set(transition, "wallet", data.wallet);
                SetString(transition, "destinationSpawnId", "Arrivee");
            }

            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>Mission 2.1 « Zéro Trace » : canal, présentoir de Bébé-Cash (Échangeur), portail vert de la planque.</summary>
        private static void AddMission21Set(Transform parent, DistrictData limete)
        {
            var set = new GameObject("Mission_2_1_ZeroTrace").transform;
            set.SetParent(parent);
            var ink = FindInkJson("Ch02_M2_1_ZeroTrace");

            // Canal d'évacuation à ciel ouvert (+ deux policiers près d'un kiosque, à ajouter en témoins)
            var canal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canal.name = "Canal_Evacuation";
            canal.transform.SetParent(set);
            canal.transform.position = new Vector3(28f, -0.4f, -20f);
            canal.transform.localScale = new Vector3(4f, 1f, 40f);
            canal.isStatic = true;
            AddDialogueZone(set, "Zone_Canal", new Vector3(24.5f, 1f, -20f), new Vector3(3f, 2f, 10f),
                            ink, "m2_1_canal", "[E] Casser et jeter le téléphone", requires: "", completes: "detruire_smartphone");

            // Présentoir de Bébé-Cash, au pied du premier repère (Tour de l'Échangeur)
            Vector3 dir = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward;
            Vector3 stallPos = dir * 36f;
            var stall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stall.name = "Presentoir_BebeCash";
            stall.transform.SetParent(set);
            stall.transform.position = stallPos + Vector3.up * 0.5f;
            stall.transform.localScale = new Vector3(2f, 1f, 0.8f);
            AddDialogueZone(set, "Zone_BebeCash", stallPos + Vector3.up, new Vector3(4f, 2f, 4f),
                            ink, "m2_1_vendeur", "[E] Parler à Bébé-Cash", requires: "detruire_smartphone", completes: "acheter_torche");

            // Portail vert rouillé, devant l'annexe de la planque
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Portail_Vert_Rouille";
            gate.transform.SetParent(set);
            gate.transform.position = new Vector3(-27.5f, 1.25f, -27.5f);
            gate.transform.localScale = new Vector3(2.5f, 2.5f, 0.2f);
            AddDialogueZone(set, "Zone_Portail", new Vector3(-26f, 1f, -25.5f), new Vector3(4f, 2f, 3f),
                            ink, "m2_1_planque", "[E] Frapper au portail vert", requires: "acheter_torche", completes: "rejoindre_planque");
        }

        private static void AddDialogueZone(Transform parent, string name, Vector3 position, Vector3 size, TextAsset ink,
                                            string knot, string prompt, string requires, string completes)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            var trigger = go.AddComponent<DialogueTrigger>();
            Set(trigger, "inkJson", ink);
            SetString(trigger, "knot", knot);
            SetString(trigger, "prompt", prompt);
            SetString(trigger, "questId", "M2_1");
            SetString(trigger, "requiresObjective", requires);
            SetString(trigger, "completesObjective", completes);
        }

        private static void AddSpawn(Transform parent, string id, Vector3 position)
        {
            var go = new GameObject($"Spawn_{id}");
            go.transform.SetParent(parent);
            go.transform.position = position;
            var spawn = go.AddComponent<ZoneSpawnPoint>();
            SetString(spawn, "id", id);
        }

        // ─── Persistent_Core ──────────────────────────────────────────────

        private static void CreateCoreScene(Ch2Data data)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var systems = CreateSystems();
            var router = systems.GetComponent<InkTagRouter>();
            Set(router, "wallet", data.wallet);
            SetArray(router, "districts", data.Districts);

            AddIfMissing<GameClock>(systems);
            AddIfMissing<PlayerInventory>(systems);
            Set(AddIfMissing<Safehouse>(systems), "wallet", data.wallet);
            var districts = AddIfMissing<DistrictManager>(systems);
            Set(districts, "startingOutfit", data.etudiant);
            SetArray(districts, "wardrobe", data.Outfits);
            Set(AddIfMissing<PlayerPhone>(systems), "wallet", data.wallet);
            var quests = AddIfMissing<QuestManager>(systems);
            SetArray(quests, "quests", data.Quests);
            Set(quests, "wallet", data.wallet);
            Set(quests, "autoStartQuest", data.m21);
            var zones = AddIfMissing<ZoneLoader>(systems);
            Set(zones, "startingDistrict", data.limete);
            SetString(zones, "startingSpawnId", "Planque");

            TryAddStarterAssetsPlayer();
            CreateHud();
            CreateDialogueBox();
            var runner = Object.FindAnyObjectByType<DialogueRunner>();
            if (runner != null)
            {
                // Mission 2.1 : l'appel de Cédric se lance dès l'arrivée à Limete ;
                // les scènes suivantes (canal, vendeur, portail) sont lancées par les DialogueTrigger.
                Set(runner, "inkJson", FindInkJson("Ch02_M2_1_ZeroTrace"));
                Set(runner, "wallet", data.wallet);
                SetString(runner, "startKnot", "m2_1_appel");
                SetBool(runner, "playOnStart", true);
            }

            EditorSceneManager.SaveScene(scene, CoreScenePath);
        }

        /// <summary>Place le joueur et les caméras des Starter Assets s'ils sont importés.</summary>
        private static void TryAddStarterAssetsPlayer()
        {
            var playerPrefab = LoadPrefab("PlayerArmature");
            if (playerPrefab == null)
            {
                Debug.LogWarning("[DMD] Starter Assets – Third Person introuvables. Importe-les, puis glisse PlayerArmature, " +
                                 "MainCamera et PlayerFollowCamera dans Persistent_Core et ajoute PlayerStats sur PlayerArmature.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            AddIfMissing<PlayerStats>(player);

            var mainCamPrefab = LoadPrefab("MainCamera");
            if (mainCamPrefab != null)
            {
                var defaultCam = Camera.main;
                if (defaultCam != null) Object.DestroyImmediate(defaultCam.gameObject);
                PrefabUtility.InstantiatePrefab(mainCamPrefab);
            }

            var followPrefab = LoadPrefab("PlayerFollowCamera");
            if (followPrefab == null) return;

            var follow = (GameObject)PrefabUtility.InstantiatePrefab(followPrefab);
            var cameraRoot = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCameraRoot");
            if (cameraRoot == null || !TryAssignCinemachineFollow(follow, cameraRoot))
                Debug.LogWarning("[DMD] Caméra : assigne PlayerCameraRoot (enfant de PlayerArmature) dans le champ Follow / Tracking Target de PlayerFollowCamera.");
        }

        /// <summary>Compatible Cinemachine 2 (m_Follow) et 3 (Target.TrackingTarget).</summary>
        private static bool TryAssignCinemachineFollow(GameObject cameraObject, Transform target)
        {
            foreach (var component in cameraObject.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                foreach (string property in new[] { "m_Follow", "Target.TrackingTarget" })
                {
                    var p = so.FindProperty(property);
                    if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    p.objectReferenceValue = target;
                    so.ApplyModifiedProperties();
                    return true;
                }
            }
            return false;
        }

        private static GameObject LoadPrefab(string fileName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{fileName} t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == fileName && path.Contains("StarterAssets"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static void AddChapter2ScenesToBuild(IEnumerable<string> zonePaths)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { CoreScenePath }.Concat(zonePaths))
                if (scenes.All(s => s.path != path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ─── Utilitaires ──────────────────────────────────────────────────

        private static T FindAsset<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }

        private static string Sanitize(string s) =>
            new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        private static void SetValue(Object target, string property, Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[DMD] Champ « {property} » introuvable sur {target.GetType().Name}."); return; }
            apply(p);
            so.ApplyModifiedProperties();
        }

        private static void SetString(Object target, string property, string value) => SetValue(target, property, p => p.stringValue = value);
        private static void SetBool(Object target, string property, bool value) => SetValue(target, property, p => p.boolValue = value);
    }
}
