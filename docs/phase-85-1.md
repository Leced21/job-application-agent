# Phase 85.1 — socle Collection et import manuel

Évolution limitée livrée le 15 octobre 2026. Voir le [guide Collection](collection.md) pour les contrats, décisions et commandes de démarrage. La phase 85 reste en cours ; les connecteurs, révisions approuvées et transferts vers Job ne sont pas encore implémentés.

## Architecture et périmètre

Six projets sont ajoutés : Collection.Domain, Application, Infrastructure, Api et deux projets de tests. La solution passe de 19 à 25 projets. Domain ne dépend de rien ; Application dépend du Domain ; Infrastructure implémente le repository Application avec EF Core/PostgreSQL ; Api compose les services et expose les imports. Aucun projet de production Collection ne référence les autres microservices.

Collection est une zone de préparation : les offres importées ne deviennent pas automatiquement des offres canoniques Job. Les endpoints existants ne changent pas. Une route YARP additionnelle relaie Collection vers localhost:5004. Le fichier Compose additionnel est séparé pour préserver les commandes existantes et n'ajouter aucun identifiant ou mot de passe en dur. Le service n'effectue aucun appel HTTP sortant.

L'import structuré valide, normalise et conserve provenance et dates. Un index unique protège l'identité ; les compteurs de réimport sont mis à jour atomiquement et un conflit d'insertion concurrente est traité comme un doublon, pas une seconde offre. Le contenu initial est préservé et une différence est signalée pour revue. L'expiration utilise seulement la date fournie. La normalisation est identifiée par `manual-v1` et les futures évolutions devront prévoir la compatibilité des clés existantes.

## Fichiers créés et modifiés

Créés : arborescences `src/services/Collection/` et `tests/Collection/` (inventaire détaillé ci-dessous), `docker-compose.collection.yml`, `docs/collection.md`, `docs/phase-85-1.md`.

Modifiés : `JobApplicationAgent.slnx`, `src/gateway/JobApplicationAgent.Gateway/appsettings.json`, `README.md` et note de suivi dans `docs/phase-84-4.md`. Les autres modifications déjà présentes dans Git sont conservées. Aucun commit, push ou changement de branche.

Nouvelle migration : `20261015112946_InitialCollectionSchema`, table `collected_offers`, tableaux de compétences, index unique de déduplication et index de pagination/expiration. Les champs de salaire sont en numeric(12,2) et les dates en UTC. La migration a été appliquée aux conteneurs éphémères de tests uniquement. Aucune base de développement n'a été créée ou modifiée et aucune migration Job/Profile/Matching n'est ajoutée dans cette tranche.

## Validation

| Suite | Réussis | Échecs |
|---|---:|---:|
| Collection.UnitTests | 28 | 0 |
| Collection.IntegrationTests | 18 | 0 |
| Job.UnitTests | 29 | 0 |
| Job.IntegrationTests | 29 | 0 |
| Matching.UnitTests | 179 | 0 |
| Matching.IntegrationTests | 62 | 0 |
| Profile.UnitTests | 289 | 0 |
| Profile.IntegrationTests | 71 | 0 |
| Total .NET | 705 | 0 |

46 nouveaux tests. Les huit suites ont été relancées après alignement d'EF Core Relational sur 10.0.12. Build final des 25 projets : zéro erreur, zéro avertissement. L'avertissement NU1900 de la première restauration restreinte a été éliminé par rafraîchissement des métadonnées ; l'audit NuGet du nouveau service ne signale aucune vulnérabilité connue avec les sources actuelles. Le conflit d'assembly Relational initial a été corrigé par une référence explicite de même version que le dépôt, sans changement des projets existants.

Les tests couvrent normalisation, alias, absence d'inférence de compétences, URL canonique et protocoles refusés, empreintes stables, échelle décimale, validation, horloge injectée, stockage réel, provenance, réimport modifié sans écrasement, compteurs monotones, expiration, pagination, entrées invalides et identifiants absents. Le test de huit imports HTTP simultanés confirme une seule création et huit observations. Les credentials PostgreSQL des tests sont générés à l'exécution.

