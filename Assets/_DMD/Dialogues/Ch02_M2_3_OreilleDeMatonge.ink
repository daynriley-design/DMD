// ==========================================
// DMD — CHAPITRE 2 : L'OMBRE DE LA VILLE
// MISSION 2.3 : L'OREILLE DE MATONGE
// Lieu : Matonge — nganda « Chez Ntemba » et cybercafé « Cyber-Éclair »
// Moment : soirée (20h30 — rumba en fond, néons blancs, fumée de braises de ntaba)
// ==========================================
//
// Knots lancés par les DialogueTrigger de Zone_Matonge :
//   m2_3_ntemba          → [E] à la table de Jean-Claude (nganda ouvert de 19h à 2h)
//   cyber_patrick        → [E] au comptoir du Cyber-Éclair : soudoyer (A) ou attendre un client (C)
//   cyber_disjoncteur    → [E] boîtier rouillé derrière le cyber (B, étape 1)
//   cyber_comptoir_noir  → [E] derrière le comptoir dans le noir (B, étape 2)
//   m2_3_retour          → [E] à la table de Jean-Claude, clé en poche
//
// stat_rue est rempli automatiquement par DialogueRunner avec la stat Rue du joueur :
// plus elle est haute, plus l'infiltration a de chances de passer inaperçue.

VAR stat_rue = 20
VAR approche_cle = ""

-> m2_3_ntemba


=== m2_3_ntemba ===
#LOC:Nganda_Chez_Ntemba
#QUEST:Start:M2_3
<i>Terrasse animée de Matonge. Chaises en plastique rouge, bouteilles de bière sur les tables, la rumba couvre les conversations. Au fond, sous une ampoule qui clignote, un homme gribouille sur un vieux journal plié en quatre. Chemise froissée, lunettes cassées réparées au scotch.</i>
TOI : "Bonsoir, Monsieur Mbuyi. C'est Mama Chantal de Victoire qui m'a dit que je pouvais vous trouver ici."
JEAN-CLAUDE : "Mama Chantal m'envoie jamais de clients. Elle m'envoie que des problèmes. T'as quel âge, toi ? Dix-huit ans ? Rentre chez tes parents, petit."
<i>Tu t'assois en face de lui et baisses la voix.</i>
TOI : "Il y a des hommes en civil qui tournent à Limete et à Matonge avec ma photo. Ils disent que j'ai volé un dossier. Un dossier sur des exportations de minerais."
<i>Il s'arrête d'écrire et lève lentement les yeux par-dessus ses lunettes.</i>
JEAN-CLAUDE : "Madini… Si t'as mis le nez dans les bordereaux de cuivre et de cobalt, t'es déjà un fantôme, mon garçon. Pourquoi tu viens me voir, moi ?"
TOI : "Parce que je sais même pas qui me chasse. Je veux un nom."
<i>Il boit une gorgée et regarde autour de lui.</i>
JEAN-CLAUDE : "Les noms, je les ai. Mais je parle pas gratuitement, et je parle pas sans mes preuves. Hier soir, j'ai voulu scanner des vieux papiers au Cyber-Éclair, deux rues plus bas. Le courant a coupé, et deux gars du quartier qui bossent pour des patrons de Gombe ont débarqué. J'ai filé par derrière, mais ma clé USB rouge est restée branchée sur le poste 4. Le gérant l'a mise sous clé dans son comptoir."
TOI : "Si je vous la ramène, vous me donnez le nom ?"
#QUEST:Objective:M2_3:trouver_journaliste
JEAN-CLAUDE : "Ramène-moi la clé intacte, et je te dirai exactement quel bureau de Gombe signe les faux manifestes. Mais fais gaffe : le gérant est nerveux, et il y a un guetteur devant la porte."
-> END


=== cyber_patrick ===
#LOC:Cyber_Eclair
<i>Petit cybercafé étroit aux murs bleus, cinq vieux PC alignés. Derrière un comptoir vitré, le gérant pianote sur son téléphone. Une clé USB rouge dépasse d'un tiroir entrouvert derrière lui.</i>
PATRICK : "Une heure, c'est 500 francs. Les postes 2 et 5 marchent pas."

