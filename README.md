# DMD — *Damu, Madini, Dola*

> Sang · Minerais · Pouvoir

Action-aventure 3D à la troisième personne en monde semi-ouvert, thriller de complot et drame de guerre.
De Kinshasa au Caire, un étudiant de l'Université Loyola, fils d'un gouverneur ambigu, remonte la chaîne
d'un réseau de minerais stratégiques après le massacre de sa famille à Goma.

**Moteur :** Unity 6 LTS · URP · C# · Ink (dialogues) · New Input System — développement solo.

## État

Prototype en cours d'écriture. Les scripts n'ont pas encore été compilés dans Unity.

| Contenu | Dossier |
| :--- | :--- |
| Stats (Rue, Savoir, Influence, Voix), rôles, régression / transcendance | `Assets/_DMD/Scripts/Core` |
| Jauge de chaleur par ville, icônes d'identification, témoins (vu / non vu) | `Assets/_DMD/Scripts/Heat` |
| Dialogues Ink, tags (#STAT, #HEAT, #CASH, #QUEST…), déclencheurs [E] | `Assets/_DMD/Scripts/Dialogue` |
| Chapitre 2 : argent CDF/USD, planque, inventaire | `Assets/_DMD/Scripts/Economy` |
| Chapitre 2 : horloge, quartiers, tenues, téléphone, scènes additives | `Assets/_DMD/Scripts/World` |
| Missions (QuestData / QuestManager) | `Assets/_DMD/Scripts/Quests` |
| HUD, écran titre, overlay de debug | `Assets/_DMD/Scripts/UI` |
| Scripts Ink : Chapitre 1 « Le Nom du Père », Mission 2.1 « Zéro Trace » | `Assets/_DMD/Dialogues` |
| Menus éditeur « DMD » (construction des scènes, HUD, données) | `Assets/_DMD/Editor` |

## Installation

1. Unity Hub → installer **Unity 6 LTS**.
2. Créer un projet **Universal 3D (URP)** n'importe où, puis copier ses dossiers `Packages/` et
   `ProjectSettings/` à la racine de ce dépôt. Ouvrir ce dossier avec Unity Hub (*Add → Add project from disk*).
3. Importer **Starter Assets – Third Person** (Asset Store) et
   **Ink** : *Package Manager → + → Add package from git URL* →
   `https://github.com/inkle/ink-unity-integration.git#upm`
4. *Window → TextMeshPro → Import TMP Essential Resources*.
5. Menus **DMD** :
   - *Créer l'écran titre* · *Appliquer le logo comme icône du jeu*
   - Dans une scène `01_Loyola_Greybox` : *Tout créer dans la scène* (Chapitre 1)
   - *Chapitre 2 → 1. Créer les données*, puis *2. Créer Persistent_Core + les 3 quartiers*
