# Phase 85.4 — contrat d'événement et outbox transactionnelle

Lot livré le 15 octobre 2026. Le fait d'acquisition d'une révision est conservé durablement dans la même transaction que les données métier. Le transport et la consommation restent à réaliser en phase 89. Le [contrat Collection](collection-events.md) définit précisément l'enveloppe, les garanties et les limites.

## Résultat et architecture

Application définit le contrat JSON versionné OfferRevisionRecordedV1 et sa conversion en message ; Domain porte CollectionOutboxMessage ; Infrastructure mappe l'outbox avec EF/PostgreSQL et l'alimente dans CollectedOfferRepository. Aucun package ni projet supplémentaire, aucune référence de production vers un autre microservice.

Une nouvelle révision crée un message `collection.offer-revision-recorded.v1`, initial ou variante. Les doublons n'en créent pas. Le message conserve un EventId, une date UTC à la précision PostgreSQL, les identifiants d'offre/révision, l'empreinte, la version de normalisation et la provenance. Le texte d'offre n'est pas dupliqué dans l'événement. La date représente l'acquisition, pas l'envoi.

L'événement atteste une acquisition préparatoire, sans approbation ou publication implicite. Il ne rend aucune offre recommandable. Les réactions à des offres Job canoniques doivent faire l'objet de contrats Job/Matching dédiés au moment de l'intégration asynchrone.

La création initiale sauvegarde offre, révision et message ensemble ; en cas de concurrence, les entités de la tentative perdante sont détachées avant réutilisation de la révision gagnante. Une variante est enregistrée avec son message dans la transaction existante. L'import par lot conserve son atomicité sur tous les messages et toutes les observations. La contrainte unique sur RevisionId assure un message par révision.

## Migration

Migration **20261015122119_AddCollectionOutbox** : table collection_outbox, index unique de révision et index filtré des messages non envoyés. Chaque révision déjà stockée reçoit un événement historique, sans modification de sa décision. L'outbox n'a pas de suppression en cascade depuis les données métier. DispatchedAtUtc reste null : aucun envoi ni acquittement n'est implémenté.

Cette migration a été appliquée uniquement aux conteneurs PostgreSQL de tests. Il faut l'appliquer sur la base Collection de développement avant le démarrage des nouveaux binaires ; aucune migration Job/Profile/Matching n'est ajoutée et aucune base utilisateur n'a été modifiée.

## Fichiers concernés

Créés :

- `src/services/Collection/JobApplicationAgent.Collection.Domain/Entities/CollectionOutboxMessage.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Events/OfferRevisionRecordedV1.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015122119_AddCollectionOutbox.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015122119_AddCollectionOutbox.Designer.cs`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/OutboxContractTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/CollectionOutboxTests.cs`
- `docs/collection-events.md`
- `docs/phase-85-4.md`

Modifiés : Infrastructure/Persistence/CollectionDbContext.cs, Persistence/Migrations/CollectionDbContextModelSnapshot.cs et Repositories/CollectedOfferRepository.cs ; fixture CollectionApiFactory et test SourcesControllerTests pour nettoyer/vérifier l'outbox ; `docs/collection.md` et `README.md`. Aucun changement de frontend, autre service de production, branche, commit ou push ; les modifications préexistantes sont préservées.

## Validation

Compilation finale des **25 projets : zéro erreur et zéro avertissement**.

**91 tests Collection réussis, zéro échec : 46 unitaires et 45 d'intégration**, soit sept nouveaux tests. Les suites autonomes Job/Profile/Matching et Angular ne sont pas relancées ; les parcours Collection vers Job restent couverts dans la suite d'intégration Collection. Le modèle EF n'a pas de changement non migré selon le test de migration.

Les nouveaux tests vérifient l'enveloppe JSON, sa version, la provenance, les références, la précision UTC et les enveloppes invalides. Sur PostgreSQL 17 : huit imports initiaux concurrents puis huit variantes concurrentes produisent exactement deux révisions et deux messages, avec EventId initial inchangé. Une erreur PostgreSQL sur le payload JSONB de l'outbox annule aussi l'offre et la révision. La panne de deuxième sauvegarde dans un lot annule également tous les messages. Le test de migration reprend une révision déjà Approved et vérifie le message historique, les dates UTC et la conservation de la décision.

Les conteneurs et credentials de test sont éphémères ; aucun secret ajouté. Les artefacts sont isolés dans TEMP pour préserver les binaires des API locales. Les commandes finales et compteurs TRX sont consignés ci-dessous ; les binaires de conception EF utilisent une configuration séparée.

```powershell
dotnet build src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj --configuration Phase85Outbox -m:1 --disable-build-servers
$env:ConnectionStrings__CollectionDatabase = 'Host=localhost;Database=collection_design'
dotnet ef migrations add AddCollectionOutbox --project src/services/Collection/JobApplicationAgent.Collection.Infrastructure --startup-project src/services/Collection/JobApplicationAgent.Collection.Api --configuration Phase85Outbox --no-build --output-dir Persistence/Migrations
dotnet test tests/Collection/JobApplicationAgent.Collection.IntegrationTests/JobApplicationAgent.Collection.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-4" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-4-results"
dotnet test tests/Collection/JobApplicationAgent.Collection.UnitTests/JobApplicationAgent.Collection.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-4" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-4-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-4" -m:1 --disable-build-servers
```

La connexion de design ne contient pas de credentials et ne sert pas à se connecter à une base. Pour appliquer la migration réelle, utiliser la connexion Collection de développement selon le guide ; aucune migration automatique au démarrage.

## Suite du plan

Le socle Collection est prêt : import manuel et JSON local, normalisation, révisions/revue, publication idempotente vers Job et événements en outbox. La phase 85 n'est pas déclarée entièrement terminée : la livraison effective des événements et leur consommation par Matching restent liées à la phase 89, avec les garanties listées dans le contrat.

Pour suivre l'ordre du parcours candidat, la prochaine tranche est **86.1 : socle du service documentaire et import sécurisé de CV PDF/DOCX**, avant extraction structurée et génération. L'intégration broker/dispatcher sera reprise en phase 89 ; ce lot ne prétend ni envoyer des messages ni assurer une livraison exactement une fois. Aucun envoi de candidature réelle n'a eu lieu.