+ [Soudoyer Patrick — rapide et propre #COST:USD:20]
    ~ approche_cle = "Soudoyer"
    TOI : "Patrick, j'ai oublié une clé USB rouge hier soir. C'est pour un mémoire de fac."
    PATRICK : "Ah ouais ? Les deux messieurs en 4x4 qui sont passés ce matin m'ont dit de les appeler si quelqu'un la réclamait."
    <i>Tu glisses un billet de 20 dollars propre sur le comptoir.</i>
    TOI : "Tu les appelles pas, et t'as jamais vu personne."
    #ITEM:Add:Cle_USB_Journaliste
    #QUEST:Objective:M2_3:recuperer_cle
    <i>Il fixe le billet, le fait disparaître dans sa poche et te tend la clé sous le comptoir.</i>
    PATRICK : "T'as trente secondes pour sortir avant que je change d'avis."
    -> END

+ [Prendre un poste et attendre une occasion (infiltration, risqué)]
    ~ approche_cle = "Infiltration"
    -> infiltration

+ [Ressortir]
    PATRICK : "Ferme la porte, les moustiques."
    -> END

= infiltration
<i>Tu t'installes au poste 3. Dix minutes passent. Au fond de la salle, un client s'énerve contre l'imprimante.</i>
CLIENT : "Patrick ! Ta machine a encore mangé ma feuille !"
<i>Patrick soupire et part vers le fond. Le comptoir est vide. Le guetteur fume, de dos, dans l'encadrement de la porte.</i>
+ [Se glisser accroupi derrière le comptoir]
    {RANDOM(1, 100) <= 35 + stat_rue:
        -> infiltration_reussie
    - else:
        -> infiltration_ratee
    }
+ [Renoncer et ressortir]
    <i>Tu fermes ta session et tu sors sans te presser.</i>
    -> END

= infiltration_reussie
#STAT:Rue:+8
#ITEM:Add:Cle_USB_Journaliste
#QUEST:Objective:M2_3:recuperer_cle
<i>Trois pas accroupi, le tiroir glisse sans bruit, la clé rouge est dans ta paume. Tu es de retour au poste 3 avant que l'imprimante ne recrache sa feuille.</i>
-> END

= infiltration_ratee
#STAT:Rue:+5
#ITEM:Add:Cle_USB_Journaliste
#QUEST:Objective:M2_3:recuperer_cle
<i>Ta main se referme sur la clé au moment où Patrick se retourne.</i>
PATRICK : "Eh ! Toi là !"
#HEAT:Kinshasa:+25
#FLAG:Add:Visage
#FLAG:Add:Tenue
GUETTEUR : "Il a pris un truc ! Attrapez-le !"
<i>Tu bouscules la porte et plonges dans les ruelles de Matonge. Derrière toi, des pas qui courent et des sifflets.</i>
-> END


=== cyber_disjoncteur ===
#LOC:Arriere_Cyber_Eclair
<i>Derrière le bâtiment, un boîtier métallique rouillé grésille contre le mur. Personne dans la ruelle.</i>
#QUEST:Objective:M2_3:couper_courant
#SFX:Breaker
<i>Tu abaisses la manette. Les néons du cyber s'éteignent d'un coup.</i>
PATRICK : "Oh ! Encore la SNEL !"
<i>Par la fenêtre, tu vois la lampe de son téléphone se diriger vers le compteur, côté rue. Le comptoir est dans le noir.</i>
-> END


=== cyber_comptoir_noir ===
#LOC:Cyber_Eclair
~ approche_cle = "Diversion"
#STAT:Savoir:+5
#ITEM:Add:Cle_USB_Journaliste
#QUEST:Objective:M2_3:recuperer_cle
<i>Dans la pénombre, tu passes derrière le comptoir. Le tiroir est resté entrouvert. La clé USB rouge glisse dans ta poche, et tu ressors par où tu es venu pendant que Patrick jure contre le compteur.</i>
-> END


=== m2_3_retour ===
#LOC:Nganda_Chez_Ntemba
#ITEM:Remove:Cle_USB_Journaliste
<i>Retour au nganda. Jean-Claude fait tourner la clé USB dans sa paume, puis la glisse dans sa chaussette.</i>
JEAN-CLAUDE : "T'as du sang-froid pour un gamin de 18 ans. Bon, une promesse est une promesse."
<i>Il déchire un coin de son journal, note une adresse et un nom au stylo Bic, et le pousse vers toi.</i>
#ITEM:Add:Note_Kivu_Transit
JEAN-CLAUDE : "Sébastien Kankonde. Officiellement, c'est un conseiller technique au ministère. Officieusement, il gère une société écran à Gombe, Kivu-Transit Logistics, avenue du 30 Juin. C'est par ses bureaux que passent les camions fantômes qui sortent les minerais sans déclarer un centime à l'État."
TOI : "Et c'est lui qui a envoyé les hommes à ma fac ?"
JEAN-CLAUDE : "Lui ou son bras droit. Si tu veux sortir de ce piège sans finir au fond du fleuve, tu dois entrer dans leurs bureaux à Gombe et photographier le manifeste d'exportation de ce mois-ci. Avec ça, ils pourront plus te faire taire sans tout faire exploser."
TOI : "Comment j'entre dans un immeuble de Gombe en t-shirt blanc ? Ils me laisseront même pas passer la barrière."
<i>Il esquisse un sourire amer.</i>
JEAN-CLAUDE : "Tu rentres à ta planque, tu te trouves une chemise propre, et demain tu passes voir mon contact près de la Gare Centrale pour un faux badge de stagiaire. Bienvenue dans la cour des grands, petit. Ici, c'est plus juste la rue… c'est l'État."
#QUEST:Objective:M2_3:rapporter_cle
#UI:MissionComplete:"L'OREILLE DE MATONGE — Débloqué : Gombe • Épingle la note sur le mur d'enquête de la planque"
<b>FIN DE LA MISSION 2.3 — L'OREILLE DE MATONGE</b>
-> END
