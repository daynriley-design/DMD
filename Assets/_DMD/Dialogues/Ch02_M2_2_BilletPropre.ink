// ==========================================
// DMD — CHAPITRE 2 : L'OMBRE DE LA VILLE
// MISSION 2.2 : BILLET PROPRE
// Lieu : Rond-Point Victoire (Kalamu / Matonge) et ruelles adjacentes
// Moment : matinée (9h — circulation dense, soleil déjà fort)
// ==========================================
//
// Knots lancés par les DialogueTrigger de Zone_Matonge :
//   m2_2_chantal    → [E] sous le parasol de Mama Chantal (mission disponible)
//   m2_2_gerard     → [E] à la grille du dépôt de boissons (enveloppe en poche)
//   m2_2_retour     → [E] chez Mama Chantal (reçu tamponné en poche)
//   chantal_change  → [E] chez Mama Chantal, après la mission (bureau de change)
// La traversée (Scène 2) est du gameplay : les agents en civil sont des WitnessDetector
// en « repérage à vue » qui réagissent à l'enveloppe (voir Zone_Matonge).
// Récompense : +60 $ propres, versés par QuestManager quand la mission se termine.

VAR a_demande_contenu = false

-> m2_2_chantal


=== m2_2_chantal ===
#LOC:Rond_Point_Victoire
#QUEST:Start:M2_2
<i>Cris des receveurs du bus 207, klaxons de wewa, musique des boutiques de téléphonie. Mama Chantal est assise sur un tabouret en plastique sous un parasol délavé, à l'angle du Rond-Point Victoire.</i>
MAMA CHANTAL : "Tu changes combien, petit ? Aujourd'hui c'est 2 850. Et je prends pas les vieux billets de 2006, ni ceux qui sont déchirés."
TOI : "Je viens pas changer, Mama. C'est Cédric de Limete qui m'envoie. Il a dit que vous avez parfois besoin de quelqu'un de rapide pour des courses."
<i>Elle lève enfin les yeux et te jauge de la tête aux pieds.</i>
MAMA CHANTAL : "Ah, le neveu du mécanicien… T'as une tête d'étudiant qui a pas dormi. Qu'est-ce qui te presse autant ?"
TOI : "J'ai besoin de 50 dollars. Propres. Aujourd'hui."
MAMA CHANTAL : "Tout le monde veut des dollars propres à Kinshasa, mwana. Mais j'ai justement un souci. Il y a deux types bizarres qui tournent près de l'avenue Kasa-Vubu depuis ce matin. Soit c'est la police fiscale, soit c'est des braqueurs en repérage. Moi, si je bouge avec du cash, ils me collent. Toi, avec ton t-shirt blanc, t'as l'air d'un gars qui rentre de la fac."
<i>Elle sort une enveloppe kraft épaisse, fermée au scotch brun, et la glisse discrètement vers toi.</i>
MAMA CHANTAL : "Tu portes ça chez Papa Gérard, au dépôt de boissons derrière le marché de Matonge. Tu passes pas par la grande avenue. Tu prends les ruelles derrière la station-service. Si quelqu'un t'arrête, tu connais pas mon nom. Reviens avec son ticket signé, je te donne 60 dollars."

