# Phase 85.2a — révisions conservées et revue explicite

Lot limité livré le 15 octobre 2026. La phase 85 reste en cours. Ce lot complète la préparation des imports ; la publication vers Job est le lot suivant, 85.2b. Voir le [guide Collection](collection.md) pour les routes, exemples et commandes de migration.

## Comportement et architecture

Chaque contenu normalisé distinct d'un import possède un instantané complet immutable, son empreinte, son titre original, la version de normalisation et une date serveur. L'import initial reste inchangé. Une nouvelle observation du même contenu ne crée pas de nouvelle révision et ne réinitialise pas sa décision. Les compteurs et les contrats d'import 85.1 sont conservés.

Le domaine OfferRevision porte les règles de revue. Application expose le port IOfferRevisionRepository et ReviewOfferHandler. Infrastructure stocke les instantanés en JSONB, lie les révisions à l'import par clé étrangère et implémente le repository EF. API expose lecture paginée et revue, avec 400/404/409 appropriés. Aucun appel entre microservices ni dépendance de projet vers Job n'est ajouté.

Un index unique sur import et empreinte empêche les variantes identiques. Le premier import et sa révision sont sauvegardés ensemble dans la transaction EF. Les réimports verrouillent la ligne parent avant l'ajout d'une variante et la mise à jour des compteurs, dans une transaction explicite. Une revue verrouille sa révision : une même décision répétée garde sa date, et une décision opposée après validation/rejet renvoie 409. L'expiration déclarée interdit l'approbation, mais pas le rejet.

Les décisions sont finales par instantané ; plusieurs contenus distincts peuvent être approuvés. La future publication devra sélectionner explicitement une révision, vérifier son approbation et contrôler à nouveau son expiration. Aucun contenu approuvé n'est actuellement publié ni désigné comme version courante.

## Migration et compatibilité

Migration `20261015114728_AddOfferRevisions` : table `offer_revisions`, FK avec suppression en cascade, index unique et index de pagination. Elle reconstruit une révision Pending du contenu initial de chaque import existant, avec dates ISO UTC. Les contenus modifiés qui n'étaient pas stockés en 85.1 restent irrécupérables sans réimport.

Appliquer `dotnet ef database update` selon le guide avant de démarrer les nouveaux binaires. La migration a uniquement été exécutée sur PostgreSQL 17 éphémère dans les tests. Aucune base persistante locale n'a été migrée. Les migrations des autres microservices ne changent pas.

Le statut PendingReview/Expired de l'import reste celui de l'instantané initial. Son marqueur historique HasPendingChanges reste un signal de variation observée ; il n'est pas un compteur de révisions Pending et n'est pas effacé par la revue. Les clients doivent consulter les décisions des révisions pour la revue.

## Fichiers concernés

Créés :

- `src/services/Collection/JobApplicationAgent.Collection.Domain/Entities/OfferRevision.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Abstractions/IOfferRevisionRepository.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Application/Imports/ReviewOfferHandler.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Repositories/OfferRevisionRepository.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015114728_AddOfferRevisions.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Infrastructure/Persistence/Migrations/20261015114728_AddOfferRevisions.Designer.cs`
- `src/services/Collection/JobApplicationAgent.Collection.Api/Controllers/RevisionsController.cs`
- `tests/Collection/JobApplicationAgent.Collection.UnitTests/OfferRevisionTests.cs`
- `tests/Collection/JobApplicationAgent.Collection.IntegrationTests/Api/RevisionsControllerTests.cs`
- `docs/phase-85-2a.md`

Modifiés : Collection.Application/DependencyInjection.cs ; Collection.Infrastructure/DependencyInjection.cs, Persistence/CollectionDbContext.cs, Persistence/Migrations/CollectionDbContextModelSnapshot.cs et Repositories/CollectedOfferRepository.cs ; Collection.Api/Program.cs ; `docs/collection.md` et `README.md`. Les changements déjà présents avant ce lot sont préservés. Aucun commit, push ou changement de branche.

## Vérifications

Compilation de la solution (25 projets) : zéro erreur, zéro avertissement. Collection.UnitTests : **32 réussis** ; Collection.IntegrationTests : **23 réussis** ; total : **55 réussis, zéro échec**, soit neuf nouveaux tests. Les suites des autres services et Angular ne sont pas relancées dans ce lot limité à Collection ; dernière validation globale 85.1 : 705 tests .NET réussis.

Les nouveaux tests vérifient les décisions finales et idempotentes, l'horodatage, les règles d'expiration, la validation avant accès au repository, la conservation du contenu, huit réimports modifiés simultanés, deux décisions opposées concurrentes, la pagination, les erreurs d'identité/appartenance et la migration d'une base 85.1 déjà alimentée. Le test de migration vérifie aussi l'absence de changements de modèle non migrés. Les 18 anciens tests d'intégration et les 28 anciens tests unitaires Collection restent verts.

Commandes exécutées depuis la racine :

```powershell
dotnet build src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj --configuration Phase85Collection -m:1 --disable-build-servers --no-restore
# Connexion de design sans credentials, sans connexion à PostgreSQL :
$env:ConnectionStrings__CollectionDatabase = 'Host=localhost;Database=collection_design'
dotnet ef migrations add AddOfferRevisions --project src/services/Collection/JobApplicationAgent.Collection.Infrastructure --startup-project src/services/Collection/JobApplicationAgent.Collection.Api --configuration Phase85Collection --no-build --output-dir Persistence/Migrations
dotnet test tests/Collection/JobApplicationAgent.Collection.UnitTests/JobApplicationAgent.Collection.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2" -m:1 --disable-build-servers
dotnet test tests/Collection/JobApplicationAgent.Collection.IntegrationTests/JobApplicationAgent.Collection.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase85-2" -m:1 --disable-build-servers --no-restore --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase85-2-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase85-2" -m:1 --disable-build-servers
```

Les builds de validation sont isolés dans TEMP pour préserver les binaires des API locales éventuellement actives. Les tests utilisent des credentials aléatoires générés à l'exécution et Docker ; aucun secret ajouté au dépôt.

## Limites et prochaine tâche

Pas d'authentification ni identité de validateur : usage local seulement jusqu'à la sécurisation. Pas d'UI de revue, de désignation de version courante, de modification d'une décision finale, de surveillance externe ni d'événement envoyé. La pagination est bornée et ordonnée, mais pas un instantané entre requêtes. L'historique conserve les contenus distincts, pas chaque observation répétée.

Prochain lot **85.2b** : contrat de publication explicite d'une révision approuvée, identité d'origine stable, réception idempotente côté Job et reprise après incident. Un simple retry du POST Job existant serait insuffisant. Les connecteurs autorisés 85.3 viennent ensuite. Aucune candidature réelle n'est soumise par ce travail.
