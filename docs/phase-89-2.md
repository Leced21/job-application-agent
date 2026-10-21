# Phase 89.2 — contrat canonique `job.published.v1`

Cette tranche publie un événement uniquement lorsque Job accepte une révision Collection approuvée et crée l’offre canonique. L’enveloppe versionnée contient l’identifiant d’événement, l’offre Job, l’offre et la révision d’origine, l’empreinte et l’heure UTC. L’acquisition ou la revue Collection seule ne produit donc pas cet événement métier.

Job écrit l’offre canonique, le reçu de publication et l’événement `job.published.v1` dans la même transaction PostgreSQL. Une répétition idempotente retourne le reçu existant sans créer d’événement supplémentaire. La migration `20261016122512_AddJobOutbox` crée la table `job_outbox` et ses index ; elle doit être appliquée à la base Job avant d’exécuter cette version.

Le relay Job réclame les lignes dues avec `FOR UPDATE SKIP LOCKED`, publie sur l’exchange topic durable `jobapplication.events` avec publisher confirms et marque l’événement distribué. Une panne conserve l’événement et planifie un retry exponentiel plafonné à cinq minutes. Comme pour tout outbox, un crash après la confirmation RabbitMQ et avant le commit peut provoquer une nouvelle livraison avec le même `EventId` ; le consommateur doit dédupliquer. Le worker reste désactivé par défaut. Pour l’activer, configurer `Job:EventTransport:Enabled=true` et une URI AMQP locale avec identifiants dans `Job:EventTransport:RabbitMq:Uri`.

Les tests couvrent le contrat et l’absence de doublon au retry HTTP, les transitions d’échec/réussite du message outbox et l’acquittement unique du relay avec PostgreSQL. La compilation de la solution est le contrôle prévu ici ; les tests ne sont pas lancés dans cette étape. La migration est générée, mais n’est pas appliquée à une base persistante et aucun broker RabbitMQ n’est démarré.

Étape suivante : le consommateur Matching persiste l’identité d’événement traitée et applique une mise à jour idempotente à partir du Job canonique, sans dépendre des événements préparatoires Collection.
