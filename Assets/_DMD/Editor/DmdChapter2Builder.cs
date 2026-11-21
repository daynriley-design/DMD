using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMD.Core;
using DMD.Dialogue;
using DMD.Economy;
using DMD.Heat;
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
                q.summary = "Mama Chantal, cambiste du Rond-Point Victoire, confie une enveloppe scellée à livrer chez Papa Gérard (dépôt de boissons derrière le marché de Matonge), par les ruelles, sans croiser les agents en civil. Retour avec le reçu : 60 $ propres.";
                q.stakes = "Apprendre l'économie CDF/USD et les raccourcis piétons. Débloque le bureau de change de Mama Chantal.";
                q.prerequisites = new() { d.m21 };
                q.rewardUSD = 60;
                q.objectives = new()
                {
                    Obj("parler_chantal", "Trouver Mama Chantal au Rond-Point Victoire"),
                    Obj("livrer_enveloppe", "Livrer l'enveloppe chez Papa Gérard par les ruelles"),
                    Obj("ramener_recu", "Ramener le reçu tamponné à Mama Chantal"),
                };
            });
            d.m23 = CreateOrLoad<QuestData>("Mission_2_3_OreilleDeMatonge", q =>
            {
                q.questId = "M2_3";
                q.title = "L'Oreille de Matonge";
                q.district = DistrictId.MatongeKalamu;
                q.summary = "Retrouver Jean-Claude Mbuyi, ancien journaliste d'investigation, au nganda « Chez Ntemba » le soir. En échange d'un nom : récupérer sa clé USB rouge, confisquée par le gérant du Cyber-Éclair.";
                q.stakes = "Premier vrai choix : soudoyer Patrick (20 $), couper le disjoncteur pour faire diversion, ou s'infiltrer derrière le comptoir (chances selon la stat Rue).";
                q.prerequisites = new() { d.m22 };
                q.objectives = new()
                {
                    Obj("trouver_journaliste", "Retrouver Jean-Claude Mbuyi « Chez Ntemba » (le soir, 19h–2h)"),
                    new QuestData.Objective { id = "couper_courant", description = "(Diversion) Couper le disjoncteur derrière le Cyber-Éclair", optional = true },
                    Obj("recuperer_cle", "Récupérer la clé USB rouge au Cyber-Éclair"),
                    Obj("rapporter_cle", "Rapporter la clé à Jean-Claude"),
                };
            });
            d.m24 = CreateOrLoad<QuestData>("Mission_2_4_LaLigneRouge", q =>
            {
                q.questId = "M2_4";
                q.title = "La Ligne Rouge";
                q.district = DistrictId.Gombe;
                q.summary = "Avec une chemise propre et un faux badge de stagiaire, entrer chez Kivu-Transit Logistics (avenue du 30 Juin, Gombe), société écran de Sébastien Kankonde, et photographier le manifeste d'exportation du mois. L'opération tourne mal : tu es reconnu sur les caméras.";
                q.stakes = "Fuite à pied puis en wewa dans les embouteillages du crépuscule jusqu'à Limete. Le complot remonte au sommet de l'État.";
                q.prerequisites = new() { d.m23 };
                q.requiredEvidence = new() { "Note_Kivu_Transit" }; // la note de Jean-Claude, à épingler sur le mur d'enquête
                q.objectives = new()
                {
                    Obj("obtenir_badge", "Récupérer le faux badge de stagiaire près de la Gare Centrale"),
                    Obj("entrer_immeuble", "Passer l'accueil de Kivu-Transit Logistics (en chemise propre)"),
                    Obj("atteindre_bureau", "Monter au 4ème étage et localiser le bureau de Kankonde"),
                    Obj("photographier_manifeste", "Photographier le manifeste d'exportation"),
                    Obj("fuir_gombe", "Fuir par l'escalier de secours, puis en wewa jusqu'à Limete"),
                    Obj("rentrer_planque", "Rentrer à la planque"),
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

                // Menu de la planque (repos, loyer, garde-robe, mur d'enquête) : ouvert après la Mission 2.1
                AddDialogueZone(root.transform, "Zone_Planque_Interieur", new Vector3(-32f, 1f, -28.5f), new Vector3(5f, 2f, 2.5f),
                                FindInkJson("Ch02_Planque"), "planque_menu", "[E] Planque : repos, loyer, garde-robe, mur",
                                requires: "", completes: "", questId: "M2_1", afterCompleted: true);
            }
            else if (district.id == DistrictId.MatongeKalamu)
            {
                AddMission22Set(root.transform, data);
                AddMission23Set(root.transform, data);
            }
            else if (district.id == DistrictId.Gombe)
            {
                AddMission24Set(root.transform);
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
                if (others[j].id == DistrictId.Gombe)
                {
                    // Gombe s'ouvre à la fin de la Mission 2.3 (Jean-Claude donne l'adresse de Kivu-Transit).
                    SetString(transition, "requiredQuestCompleted", "M2_3");
                    SetString(transition, "lockedMessage", "Gombe, sans raison et en t-shirt de fac ? Pas encore.");
                }
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
                                            string knot, string prompt, string requires, string completes,
                                            string questId = "M2_1", bool afterCompleted = false, bool requireKey = true)
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
            SetString(trigger, "questId", questId);
            SetString(trigger, "requiresObjective", requires);
            SetString(trigger, "completesObjective", completes);
            SetBool(trigger, "onlyAfterQuestCompleted", afterCompleted);
            SetBool(trigger, "requireInteractKey", requireKey);
        }

        /// <summary>
        /// Mission 2.4 « La Ligne Rouge » dans Zone_Gombe : contact de la Gare Centrale, hall de Kivu-Transit,
        /// 4ème étage (plateau isolé en hauteur : couloir, caméra, secrétaire intermittente, bureau de Kankonde,
        /// escalier de secours), ruelle arrière et wewa de la fuite.
        /// </summary>
        private static void AddMission24Set(Transform parent)
        {
            var set = new GameObject("Mission_2_4_LaLigneRouge").transform;
            set.SetParent(parent);
            var ink = FindInkJson("Ch02_M2_4_LaLigneRouge");

            // Gare Centrale + contact de Jean-Claude
            Cube(set, "Gare_Centrale", new Vector3(16f, 3f, -22f), new Vector3(14f, 6f, 6f));
            Capsule(set, "Contact_Badge (photographe ambulant)", new Vector3(9f, 1f, -17f));
            AddDialogueZone(set, "Zone_Contact_Badge", new Vector3(9f, 1f, -15.5f), new Vector3(3f, 2f, 3f), ink,
                            "m2_4_badge", "[E] Aborder le contact de Jean-Claude", requires: "", completes: "obtenir_badge", questId: "M2_4");

            // Immeuble Kivu-Transit Logistics (repère « Tours d'affaires », 200°), entrée face au centre
            Vector3 dir = Quaternion.Euler(0f, 200f, 0f) * Vector3.forward;
            Vector3 building = dir * 30f;
            Vector3 toCenter = -dir;
            var tower = Cube(set, "Kivu_Transit_Logistics (tour vitrée)", building + Vector3.up * 12f, new Vector3(14f, 24f, 14f));
            tower.transform.rotation = Quaternion.LookRotation(toCenter);
            Vector3 entrance = building + toCenter * 8f;
            Capsule(set, "Vigile_Accueil", entrance + toCenter * 0.5f + Vector3.up);
            AddDialogueZone(set, "Zone_Accueil", entrance + toCenter * 2f + Vector3.up, new Vector3(4f, 2f, 3f), ink,
                            "m2_4_accueil", "[E] Passer le portique (vigile)", requires: "obtenir_badge", completes: "entrer_immeuble", questId: "M2_4");

            // Ruelle arrière + wewa (sortie de l'escalier de secours)
            Vector3 alley = building - toCenter * 11f;
            AddSpawn(set, "Ruelle_Arriere", alley);
            Capsule(set, "Chauffeur_Wewa", alley - toCenter * 5f + Vector3.up);
            AddDialogueZone(set, "Zone_Wewa", alley - toCenter * 4f + Vector3.up, new Vector3(4f, 2f, 4f), ink,
                            "m2_4_wewa", "", requires: "photographier_manifeste", completes: "fuir_gombe", questId: "M2_4", requireKey: false);

            // 4ème étage : plateau isolé à 20 m de haut (axe X = couloir, de l'ascenseur à l'ouest au bureau à l'est)
            var floor4 = new GameObject("Etage4_Kivu_Transit").transform;
            floor4.SetParent(set);
            Vector3 o = new Vector3(60f, 20f, -60f);
            Cube(floor4, "Sol_Moquette", o, new Vector3(26f, 1f, 16f));
            Cube(floor4, "Mur_Nord", o + new Vector3(0f, 2f, 8f), new Vector3(26f, 3f, 0.4f));
            Cube(floor4, "Mur_Sud", o + new Vector3(0f, 2f, -8f), new Vector3(26f, 3f, 0.4f));
            Cube(floor4, "Mur_Ouest (ascenseur)", o + new Vector3(-13f, 2f, 0f), new Vector3(0.4f, 3f, 16f));
            Cube(floor4, "Mur_Est (baie vitrée, vue sur le fleuve)", o + new Vector3(13f, 2f, 0f), new Vector3(0.4f, 3f, 16f));
            Cube(floor4, "Cloison_Mur_Aveugle", o + new Vector3(-1f, 2f, 1.5f), new Vector3(12f, 3f, 0.3f));

            AddSpawn(floor4, "Etage4", o + new Vector3(-11f, 0.6f, -4f));
            AddDialogueZone(floor4, "Zone_Couloir_Kankonde", o + new Vector3(-10f, 1.6f, -4f), new Vector3(4f, 2f, 5f), ink,
                            "m2_4_couloir", "", requires: "entrer_immeuble", completes: "atteindre_bureau", questId: "M2_4", requireKey: false);

            // Caméra murale : balaie le côté sud du couloir (le côté nord, derrière la cloison, est le mur aveugle)
            var cam = Cube(floor4, "Camera_Murale", o + new Vector3(4f, 2.8f, -7.5f), new Vector3(0.4f, 0.3f, 0.6f));
            cam.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var camWitness = cam.AddComponent<WitnessDetector>();
            SetEnum(camWitness, "kind", (int)WitnessDetector.WitnessKind.Camera);
            SetValue(camWitness, "eyeHeight", p => p.floatValue = 0f);
            SetValue(camWitness, "viewAngle", p => p.floatValue = 70f);
            SetBool(camWitness, "spotOnSight", true);
            SetValue(camWitness, "timeToSpot", p => p.floatValue = 2f);
            SetValue(camWitness, "heatOnSpot", p => p.floatValue = 15f);
            SetString(camWitness, "spotBark", "Sécurité, 4ème étage : quelqu'un dans le couloir !");

            // Secrétaire : présente 12 s, absente 8 s (elle va à l'imprimante)
            var secretaryCycle = new GameObject("Secretaire (cycle imprimante)");
            secretaryCycle.transform.SetParent(floor4);
            var secretary = Capsule(secretaryCycle.transform, "Secretaire", o + new Vector3(6f, 1.5f, -2f));
            secretary.transform.rotation = Quaternion.LookRotation(Vector3.left);
            var secWitness = secretary.AddComponent<WitnessDetector>();
            SetBool(secWitness, "spotOnSight", true);
            SetValue(secWitness, "spotRange", p => p.floatValue = 7f);
            SetString(secWitness, "spotBark", "Vous cherchez quelqu'un, jeune homme ?");
            var cycle = secretaryCycle.AddComponent<CycleToggle>();
            Set(cycle, "target", secretary);

            // Bureau de Kankonde (extrémité est) + escalier de secours
            Cube(floor4, "Bureau_Bois_Massif (dossier rouge)", o + new Vector3(10f, 1f, 3f), new Vector3(2.5f, 1f, 1.2f));
            AddDialogueZone(floor4, "Zone_Manifeste", o + new Vector3(10f, 1.6f, 1.5f), new Vector3(3f, 2f, 2f), ink,
                            "m2_4_manifeste", "[E] Photographier le manifeste", requires: "atteindre_bureau", completes: "photographier_manifeste", questId: "M2_4");
            AddDialogueZone(floor4, "Zone_Escalier_Secours", o + new Vector3(11.5f, 1.6f, -6f), new Vector3(2f, 2f, 3f), ink,
                            "m2_4_escalier", "[E] Escalier de secours !", requires: "photographier_manifeste", completes: "fuir_gombe", questId: "M2_4");
        }

        private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            return go;
        }

        private static GameObject Capsule(Transform parent, string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// Mission 2.3 « L'Oreille de Matonge » dans Zone_Matonge :
        /// nganda « Chez Ntemba » (Jean-Claude présent de 19h à 2h) et Cyber-Éclair
        /// (comptoir de Patrick, guetteur à la porte, disjoncteur rouillé à l'arrière).
        /// </summary>
        private static void AddMission23Set(Transform parent, Ch2Data data)
        {
            var set = new GameObject("Mission_2_3_OreilleDeMatonge").transform;
            set.SetParent(parent);
            var ink = FindInkJson("Ch02_M2_3_OreilleDeMatonge");

            // Repères de Zone_Matonge : [2] Nganda (164°), [3] Cybercafé (236°)
            Vector3 nganda = Quaternion.Euler(0f, 164f, 0f) * Vector3.forward * 36f;
            Vector3 cyber = Quaternion.Euler(0f, 236f, 0f) * Vector3.forward * 36f;

            // Nganda « Chez Ntemba » : terrasse toujours là, Jean-Claude seulement le soir
            var terrace = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terrace.name = "Nganda_Chez_Ntemba (terrasse)";
            terrace.transform.SetParent(set);
            terrace.transform.position = nganda + Vector3.up * 0.05f;
            terrace.transform.localScale = new Vector3(10f, 0.1f, 8f);

            var evening = new GameObject("Soiree_19h_2h").transform; // désactivé hors des heures d'ouverture
            evening.SetParent(set);
            var jeanClaude = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            jeanClaude.name = "Jean-Claude_Mbuyi";
            jeanClaude.transform.SetParent(evening);
            jeanClaude.transform.position = nganda + new Vector3(0f, 1f, 3f);
            AddDialogueZone(evening, "Zone_JeanClaude", nganda + new Vector3(0f, 1f, 1.5f), new Vector3(4f, 2f, 3f), ink,
                            "m2_3_ntemba", "[E] S'asseoir en face de Jean-Claude", requires: "", completes: "trouver_journaliste", questId: "M2_3");
            AddDialogueZone(evening, "Zone_JeanClaude_Retour", nganda + new Vector3(0f, 1f, 1.5f), new Vector3(4f, 2f, 3f), ink,
                            "m2_3_retour", "[E] Rendre la clé à Jean-Claude", requires: "recuperer_cle", completes: "rapporter_cle", questId: "M2_3");

            var gate = set.gameObject.AddComponent<DMD.World.TimeWindowGate>();
            SetValue(gate, "fromHour", p => p.floatValue = 19f);
            SetValue(gate, "toHour", p => p.floatValue = 2f);
            SetArray(gate, "targets", new Object[] { evening.gameObject });

            // Cyber-Éclair : salle étroite, comptoir, guetteur à l'entrée, disjoncteur à l'arrière
            Vector3 inward = (-cyber).normalized;
            var room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.name = "Cyber_Eclair (murs bleus)";
            room.transform.SetParent(set);
            room.transform.position = cyber - inward * 4f + Vector3.up * 1.5f;
            room.transform.rotation = Quaternion.LookRotation(inward);
            room.transform.localScale = new Vector3(6f, 3f, 8f);
            room.isStatic = true;

            var patrick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            patrick.name = "Patrick (gerant)";
            patrick.transform.SetParent(set);
            patrick.transform.position = cyber + inward * 1f + Vector3.up;

            AddDialogueZone(set, "Zone_Comptoir_Patrick", cyber + inward * 2.5f + Vector3.up, new Vector3(3f, 2f, 3f), ink,
                            "cyber_patrick", "[E] Parler à Patrick (comptoir)", requires: "trouver_journaliste", completes: "recuperer_cle", questId: "M2_3");
            AddDialogueZone(set, "Zone_Comptoir_Noir", cyber + inward * 1.5f + Vector3.up, new Vector3(2f, 2f, 2f), ink,
                            "cyber_comptoir_noir", "[E] Prendre la clé dans la pénombre", requires: "couper_courant", completes: "recuperer_cle", questId: "M2_3");

            Vector3 back = cyber - inward * 9f;
            var breaker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            breaker.name = "Disjoncteur_Rouille";
            breaker.transform.SetParent(set);
            breaker.transform.position = back + Vector3.up * 1.5f;
            breaker.transform.localScale = new Vector3(0.6f, 0.8f, 0.3f);
            AddDialogueZone(set, "Zone_Disjoncteur", back + Vector3.up - inward * 1.2f, new Vector3(3f, 2f, 2f), ink,
                            "cyber_disjoncteur", "[E] Couper le disjoncteur", requires: "trouver_journaliste", completes: "couper_courant", questId: "M2_3");

            var lookout = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            lookout.name = "Guetteur (porte du cyber)";
            lookout.transform.SetParent(set);
            lookout.transform.position = cyber + inward * 4f + Vector3.up;
            lookout.transform.rotation = Quaternion.LookRotation(inward);
            var witness = lookout.AddComponent<WitnessDetector>();
            SetEnum(witness, "kind", (int)WitnessDetector.WitnessKind.Pnj);
        }

        /// <summary>
        /// Mission 2.2 « Billet Propre » dans Zone_Matonge :
        /// parasol de Mama Chantal (Rond-Point Victoire), dépôt de Papa Gérard (derrière le marché),
        /// deux agents en civil sur la grande avenue, et la ruelle des salons de coiffure pour les contourner.
        /// </summary>
        private static void AddMission22Set(Transform parent, Ch2Data data)
        {
            var set = new GameObject("Mission_2_2_BilletPropre").transform;
            set.SetParent(parent);
            var ink = FindInkJson("Ch02_M2_2_BilletPropre");

            // Repères de Zone_Matonge : [0] Rond-Point Victoire (20°), [1] Marché (92°) — rayon 45 m.
            Vector3 victoire = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward * 36f;
            Vector3 market = Quaternion.Euler(0f, 92f, 0f) * Vector3.forward * 36f;

            // Mama Chantal : tabouret + parasol + bureau de change
            var chantal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            chantal.name = "Mama_Chantal (cambiste)";
            chantal.transform.SetParent(set);
            chantal.transform.position = victoire + Vector3.up;
            var exchange = chantal.AddComponent<CambisteExchange>();
            Set(exchange, "wallet", data.wallet);

            AddDialogueZone(set, "Zone_Chantal_Mission", victoire + Vector3.up, new Vector3(4f, 2f, 4f), ink,
                            "m2_2_chantal", "[E] Parler à Mama Chantal", requires: "", completes: "parler_chantal", questId: "M2_2");
            AddDialogueZone(set, "Zone_Chantal_Retour", victoire + Vector3.up, new Vector3(4f, 2f, 4f), ink,
                            "m2_2_retour", "[E] Rendre le reçu à Mama Chantal", requires: "livrer_enveloppe", completes: "ramener_recu", questId: "M2_2");
            AddDialogueZone(set, "Zone_Chantal_Change", victoire + Vector3.up, new Vector3(4f, 2f, 4f), ink,
                            "chantal_change", "[E] Changer de l'argent (Mama Chantal)", requires: "", completes: "", questId: "M2_2", afterCompleted: true);

            // Dépôt de boissons de Papa Gérard
            var depot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            depot.name = "Depot_Boissons_PapaGerard";
            depot.transform.SetParent(set);
            depot.transform.position = market + new Vector3(0f, 2f, 6f);
            depot.transform.localScale = new Vector3(8f, 4f, 6f);
            depot.isStatic = true;
            AddDialogueZone(set, "Zone_Grille_Gerard", market + new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 3f), ink,
                            "m2_2_gerard", "[E] Frapper à la grille du dépôt", requires: "parler_chantal", completes: "livrer_enveloppe", questId: "M2_2");

            // Agents en civil sur la grande avenue (entre Victoire et le marché), regard vers le centre
            Vector3 avenue = Vector3.Lerp(victoire, market, 0.5f);
            for (int i = 0; i < 2; i++)
            {
                var agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agent.name = $"Agent_Civil_{i + 1}";
                agent.transform.SetParent(set);
                agent.transform.position = avenue + new Vector3(i * 3f - 1.5f, 1f, 0f);
                agent.transform.LookAt(new Vector3(0f, 1f, 0f));
                var witness = agent.AddComponent<WitnessDetector>();
                SetEnum(witness, "kind", (int)WitnessDetector.WitnessKind.Policier);
                SetBool(witness, "spotOnSight", true);
                SetString(witness, "suspiciousItem", "Enveloppe_Scellee");
                if (i == 1) SetString(witness, "spotBark", ""); // un seul agent interpelle
            }

            // Ruelle des salons de coiffure : deux murs qui cachent un passage étroit (raccourci piéton)
            Vector3 toCenter = (-avenue).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, toCenter);
            for (int i = -1; i <= 1; i += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = i < 0 ? "Salon_Coiffure_A" : "Salon_Coiffure_B";
                wall.transform.SetParent(set);
                wall.transform.position = avenue + toCenter * 10f + side * (i * 2.5f) + Vector3.up * 2f;
                wall.transform.rotation = Quaternion.LookRotation(side);
                wall.transform.localScale = new Vector3(14f, 4f, 3f);
                wall.isStatic = true;
            }

            // Étal du marché pour se fondre dans la foule
            var stall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stall.name = "Etal_Marche (se cacher)";
            stall.transform.SetParent(set);
            stall.transform.position = market + new Vector3(-5f, 1f, -3f);
            stall.transform.localScale = new Vector3(4f, 2f, 1.5f);
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
        private static void SetEnum(Object target, string property, int index) => SetValue(target, property, p => p.enumValueIndex = index);
    }
}