* [Demander ce qu'il y a dedans]
    ~ a_demande_contenu = true
    TOI : "Il y a combien là-dedans ?"
    MAMA CHANTAL : "Moins tu sais, moins tu transpires si on te contrôle. Tu prends ou tu laisses ?"
    TOI : "Je prends."
    -> enveloppe

* [Accepter directement]
    TOI : "Compris. Je passe par derrière."
    MAMA CHANTAL : "Bien. T'es malin. Fais vite, il ferme sa caisse à midi."
    -> enveloppe

= enveloppe
#ITEM:Add:Enveloppe_Scellee
#QUEST:Objective:M2_2:parler_chantal
<i>Tu glisses l'enveloppe sous ton t-shirt. Elle est lourde.</i>
-> END


=== m2_2_gerard ===
#LOC:Depot_Papa_Gerard
<i>Ruelle ombragée de Matonge. Des casiers de bières et de sucrés empilés jusqu'au plafond. Un homme massif en polo vérifie un cahier de comptes derrière une grille métallique.</i>
PAPA GÉRARD : "On est fermé pour les livraisons, petit. Reviens à 14 heures."
TOI : "Je viens de la part de Mama Chantal, à Victoire."
<i>Il jette un coup d'œil rapide dans la ruelle derrière toi, puis ouvre la grille d'un geste sec.</i>
PAPA GÉRARD : "Donne ça vite."
#ITEM:Remove:Enveloppe_Scellee
<i>Il ouvre l'enveloppe d'un coup de couteau, vérifie la liasse à la lumière, puis tamponne un petit bout de papier rose.</i>
#ITEM:Add:Recu_Tamponne
#QUEST:Objective:M2_2:livrer_enveloppe
PAPA GÉRARD : "C'est bon, le compte est juste. Tiens, ramène-lui le reçu. Et dis-lui que les « visiteurs » de Gombe ont aussi posé des questions dans le quartier hier soir. Ils cherchent un jeune qui a disparu avec un dossier."
<i>Tu gardes ton calme.</i>
TOI : "Je lui transmets."
-> END


=== m2_2_retour ===
#LOC:Rond_Point_Victoire
#ITEM:Remove:Recu_Tamponne
<i>Retour au Rond-Point Victoire. Mama Chantal prend le reçu rose, le déchire en petits morceaux et les jette dans un seau.</i>
MAMA CHANTAL : "Rapide et propre. Comme promis."
<i>Elle te tend un billet de 50 dollars neuf et un billet de 10.</i>
MAMA CHANTAL : "Regarde bien les bords : pas une déchirure, pas de tache d'encre, série récente. À Kinshasa, un billet sale vaut rien. Garde ça en tête."
TOI : "Papa Gérard m'a dit que des gens de Gombe posaient des questions dans le quartier hier soir."
<i>Son visage se ferme légèrement.</i>
MAMA CHANTAL : "Écoute-moi bien, petit. Moi je change de l'argent, je fais pas de politique. Mais si c'est toi qu'ils cherchent, t'as intérêt à savoir qui tire les ficelles avant qu'ils te trouvent. Il y a un ancien journaliste qui traîne le soir au nganda « Chez Ntemba », vers Matonge. Il boit trop, mais il connaît tous les noms des gens qui touchent aux minerais."
TOI : "Merci, Mama."
MAMA CHANTAL : "Va payer ta dette. Et si t'as besoin de changer tes dollars en francs, tu sais où je suis."
#QUEST:Objective:M2_2:ramener_recu
#UI:MissionComplete:"BILLET PROPRE — Débloqué : bureau de change chez Mama Chantal • Mission 2.3 : L'Oreille de Matonge"
<b>FIN DE LA MISSION 2.2 — BILLET PROPRE</b>
-> END


=== chantal_change ===
#LOC:Rond_Point_Victoire
<i>Mama Chantal tapote sa calculatrice sans lever les yeux.</i>
MAMA CHANTAL : "Aujourd'hui c'est 2 850 si tu vends tes dollars, 2 900 si tu en achètes. Et pas de vieux billets."
- (menu)
+ [Vendre 10 $]
    #EXCHANGE:Chantal:SellUsd:10
    <i>Elle compte les liasses de francs d'un pouce rapide.</i>
    -> menu
+ [Acheter 10 $ propres]
    #EXCHANGE:Chantal:BuyUsd:10
    <i>Elle te tend un billet neuf, série récente.</i>
    -> menu
+ [Lui proposer tes billets usés]
    #EXCHANGE:Chantal:SellWorn:10
    MAMA CHANTAL : "Je t'ai dit : pas ces billets-là. Va voir les petits changeurs du marché, ils prennent tout… à moitié prix."
    -> menu
+ [Partir]
    MAMA CHANTAL : "Tu sais où je suis."
    -> END
