# Phase 92.5 — readiness RabbitMQ conditionnelle

Les contrôles ready de Job, Collection et Matching vérifient maintenant RabbitMQ lorsque le transport d’événements du service est activé. La connexion de santé est limitée à cinq secondes et utilise les mêmes restrictions d’URI locale et les mêmes clés de configuration que les workers. Quand le transport est désactivé, le contrôle renvoie Healthy sans tenter de connexion ; la readiness continue alors de dépendre de PostgreSQL.

Les erreurs et valeurs de configuration ne sont pas renvoyées dans la réponse de santé. Les workers restent responsables de leur reconnexion ; le contrôle ready sert uniquement à rendre l’état du broker visible aux orchestrateurs.

Vérification : compilation des APIs Job, Collection et Matching réussie. Les tests ne sont pas exécutés conformément à la consigne de travail en cours.
