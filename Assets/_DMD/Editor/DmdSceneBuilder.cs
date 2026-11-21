using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMD.Dialogue;
using DMD.Heat;
using DMD.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DMD.EditorTools
{
    /// <summary>
    /// Menu « DMD » dans la barre d'Unity : construit en un clic, dans la scène ouverte,
    /// les systèmes, le HUD et la boîte de dialogue, avec toutes les références branchées.
    /// Tout est annulable avec Ctrl+Z.
    /// </summary>
    internal static partial class DmdSceneBuilder
    {
        private static readonly Color Black55 = new(0f, 0f, 0f, 0.55f);
        private static readonly Color Traque = new(0.78f, 0.05f, 0.05f);
        private static readonly Color Amber = new(1f, 0.72f, 0.25f);
        private static readonly string[] IconLabels = { "VISAGE", "TENUE", "VÉHIC.", "ARME", "CAMÉRA", "COMPLICE" };

        // ─── Menus ────────────────────────────────────────────────────────

        [MenuItem("DMD/Tout créer dans la scène", priority = 0)]
        private static void CreateAllMenu()
        {
            if (!CheckTmpResources()) return;
            CreateSystems();
            CreateHud();
            CreateDialogueBox();
            Debug.Log("[DMD] Systèmes, HUD et boîte de dialogue créés. Pense à ajouter PlayerStats sur ton joueur.");
        }

        [MenuItem("DMD/Créer les systèmes (Heat, Tags Ink, Debug)", priority = 20)]
        private static void CreateSystemsMenu() => CreateSystems();

        [MenuItem("DMD/Créer le HUD", priority = 21)]
        private static void CreateHudMenu()
        {
            if (CheckTmpResources()) CreateHud();
        }

        [MenuItem("DMD/Créer la boîte de dialogue", priority = 22)]
        private static void CreateDialogueMenu()
        {
            if (CheckTmpResources()) CreateDialogueBox();
        }

        // ─── Systèmes ─────────────────────────────────────────────────────

        private static GameObject CreateSystems()
        {
            // HeatManager sur son PROPRE objet : il persiste entre les scènes (la chaleur de Kinshasa du
            // Chapitre 1 suit le joueur au Chapitre 2) et son doublon éventuel peut être détruit sans rien emporter.
            if (Object.FindAnyObjectByType<HeatManager>() == null)
            {
                var heatGo = new GameObject("_DMD_HeatManager");
                heatGo.AddComponent<HeatManager>();
                Undo.RegisterCreatedObjectUndo(heatGo, "Créer HeatManager DMD");
            }

            var existing = Object.FindAnyObjectByType<InkTagRouter>();
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("_DMD_Systemes");
                Undo.RegisterCreatedObjectUndo(go, "Créer systèmes DMD");
            }

            var router = AddIfMissing<InkTagRouter>(go);
            AddIfMissing<DebugHud>(go);
            var audio = AddIfMissing<AudioSource>(go);
            audio.playOnAwake = false;
            Set(router, "sfxSource", audio);

            MarkDirty();
            Selection.activeGameObject = go;
            return go;
        }

        // ─── HUD ──────────────────────────────────────────────────────────

        private static void CreateHud()
        {
            var already = Object.FindAnyObjectByType<HudController>();
            if (already != null)
            {
                Selection.activeGameObject = already.gameObject;
                Debug.Log("[DMD] Un HUD existe déjà dans la scène (sélectionné).");
                return;
            }

            EnsureEventSystem();
            var canvas = CreateCanvas("DMD_HUD", 10);
            Transform root = canvas.transform;

            // Haut-gauche : contexte
            var context = Ui("Contexte", root);
            SetRect(context, new(0, 1), new(0, 1), new(24, -220), new(724, -20));
            var city = Text("Ville", context, "KINSHASA", 34, FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(city, new(0, 1), new(1, 1), new(0, -44), new(0, 0));
            var role = Text("Role", context, "Rôle : CIVIL", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.85f));
            SetRect(role, new(0, 1), new(1, 1), new(0, -76), new(0, -46));
            var money = Text("Argent", context, "", 20, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.95f, 0.8f));
            SetRect(money, new(0, 1), new(1, 1), new(0, -106), new(0, -78));
            var clock = Text("Heure", context, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.65f));
            SetRect(clock, new(0, 1), new(1, 1), new(0, -134), new(0, -108));
            var objective = Text("Objectif", context, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, Amber);
            SetRect(objective, new(0, 1), new(1, 1), new(0, -196), new(0, -140));

            // Haut-droite : bloc chaleur & identité
            var block = Ui("BlocChaleur", root);
            SetRect(block, new(1, 1), new(1, 1), new(-404, -140), new(-24, -20));
            var group = block.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var border = Img("BordureTraque", block, Traque);
            SetRect(border, Vector2.zero, Vector2.one, new(-3, -3), new(3, 3));
            border.enabled = false;
            var background = Img("Fond", block, Black55);
            SetRect(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var title = Text("Titre", block, "CHALEUR", 14, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(1, 1, 1, 0.6f));
            SetRect(title, new(0, 1), new(1, 1), new(14, -38), new(-14, -8));
            var state = Text("Etat", block, "CALME", 18, FontStyles.Bold, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(state, new(0, 1), new(1, 1), new(14, -38), new(-14, -8));

            var barBack = Img("JaugeFond", block, new Color(1, 1, 1, 0.12f));
            SetRect(barBack, new(0, 1), new(1, 1), new(14, -64), new(-14, -46));
            var fill = Img("JaugeRemplissage", barBack.rectTransform, Color.white);
            SetRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            foreach (float mark in new[] { 0.3f, 0.7f }) // séparations Calme | Recherché | Traqué
            {
                var tick = Img($"Repere{mark * 100:0}", barBack.rectTransform, new Color(1, 1, 1, 0.55f));
                SetRect(tick, new(mark, 0), new(mark, 1), new(-1, -3), new(1, 3));
            }

            var iconsRow = Ui("Icones", block);
            SetRect(iconsRow, new(0, 0), new(1, 0), new(14, 12), new(-14, 48));
            var layout = iconsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            var icons = new Image[IconLabels.Length];
            for (int i = 0; i < IconLabels.Length; i++)
            {
                icons[i] = Img($"Icone_{HeatRules.AllFlags[i]}", iconsRow, new Color(1, 1, 1, 0.2f));
                var label = Text("Label", icons[i].rectTransform, IconLabels[i], 11, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
                SetRect(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            // Bas-gauche : emplacement de la mini-carte (à venir)
            var minimap = Img("MiniCarte_AVenir", root, new Color(0, 0, 0, 0.35f));
            SetRect(minimap, new(0, 0), new(0, 0), new(24, 24), new(264, 194));
            var minimapLabel = Text("Label", minimap.rectTransform, "MINI-CARTE", 16, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.4f));
            SetRect(minimapLabel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Bas-droite : notifications de stats
            var notifRoot = Ui("Notifications", root);
            SetRect(notifRoot, new(1, 0), new(1, 0), new(-484, 120), new(-24, 420));
            var template = Text("NotifModele", notifRoot, "+5 SAVOIR", 24, FontStyles.Bold, TextAlignmentOptions.BottomRight, Color.white);
            SetRect(template, new(0, 0), new(1, 0), new(0, 0), new(0, 32));
            template.gameObject.SetActive(false);

            // Contrôleur
            var hud = canvas.gameObject.AddComponent<HudController>();
            var alert = canvas.gameObject.AddComponent<AudioSource>();
            alert.playOnAwake = false;

            Set(hud, "cityText", city);
            Set(hud, "roleText", role);
            Set(hud, "heatBlock", group);
            Set(hud, "heatFill", fill);
            Set(hud, "heatStateText", state);
            Set(hud, "traqueBorder", border);
            SetArray(hud, "identityIcons", icons);
            Set(hud, "notificationRoot", notifRoot);
            Set(hud, "notificationTemplate", template);
            Set(hud, "alertSource", alert);
            Set(hud, "moneyText", money);
            Set(hud, "clockText", clock);
            Set(hud, "objectiveText", objective);
            Set(hud, "wallet", FindAsset<DMD.Economy.CurrencyWallet>());

            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Créer HUD DMD");
            MarkDirty();
            Selection.activeGameObject = canvas.gameObject;
        }

        // ─── Boîte de dialogue ────────────────────────────────────────────

        private static void CreateDialogueBox()
        {
            var already = Object.FindAnyObjectByType<DialogueRunner>();
            if (already != null)
            {
                Selection.activeGameObject = already.gameObject;
                Debug.Log("[DMD] Une boîte de dialogue existe déjà dans la scène (sélectionnée).");
                return;
            }

            EnsureEventSystem();
            var router = Object.FindAnyObjectByType<InkTagRouter>();
            if (router == null) router = CreateSystems().GetComponent<InkTagRouter>();

            var canvas = CreateCanvas("DMD_Dialogue", 20);
            Transform root = canvas.transform;

            var panel = Img("Panneau", root, new Color(0.04f, 0.04f, 0.05f, 0.85f));
            panel.raycastTarget = true;
            SetRect(panel, new(0.5f, 0), new(0.5f, 0), new(-560, 24), new(560, 274));

            var speaker = Text("Nom", panel.rectTransform, "AGENT", 24, FontStyles.Bold, TextAlignmentOptions.TopLeft, Amber);
            SetRect(speaker, new(0, 1), new(1, 1), new(28, -52), new(-28, -16));
            var body = Text("Texte", panel.rectTransform, "Le soleil tape déjà sur l'esplanade de Loyola...", 24, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(body, Vector2.zero, Vector2.one, new(28, 40), new(-28, -58));
            var hint = Text("Continuer", panel.rectTransform, "Espace / clic  >", 16, FontStyles.Normal, TextAlignmentOptions.BottomRight, new Color(1, 1, 1, 0.5f));
            SetRect(hint, new(1, 0), new(1, 0), new(-260, 10), new(-20, 36));

            var choices = Ui("Choix", root);
            SetRect(choices, new(0.5f, 0), new(0.5f, 0), new(-560, 290), new(560, 600));
            var vertical = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 8;
            vertical.childAlignment = TextAnchor.LowerCenter;
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var choiceImage = Img("ChoixModele", choices, new Color(0.12f, 0.12f, 0.14f, 0.92f));
            choiceImage.raycastTarget = true;
            var element = choiceImage.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 60;
            var button = choiceImage.gameObject.AddComponent<Button>();
            button.targetGraphic = choiceImage;
            var colors = button.colors;
            colors.highlightedColor = Amber;
            colors.pressedColor = new Color(0.8f, 0.55f, 0.15f);
            button.colors = colors;
            var choiceLabel = Text("Label", choiceImage.rectTransform, "1. Choix", 19, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(choiceLabel, Vector2.zero, Vector2.one, new(18, 4), new(-18, -4));
            choiceImage.gameObject.SetActive(false);

            var runner = canvas.gameObject.AddComponent<DialogueRunner>();
            Set(runner, "inkJson", FindInkJson("Ch01_LeNomDuPere"));
            Set(runner, "dialoguePanel", panel.gameObject);
            Set(runner, "speakerText", speaker);
            Set(runner, "bodyText", body);
            Set(runner, "continueHint", hint.gameObject);
            Set(runner, "choicesRoot", choices);
            Set(runner, "choiceTemplate", button);
            Set(runner, "tagRouter", router);
            Set(runner, "wallet", FindAsset<DMD.Economy.CurrencyWallet>()); // null tant que les données du Chapitre 2 n'existent pas
            SetArray(runner, "disableDuringDialogue", FindPlayerControllers());

            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Créer dialogue DMD");
            MarkDirty();
            Selection.activeGameObject = canvas.gameObject;
        }

        // ─── Écran titre & identité visuelle ──────────────────────────────

        private const string BrandingDir = "Assets/_DMD/Art/Branding/";
        private const string KeyArtPath = BrandingDir + "DMD_KeyArt.png";
        private const string LogoPath = BrandingDir + "DMD_Logo.png";
        private const string TitleScenePath = "Assets/_DMD/Scenes/00_Titre.unity";
        private const string FirstScenePath = "Assets/_DMD/Scenes/01_Loyola_Greybox.unity";

        private static readonly Color TitleBackground = new(0.07f, 0.055f, 0.045f);
        private static readonly Color Muted = new(1f, 1f, 1f, 0.55f);

        [MenuItem("DMD/Créer l'écran titre (nouvelle scène 00_Titre)", priority = 40)]
        private static void CreateTitleSceneMenu()
        {
            if (!CheckTmpResources()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(TitleScenePath) &&
                !EditorUtility.DisplayDialog("DMD", "La scène 00_Titre existe déjà. La remplacer ?", "Remplacer", "Annuler"))
                return;

            ConfigureBrandingTextures();
            var keyArt = AssetDatabase.LoadAssetAtPath<Sprite>(KeyArtPath);
            if (keyArt == null) Debug.LogWarning($"[DMD] {KeyArtPath} introuvable : l'écran titre sera créé sans visuel.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            EnsureEventSystem();

            var canvas = CreateCanvas("DMD_Titre", 0);
            Transform root = canvas.transform;

            var background = Img("Fond", root, TitleBackground);
            SetRect(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Key art (portrait) collé à gauche, sur toute la hauteur
            var art = Img("KeyArt", root, Color.white);
            art.sprite = keyArt;
            art.preserveAspect = true;
            SetRect(art, new(0, 0), new(0, 1), new(0, 0), new(807, 0));

            // Zone de droite
            var zone = Ui("Zone", root);
            SetRect(zone, Vector2.zero, Vector2.one, new(900, 90), new(-90, -90));
            var tagline = Text("Devise", zone, "SANG  ·  MINERAIS  ·  POUVOIR", 18, FontStyles.Bold, TextAlignmentOptions.Center, Muted);
            SetRect(tagline, new(0, 0), new(1, 0), new(0, 0), new(0, 36));

            // Étape 1 : appuie sur une touche
            var stepPress = Ui("Etape_AppuieTouche", zone);
            SetRect(stepPress, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var pressText = Text("Texte", stepPress, "APPUIE SUR UNE TOUCHE", 30, FontStyles.Bold, TextAlignmentOptions.Center, Amber);
            SetRect(pressText, new(0, 0.35f), new(1, 0.35f), new(0, -30), new(0, 30));

            // Étape 2 : menu principal
            var stepMenu = Ui("Etape_Menu", zone);
            SetRect(stepMenu, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menuButtons = Ui("Boutons", stepMenu);
            SetRect(menuButtons, new(0.5f, 0.5f), new(0.5f, 0.5f), new(-210, -88), new(210, 88));
            var menuLayout = menuButtons.gameObject.AddComponent<VerticalLayoutGroup>();
            menuLayout.spacing = 16;
            menuLayout.childControlWidth = menuLayout.childControlHeight = true;
            menuLayout.childForceExpandWidth = menuLayout.childForceExpandHeight = true;
            var newGame = MakeButton("NouvellePartie", menuButtons, "NOUVELLE PARTIE", 26);
            var quit = MakeButton("Quitter", menuButtons, "QUITTER", 26);

            // Étape 3 : choix du prénom
            var stepName = Ui("Etape_Prenom", zone);
            SetRect(stepName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var question = Text("Question", stepName, "QUI ES-TU ?", 44, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(question, new(0, 1), new(1, 1), new(0, -70), new(0, 0));
            var context = Text("Contexte", stepName, "Étudiant à l'Université Loyola. Fils du gouverneur.", 22, FontStyles.Italic, TextAlignmentOptions.Center, Muted);
            SetRect(context, new(0, 1), new(1, 1), new(0, -112), new(0, -74));

            var grid = Ui("Prenoms", stepName);
            SetRect(grid, new(0.5f, 1), new(0.5f, 1), new(-403, -370), new(403, -140));
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(260, 64);
            gridLayout.spacing = new Vector2(14, 14);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var nameTemplate = MakeButton("PrenomModele", grid, "PRÉNOM", 24);
            nameTemplate.gameObject.SetActive(false);

            var chosen = Text("PrenomChoisi", stepName, "Choisis ton prénom", 24, FontStyles.Normal, TextAlignmentOptions.Center, Amber);
            SetRect(chosen, new(0, 1), new(1, 1), new(0, -430), new(0, -384));

            var actions = Ui("Actions", stepName);
            SetRect(actions, new(0.5f, 1), new(0.5f, 1), new(-403, -520), new(403, -448));
            var actionsLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 14;
            actionsLayout.childControlWidth = actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = actionsLayout.childForceExpandHeight = true;
            var back = MakeButton("Retour", actions, "RETOUR", 22);
            var start = MakeButton("Commencer", actions, "COMMENCER", 22);

            // Contrôleur
            var title = canvas.gameObject.AddComponent<TitleScreen>();
            Set(title, "pressAnyKeyStep", stepPress.gameObject);
            Set(title, "mainMenuStep", stepMenu.gameObject);
            Set(title, "namePickerStep", stepName.gameObject);
            Set(title, "pressAnyKeyText", pressText);
            Set(title, "newGameButton", newGame);
            Set(title, "quitButton", quit);
            Set(title, "namesGrid", grid);
            Set(title, "nameTemplate", nameTemplate);
            Set(title, "selectedNameText", chosen);
            Set(title, "startButton", start);
            Set(title, "backButton", back);

            Directory.CreateDirectory(Path.GetDirectoryName(TitleScenePath));
            EditorSceneManager.SaveScene(scene, TitleScenePath);
            AddScenesToBuild();
            Debug.Log($"[DMD] Écran titre créé : {TitleScenePath} (première scène du build).");
        }

        [MenuItem("DMD/Appliquer le logo comme icône du jeu", priority = 41)]
        private static void ApplyLogoAsIcon()
        {
            ConfigureBrandingTextures();
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null)
            {
                Debug.LogError($"[DMD] {LogoPath} introuvable.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { logo }, IconKind.Any);
            PlayerSettings.productName = "DMD";
            AssetDatabase.SaveAssets();
            Debug.Log("[DMD] Logo appliqué comme icône par défaut (Project Settings > Player).");
        }

        /// <summary>Les PNG du dossier Branding sont importés comme Sprites (utilisables dans l'UI).</summary>
        private static void ConfigureBrandingTextures()
        {
            foreach (string path in new[] { KeyArtPath, LogoPath })
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        /// <summary>00_Titre en première position du build, puis 01_Loyola_Greybox s'il existe.</summary>
        private static void AddScenesToBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != TitleScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));

            if (File.Exists(FirstScenePath) && scenes.All(s => s.path != FirstScenePath))
                scenes.Insert(1, new EditorBuildSettingsScene(FirstScenePath, true));
            else if (!File.Exists(FirstScenePath))
                Debug.LogWarning($"[DMD] {FirstScenePath} n'existe pas encore : crée-la puis ajoute-la à la Scene List du build.");

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Button MakeButton(string name, Transform parent, string label, float fontSize)
        {
            var image = Img(name, parent, Color.white);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.1f, 0.08f, 0.92f);
            colors.highlightedColor = new Color(0.55f, 0.38f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.55f, 0.15f, 1f);
            colors.disabledColor = new Color(0.13f, 0.1f, 0.08f, 0.4f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            var text = Text("Label", image.rectTransform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(text, Vector2.zero, Vector2.one, new(12, 4), new(-12, -4));
            return button;
        }

        // ─── Recherche d'assets / d'objets ────────────────────────────────

        private static TextAsset FindInkJson(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json")) return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
            Debug.LogWarning($"[DMD] {name}.json introuvable : installe Ink (il compile le .ink en .json), puis assigne-le dans DialogueRunner.");
            return null;
        }

        /// <summary>Le contrôleur des Starter Assets, s'il est dans la scène (coupé pendant les dialogues).</summary>
        private static Object[] FindPlayerControllers()
        {
            var found = new List<Object>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour.GetType().Name == "ThirdPersonController") found.Add(behaviour);
            return found.ToArray();
        }

        // ─── Construction d'UI ────────────────────────────────────────────

        private static bool CheckTmpResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

            EditorUtility.DisplayDialog("DMD — TextMeshPro",
                "Les ressources TextMeshPro ne sont pas encore importées.\n\n" +
                "Menu Window > TextMeshPro > Import TMP Essential Resources, puis relance cette commande.",
                "OK");
            return false;
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(go, "Créer EventSystem");
        }

        private static RectTransform Ui(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Img(string name, Transform parent, Color color)
        {
            var image = Ui(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string content, float size,
                                            FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var text = Ui(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(Component c, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // ─── Câblage des références privées ([SerializeField]) ────────────

        private static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[DMD] Champ « {property} » introuvable sur {target.GetType().Name}."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null || !p.isArray) { Debug.LogError($"[DMD] Tableau « {property} » introuvable sur {target.GetType().Name}."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }

        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static void MarkDirty() => EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
