# Phase 84.3 — pagination globale et traitement synchrone borné

Livraison du 15 octobre 2026. Le parcours synchrone est implémenté et testé ; la persistance, le cache et le workflow asynchrone sont **définis mais non implémentés** dans la [décision d'architecture](recommendations-storage-strategy.md). Aucune phase 85+ n'est ajoutée.

## Contrat et comportement livrés

- L'enveloppe Matching conserve `candidateProfileId`, `items`, `page`, `pageSize` et ajoute `totalCount`, `totalPages`. Les quatre paramètres existants du constructeur C# restent compatibles.
- Les totaux comptent les offres éligibles **uniques observées pendant le parcours**. `totalPages = ceil(totalCount / pageSize)` ; zéro offre donne zéro page. Une page au-delà de la fin renvoie une liste vide avec les totaux réels observés, pas un total nul artificiel.
- Job et ses endpoints ne changent pas. La récupération par lots de 100 est indépendante de la taille de la page de recommandations.
- Les doublons sont éliminés avant scoring et comptage. La première version observée d'un identifiant est retenue. Un lot plein contenant des doublons ne termine pas prématurément le parcours.
- L'ordre reste déterministe : score de classement décroissant, scores absents à la fin, couverture décroissante, identifiant croissant.
- La liste de toutes les offres puis celle de tous les scores sont remplacées par un traitement lot par lot et une file des K meilleurs résultats nécessaires à la page. La mémoire contient les identifiants observés, un lot et la file bornée ; elle n'est pas constante. Les pages profondes peuvent conserver davantage de résultats, jusqu'à la limite configurée.
- Les limites synchrones par défaut sont 10 000 identifiants uniques, 200 pages source, 30 secondes pour toute la requête. Elles se configurent dans `Recommendations:Processing` et sont validées au démarrage. Le client HTTP Job borne aussi sa réponse à 8 Mio.
- Un dépassement de volume ou de pages source donne 503, sans liste ni total partiels. Le délai global et les timeouts HTTP donnent 503 ; une cancellation par l'appelant se propage. Les erreurs JSON restent 502. Les champs et règles du calcul individuel ne changent pas.
- Les réponses de recommandations portent `Cache-Control: no-store`. Aucun cache applicatif n'est activé tant que les versions de profil/offres ne sont pas disponibles.
- Angular affiche les totaux et désactive « Suivant » à la dernière page, même lorsqu'elle est pleine. Les états loading, empty et error sont conservés.

## Fichiers de cette évolution

| Action | Fichier |
|---|---|
| Créé | `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/RecommendationProcessingLimits.cs` |
| Créé | `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/RecommendationCapacityExceededException.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/RecommendationHandler.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Application/Models/RecommendationResponse.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Api/Program.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Api/Controllers/MatchingController.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Api/appsettings.json` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/DependencyInjection.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/RecommendationPaginationTests.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Api/RecommendationPaginationContractTests.cs` |
| Modifié | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Infrastructure/MatchingApiFactory.cs` |
| Modifié | `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/MatchingJobContractTests.cs` |
| Modifié | `src/frontend/job-application-agent-ui/src/app/features/matching/matching-api.service.ts` |
| Modifié | `src/frontend/job-application-agent-ui/src/app/features/matching/recommendations.ts` |
| Modifié | `src/frontend/job-application-agent-ui/src/app/features/matching/recommendations.html` |
| Modifié | `src/frontend/job-application-agent-ui/src/app/features/matching/recommendations.spec.ts` |
| Créé | `docs/phase-84-3.md`, `docs/recommendations-storage-strategy.md` |
| Modifié | `docs/matching.md`, `docs/phase-84-2.md`, `README.md` |

Aucun nouveau package, secret, changement de schéma ou migration. Les API déjà ouvertes, leurs bases et la branche Git sont préservées ; aucun commit ni push effectué.

## Validation finale

| Suite | Avant | Après | Échecs finaux |
|---|---:|---:|---:|
| Job.UnitTests | 29 | 29 | 0 |
| Job.IntegrationTests | 29 | 29 | 0 |
| Matching.UnitTests | 134 | 161 | 0 |
| Matching.IntegrationTests | 48 | 57 | 0 |
| Profile.UnitTests | 289 | 289 | 0 |
| Profile.IntegrationTests | 71 | 71 | 0 |
| Total .NET | 600 | 636 | 0 |
| Angular | 63 | 64 | 0 |

Les six rapports TRX finaux confirment 636 tests réussis, zéro échec. Les 36 nouveaux tests .NET couvrent : totaux vides/exacts/partiels, page hors limite du catalogue, page très profonde, sélection comparée à un tri complet sur 257 offres et plusieurs pages, scores absents et zéro, égalités, déduplication dans et entre lots, évaluation d'un lot avant lecture du suivant, version initiale d'un doublon retenue, budget exact et dépassement, producteur répétant des lots, configuration invalide, cancellation, délai global, absence de cache et enveloppe HTTP compatible. Le contrat PostgreSQL Job/Matching vérifie aussi les totaux et le passage à zéro après expiration de toutes les offres.

Les tests HTTP lisent leurs propres DTO de réponse ou JSON, pas les entités Domain. La comparaison avec un tri complet vérifie le résultat de la file de priorité ; elle n'est pas un benchmark de consommation mémoire. Les budgets sont vérifiés fonctionnellement, sans mesure de heap ou test de charge concurrente.

Build final .NET : zéro erreur, zéro avertissement. Build Angular réussi ; cinq avertissements de budget CSS préexistants restent présents, aucun lié à cette évolution. Les 64 tests Angular passent, dont le dernier bouton « Suivant » sur une page pleine et l'adaptation aux totaux après changement du catalogue.

Commandes de validation finale (racine, PowerShell) :

```powershell
dotnet test JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-3" --no-restore -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase84-3-validated"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-3" --no-restore -m:1 --disable-build-servers
```

La première exécution avait restauré la même solution sans `--no-restore`. Depuis `src/frontend/job-application-agent-ui` :

```powershell
npm.cmd test -- --watch=false
npm.cmd run build
```

Le build isolé n'actualise pas les processus locaux déjà ouverts : recompiler/redémarrer Matching et recharger le frontend pour utiliser les nouveaux totaux. Job n'a pas de changement dans cette étape. Aucune application de migration sur les bases locales n'est requise.

## Limites et suite

Note de suivi : la base configurable et versionnée du scoring est désormais livrée dans le [rapport 84.4](phase-84-4.md). Les recommandations de suite ci-dessous décrivent l'état à la livraison 84.3.

La cohérence d'un instantané n'est pas garantie sous mutations concurrentes du catalogue : les totaux sont ceux des offres observées, certaines offres déplacées entre pages peuvent manquer et une offre peut changer après sa lecture. La déduplication traite les répétitions, pas ce problème de transaction distribuée. Les pages successives sont recalculées et peuvent donc changer entre deux requêtes.

La [stratégie de stockage](recommendations-storage-strategy.md) décrit les versions de profil/catalogue/scoring, des snapshots publiés atomiquement, le workflow asynchrone et l'invalidation. Ces mécanismes ne sont pas présents aujourd'hui ; au-delà des budgets, le service refuse le calcul au lieu de créer une tâche fictive. Les bornes sont par requête : la concurrence globale doit encore être dimensionnée.

Prochaine évolution fonctionnelle : **84.4**, scoring versionné et pondérations configurables, en conservant OverallScore, EvaluationCoverage, RankingScore et leurs explications. La stratégie de snapshots devra être implémentée avant de lever les plafonds pour de grands volumes. Les dettes de sécurité npm et de configuration déjà recensées restent suivies dans l'audit initial.
