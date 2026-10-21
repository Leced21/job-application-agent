# Phase 92.2 — propagation de la corrélation aux APIs

Le middleware de corrélation est maintenant partagé par le Gateway et les APIs Profile, Job, Matching, Agents, Applications, Documents et Collection. Chaque API conserve un GUID reçu du Gateway ou en crée un si l’appel arrive directement sans identifiant valide. Elle renvoie `X-Correlation-ID`, renseigne le `TraceIdentifier`, ajoute le tag à l’activité courante et écrit une entrée structurée avec le statut et la durée de la requête.

Le middleware s’exécute avant les gestionnaires d’exceptions afin que les réponses d’erreur portent aussi l’identifiant et soient journalisées. Les événements RabbitMQ et les workers sans serveur HTTP ne sont pas encore corrélés ; ils feront l’objet d’un lot distinct.

Vérification : les compilations des APIs et du projet de tests unitaires Gateway réussissent. Les tests ne sont pas exécutés conformément à la consigne de travail en cours.
