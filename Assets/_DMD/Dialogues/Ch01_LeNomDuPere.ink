// ==========================================
// DMD — CHAPITRE 1 : LE NOM DU PÈRE
// Lieu : Université Loyola, Kinshasa
// ==========================================
//
// Tags lus par InkTagRouter.cs :
//   #STAT:<Rue|Savoir|Influence|Voix>:<+/-n>     → PlayerStats (appliqués ensemble, par ligne)
//   #HEAT:<Ville>:<+/-n>                          → HeatManager
//   #FLAG:<Add|Clear>:<Visage|Tenue|Vehicule|Arme|Camera|Complice>
//   #ITEM:Add:<Id>    #LOC:<Lieu>    #SFX:<Id>    #UI:<Type>:"texte"    #SCENE:Load:<Scène>

VAR prenom = "Toi"  // remplacé au lancement par le prénom choisi à l'écran titre (DialogueRunner)
VAR nom_pere_utilise = false
VAR dossier_kivu_recupere = false
VAR approche_fuite = ""

-> scene_controle

=== scene_controle ===
#LOC:Portail_Loyola
Le soleil tape déjà sur l'esplanade de Loyola. Devant le tourniquet, l'agent de sécurité bloque le passage à Junior, ton ami de promo boursier.

AGENT : "Pas de bordereau de minerval, pas d'accès. Dégage, tu bloques les autres."
JUNIOR : "Chef, la banque n'a pas encore validé le virement, j'ai examen dans vingt minutes..."

L'agent t'aperçoit derrière Junior. Son visage se détend aussitôt.
AGENT : "Bonjour, fils du Gouverneur. Passez, je vous en prie."

* [Utiliser le nom de ton père : "Il est avec moi. Laisse-le passer."]
    ~ nom_pere_utilise = true
    #STAT:Influence:+5
    #STAT:Voix:-5
    Tu poses la main sur le tourniquet et fixes l'agent.
    TOI : "C'est un proche. Si le rectorat a un problème, dis-leur d'appeler le cabinet de mon père."
    L'agent ravale sa salive et s'écarte. Junior passe en baissant les yeux.
    JUNIOR : "Merci... mais tu n'avais pas besoin de lui parler comme ça."
    -> scene_fissure

* [Payer discrètement en liquide pour débloquer la situation.]
    #STAT:Voix:+5
    #STAT:Savoir:+3
    Tu glisses discrètement un billet plié dans la main de Junior, hors de vue des autres étudiants.
    TOI : "Va au guichet du fond, règle l'avance et rejoins-moi en amphi."
    JUNIOR : "Je te rembourse dès que la bourse tombe. Merci frère."
    -> scene_fissure

* [Ne pas t'en mêler et passer par le couloir libre.]
    #STAT:Voix:-5
    Tu ajustes ton sac et passes sans croiser le regard de Junior. L'agent te salue militairement.
    -> scene_fissure


=== scene_fissure ===
#LOC:Cafeteria_Loyola
Une heure plus tard, à la cafétéria. Une étudiante que tu connais de vue — Nadia, originaire du Nord-Kivu — s'assoit brusquement en face de toi et pose une photocopie pliée sur la table.

NADIA : "Tu as cinq secondes avant que je parte. Reconnais cette signature en bas de la page."

C'est un arrêté d'exploitation minière dans le Masisi. En bas à droite : le paraphe exact de ton père, daté d'il y a six mois.

NADIA : "Deux semaines après ce papier, des hommes armés ont vidé le village de mon oncle pour sécuriser le carré minier. Ton père ne gouverne pas Goma, il la vend."

* [Garder la photocopie : "Laisse-moi vérifier si c'est un faux."]
    ~ dossier_kivu_recupere = true
    #STAT:Savoir:+10
    #STAT:Influence:-5
    #ITEM:Add:Arrete_Minier_Goma
    Tu plies la feuille et la glisses dans la poche intérieure de ta veste.
    TOI : "Si c'est vrai, je veux savoir qui d'autre a signé avec lui."
    NADIA : "Ne montre ça à personne de son entourage. Pas même à sa sécurité."
    -> scene_appel_goma

* [L'accuser de manipulation et quitter la table.]
    #STAT:Influence:+5
    #STAT:Savoir:-10
    TOI : "C'est de la politique sale. Mon père se bat contre ces réseaux-là."
    NADIA : "Continue de te mentir. Tu finiras par payer sa facture."
    -> scene_appel_goma

* [Lui arracher la feuille et la menacer de la faire exclure.]
    ~ dossier_kivu_recupere = true
    #STAT:Rue:+10
    #STAT:Voix:-10
    #ITEM:Add:Arrete_Minier_Goma
    Tu plaques ta main sur le document et le tires vers toi.
    TOI : "Tu diffuses ça sur le campus et tu ne remets plus un pied à Loyola."
    Nadia recule, le regard glacé, et se lève sans un mot.
    -> scene_appel_goma


=== scene_appel_goma ===
#LOC:Parking_Loyola
#SFX:Phone_Ring
En sortant vers ton 4x4, ton téléphone vibre. Numéro de la résidence à Goma.
Tu décroches. À l'autre bout, des détonations sourdes et la voix brisée d'un garde :
"Ils ont forcé le portail ! Le Gouverneur et Madame... ils sont..."
La ligne coupe.

Autour de toi, les étudiants s'arrêtent en regardant leurs téléphones. Les alertes infos tombent déjà.
Au même instant, deux berlines noires aux vitres teintées franchissent le portail du campus.

#SFX:SMS_Alert
Un SMS s'affiche — c'est Kabasele, le vieux chauffeur de ton père :
"NE MONTE PAS AVEC EUX. C'est les mêmes qui ont donné l'ordre à Goma. Fuis maintenant."

* [Approche Jeu Propre : Abandonner le 4x4, changer de veste et sortir avec la foule par l'arrière-cour.]
    ~ approche_fuite = "Propre"
    #STAT:Savoir:+5
    #HEAT:Kinshasa:+10
    #FLAG:Clear:Tenue
    Tu laisses tes clés dans le caniveau, enfiles le blouson anonyme prêté par un camarade et te fonds dans la sortie des cours. Les hommes en noir te cherchent près du parking.
    -> fin_chapitre_1

* [Approche Jeu Sale : Foncer vers le 4x4 et forcer le barrage au portail.]
    ~ approche_fuite = "Sale"
    #STAT:Rue:+10
    #HEAT:Kinshasa:+45
    #FLAG:Add:Visage
    #FLAG:Add:Vehicule
    Tu démarres en trombe. Un homme en costume dégaine devant le portail ; tu braques sec, arraches la barrière et t'engouffres dans le trafic de Kinshasa. Les sirènes se déclenchent derrière toi.
    -> fin_chapitre_1


=== fin_chapitre_1 ===
// Une ligne de texte porte les tags finaux : Ink rattache les tags à la ligne qui suit.
#UI:BankNotification:"COMPTE BLOQUÉ — DÉCISION JUDICIAIRE"
#SCENE:Load:Persistent_Core
Ton téléphone vibre une dernière fois. Une notification de ta banque.
-> END