L'import d'une URL example.invalid réussit sans résolution/téléchargement : seul le texte fourni est collecté. Les tests API exécutent la vraie DI, le repository EF et PostgreSQL 17. Le test `HasPendingModelChanges` et le contrôle EF CLI ne détectent pas de migration manquante. Le Compose a été vérifié avec `config --quiet`, sans lancer la base persistante locale.

Aucun frontend modifié : ses tests ne sont pas relancés dans cette tranche (dernière validation : 65 réussis en 84.4). Aucun connecteur de service externe, envoi de message ou candidature réelle effectué.

Commandes principales exécutées depuis la racine :

```powershell
dotnet build src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj --configuration Phase85Collection -m:1 --disable-build-servers
dotnet ef migrations add InitialCollectionSchema --project src/services/Collection/JobApplicationAgent.Collection.Infrastructure --startup-project src/services/Collection/JobApplicationAgent.Collection.Api --configuration Phase85Collection --no-build --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/services/Collection/JobApplicationAgent.Collection.Infrastructure --startup-project src/services/Collection/JobApplicationAgent.Collection.Api --configuration Phase85Collection --no-build
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-1" -m:1 --disable-build-servers
dotnet test JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-1" --no-build --no-restore -m:1 --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-1-validated"
dotnet list src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj package --vulnerable --include-transitive --no-restore
docker compose -f docker-compose.collection.yml config --quiet
```

Pour générer/contrôler le modèle, la factory EF utilise une connexion de design sans credentials, sans connexion à la base. Pour les tests et le Compose de validation, des variables d'environnement temporaires sont utilisées ; aucune valeur sensible n'est enregistrée. Le guide de lancement indique les variables à fournir pour une vraie base locale.

## Limites et prochaine tâche

Les imports sont préparatoires. Une URL seule n'est pas extraite, la disponibilité en ligne n'est pas vérifiée, les doublons entre différentes URL ne sont pas fusionnés et les rapprochements flous ne sont pas tentés. L'expiration est celle de la date déclarée. Les réimports modifiés ne stockent pas encore la seconde version complète ; le marqueur persistant `HasPendingChanges` indique une revue nécessaire mais ne remplace pas un historique de révisions. Les totaux de liste ne sont pas un instantané sous imports concurrents.

La prochaine tranche est **85.2 : révisions validées et publication idempotente vers Job**, avant les connecteurs externes et avant de passer à 86. Les contrats d'événements/outbox seront à préparer avec la phase asynchrone. Le service doit rester local jusqu'à l'authentification/isolation prévues ; aucune candidature ne sera soumise dans ces étapes de collecte.

## Inventaire des nouveaux fichiers de code

- `src/services/Collection/JobApplicationAgent.Collection.Api/appsettings.json`
- `src/services/Collection/JobApplicationAgent.Collection.Api/Controllers/ImportsController.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj`
- `src/services/Collection/JobApplicationAgent.Collection.Api/Program.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Api/Properties/launchSettings.json`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Abstractions/ICollectedOfferRepository.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/DependencyInjection.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/GetImportedOffersHandler.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/ImportedOfferDto.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/ImportOfferCommand.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/ImportOfferCommandValidator.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/ImportOfferHandler.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/OfferNormalizer.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/JobApplicationAgent.Collection.Application.csproj`
- `src/services/Collection/JobApplicationAgent.Collection.Domain/Entities/CollectedOffer.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Domain/JobApplicationAgent.Collection.Domain.csproj`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/DependencyInjection.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/JobApplicationAgent.Collection.Infrastructure.csproj`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/CollectionDbContext.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/CollectionDbContextFactory.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015112946_InitialCollectionSchema.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015112946_InitialCollectionSchema.Designer.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/CollectionDbContextModelSnapshot.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Repositories/CollectedOfferRepository.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/ImportsControllerTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Infrastructure/CollectionApiFactory.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/JobApplicationAgent.Collection.IntegrationTests.csproj`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/JobApplicationAgent.Collection.UnitTests.csproj`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/ManualImportTests.cs`
