# Phase 84.4 — scoring configurable et versionné

Livraison du 15 octobre 2026. Le moteur reste déterministe. Les fonctionnalités existantes sont conservées ; le périmètre de cette étape est la configuration des trois critères actuels, leur applicabilité, la traçabilité des versions et les tests de non-régression. Aucun service de phase 85+ n'est créé ici.

## Décisions de scoring

`Matching:Scoring` configure `JobTitleWeight`, `SkillsWeight`, `LocationWeight`. Valeurs par défaut inchangées : 0,30 / 0,50 / 0,20. Chaque poids doit appartenir à [0,1], posséder au plus quatre décimales (cohérence avec le stockage EF existant) et la somme doit être exactement 1. Une configuration invalide empêche le démarrage, au lieu de produire des résultats incohérents.

| Situation | Statut du critère | Impact |
|---|---|---|
| Données disponibles | Evaluated = 0 | Score pondéré et poids évalué |
| Donnée nécessaire absente ou exigences non détectées | NotEvaluated = 1 | Aucun score ; poids inclus dans le dénominateur de couverture |
| Critère désactivé par poids nul | NotApplicable = 2 | Aucun appel au service de scoring ; exclu du dénominateur de couverture |

Le Domain permet aussi de représenter un critère explicitement non applicable avec un poids positif : ce poids est exclu de la couverture. Le moteur actuel ne conclut pas automatiquement à la non-applicabilité à partir de champs vides. Des préférences absentes ou l'absence de compétences détectées restent des données manquantes, jamais une compatibilité inventée.

`OverallScore` reste la moyenne pondérée des critères évalués. `EvaluationCoverage` devient la proportion du poids évalué parmi les critères applicables. `RankingScore = OverallScore × EvaluationCoverage / 100` est conservé. Si aucun critère applicable n'est évalué, le résultat reste InsufficientData, score nul et couverture 0 ; tout non applicable ne donne pas un score artificiel de 100.

## Version et compatibilité

- `ScoringRules.AlgorithmVersion` est une constante de code (`deterministic-v2`). Toute évolution des règles, normalisations ou dictionnaires de synonymes doit l'incrémenter.
- `RulesVersion` est le libellé de configuration, validé sur 1–48 caractères ASCII lettres/chiffres/point/tiret/underscore.
- L'identifiant émis est `AlgorithmVersion:SHA256`, où le hash contient la version de code, le libellé de configuration et les trois poids sous forme décimale canonique, indépendante de la culture. Changer les poids change l'identifiant même si le libellé configuré n'est pas changé. Les zéros décimaux de présentation ne changent pas la version.
- Le moteur calcule son identifiant une fois à sa construction. Les règles sont immuables pendant le processus ; une modification de configuration demande un redémarrage, pas un changement à mi-parcours de classement.
- `IMatchingScoringEngine.Version` possède une implémentation par défaut `legacy-v1` pour préserver les implémentations existantes ; un moteur personnalisé doit fournir sa propre version pour une traçabilité complète.
- Le Domain conserve la version dans chaque MatchingResult. La même version est exposée par le calcul HTTP, les détails et résumés de recherche persistés, chaque recommandation et l'enveloppe, y compris une enveloppe vide.
- Les constructeurs de DTO existants sont conservés ; `scoringVersion` est une propriété additive. Les valeurs enum 0 et 1 ne changent pas ; la nouvelle valeur 2 apparaît lorsque des critères sont désactivés. Le calcul expose les statuts sous forme de chaînes comme avant ; les recommandations conservent la représentation numérique existante.
- Les résultats historiques reçoivent `legacy-v1`. Cette étiquette désigne l'historique non versionné, pas la garantie de pouvoir reproduire sa configuration d'origine. Ils ne sont ni recalculés ni réinterprétés avec les nouveaux poids.

Les normalisations et synonymes déjà présents (accents/casse/espaces des titres, intitulés liés, Spark/PySpark, fournisseurs cloud et autres compétences connues) sont réutilisés avec leurs tests existants. Aucun modèle sémantique ou LLM ne remplace le moteur déterministe.

## Migration et installation locale

Nouvelle migration EF : `20261015110631_AddScoringVersion`. Elle ajoute uniquement `matching_results.ScoringVersion`, texte requis de longueur maximale 128, avec `legacy-v1` par défaut pour les lignes historiques et les anciens insertions sans colonne explicite. Le snapshot EF et le designer sont générés par l'outil EF. Les statuts de critères étaient déjà stockés en texte, sans modification de schéma nécessaire pour NotApplicable.

La migration a été testée sur PostgreSQL éphémère : insertion d'une ligne sous l'ancien schéma, upgrade puis vérification de sa version et de ses scores. Les bases locales existantes n'ont **pas** été modifiées.

