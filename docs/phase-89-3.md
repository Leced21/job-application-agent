# Phase 89.3 — consommateur Matching idempotent

Matching consomme uniquement `job.published.v1` sur la file durable `matching.job-published.v1`. Il valide le type AMQP, l’identifiant du message et l’enveloppe versionnée, puis récupère l’offre canonique auprès du service Job et évalue le profil local. Les événements d’acquisition Collection ne sont pas abonnés.

L’inbox `matching_event_inbox` utilise `EventId` comme clé primaire. L’insertion de l’inbox et celle du résultat Matching sont faites dans une seule transaction PostgreSQL. Une redélivrance déjà enregistrée est acquittée sans créer de second résultat. La comparaison de l’empreinte du payload, du type, du Job et de l’heure détecte aussi la réutilisation d’un identifiant avec un contenu différent. Si aucun profil n’est disponible, l’événement est enregistré comme traité sans résultat pour éviter une redélivrance sans fin.

RabbitMQ utilise un acquittement manuel après le commit. Un message mal formé ou incompatible est rejeté vers la dead-letter queue `matching.job-published.v1.dead`. Une erreur de traitement est retentée après un délai de cinq secondes. Le worker est désactivé par défaut. Pour l’activer localement, appliquer d’abord les migrations Job `20261016122512_AddJobOutbox` et Matching `20261016123259_AddJobPublishedEventInbox`, démarrer RabbitMQ, puis configurer les deux services avec les mêmes identifiants locaux :

```text
Job__EventTransport__Enabled=true
Job__EventTransport__RabbitMq__Uri=amqp://<utilisateur>:<mot-de-passe>@localhost:5672/%2f
Matching__EventTransport__Enabled=true
Matching__EventTransport__RabbitMq__Uri=amqp://<utilisateur>:<mot-de-passe>@localhost:5672/%2f
```

Le test d’intégration ajouté vérifie avec PostgreSQL l’écriture atomique du résultat et de l’inbox, l’ignorance d’une redélivrance et le rejet d’une collision d’identifiant. La compilation complète de `JobApplicationAgent.slnx` réussit sans avertissement ni erreur. Les tests ne sont pas exécutés, la migration n’est pas appliquée à une base persistante et aucun broker n’est démarré dans cette étape.

Suite recommandée : activer ensemble les deux relays localement après migration, puis ajouter un test de bout en bout avec RabbitMQ qui vérifie la publication Job, l’acquittement Matching et la dead-letter queue.
