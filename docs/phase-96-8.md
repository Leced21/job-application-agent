# Phase 96.8 — Interface de soumission contrôlée

L’écran Applications présente l’aperçu lié à la révision du dossier, recueille un accord explicite, réserve une tentative avec une clé stable, puis consigne le résultat déclaré par le candidat. Le parcours indique à chaque étape que l’application n’envoie ni formulaire ni document à la plateforme. Le formulaire d’envoi manuel reste disponible lorsqu’aucune tentative incertaine ou confirmée n’est en cours.

L’API authentifiée `GET /api/v1/applications/submission-platforms` expose l’état d’activation et les identifiants des plateformes configurées qui satisfont les règles de sécurité. Elle ne renvoie aucun secret ni détail de connexion. L’interface utilise ces identifiants au lieu de faire confiance à une plateforme déclarée par le navigateur.

Une tentative active permet de déclarer `Submitted`, `DefinitelyNotSubmitted` ou `Unknown`. Un résultat incertain est conservé et bloque une nouvelle tentative. Un résultat d’envoi confirmé fait passer la candidature au statut `Submitted`. Après un échec certain, le candidat doit relire et approuver de nouveau la révision avant une nouvelle réservation.

La réservation reste inaccessible tant que l’automatisation n’est pas activée et qu’une plateforme admissible n’est pas configurée côté serveur. Aucun adaptateur de soumission externe n’est implémenté dans cette phase.

Validation : compilation Angular, tests unitaires du catalogue et tests d’intégration Applications, dont le contrat de découverte des plateformes.
