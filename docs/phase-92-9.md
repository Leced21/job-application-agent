# Phase 92.9 — statut et événements des traces

Les spans de calcul Matching, dispatch outbox et consommation RabbitMQ signalent désormais les échecs et annulations avec un statut et un résultat borné. Les rejets de messages ajoutent un événement de trace avec le type d’erreur technique ; le contenu de l’exception et les identifiants métier ne sont pas enregistrés dans les attributs.

Les résultats métier `job_not_found` et `profile_not_found` restent des calculs terminés sans statut d’erreur. Les métriques conservent leurs dimensions à faible cardinalité.

Vérification : compilation des APIs Job, Collection et Matching et des projets de tests associés ; tests compilés, non exécutés.
