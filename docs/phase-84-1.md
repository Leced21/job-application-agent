# Phase 84.1 — filtrage des offres recommandables

Étape validée le 15 octobre 2026, après l'[audit de phase 0](phase-0-audit.md). Périmètre limité au Job Service. Les phases suivantes ne sont pas implémentées dans cette modification.

## Décisions et contrat

- `Discovered` et `Saved` sont recommandables ; `Ignored`, `Expired` et tout statut stocké non reconnu sont exclus par une liste positive SQL.
- `Saved` est une préférence de conservation ; le modèle actuel ne contient pas de second indicateur d'expiration. Comme les statuts sont exclusifs, une offre passée à `Expired` est exclue même si elle a auparavant été sauvegardée. Une offre sauvegardée réellement expirée mais dont le statut n'est pas mis à jour reste une limite métier, à traiter avec la collecte/expiration.
- La politique des statuts appartient au handler Application. Le repository Infrastructure applique la requête EF Core, le filtrage puis le tri (`CreatedAtUtc DESC`, `Id ASC`), puis `Skip`/`Take`. Pas de filtrage en mémoire après pagination.
- Nouveau endpoint : `GET /api/v1/jobs/recommendable?page=1&pageSize=20`.
- Réponse 200 : tableau JSON de `JobDto`, avec les mêmes propriétés et enums sous forme de chaînes que le GET existant. Liste vide ou page au-delà de la fin : `[]`. Bornes : page 1–1 000 000, taille 1–100. Une pagination invalide renvoie 400 via la gestion d'erreurs existante.
- Les statuts ne sont pas un paramètre utilisateur de ce endpoint : le client ne peut pas élargir la politique. La méthode repository rejette les valeurs enum inconnues et une collection nulle ; une collection vide renvoie zéro résultat.
- `GET /api/v1/jobs` reste un tableau non filtré, avec son comportement actuel. Le GUID de la route de détail est contraint, donc `recommendable` ne peut pas être interprété comme un identifiant.
- Aucune modification de schéma, aucune migration, aucun nouveau package, aucune modification des bases locales existantes. L'index de statut est déjà présent.

Le contrat enrichi (`items`, `totalCount`, `page`, `pageSize`, `totalPages`) sera traité explicitement en 84.3 avec une stratégie de compatibilité. Aucun total fictif n'est ajouté au contrat existant.

## Fichiers de cette étape

| Action | Fichier |
|---|---|
| Modifié | `src/services/Job/JobApplicationAgent.Job.Application/Abstractions/IJobRepository.cs` |
| Modifié | `src/services/Job/JobApplicationAgent.Job.Infrastructure/Repositories/JobRepository.cs` |
| Créé | `src/services/Job/JobApplicationAgent.Job.Application/Jobs/Get/GetRecommendableJobsHandler.cs` |
| Modifié | `src/services/Job/JobApplicationAgent.Job.Application/DependencyInjection.cs` |
| Modifié | `src/services/Job/JobApplicationAgent.Job.Api/Controllers/JobsController.cs` |
| Créé | `tests/Job/JobApplicationAgent.Job.UnitTests/Application/Jobs/Get/GetRecommendableJobsHandlerTests.cs` |
| Créé | `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/RecommendableJobsTests.cs` |
| Créé | `docs/development-plan.md` (cahier des charges conservé) |
| Créé | `docs/phase-0-audit.md` |
| Créé | `docs/phase-0-inventory.md` |
| Créé | `docs/phase-84-1.md` |
| Modifié | `README.md` (liens de suivi) |

Les autres modifications déjà présentes dans l'arbre Git n'appartiennent pas à cette étape. Aucun commit, push ou changement de branche effectué.

## Vérifications

| Suite | Avant | Après | Échecs après |
|---|---:|---:|---:|
| Job.UnitTests | 20 | 29 | 0 |
| Job.IntegrationTests | 15 | 27 | 0 |
| Matching.UnitTests | 105 | 105 | 0 |
| Matching.IntegrationTests | 37 | 37 | 0 |
| Profile.UnitTests | 289 | 289 | 0 |
| Profile.IntegrationTests | 71 | 71 | 0 |
| Total .NET | 537 | 558 | 0 |
| Angular, inchangé | 63 | 63 | 0 |

Les 21 nouveaux cas couvrent : liste de statuts transmise, mapping DTO, token de cancellation, pagination invalide et bornes acceptées, filtre SQL avant pagination, pages distinctes, égalité de dates départagée par identifiant, offre Saved incluse, Ignored/Expired exclus, statut inconnu stocké exclu sans erreur de conversion, liste vide, statut repository inconnu rejeté, statuts dupliqués, collection nulle et cancellation. Les tests API utilisent la DI réelle, EF Core et PostgreSQL 17 dans un conteneur neuf.

Le build initial et le build final de solution réussissent sans erreur ni avertissement .NET. Le build Angular initial réussit avec cinq avertissements de budget CSS préexistants ; ses 63 tests passent. Aucun frontend modifié pendant cette étape.

Commandes principales (PowerShell, racine du dépôt) :

```powershell
dotnet test JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=phase84' --results-directory "$env:TEMP/job-agent-phase84-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84" --no-restore -m:1 --disable-build-servers
```

Contrôle EF exécuté pour Job, Profile et Matching (remplacer SERVICE) :

```powershell
dotnet ef migrations has-pending-model-changes --project src/services/SERVICE/JobApplicationAgent.SERVICE.Infrastructure --startup-project src/services/SERVICE/JobApplicationAgent.SERVICE.Api --no-build
```

Résultat des trois contrôles : aucun changement depuis la dernière migration sur les binaires initiaux. Le test PostgreSQL Job vérifie également le modèle reconstruit après l'ajout du filtre. Les commandes d'audit npm, NuGet et frontend figurent dans le rapport de phase 0. Les rapports TRX sont conservés dans le répertoire temporaire.

## Limites et prochaine tâche

Note de suivi : cette section décrit l'état à la livraison 84.1. Le branchement décrit ci-dessous est désormais validé dans le [rapport 84.2](phase-84-2.md).

Le Matching continue intentionnellement d'appeler le GET général : l'exclusion des offres ignorées/expirées n'est donc pas encore appliquée aux recommandations. **Prochaine tâche : 84.2**, ajouter une méthode HTTP dédiée aux offres recommandables, adapter uniquement RecommendationHandler et ses doubles de tests, vérifier les contrats JSON et les cas erreur/timeout/réponse invalide. Le calcul individuel doit conserver son client par identifiant.

Le classement global existant est en mémoire ; cache, totaux, déduplication et invalidation restent à définir en 84.3. Le parcours de pages n'est pas un instantané transactionnel du catalogue.

Les sept alertes npm, l'absence d'authentification, les identifiants locaux déjà stockés dans les configurations et l'écart de port Matching (5186 dans la configuration, 5003 à l'exécution manuelle) restent consignés dans l'audit. Ne pas exposer la plateforme avant traitement de la sécurité.

Le build est isolé pour préserver les services actuellement ouverts : redémarrer/recompiler Job pour essayer manuellement le nouvel endpoint sur 5002. L'exposition est déjà couverte par les tests HTTP de cette étape ; aucune donnée locale n'a été créée pour la démonstration.
