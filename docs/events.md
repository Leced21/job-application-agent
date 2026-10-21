# Événements asynchrones

Ce guide décrit les contrats actuellement transportés par RabbitMQ, leurs producteurs et leurs garanties. Les événements sont des faits versionnés : la publication d'une révision en zone Collection ne vaut pas publication d'une offre canonique. Matching ne consomme actuellement que `job.published.v1`.

## Contrats et routage

| Événement | Producteur et déclencheur | Routage RabbitMQ | Consommateur |
| --- | --- | --- | --- |
| `collection.offer-revision-recorded.v1` | Collection, quand une révision est enregistrée ou réimportée | Exchange topic durable `jobapplication.events`, clé et file `collection.offer-revision-recorded.v1` | Aucun pour le moment |
| `job.published.v1` | Job, quand une révision Collection approuvée devient une offre canonique Job | Exchange topic durable `jobapplication.events`, clé et file `matching.job-published.v1` | Matching |

Les deux contrats ont une enveloppe JSON en camelCase, un `eventId` stable, un `eventType`, `schemaVersion` égal à 1, une date UTC, un producteur et une empreinte SHA-256. Exemple de l'événement Job :

```json
{
  "eventId": "e706ac6c-15da-4e1e-89c1-13e75e389eb0",
  "eventType": "job.published.v1",
  "schemaVersion": 1,
  "occurredAtUtc": "2026-10-18T10:30:00Z",
  "producer": "Job",
  "jobId": "5c37703d-afb0-42eb-9e3b-c394aa1b88a7",
  "collectionOfferId": "cb92d317-75cf-44eb-8441-ab49af1bcd15",
  "collectionRevisionId": "513cad34-d6df-4ec8-8b19-6ef74bca33f3",
  "fingerprint": "0000000000000000000000000000000000000000000000000000000000000000"
}
```

Le contrat Collection complet et sa signification sont dans [collection-events.md](collection-events.md). Les contrats sont compatibles par version : un changement incompatible doit ajouter une nouvelle version de type d'événement et de routage. Les payloads ne contiennent pas le contenu intégral du CV ou de l'offre.

## Livraison et reprise

Collection et Job écrivent leurs événements dans leur outbox PostgreSQL dans la transaction métier concernée. Le relay réclame les messages dus avec `FOR UPDATE SKIP LOCKED`, publie un message persistant et attend la confirmation RabbitMQ avant de marquer la ligne distribuée. Les échecs de publication restent dans l'outbox et sont retentés avec délai exponentiel, plafonné à cinq minutes. Les workers de transport restent désactivés par défaut.

La livraison est **au moins une fois** : un arrêt entre la confirmation du broker et le commit PostgreSQL peut rediffuser le même `eventId`. Les consommateurs doivent donc dédupliquer. Matching valide le type AMQP, l'identité du message et l'enveloppe, calcule à partir du Job canonique, puis écrit l'inbox et le résultat dans une seule transaction. L'inbox `matching_event_inbox` utilise `EventId` comme clé primaire et conserve une empreinte du payload. Une redélivrance identique est acquittée sans second résultat ; le même identifiant avec un autre contenu est une erreur.

Matching acquitte après le commit. Un message JSON invalide ou incompatible est rejeté sans requeue vers `matching.job-published.v1.dead`. Une erreur transitoire attend cinq secondes puis est remise en file. Le consommateur utilise un préchargement de 1 et reconnecte le broker avec délai croissant plafonné à 30 secondes. Si aucun profil correspondant n'est disponible au traitement, l'événement est enregistré comme traité sans résultat : une arrivée ultérieure du profil ne relance pas automatiquement ce calcul.

Une file morte exige une inspection avant toute remise en file : vérifier le type de rejet et le payload, corriger la cause, puis republier en préservant l'identité si le message reste identique. Pour rejouer un événement avec un payload corrigé, émettre un nouvel EventId selon une procédure opératoire revue ; réutiliser l'EventId avec un autre payload est explicitement refusé. Aucun endpoint public de replay ou d'acquittement n'est fourni.

## Configuration locale

La configuration s'effectue sur les processus Job et Matching avec les mêmes identifiants RabbitMQ. Les migrations Job et Matching doivent être appliquées avant activation. Les clés d'environnement sont :

```text
Job__EventTransport__Enabled=true
Job__EventTransport__RabbitMq__Uri=amqp://<utilisateur>:<mot-de-passe>@localhost:5672/%2F
Matching__EventTransport__Enabled=true
Matching__EventTransport__RabbitMq__Uri=amqp://<utilisateur>:<mot-de-passe>@localhost:5672/%2F
```

Le worker Matching exige une URI `amqp` ou `amqps` avec identifiants, sans query ni fragment ; l'hôte doit être loopback ou `rabbitmq`. Dans Compose, la connexion PostgreSQL Matching s'appelle `ConnectionStrings__MatchingDb`. Les fichiers Compose gardent le transport désactivé ; ils ne démarrent pas RabbitMQ dans la topologie principale. L'overlay RabbitMQ Collection est documenté dans [collection-events.md](collection-events.md). La configuration de Collection seule ne crée pas de consommateur Matching pour son événement.

## Inspection en lecture seule

Les requêtes suivantes permettent de diagnostiquer le retard sans modifier les files ni les bases :

```sql
-- Messages Collection non distribués
SELECT "Id", "EventType", "OccurredAtUtc", "AttemptCount", "NextAttemptAtUtc", "LastFailureCode"
FROM collection_outbox
WHERE "DispatchedAtUtc" IS NULL
ORDER BY "OccurredAtUtc", "Id"
LIMIT 100;

-- Messages Job non distribués
SELECT "Id", "EventType", "OccurredAtUtc", "AttemptCount", "NextAttemptAtUtc", "LastFailureCode"
FROM job_outbox
WHERE "DispatchedAtUtc" IS NULL
ORDER BY "OccurredAtUtc", "Id"
LIMIT 100;

-- Événements Job enregistrés par Matching
SELECT "EventId", "JobId", "OccurredAtUtc", "ProcessedAtUtc"
FROM matching_event_inbox
ORDER BY "ProcessedAtUtc" DESC
LIMIT 100;
```

Les tables sont propres à leurs bases et services. Les schémas complets et les commandes de migrations sont dans [database-schemas.md](database-schemas.md) et [migrations.md](migrations.md). Les migrations et les workers ne sont pas activés par ce guide.
