// ==========================================
// DMD — CHAPITRE 2 : L'OMBRE DE LA VILLE
// MISSION 2.4 : LA LIGNE ROUGE (climax du chapitre)
// Lieu : Gombe — Boulevard du 30 Juin, immeuble Kivu-Transit Logistics → fuite vers Limete
// Moment : fin d'après-midi (16h30 — sortie des bureaux, embouteillages)
// ==========================================
//
// Knots lancés par les DialogueTrigger de Zone_Gombe (sauf m2_4_final, enchaîné à l'arrivée à Limete) :
//   m2_4_badge       → [E] le contact de Jean-Claude près de la Gare Centrale (mission disponible)
//   m2_4_accueil     → [E] le vigile du hall (badge en poche ; refuse si tu n'es pas en chemise)
//   m2_4_couloir     → automatique en arrivant au 4ème étage : Kankonde passe dans le couloir
//   m2_4_manifeste   → [E] le dossier rouge sur le bureau de Kankonde
//   m2_4_escalier    → [E] la porte de l'escalier de secours (après la photo)
//   m2_4_wewa        → automatique dans la ruelle arrière : fuite en moto jusqu'à Limete
//   m2_4_final       → retour à la planque, fin du Chapitre 2
//
// tenue est rempli par DialogueRunner avec l'identifiant de la tenue portée (ex. "ChemiseGombe").

VAR tenue = "Etudiant"
VAR nerveux = false
VAR erreurs_poursuite = 0

-> m2_4_badge


=== m2_4_badge ===
// Scène ajoutée (le script mentionne seulement « mon contact près de la Gare Centrale »).
#LOC:Gare_Centrale
#QUEST:Start:M2_4
<i>Devant la Gare Centrale, un homme en gilet de photographe ambulant fait semblant d'attendre un client. Il t'a reconnu avant que tu ouvres la bouche.</i>
LE CONTACT : "C'est le vieux Mbuyi qui t'envoie ? Stagiaire en comptabilité, Kivu-Transit Logistics. Ta photo est floue exprès : de loin, t'es n'importe qui."
#ITEM:Add:Faux_Badge
#QUEST:Objective:M2_4:obtenir_badge
LE CONTACT : "Tu le portes autour du cou, tu marches comme si t'étais en retard, et tu regardes jamais les caméras. Et si tu tombes, tu m'as jamais vu."
-> END


=== m2_4_accueil ===
#LOC:Hall_Kivu_Transit
{tenue != "ChemiseGombe":
    <i>Le vigile te barre la route avant même le portique.</i>
    AGENT DE SÉCURITÉ : "Les livreurs, c'est par derrière. Ici, c'est chemise et cravate, mon petit."
    <i>Avec ce look, tu n'entreras pas. Il te faut la chemise propre (garde-robe de la planque).</i>
    -> END
}
<i>Façades vitrées, 4x4 aux vitres teintées, la climatisation souffle dès la porte automatique. Tu ajustes le col de ta chemise et passes le portique.</i>
AGENT DE SÉCURITÉ : "Bonjour. Badge et pièce d'identité, s'il vous plaît."
<i>Tu tends ton faux badge, la voix posée.</i>
TOI : "Bonjour chef. Je suis le nouveau stagiaire en comptabilité, j'apporte les bordereaux pour le 4ème étage."
<i>Il examine le badge, puis ton visage.</i>
AGENT DE SÉCURITÉ : "T'es jeune, toi. C'est Monsieur Kankonde qui t'a validé ?"

* [Rester ferme et professionnel]
    TOI : "Oui, le secrétariat m'attend avant 17 heures. Si vous voulez, appelez le 4ème, mais je vais être en retard."
    AGENT DE SÉCURITÉ : "C'est bon, passe. Ascenseur à gauche."
    -> ascenseur

* [Montrer de la nervosité]
    ~ nerveux = true
    TOI : "Euh… oui, enfin c'est son adjoint qui m'a dit de monter."
    #SUSPICION:30
    <i>Il plisse les yeux.</i>
    AGENT DE SÉCURITÉ : "Attends là, je note ton nom dans le registre…"
    <i>Il griffonne quelque chose, puis te fait signe de passer. Là-haut, on t'attendra peut-être.</i>
    -> ascenseur

= ascenseur
#QUEST:Objective:M2_4:entrer_immeuble
#TELEPORT:Etage4
<i>Les portes de l'ascenseur se referment. 1… 2… 3… 4.</i>
-> END


=== m2_4_couloir ===
#LOC:Etage4_Kivu_Transit
<i>Couloir silencieux, moquette grise, caméras au plafond. La secrétaire parle fort au téléphone devant l'accueil. Derrière la vitre dépolie du bureau du fond, une silhouette en costume quitte la pièce avec un garde du corps.</i>
SÉBASTIEN KANKONDE : "…non, le convoi de Kolwezi part ce soir, peu importe ce que dit la douane. Et trouvez-moi ce gamin de la fac avant demain matin, je veux pas d'une fuite avant la signature au ministère."
<i>Il entre dans l'ascenseur privé sans te voir. Le couloir se vide. Son bureau reste ouvert.</i>
#QUEST:Objective:M2_4:atteindre_bureau
<i>Longe le mur aveugle pour éviter la caméra, et attends que la secrétaire parte vers l'imprimante.</i>
-> END


