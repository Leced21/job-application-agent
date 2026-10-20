# Phase 85.2b — publication explicite et idempotente vers Job

Lot livré le 15 octobre 2026. La publication d'une révision approuvée fonctionne entre Collection et Job ; la phase 85 reste en cours pour les connecteurs autorisés. Le [guide Collection](collection.md#publication-vers-job--852b) donne les contrats, commandes de migration et exemples.

## Résultat et décisions

Le nouveau POST Collection `/api/v1/collection/imports/{offerId}/revisions/{revisionId}/publish` exige une révision existante et Approved. Il consulte d'abord le reçu Job, puis publie si aucun reçu n'existe et si l'expiration déclarée n'est pas atteinte. L'import et la revue seuls ne créent aucune offre Job.

Job reçoit la publication par un endpoint distinct du CRUD existant. Une identité d'origine Collection crée une seule offre Discovered. Une reprise exacte retrouve son reçu ; une autre révision ou un corps différent pour cette identité donne 409. Les changements ultérieurs du candidat sur l'offre Job sont préservés, aucune synchronisation destructive n'est introduite.

L'offre et le reçu sont enregistrés dans la même transaction PostgreSQL. Un verrou transactionnel consultatif, dérivé de l'identité d'origine, sérialise les tentatives même avant l'existence d'une ligne de reçu ; la clé primaire sur OfferId garantit l'unicité. Une collision de hash de verrou sérialise seulement deux identités sans les fusionner. Le corps validé, son SHA256, l'identité de révision et son empreinte sont conservés dans le reçu.

Le reçu Job est la référence unique : aucun indicateur local Collection à mettre à jour après l'appel HTTP. Une réponse perdue après commit se récupère avec la même demande. Un reçu existant peut être récupéré après expiration de la révision. Il subsiste après suppression de l'offre Job, sans FK en cascade : une reprise ne recrée pas une offre supprimée. Le reçu identifie alors une publication passée dont le GET Job peut répondre 404.

La première vérification a révélé une différence submicroseconde entre le reçu en mémoire et sa relecture PostgreSQL. La date est maintenant fixée à la précision microseconde dès sa création ; le test vérifie l'égalité exacte de la première réponse et de la reprise.

## Architecture et compatibilité

Domain Job porte CollectionPublication. Application Job définit le contrat de réception, les validations et le port repository. Infrastructure Job réalise la transaction ; API expose GET/POST des reçus. Collection ajoute PublishOfferHandler et le port IJobPublicationClient ; son Infrastructure réalise le client HTTP et mappe les champs normalisés vers le contrat Job. Aucun projet de production ne référence un autre microservice.

Les champs compatibles deviennent ceux de Job, dont SourceName vers Source. Compétences et expiration restent conservées dans le reçu JSONB : elles ne sont ni perdues ni présentées comme des propriétés actuellement exploitées par Job/Matching. Il n'y a pas de modification du modèle de scoring ni de désactivation automatique à expiration après publication.

Les endpoints existants restent compatibles. La nouvelle dépendance HTTP utilise une base configurable Services:Job:BaseUrl, par défaut localhost:5002, sans suivre les URL d'offres. Redirections désactivées, dix secondes par appel, 64 Kio maximum pour la réponse, aucun retry automatique. Un appel Collection peut effectuer successivement GET puis POST. Les pannes réseau/timeout donnent 503 ; les réponses/contrats invalides, 502 ; les conflits métier, 409. Les réponses d'erreur amont ne sont pas exposées brutes.

## Migration

Migration Job **20261015115904_AddCollectionPublications** : ajout de la table collection_publications, sans modification ni suppression des offres existantes. Le snapshot existant reste dans Infrastructure/Migrations et la nouvelle migration suit les fichiers déjà présents dans Infrastructure/Persistence/Migrations. Les tests de modèle ne détectent aucune migration manquante.

Appliquer la migration Job avant de lancer les nouveaux binaires. Collection doit déjà être migré en 85.2a ; aucun nouveau schéma Collection n'est nécessaire. La migration a uniquement été appliquée aux conteneurs de tests ; aucune base persistante utilisateur n'a été migrée. Ne pas supprimer les reçus ou rétrograder cette migration pendant que des reprises peuvent encore arriver.

## Fichiers concernés

Créés :

- `src/services/Job/JobApplicationAgent.Job.Domain/Entites/CollectionPublication.cs`
- `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Create/PublishCollectionHandler.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Repositories/CollectionPublicationRepository.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Persistence/Migrations/20261015115904_AddCollectionPublications.cs`
- `src/services/Job/JobApplicationAgent.Job.Infrastructure/Persistence/Migrations/20261015115904_AddCollectionPublications.Designer.cs`
- `src/services/Job/JobApplicationAgent.Job.Api/Controllers/CollectionPublicationsController.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/PublishOfferHandler.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/JobPublicationClient.cs`
- `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Create/PublishCollectionHandlerTests.cs`
- `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/CollectionPublicationsTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/PublishOfferTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/PublicationFlowTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/JobPublicationClientTests.cs`
- `docs/phase-85-2b.md`