Pour utiliser les nouveaux binaires localement, arrêter Matching avant de compiler, conserver la configuration/connexion de développement, puis depuis la racine PowerShell :

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update --project src/services/Matching/JobApplicationAgent.Matching.Infrastructure --startup-project src/services/Matching/JobApplicationAgent.Matching.Api
```

Cette commande est fournie pour l'application locale, pas exécutée sur la base de développement pendant cette étape. Redémarrer Matching et recharger le frontend après migration. Job/Profile n'ont pas de migration. Un retour de migration supprimerait la colonne de version ; ne pas l'utiliser comme simple moyen de restaurer les poids, qui restent une configuration.

## Fichiers de cette étape

| Action | Fichiers |
|---|---|
| Créé | `src/services/Matching/JobApplicationAgent.Matching.Application/Scoring/ScoringRules.cs` |
| Modifié | `...Matching.Application/Scoring/StructuredMatchingScoringEngine.cs`, `Abstractions/IMatchingScoringEngine.cs` |
| Modifié | `...Matching.Application/Matching/CalculateMatchingHandler.cs`, `RecommendationHandler.cs` |
| Modifié | `...Matching.Application/Models/RecommendationResponse.cs`, `JobRecommendation.cs`, `MatchingResultDetails.cs`, `MatchingResultPage.cs` |
| Modifié | `...Matching.Domain/Entities/MatchingCriterion.cs`, `MatchingResult.cs`, `Enums/MatchingCriterionStatus.cs` |
| Modifié | `...Matching.Infrastructure/Persistence/Entities/MatchingResultRecord.cs`, `MatchingDbContext.cs`, `Repositories/MatchingResultRepository.cs` |
| Créé / actualisé | `...Matching.Infrastructure/Persistence/Migrations/20261015110631_AddScoringVersion.cs`, son `.Designer.cs`, `MatchingDbContextModelSnapshot.cs` |
| Modifié | `...Matching.Api/Program.cs`, `appsettings.json`, `Controllers/MatchingController.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.UnitTests/Application/Scoring/ScoringRulesTests.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Api/ScoringRulesContractTests.cs` |
| Créé | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Infrastructure/ScoringVersionPersistenceTests.cs` |
| Modifié | `tests/Matching/JobApplicationAgent.Matching.IntegrationTests/Infrastructure/MatchingApiFactory.cs` |
| Modifié | `src/frontend/job-application-agent-ui/src/app/features/matching/matching-api.service.ts`, `recommendations.ts`, `recommendations.html`, `recommendations.spec.ts` |
| Créé / modifié | `docs/phase-84-4.md`, `docs/matching.md`, `docs/phase-84-3.md`, `README.md` |

Dans les chemins abrégés ci-dessus, `...Matching.COUCHE` correspond à `src/services/Matching/JobApplicationAgent.Matching.COUCHE`. Aucun nouveau package ou secret ajouté, aucun commit, push ou changement de branche.

## Validation finale

| Suite | Avant | Après | Échecs finaux |
|---|---:|---:|---:|
| Job.UnitTests | 29 | 29 | 0 |
| Job.IntegrationTests | 29 | 29 | 0 |
| Matching.UnitTests | 161 | 179 | 0 |
| Matching.IntegrationTests | 57 | 62 | 0 |
| Profile.UnitTests | 289 | 289 | 0 |
| Profile.IntegrationTests | 71 | 71 | 0 |
| Total .NET | 636 | 659 | 0 |
| Angular | 64 | 65 | 0 |

Les 23 nouveaux cas .NET couvrent configuration invalide, empreinte stable et changements de poids/version, exemples de scoring par défaut et personnalisés, distinction manquant/non applicable, service désactivé non appelé, couverture sans poids non applicable, absence de score inventé, validation de version, contrats calcul/recommandations, enveloppe vide versionnée, persistance/recherche et upgrade d'une ligne historique. Les suites existantes assurent la non-régression des synonymes, normalisations, scores et classements. Le nouveau test Angular vérifie les deux libellés et la version affichée.

Build .NET final : zéro erreur, zéro avertissement. Build Angular réussi avec les cinq avertissements CSS préexistants. EF `has-pending-model-changes` ne détecte aucun écart après génération et compilation de la migration.

Commandes principales exécutées :

```powershell
dotnet build src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj --configuration Phase84Scoring -m:1 --disable-build-servers
dotnet ef migrations add AddScoringVersion --project src/services/Matching/JobApplicationAgent.Matching.Infrastructure --startup-project src/services/Matching/JobApplicationAgent.Matching.Api --configuration Phase84Scoring --no-build --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/services/Matching/JobApplicationAgent.Matching.Infrastructure --startup-project src/services/Matching/JobApplicationAgent.Matching.Api --configuration Phase84Scoring --no-build
dotnet test JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-4" --no-restore -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase84-4-validated"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase84-4" --no-restore -m:1 --disable-build-servers
```

Une première compilation/test a restauré le répertoire temporaire ; la configuration isolée Phase84Scoring sert à la génération EF sans verrouiller les binaires Debug ouverts. Depuis le frontend : `npm.cmd test -- --watch=false`, `npm.cmd run build`.

## Limites et prochaine étape

Note de suivi : le socle d'import manuel de la phase 85 est livré dans le [rapport 85.1](phase-85-1.md). Les connecteurs et le transfert vers Job ne sont pas encore livrés.

La configuration exprime une politique de scoring, pas des préférences individuelles sauvegardées. Les règles additionnelles salaire, contrat, expérience, langues et diplômes ainsi que le matching sémantique restent des extensions optionnelles non implémentées. La non-applicabilité métier n'est pas inférée d'une absence de données. Le versionnage trace la configuration, mais ne conserve pas un snapshot complet des entrées permettant à lui seul de rejouer un ancien calcul.

Le cache et l'asynchrone conçus en 84.3 restent non implémentés ; leur clé doit utiliser cette version effective des règles. Les limites de cohérence du catalogue concurrent et les plafonds synchrones restent applicables. Les dettes de sécurité et les avertissements CSS de l'audit ne sont pas traités par cette évolution.

Prochaine mission recommandée : **phase 85**, commencer par le contrat de collecte/import manuel et la normalisation, puis des connecteurs autorisés indépendants. Ne pas soumettre de candidature ni appeler une plateforme externe sans cadrer le connecteur et les autorisations correspondantes.
