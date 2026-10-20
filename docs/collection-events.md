# Événements Collection — contrat v1 et outbox

Le type `collection.offer-revision-recorded.v1` décrit l'enregistrement d'une révision dans la zone de préparation Collection. **Ce fait n'est ni une approbation ni une publication Job.** Il ne doit pas rendre une offre recommandable ni lancer un calcul de matching sur une offre préparatoire.

## Enveloppe JSON

Le contrat est défini par `Collection.Application/Events/OfferRevisionRecordedV1.cs`. Les noms JSON sont en camelCase. Exemple synthétique :

```json
{
  "eventId": "7e31d05b-1772-41ca-b503-49761bb80269",
  "eventType": "collection.offer-revision-recorded.v1",
  "schemaVersion": 1,
  "occurredAtUtc": "2026-10-15T12:00:00Z",
  "producer": "Collection",
  "offerId": "cb92d317-75cf-44eb-8441-ab49af1bcd15",
  "revisionId": "513cad34-d6df-4ec8-8b19-6ef74bca33f3",
  "fingerprint": "0000000000000000000000000000000000000000000000000000000000000000",
  "normalizationVersion": "manual-v1",
  "sourceName": "LocalJsonExport"
}
```

Tous les champs sont requis. EventId, OfferId et RevisionId sont des UUID non vides. SchemaVersion vaut 1 ; EventType et Producer ont les valeurs ci-dessus. OccurredAtUtc est la date de collecte de la révision, en UTC, limitée à la précision microseconde PostgreSQL ; ce n'est pas la date d'envoi. Fingerprint est le SHA256 hexadécimal minuscule du contenu normalisé. NormalizationVersion et SourceName proviennent de la révision, sans nouvelle inférence de plateforme.

L'enveloppe contient des références et métadonnées, pas le texte d'offre, les compétences complètes ou des données candidat. Elle ne contient pas de JobId : seul Job possède les offres canoniques. Le contrat ne décrit pas une transition de revue et ne garantit pas que la révision soit toujours Pending au moment de sa consommation.

Les consommateurs devront dédupliquer sur EventId, supporter les livraisons répétées et consulter l'état pertinent plutôt qu'inférer un ordre métier depuis les dates ou UUID. Un changement incompatible doit créer une nouvelle version du contrat. Aucun assembly de production d'un autre service ne référence Collection.

## Écriture transactionnelle

La table `collection_outbox` contient Id (= EventId), RevisionId, EventType, SchemaVersion, OccurredAtUtc, PayloadJson et DispatchedAtUtc. Un index unique sur RevisionId garantit un événement d'acquisition par révision. L'index des messages non envoyés porte sur OccurredAtUtc et Id lorsque DispatchedAtUtc est null.

L'import initial enregistre offre, révision et message dans la même sauvegarde EF transactionnelle. Une nouvelle variante est ajoutée avec son message dans la transaction de réimport. Un réimport de contenu déjà vu augmente ses observations, sans nouvel événement ni nouvel EventId. Une collecte par lot conserve sa transaction globale : le rollback annule également les messages du lot.

La reprise après une réponse perdue réutilise l'identité et la révision persistées ; elle ne crée pas un second message pour cette révision. L'UUID du message ne change pas à la relecture. Cette garantie porte sur l'enregistrement en base, pas sur une livraison réseau exactement une fois.

La migration `20261015122119_AddCollectionOutbox` prépare aussi un message par révision existante, avec la date historique de collecte. Elle ne modifie pas les décisions déjà Approved/Rejected et ne publie rien dans Job. Aucun événement d'approbation ou de publication n'est inventé lors de cette reprise.

L'outbox n'a pas de FK avec suppression en cascade vers les révisions : un nettoyage de données métier ne doit pas supprimer silencieusement des messages non envoyés. Une politique de rétention coordonnée sera nécessaire avec le dispatcher. La migration inverse supprime l'outbox ; ne pas l'utiliser comme opération de nettoyage de messages.

## Transport ajouté en phase 89.1

Le relay Collection utilise RabbitMQ, un exchange topic durable `jobapplication.events`, et la file `collection.offer-revision-recorded.v1`. Il publie avec un message persistant et publisher confirms ; `DispatchedAtUtc` n'est renseigné qu'après confirmation, dans une transaction PostgreSQL qui verrouille la ligne avec `FOR UPDATE SKIP LOCKED`. Plusieurs instances peuvent donc partager le relay sans traiter simultanément la même ligne. Aucun endpoint public de lecture ou d'acquittement de l'outbox n'est ajouté.

Une panne de publication conserve la ligne avec AttemptCount, LastFailureCode et NextAttemptAtUtc ; le délai augmente exponentiellement et plafonne à cinq minutes. Le worker relit les échéances au redémarrage. Un crash après confirmation RabbitMQ mais avant commit PostgreSQL peut republier le même EventId ; c'est une livraison au moins une fois, et chaque consommateur devra persister sa déduplication par EventId. La dead-letter queue est déclarée pour les consommateurs qui rejetteront durablement un message ; le dispatcher ne déplace pas un événement hors de l'outbox pour une panne réseau.

Le worker est désactivé par défaut. Après application manuelle de la migration Collection, démarrer RabbitMQ en local avec des identifiants gardés dans l'environnement :

```powershell
$env:RABBITMQ_DEFAULT_USER = '<utilisateur local>'
$env:RABBITMQ_DEFAULT_PASS = '<mot de passe local>'
docker compose -f docker-compose.collection.yml up -d rabbitmq
$env:Collection__EventTransport__RabbitMq__Uri = 'amqp://<utilisateur>:<mot-de-passe>@127.0.0.1:5672/%2F'
$env:Collection__EventTransport__Enabled = 'true'
```

Si le mot de passe contient des caractères réservés dans une URI, les encoder en percent-encoding. L'API doit aussi recevoir `ConnectionStrings__CollectionDatabase`. Garder `Enabled=false` jusqu'à ce que RabbitMQ et la migration soient prêts ; sans cela, les messages restent durablement en attente dans PostgreSQL. Le tableau de bord local RabbitMQ est publié sur `http://127.0.0.1:15672`.

Les événements Job liés aux offres canoniques et la réaction de Matching doivent être définis séparément : l'événement d'acquisition Collection ne remplace pas ce contrat.

Le registre des deux événements, les garanties du consommateur Matching et les requêtes d'inspection sont regroupés dans le [guide des événements asynchrones](events.md).

Pour une inspection locale en lecture seule, dans l'outil SQL de la base Collection :

```sql
SELECT "Id", "RevisionId", "EventType", "OccurredAtUtc", "DispatchedAtUtc",
       "AttemptCount", "NextAttemptAtUtc", "LastFailureCode"
FROM collection_outbox
WHERE "DispatchedAtUtc" IS NULL
ORDER BY "OccurredAtUtc", "Id"
LIMIT 100;
```

La migration a été testée sur des bases PostgreSQL 17 éphémères, pas appliquée à la base persistante utilisateur. Pour lancer les nouveaux binaires, appliquer les migrations Collection avec la connexion locale déjà configurée, selon le [guide Collection](collection.md).
