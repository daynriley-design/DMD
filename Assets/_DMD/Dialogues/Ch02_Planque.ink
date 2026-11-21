// ==========================================
// DMD — CHAPITRE 2 : LA PLANQUE (12ème rue, Limete)
// Menu réutilisable, ouvert par [E] dans l'annexe après la Mission 2.1.
//   • Repos : faire passer le temps (le nganda ouvre le soir, Mama Chantal travaille le matin)
//   • Loyer : la dette au gardien du quartier (50 $ tous les 3 jours)
//   • Garde-robe : changer de tenue efface l'icône Tenue
//   • Mur d'enquête : relier les preuves pour débloquer la mission suivante
// ==========================================

-> planque_menu

=== planque_menu ===
#LOC:Planque_Limete
<i>L'annexe en parpaings. Le matelas au sol, la table en plastique, l'ampoule qui grésille.</i>
- (menu)
+ [Dormir jusqu'au matin (7h)]
    #TIME:Until:7
    <i>Tu dors d'un sommeil agité. Les klaxons du Boulevard Lumumba te réveillent.</i>
    -> menu
+ [Attendre la tombée de la nuit (19h)]
    #TIME:Until:19
    <i>L'ampoule devient la seule lumière de la pièce. Dehors, la rumba commence dans les parcelles voisines.</i>
    -> menu
+ [Payer le gardien du quartier (50 $)]
    #SAFEHOUSE:PayRent
    <i>Tu plies les billets et tu les glisses sous la porte du gardien.</i>
    -> menu
+ [Garde-robe]
    -> garde_robe
+ [Mur d'enquête]
    #EVIDENCE:Board
    <i>Tu épingles tes papiers au mur et tu relies les noms au feutre. Une adresse ressort : Kivu-Transit Logistics, avenue du 30 Juin, Gombe.</i>
    -> menu
+ [Sortir]
    -> END

= garde_robe
+ [T-shirt blanc d'étudiant]
    #OUTFIT:Etudiant
    <i>Le t-shirt de la fac. On te reconnaît trop bien avec.</i>
    -> planque_menu.menu
+ [Chemise propre (pour Gombe)]
    #OUTFIT:ChemiseGombe
    <i>Chemise repassée, rentrée dans le pantalon. Tu ressembles à un stagiaire de ministère.</i>
    -> planque_menu.menu
+ [Tenue de travailleur de rue]
    #OUTFIT:TravailleurRue
    <i>Vieux polo, casquette, sandales. Personne ne regarde deux fois un gars comme ça à Matonge.</i>
    -> planque_menu.menu
+ [Retour]
    -> planque_menu.menu
