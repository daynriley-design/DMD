// ==========================================
// DMD — CHAPITRE 2 : L'OMBRE DE LA VILLE
// MISSION 2.1 : ZÉRO TRACE
// Lieu : Limete (ruelles parallèles au Boulevard Lumumba, abords de l'Échangeur)
// Moment : fin d'après-midi → crépuscule
// ==========================================
//
// Chaque scène est un knot lancé par le jeu (DialogueTrigger dans Zone_Limete) :
//   m2_1_appel    → au début du chapitre (DialogueRunner de Persistent_Core, lancement auto)
//   m2_1_canal    → [E] au bord du canal         (après l'appel)
//   m2_1_vendeur  → [E] près du présentoir de Bébé-Cash, à l'Échangeur (smartphone détruit)
//   m2_1_planque  → [E] devant le portail vert rouillé, 12ème rue (téléphone torche acheté)
// Les tags (#QUEST, #CASH, #PHONE...) sont appliqués par InkTagRouter.

VAR prenom = "Toi"
VAR a_negocie = false

-> m2_1_appel


=== m2_1_appel ===
#LOC:Ruelle_Limete
#SFX:Phone_Vibrate
<i>Ruelle en terre battue, à deux cents mètres du Boulevard Lumumba. Klaxons sourds des taxis jaunes au loin. Ton smartphone vibre violemment dans ta poche : CÉDRIC — Appel entrant.</i>
CÉDRIC : "Allô ? Dis-moi que t'es pas sur le boulevard."
TOI : "Non, je suis passé par les ruelles derrière la 7ème rue. Pourquoi ?"
CÉDRIC : "Ils sont passés à la fac tout à l'heure. Deux mecs en civil, chemises rentrées, lunettes noires. Ils ont posé des questions au concierge avec ta photo sur un téléphone."
<i>Tu t'arrêtes net.</i>
TOI : "Ils ont dit quoi ?"
CÉDRIC : "Que t'étais impliqué dans une affaire de « vol de documents ». Écoute-moi bien, ndeko : ton téléphone, c'est une balise. Tant que ta SIM est dedans, ils savent où tu respires."
TOI : "J'ai pas d'autre moyen de te joindre, Cédric."
CÉDRIC : "T'as besoin de rien d'autre que de mes instructions. Tu vas vers le canal, tu casses ce téléphone et tu le jettes. Pas dans une poubelle, dans l'eau. Ensuite, tu montes près de l'Échangeur, tu t'achètes un petit téléphone torche et une puce déjà enregistrée. Tu connais le prix."
TOI : "Et la planque ?"
CÉDRIC : "La parcelle de mon oncle, 12ème rue Limete, portail vert rouillé. Je t'attends devant le petit dépôt de ciment. Mais viens pas avec ton smartphone, sinon j'ouvre même pas. Keba, frère."
#QUEST:Start:M2_1
<i>L'appel coupe.</i>
-> END


=== m2_1_canal ===
#LOC:Canal_Limete
<i>Tu longes un mur de parpaings jusqu'au canal d'évacuation à ciel ouvert. Deux policiers discutent près d'un kiosque à crédit, à trente mètres.</i>
#SFX:Phone_Vibrate
<i>Le smartphone vibre à nouveau. NUMÉRO MASQUÉ.</i>
TOI : "C'est pas Cédric…"
#PHONE:DestroySmartphone
#QUEST:Objective:M2_1:detruire_smartphone
#SFX:Splash
<i>Tu arraches la carte SIM, la plies en deux, écrases l'écran contre le béton du muret et balances le tout dans l'eau boueuse. Le téléphone coule.</i>
TOI : "Voilà. Plus de compte, plus de contacts, plus de nom. Maintenant, il me faut une ligne propre."
-> END


