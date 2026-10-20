# Phase 90.6 — suivi des candidatures

L’écran `/applications` relie l’interface Angular à l’API locale Applications. Il présente les dossiers filtrables par statut, les relances arrivées à échéance, le détail d’une candidature, ses documents associés et son historique.

Un jeton Applications distinct est demandé à l’ouverture. Il est conservé en mémoire dans le navigateur et supprimé à la déconnexion ou après une réponse 401. La liste et les relances échues sont limitées à 100 éléments par requête.

Depuis un dossier envoyé, en cours ou en entretien, le candidat peut ajouter une note privée, planifier un rappel ou consigner un retour recruteur déjà reçu. Ces actions transmettent la révision attendue pour détecter les modifications concurrentes. Les relances restent des rappels et les retours sont saisis manuellement ; l’interface ne contacte aucun recruteur.

La phase 95.12 ajoute l’enregistrement explicite d’un envoi effectué manuellement pour un dossier approuvé. La confirmation est obligatoire ; la date, le canal et une référence facultative sont conservés dans l’historique. Cette déclaration ne soumet rien à une plateforme externe.

La phase 95.14 permet de marquer une relance comme effectuée avec son résultat ou de l’annuler avec un motif. Ces actions actualisent le dossier et son historique sans envoyer de message.

La phase 95.12 ajoute l’enregistrement explicite d’un envoi effectué manuellement pour un dossier approuvé. Le formulaire exige une confirmation, conserve la date, le canal et une référence facultative, puis l’événement apparaît dans l’historique. Il ne soumet rien à une plateforme externe.

## Vérification

La compilation Angular et la compilation TypeScript des spécifications sont les contrôles prévus pour cette tranche. Les tests ne sont pas exécutés dans le flux de travail demandé.
