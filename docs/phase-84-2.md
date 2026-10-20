# Phase 84.2 — recommandations sur offres éligibles

Validée le 15 octobre 2026 après la phase 84.1. Évolution limitée au branchement HTTP de Matching et à ses tests. Aucun commit, push, changement de branche ou redémarrage d'instance locale effectué dans cette étape.

## Comportement et architecture

- `IJobMatchingDataClient.GetRecommendablePageAsync` exprime la récupération des offres éligibles ; `JobMatchingHttpClient` appelle `api/v1/jobs/recommendable?page=&pageSize=`.
- `RecommendationHandler` utilise uniquement cette méthode pour ses lots de 100. Le filtrage reste dans Job, avant pagination. Le classement global et la pagination finale existants sont conservés.
- Aucun repli vers le GET non filtré : si Job n'expose pas encore l'endpoint, le 404 amont devient une indisponibilité 503, jamais un catalogue sans filtre.
- `GetPageAsync` reste disponible sur `api/v1/jobs` avec ses validations et sa réponse existantes. Son envoi HTTP est mutualisé dans une méthode privée.
- `GetByIdAsync` et le code de `CalculateMatchingHandler` ne sont pas modifiés : un calcul individuel peut toujours cibler une offre ignorée ou expirée. L'éligibilité concerne les recommandations, pas l'existence de l'offre.
- Le contrat producteur reste un tableau de `JobDto`. Le consommateur ignore les propriétés supplémentaires, conserve les champs facultatifs nuls et vérifie les champs requis : identifiant non vide, titre, entreprise, modalité de travail et contrat non vides. Un élément nul, un tableau plus grand que la taille demandée, un objet/enveloppe inattendu ou un JSON invalide est rejeté ; aucun tableau partiellement valide n'est accepté.
- Le timeout HTTP de production (10 secondes), les tokens de cancellation et les configurations DI des clients sont conservés.

| Situation | Réponse de l'API recommandations |
|---|---|
| Catalogue éligible vide | 200, `items: []` |
| Profil absent | 404, comportement conservé |
| Erreur HTTP Job/Profile, erreur réseau | 503 |
| Timeout de dépendance, hors annulation du client appelant | 503 |
| JSON/contrat de dépendance invalide | 502, ProblemDetails sans contenu brut de l'offre |
| Annulation par l'appelant | Propagation ; ne pas la convertir en timeout de dépendance |

La nouvelle référence de projet Matching.Infrastructure est **uniquement dans le projet de tests d'intégration Job**. Elle permet de tester le contrat producteur/consommateur avec l'API Job réelle, son PostgreSQL éphémère, puis le client HTTP Matching et le moteur de scoring réel. Aucune couche du Job Service de production ne dépend de Matching, aucune base n'est partagée entre services. Profile est simulé dans ce test ; le stockage des résultats Matching n'y est pas utilisé. Les tests de l'API Matching emploient un transport HTTP contrôlé pour simuler erreurs et timeouts.

## Fichiers créés et modifiés

| Action | Fichier |
|---|---|
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Application/Abstractions/IJobMatchingDataClient.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Application/Matching/RecommendationHandler.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Infrastructure/HttpClients/JobMatchingHttpClient.cs` |
| Modifié | `src/services/Matching/JobApplicationAgent.Matching.Api/Controllers/MatchingController.cs` |
| Modifié | `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/CalculateMatchingHandlerTests.cs` |
| Modifié | `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Matching/RecommendationHandlerTests.cs` |
| Modifié | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Infrastructure/MatchingApiFactory.cs` |
| Modifié | `tests/Job/JobApplicationAgent.Job.IntegrationTests/JobApplicationAgent.Job.IntegrationTests.csproj` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.UnitTests/Infrastructure/HttpClients/RecommendableJobHttpClientTests.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Api/RecommendationDependencyContractTests.cs` |
| Créé | `tests/Job/JobApplicationAgent.Job.IntegrationTests/Api/MatchingJobContractTests.cs` |
| Créé | `docs/phase-84-2.md` |
| Modifié | `docs/matching.md`, `docs/phase-84-1.md`, `README.md` |

Les doubles de recommandations lèvent une exception si l'ancien `GetPageAsync` est utilisé. Le double du calcul individuel refuse le nouvel accès paginé, pour protéger la séparation des deux usages.

## Validation exécutée

Build des 19 projets : **0 erreur, 0 avertissement**. Tests : **600 réussis, 0 échec, 0 ignoré**. Rapports TRX contrôlés sur les six suites ; 42 cas ajoutés.

| Suite | Avant | Après |
|---|---:|---:|
| Job.UnitTests | 29 | 29 |
| Job.IntegrationTests | 27 | 29 |
| Matching.UnitTests | 105 | 134 |
| Matching.IntegrationTests | 37 | 48 |
| Profile.UnitTests | 289 | 289 |
| Profile.IntegrationTests | 71 | 71 |
| Total .NET | 558 | 600 |

Les nouveaux tests couvrent les routes et paramètres, bornes et erreurs de pagination, listes vides, champs optionnels, champs requis manquants, JSON/éléments invalides, cardinalité excessive, erreurs HTTP dont 404 amont, erreur réseau, timeout HTTP réel du client et annulation en vol. Les tests API vérifient les codes 502/503 et l'utilisation réelle du client HTTP. Les deux tests de contrat vérifient :

1. Sérialisation de l'API Job compatible avec le DTO Matching, Discovered/Saved inclus, Ignored/Expired exclus, catalogue général et accès par identifiant conservés.
2. Récupération de 102 offres éligibles sur plusieurs lots, exclusion de deux offres inéligibles, classement global déterministe et pagination finale, puis ensemble vide quand toutes les offres passent à Expired.

Commandes exécutées depuis la racine PowerShell :

```powershell
git status --short
dotnet test JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-2" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=phase84-2' --results-directory "$env:TEMP/job-agent-phase84-2-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-2" --no-restore -m:1 --disable-build-servers
```

Compilation et tests isolés des API locales pour ne pas verrouiller leurs binaires. Les conteneurs des tests sont éphémères. Aucun changement de schéma, aucune migration, aucun nouveau package NuGet et aucune nouvelle configuration de secret. Le frontend et ses contrats de réponse n'ont pas changé : ses tests n'ont pas été relancés dans cette phase (dernière validation 84.1 : 63 réussis).

## Limites et suite

Note de suivi : l'évolution synchrone du classement et les totaux sont désormais livrés dans le [rapport 84.3](phase-84-3.md). Les limites ci-dessous décrivent l'état à la livraison 84.2.

**84.3 est la prochaine étape** : enrichir le contrat avec les totaux, borner la mémoire, gérer les doublons et la cohérence des parcours, définir persistance/cache, invalidation et stratégie pour les gros volumes. Le parcours actuel n'est pas un instantané ; le catalogue et les scores sont encore recalculés et chargés en mémoire à chaque requête. Aucune implémentation des phases 85+ n'est ajoutée ici.

Recompiler/redémarrer Job avant Matching pour essayer manuellement les nouveaux binaires ; les instances déjà ouvertes ne sont pas mises à jour par ces builds isolés. L'écart de port Matching signalé dans l'audit et la dette de sécurité npm restent à traiter séparément.
