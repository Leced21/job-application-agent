# Phase 89.1 — relay RabbitMQ de l'outbox Collection

Cette tranche livre le premier transport asynchrone du plan : l'événement versionné `collection.offer-revision-recorded.v1` passe de l'outbox PostgreSQL à RabbitMQ. Il atteste seulement l'acquisition d'une révision préparatoire et ne déclenche aucune publication Job ou action Matching.

Le relay verrouille une ligne éligible avec `FOR UPDATE SKIP LOCKED`, publie un message durable avec publisher confirms, puis renseigne `DispatchedAtUtc` dans la transaction. Les erreurs de publication gardent l'événement dans l'outbox, incrémentent `AttemptCount`, enregistrent le type d'erreur et définissent une prochaine tentative exponentielle plafonnée à cinq minutes. Les envois restent au moins une fois : un arrêt après confirmation broker mais avant commit peut republier le même `EventId`, que tout consommateur doit dédupliquer.

RabbitMQ déclare l'exchange topic durable, une file dédiée à ce contrat et une dead-letter exchange/queue pour les rejets futurs des consommateurs. Le worker est désactivé par défaut. La configuration locale et la migration sont décrites dans [Événements Collection](collection-events.md). Le modèle ajoute les champs de reprise via `20261016121138_AddCollectionOutboxDelivery` ; cette migration n'est pas appliquée à une base Collection persistante.

Trois tests sont ajoutés : transitions de retry de l'entité et intégration du dispatcher (confirmation, absence de doublon après marquage, reprise après panne avec identité stable). La compilation complète de `JobApplicationAgent.slnx` réussit sans avertissement ni erreur, et la configuration Compose passe son contrôle de syntaxe. Les tests ne sont pas exécutés dans cette étape. Aucune connexion RabbitMQ réelle n'est ouverte et aucune migration n'est appliquée à la base locale.

La phase 89 continue : le contrat Collection ne doit pas être consommé par Matching, car il ne prouve ni approbation ni publication Job. La suite devra définir un événement de publication canonique Job et un consommateur idempotent Matching avant de recalculer les recommandations.