=== m2_1_vendeur ===
#LOC:Echangeur_Limete
<i>Abords animés de l'Échangeur de Limete. Lumière dorée du coucher de soleil. Un jeune vendeur tient un petit présentoir en bois rempli de chargeurs, d'écouteurs et de téléphones à touches.</i>
BÉBÉ-CASH : "Mbote, petit ! Tu cherches quoi ? Chargeur original ? Écouteurs ? J'ai même des powerbanks qui tiennent deux jours sans courant de la SNEL."
TOI : "Un téléphone torche. Simple. Et une puce qui marche direct, sans papier."
<i>Il te dévisage, jette un œil à ton t-shirt blanc.</i>
BÉBÉ-CASH : "T'es étudiant, toi, non ? T'as l'air pressé. Pour un bon petit Itel avec la batterie qui tient, plus une SIM déjà activée… ça fait 35 000 Francs."

* [Négocier — économise 10 000 FC, mais attire un peu l'attention]
    ~ a_negocie = true
    TOI : "35 000 ? Tu me prends pour un touriste de Gombe ? Je te donne 25 000, cash, et je disparais."
    #CASH:CDF:-25000
    #HEAT:Kinshasa:+3
    BÉBÉ-CASH : "Ah, t'es un enfant de Kin quand même ! Va pour 25 000, mais tu dis pas où t'as pris la puce."
    -> conseil

* [Payer sans discuter — discret et rapide, 35 000 FC]
    TOI : "Tiens. 35 000. Donne-moi ça vite."
    #CASH:CDF:-35000
    BÉBÉ-CASH : "J'aime les clients qui parlent pas trop. Tiens, la batterie est déjà à moitié chargée."
    -> conseil

= conseil
<i>Il baisse la voix en te tendant le téléphone.</i>
BÉBÉ-CASH : "Un conseil gratuit, petit : si tu veux pas d'ennuis ce soir, évite le rond-point. Les voitures noires tournent depuis 17 heures."
#PHONE:GiveBurner
#QUEST:Objective:M2_1:acheter_torche
TOI : "Merci."
-> END


=== m2_1_planque ===
#LOC:Planque_Limete
<i>Nuit tombante. Une rue calme de Limete résidentiel. Cédric est adossé près d'un portail vert rouillé, les mains dans les poches. Il sursaute en te voyant arriver.</i>
CÉDRIC : "T'as mis le temps. T'as jeté l'ancien ?"
<i>Tu sors le téléphone torche de ta poche.</i>
TOI : "Il est au fond du canal. Et j'ai pris ça à l'Échangeur."
<i>Il soupire de soulagement et ouvre le portail.</i>
CÉDRIC : "Entre vite. Mon oncle est à Lubumbashi pour deux mois, le voisinage pose pas de questions tant que tu fais pas de bruit et que tu paies l'eau et le courant."
<i>Une petite annexe en parpaings : un matelas posé au sol, une table en plastique, une ampoule nue qui grésille faiblement.</i>
CÉDRIC : "C'est pas le Pullman, mais ici personne te connaît. Note mon nouveau numéro sur ton torche, mais enregistre-le pas sous mon vrai nom. Mets « Plombier »."
TOI : "Cédric… merci. Je sais que tu prends un risque."
<i>Il s'arrête sur le seuil, grave.</i>
CÉDRIC : "T'es mon frère, mais je vais être honnête avec toi : je peux pas t'aider plus que ça. J'ai ma famille ici. Dès demain, tu dois trouver du cash par toi-même. Mon oncle a laissé une dette au gardien du quartier : 50 dollars d'ici trois jours, sinon il appelle le chef de rue pour vérifier qui dort ici."
TOI : "50 dollars… sans carte bancaire, sans boulot."
CÉDRIC : "Bienvenue à Kinshasa, ndeko. Va voir du côté du Rond-Point Victoire demain matin. Les cambistes ont toujours besoin de quelqu'un qui sait courir et fermer sa bouche. Repose-toi."
#SAFEHOUSE:Unlock
#ITEM:Add:Contact_Plombier
<i>Cédric sort et referme le portail. Tu restes seul dans la pièce : le matelas, la table, le mur vide.</i>
#QUEST:Objective:M2_1:rejoindre_planque
#UI:MissionComplete:"ZÉRO TRACE — Débloqué : la Planque • Mission 2.2 : Billet Propre"
<b>FIN DE LA MISSION 2.1 — ZÉRO TRACE</b>
-> END
