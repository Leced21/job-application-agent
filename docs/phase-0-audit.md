# Audit initial — 15 octobre 2026

Périmètre : état du dépôt avant la phase 84.1. Référence fonctionnelle : [cahier des charges](development-plan.md). Aucun commit, push ou changement de branche effectué.

## État Git et architecture

Branche active : `feature/maching_service`, différente de `dev` demandée dans le plan. Arbre déjà largement modifié : solution, Compose, Matching, passerelle, tests et frontend ; nombreux fichiers Matching non suivis. Les modifications existantes sont conservées. Le `.gitignore` exclut `obj/`, mais des fichiers générés déjà suivis restent modifiés : nettoyage Git à prévoir séparément. Ne pas basculer de branche ou restaurer ces fichiers sans préserver le travail.

La solution contient 19 projets : passerelle, trois services de quatre couches chacun, six projets de tests. Angular est hors de la solution .NET. Aucun accès direct entre bases de services constaté dans les clients Matching. Voir l'[inventaire détaillé](phase-0-inventory.md) pour les chemins de projets, contrôleurs, handlers, repositories, migrations et tests (actualisé avec l'ajout 84.1).

| Composant | Inventaire et état vérifié | Limites |
|---|---|---|
| Profile | Domain/Application/Infrastructure/Api ; 32 handlers, 1 repository EF, 1 contrôleur ; profil, expériences, compétences, langues, diplômes, certifications, liens, préférences et DTO de matching | Profil unique, absence d'isolation par utilisateur |
| Job | Quatre couches ; 6 handlers, 1 repository EF, 1 contrôleur ; CRUD, statuts, pagination | Filtrage recommandable absent à l'audit |
| Matching | Quatre couches ; 4 handlers, 1 repository EF, 1 contrôleur ; calcul, persistance, lecture/recherche et recommandations | Tous les statuts actuellement récupérés ; catalogue et scores chargés en mémoire ; aucune invalidation/cache |
| Gateway | YARP, routes Profile/Job/Matching | Adresse Matching configurée sur 5186, alors que l'instance lancée manuellement utilise 5003 : aligner les paramètres de lancement avant validation du parcours via Gateway |
| Angular | Profil et offres existants ; écran recommandations ajouté précédemment, standalone et typé | Parcours documents/candidatures/authentification absents ; 5 avertissements de budget CSS |
| Docker | Compose pour 3 PostgreSQL 17, volumes et health checks ; un Dockerfile Profile | Compose ne démarre pas les API, Gateway ou Angular ; RabbitMQ absent |
| Services futurs | Collecte, documents, candidatures, orchestration IA absents | Phases 85–89 à construire, sans recréer les services existants |
| Industrialisation | Tests unitaires et intégration avec PostgreSQL éphémère présents | CI/CD, tests de parcours complets, authentification et observabilité distribuée à compléter |

## Contrats HTTP inspectés

- Profile : `GET /api/v1/profile/matching-data` renvoie un objet ou 404. Les champs, nullabilités et sous-collections du DTO producteur correspondent à `ProfileMatchingData` côté Matching, y compris `DateOnly` et les préférences.
- Job : `GET /api/v1/jobs?page=&pageSize=` renvoie un tableau `JobDto`, pas une enveloppe. Les champs consommés par `JobMatchingData` sont présents ; les enums Job sont sérialisés en chaînes. Les champs supplémentaires sont ignorés à la désérialisation. `GET /api/v1/jobs/{id}` est conservé pour le calcul individuel.
- Matching : le client Job valide les bornes de pagination et le HTTP, rejette un JSON nul/invalide ; le client Profile traite le 404. Recommandations et calcul traitent les indisponibilités et timeouts avec 503 dans le code source actuel.
- `RecommendationResponse` expose `candidateProfileId`, `items`, `page`, `pageSize`, mais pas encore `totalCount`/`totalPages`. Le classement global est déjà corrigé : `RankingScore`, couverture, identifiant, puis pagination finale. Ce point du cahier des charges est donc partiellement déjà réalisé.
- Les tests HTTP utilisent des réponses simulées ; ils ne constituent pas une preuve complète de compatibilité entre deux services réellement démarrés. Ajouter des tests de contrat producteur/consommateur en 84.2.

## Migrations