Modifiés côté Job : Application/DependencyInjection.cs ; Infrastructure/DependencyInjection.cs, Persistence/JobDbContext.cs et Migrations/JobDbContextModelSnapshot.cs ; Api/ExceptionHandling/GlobalExceptionHandler.cs ; fixture JobApiFactory (nettoyage des reçus de tests et credentials éphémères aléatoires).

Modifiés côté Collection : Application/DependencyInjection.cs et Abstractions/IOfferRevisionRepository.cs ; Infrastructure/DependencyInjection.cs et Repositories/OfferRevisionRepository.cs ; Api/Controllers/RevisionsController.cs, Program.cs et appsettings.json ; projet IntegrationTests (référence Job.Api avec alias, uniquement pour héberger les deux API dans les tests). `docs/collection.md` et `README.md` sont actualisés. Aucun changement de frontend, commit, push ou branche ; les modifications préexistantes sont préservées.

## Validation

Compilation finale des **25 projets : zéro erreur et zéro avertissement**.

| Suite exécutée | Réussis | Échecs |
|---|---:|---:|
| Collection.UnitTests | 36 | 0 |
| Collection.IntegrationTests | 32 | 0 |
| Job.UnitTests | 32 | 0 |
| Job.IntegrationTests | 35 | 0 |
| Total de ce lot | **135** | **0** |

22 nouveaux tests depuis 85.2a. Les suites Matching, Profile et Angular n'ont pas été relancées ; la compilation couvre toute la solution. Les tests Job et Collection précédents restent verts.

Les scénarios vérifient l'approbation obligatoire avant accès réseau, l'expiration, le retour d'un reçu passé, les payloads invalides, les contrats HTTP défaillants, le timeout et l'annulation appelante. La réception Job est testée avec huit tentatives concurrentes, conflits de révision/corps, métadonnées conservées, égalité des reçus et suppression sans résurrection.

Deux tests de parcours démarrent les deux vraies API et deux bases PostgreSQL 17 indépendantes, reliées par transport HTTP TestServer et le vrai client de production. Ils couvrent import, revue, publication concurrente, refus d'une autre révision approuvée et reprise après suppression/expiration. L'un simule une perte de réponse **après** le commit Job : la première réponse Collection est 503, les reprises retrouvent toutes le même Job. Les credentials des conteneurs sont générés à l'exécution. Aucun serveur externe ou base utilisateur n'est contacté.

Commandes principales :

```powershell
dotnet build src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj --configuration Phase85Publication -m:1 --disable-build-servers
# Connexion de design sans credentials ; aucune connexion à la base :
$env:ConnectionStrings__JobDatabase = 'Host=localhost;Database=job_design'
dotnet ef migrations add AddCollectionPublications --project src/services/Job/JobApplicationAgent.Job.Infrastructure --startup-project src/services/Job/JobApplicationAgent.Job.Api --configuration Phase85Publication --no-build --output-dir Persistence/Migrations

dotnet test tests/Collection/JobApplicationAgent.Collection.UnitTests/JobApplicationAgent.Collection.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2b" -m:1 --disable-build-servers --no-restore
dotnet test tests/Job/JobApplicationAgent.Job.UnitTests/JobApplicationAgent.Job.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2b" -m:1 --disable-build-servers --no-restore
dotnet test tests/Job/JobApplicationAgent.Job.IntegrationTests/JobApplicationAgent.Job.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2b" -m:1 --disable-build-servers --no-restore --logger 'trx;LogFilePrefix=final' --results-directory "$env:TEMP/job-agent-phase85-2b-final"
dotnet test tests/Collection/JobApplicationAgent.Collection.IntegrationTests/JobApplicationAgent.Collection.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2b" -m:1 --disable-build-servers --no-restore --logger 'trx;LogFilePrefix=final' --results-directory "$env:TEMP/job-agent-phase85-2b-final"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-2b" -m:1 --disable-build-servers --no-restore
```

Une restauration initiale a été faite dans le nouveau répertoire d'artefacts. Les binaires des API locales ne sont pas remplacés par les builds de validation. Les deux TRX d'intégration finaux sont dans TEMP/job-agent-phase85-2b-final.

## Limites et suite

Le système reste local sans authentification : Collection contrôle la revue, mais le endpoint interne Job fait confiance à l'appelant local. L'identité de validateur et l'autorisation interservices relèvent de la phase de sécurisation. Une reprise exige la même identité et le même corps ; ni modification d'une offre déjà publiée, ni synchronisation de révisions, ni désactivation à expiration ne sont introduites. Aucun événement/outbox, retry asynchrone ou UI n'est ajouté.

Prochaine tranche **85.3** : connecteurs de sources autorisées avec contrats, limites, provenance et tests. Le choix d'une source et de ses accès doit précéder toute récupération réelle. La collecte manuelle, la revue et la publication de ce lot fonctionnent sans service externe. Aucune candidature réelle n'a été soumise.