=== m2_4_manifeste ===
#LOC:Bureau_Kankonde
<i>Bureau luxueux avec vue sur le fleuve Congo. Sur le bureau en bois massif : un dossier rouge tamponné CONFIDENTIEL — EXPORTATION COBALT.</i>
#SFX:Camera_Shutter
#ITEM:Add:Photo_Manifeste
#QUEST:Objective:M2_4:photographier_manifeste
<i>Tu photographies chaque page, les codes et les signatures.</i>
TOI : "C'est pas juste Kankonde… Il y a le tampon du cabinet du ministre. C'est pour ça qu'ils veulent m'effacer."
<i>La porte s'ouvre d'un coup. Un garde du corps entre récupérer une mallette oubliée. Son regard croise le tien, puis tombe sur le dossier rouge ouvert.</i>
#HEAT:Kinshasa:+40
#FLAG:Add:Visage
#FLAG:Add:Camera
GARDE DU CORPS : "Hé ! C'est toi le petit de la photo ! Bouge pas !"
<i>Alarme silencieuse. Les portes de l'ascenseur se bloquent.</i>
TOI : "Merde ! Les escaliers !"
-> END


=== m2_4_escalier ===
#LOC:Escalier_Secours
<i>Tu dévales les quatre étages, la rampe brûlante sous la main. La porte coupe-feu cède sur une ruelle arrière de Gombe.</i>
#TELEPORT:Ruelle_Arriere
<i>Au bout de la rue, deux 4x4 noirs démarrent en trombe. Le crépuscule est rouge-orangé.</i>
-> END


=== m2_4_wewa ===
#LOC:Boulevard_30_Juin
<i>Au bord du boulevard embouteillé, un chauffeur de moto wewa attend un client.</i>

+ [Sauter à l'arrière et lui tendre 10 000 FC #COST:CDF:10000]
    TOI : "Limete ! Échangeur ! Vite, ils sont derrière moi !"
    -> depart
+ [Sauter à l'arrière : « Je te paie à Limete ! »]
    TOI : "Limete ! Échangeur ! Je te paie là-bas, ils sont derrière moi !"
    CHAUFFEUR WEWA : "Tu paies double, alors !"
    -> depart

= depart
<i>Il voit les hommes en costume jaillir de l'immeuble, sourit et met les gaz.</i>
CHAUFFEUR WEWA : "Accroche-toi bien, petit frère ! À cette heure-ci, aucun 4x4 peut nous rattraper !"
-> carrefour_1

= carrefour_1
<i>Boulevard du 30 Juin : un mur de voitures. À droite, des gyrophares bleus au carrefour. À gauche, un trottoir encombré de vendeuses de pain.</i>
CHAUFFEUR WEWA : "Gauche ou droite ?!"
+ [Gauche — le trottoir]
    <i>La moto monte sur le trottoir, les vendeuses crient, les paniers volent. Les gyrophares restent derrière.</i>
    -> carrefour_2
+ [Droite — le carrefour]
    ~ erreurs_poursuite++
    #HEAT:Kinshasa:+10
    <i>Un policier siffle et se jette presque devant la roue. Le chauffeur braque au dernier moment.</i>
    -> carrefour_2

= carrefour_2
<i>Raccourci vers Kasa-Vubu. Un 4x4 noir surgit de la ruelle de gauche pour vous couper la route.</i>
CHAUFFEUR WEWA : "Il arrive par où ?!"
+ [Droite — l'avenue en sens interdit]
    <i>Contresens, klaxons, un taxi jaune pile. Le 4x4 reste coincé dans la ruelle.</i>
    -> carrefour_3
+ [Gauche — foncer vers la ruelle]
    ~ erreurs_poursuite++
    #HEAT:Kinshasa:+10
    <i>Le pare-chocs du 4x4 frôle ta jambe. Le chauffeur jure et fait demi-tour sur une roue.</i>
    -> carrefour_3

= carrefour_3
<i>Entrée du Boulevard Lumumba. Barrage de police devant la station-service à gauche ; à droite, un passage entre deux camions de ciment.</i>
CHAUFFEUR WEWA : "Dernier virage, petit !"
+ [Droite — entre les camions]
    <i>La moto se faufile entre les deux camions, l'Échangeur apparaît au loin.</i>
    -> arrivee
+ [Gauche — passer le barrage au culot]
    ~ erreurs_poursuite++
    #HEAT:Kinshasa:+15
    #FLAG:Add:Vehicule
    <i>Un agent lève le bras, le chauffeur accélère. Derrière vous, quelqu'un note la plaque de la moto.</i>
    -> arrivee

= arrivee
{erreurs_poursuite == 0:
    #STAT:Rue:+5
    CHAUFFEUR WEWA : "Ha ! T'as les yeux d'un vrai motard, toi !"
}
#QUEST:Objective:M2_4:fuir_gombe
#ZONE:Limete:Planque
#NEXT:m2_4_final
<i>Limete. L'Échangeur se découpe sur le ciel noir. Tu sautes de la moto et tu disparais dans les ruelles.</i>
-> END


=== m2_4_final ===
#LOC:Planque_Limete
<i>Nuit tombée. Tu refermes le portail vert rouillé, le souffle court, la chemise trempée de sueur. Tu verrouilles la porte de l'annexe et tu poses la photo du manifeste sur le mur d'enquête.</i>
#SFX:SMS_Alert
<i>Ton téléphone torche vibre. Un SMS d'un numéro inconnu.</i>
JEAN-CLAUDE : "T'as survécu à Gombe. Bravo. Mais maintenant ils savent que t'as les noms. Reste pas à Limete trop longtemps. Demain, on passe à l'étape suivante."
<i>Sur le mur, trois mots reliés au marqueur : DAMU — le sang versé. MADINI — les minerais. DOLA — l'État.</i>
#QUEST:Objective:M2_4:rentrer_planque
#UI:ChapterComplete:"FIN DU CHAPITRE 2 — L'OMBRE DE LA VILLE • Débloqué : Chapitre 3 — L'Étau"
<b>FIN DU CHAPITRE 2 — L'OMBRE DE LA VILLE</b>
-> END