- Profile : 8 migrations, d'`InitialProfileSchema` à `AddPreferences`.
- Job : 2 migrations, `InitialJobs` puis `InitialJobSchema`. La seconde renomme les colonnes : conserver la chaîne historique malgré son nom « Initial ». Deux répertoires/snapshots existent ; ne pas supprimer de migration sur ce seul constat.
- Matching : 1 migration `InitialMatchingSchema`.
- Les tests PostgreSQL appliquent les migrations sur des bases neuves. Les tests Job et Profile vérifient aussi `HasPendingModelChanges`. Le contrôle EF CLI `migrations has-pending-model-changes --no-build` pour les trois services ne détecte aucun changement de modèle manquant sur les binaires initiaux. Les bases locales existantes n'ont pas été migrées ou réinitialisées par l'audit. Aucune modification de schéma n'est nécessaire pour le filtrage.

## Validation initiale

Build global : **0 erreur, 0 avertissement**. Tests .NET : **537 réussis, 0 échec, 0 ignoré**, répartis ci-dessous. Le point de référence de 515 est dépassé ; ne pas le réutiliser comme total actuel.

| Suite | Réussis |
|---|---:|
| Job.UnitTests | 20 |
| Job.IntegrationTests | 15 |
| Matching.UnitTests | 105 |
| Matching.IntegrationTests | 37 |
| Profile.UnitTests | 289 |
| Profile.IntegrationTests | 71 |
| Angular | 63 |

Build Angular réussi avec 5 avertissements CSS préexistants (skills, profile, jobs, job-details, experiences). Les tests s'exécutent hors du bac à sable qui empêche la communication du runner .NET et certaines écritures temporaires Vitest. Les API ouvertes sont préservées via `--artifacts-path` temporaire.

## Dépendances et sécurité

- Audit NuGet transitif : aucune vulnérabilité connue signalée par les sources configurées au moment du contrôle.
- Audit npm : **7 packages signalés : 5 high, 2 critical**. Packages : `@angular/build`, `@angular/cli`, `@angular/router`, `@modelcontextprotocol/sdk`, `http-cache-semantics`, `piscina`, `source-map-js`. Des correctifs sont annoncés disponibles ; aucun `audit fix` automatique appliqué. Prioriser une mise à jour contrôlée avec build/tests avant exposition externe.
- Identifiants de développement en clair déjà présents dans Compose, configurations et factory EF Profile. Ne pas les recopier dans ce rapport ni ajouter de secrets. Migration vers variables d'environnement/User Secrets à prévoir ; absence d'authentification = usage local uniquement à ce stade.

## Backlog ajusté et décision

1. **84.1 maintenant** : ajouter `GetPageByStatusesAsync`, `GetRecommendableJobsHandler`, DI et endpoint dédié ; inclure Discovered/Saved, exclure Ignored/Expired et valeurs non reconnues ; filtrer en SQL avant pagination ; tester bornes, ordre, ensembles vides et compatibilité du GET existant.
2. **84.2 ensuite** : méthode client Matching dédiée au nouvel endpoint, tests de contrats, erreurs, timeouts et JSON invalides ; conserver CalculateMatchingHandler.
3. **84.3** : contrat enrichi avec totaux, parcours par lots et mémoire bornée, doublons et cohérence concurrente ; proposer cache/persistance versionnée par profil/offre, invalidation sur modification et traitement asynchrone avant les volumes élevés. Le classement global existant n'est pas à recréer.
4. **84.4** : scoring versionné, pondérations configurables, critères absents/non applicables ; conserver la formule actuelle.
5. Phases 85–89, puis 90, puis 91–95 selon l'ordre demandé ; 96–97 après validation. Les correctifs de sécurité npm restent une dette prioritaire, pas une raison d'ajouter les fonctionnalités des phases suivantes à cette modification.

Commandes de référence exécutées depuis la racine (PowerShell) :

```powershell
git status --short
git branch --show-current
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase0" -m:1 --disable-build-servers
dotnet test JobApplicationAgent.slnx --no-build --no-restore --artifacts-path "$env:TEMP/job-agent-phase0" -m:1 --logger 'trx;LogFilePrefix=baseline' --results-directory "$env:TEMP/job-agent-phase0-results"
dotnet list JobApplicationAgent.slnx package --vulnerable --include-transitive
```

Depuis `src/frontend/job-application-agent-ui` : `npm.cmd audit --json`, `npm.cmd run build`, `npm.cmd test -- --watch=false`. Les résultats TRX et le rapport brut npm sont dans le répertoire temporaire ; aucun secret ou résultat généré n'est ajouté au dépôt.
