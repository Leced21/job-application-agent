# Phase 92.3 — corrélation des événements RabbitMQ

Collection et Job enregistrent l’identifiant de corrélation HTTP dans leur outbox au moment où l’événement est créé. Le relay le publie dans l’en-tête AMQP `x-correlation-id`; le worker Matching ouvre un scope de log portant le même identifiant pendant le traitement. Les événements anciens ou créés hors d’une requête restent acceptés et sont journalisés avec `CorrelationId=unknown`.

Les tables `collection_outbox` et `job_outbox` reçoivent une colonne nullable `CorrelationId`, avec des migrations additives distinctes. Les messages JSON métier restent inchangés ; aucun contenu de profil ou autre donnée utilisateur n’est ajouté aux en-têtes.

Vérification : compilations des projets Collection, Job, Matching et des tests unitaires concernés réussies. Les tests ne sont pas exécutés conformément à la consigne de travail en cours. Appliquer les migrations Collection `20261017170000_AddCollectionOutboxCorrelationId` et Job `20261017162224_AddJobOutboxCorrelationId` avant de déployer ces binaires sur des bases existantes.
