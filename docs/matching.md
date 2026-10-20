# Recommandations

L'écran `/recommendations` appelle `GET /api/v1/matching/recommendations?page=1&pageSize=10` via la passerelle locale (`http://localhost:8080`). La passerelle transmet les routes `/api/v1/matching/**` au service Matching (`http://localhost:5186`, profil de lancement HTTP).

Lancer les services Profile, Job, Matching et Gateway, puis `npm start` dans `src/frontend/job-application-agent-ui`. Les services doivent avoir leurs bases et migrations configurées. Docker doit être actif pour les tests PostgreSQL.

Pour démarrer les trois API et leurs bases PostgreSQL via Docker Compose, utilisez `docker compose up -d --build profile-db profile-api job-db job-api matching-db matching-api`. Matching est publié sur `127.0.0.1:5186`, appelle Job et Profile par leurs noms de service Docker, et garde son consommateur RabbitMQ désactivé. Appliquez les migrations Profile, Job et Matching depuis l’hôte avant les opérations métier.

Le classement utilise `RankingScore = OverallScore × EvaluationCoverage / 100`. Les offres sans score arrivent à la fin. En cas d'égalité, la couverture puis l'identifiant d'offre stabilisent l'ordre. Le service récupère les offres recommandables via `GET /api/v1/jobs/recommendable` par lots de 100 : seuls les statuts `Discovered` et `Saved` sont inclus. Il traite chaque lot immédiatement et conserve une file des meilleurs résultats nécessaires à la page demandée. Une page au-delà de la fin est vide.

La réponse contient `candidateProfileId`, `items`, `totalCount`, `page`, `pageSize`, `totalPages`. Les totaux portent sur les identifiants uniques observés pendant le calcul ; les doublons sont évalués une seule fois. Un catalogue vide donne `totalCount: 0`, `totalPages: 0`. Angular utilise les totaux pour sa navigation.

Le calcul individuel utilise toujours `GET /api/v1/jobs/{id}` ; les statuts exclus des recommandations restent accessibles par identifiant. Le GET général Job n'est pas modifié. Il n'existe aucun repli vers le catalogue non filtré si l'endpoint recommandable échoue.

Les poids du scoring sont configurés par `Matching:Scoring` : titre 0,30, compétences 0,50, localisation 0,20 par défaut. Ils doivent être compris entre 0 et 1, avec au plus quatre décimales et une somme de 1. Un poids nul désactive le critère (`NotApplicable`) ; une donnée manquante garde le statut `NotEvaluated`. La couverture exclut les critères non applicables, mais reste pénalisée par les données manquantes.

Le champ additif `scoringVersion` identifie l'algorithme et l'empreinte de sa configuration. Il figure dans le calcul, le détail/recherche persistés, chaque recommandation et son enveloppe. Les anciens résultats sont marqués `legacy-v1`, sans modification des scores. Voir le [rapport 84.4](phase-84-4.md) pour la migration `AddScoringVersion`, obligatoire avant redémarrage du nouveau Matching sur une base existante. Angular distingue « Non applicable » et « Non évalué » et affiche la version des règles.

Pour les recommandations, une dépendance HTTP indisponible, un statut d'erreur HTTP ou un timeout renvoie 503 ; un JSON invalide ou une offre sans les champs requis renvoie 502. Un profil absent renvoie 404, un catalogue éligible vide renvoie 200 avec `items: []`. Voir le [rapport 84.2](phase-84-2.md) pour les tests et commandes. Recompiler/redémarrer Job puis Matching pour utiliser ce branchement sur les instances locales déjà ouvertes.

Chaque requête recalcule le catalogue, sans réutiliser de scores mis en cache. Le parcours synchrone est borné par `Recommendations:Processing` : 10 000 offres uniques, 200 pages source et 30 secondes par défaut. Une limite dépassée renvoie 503 sans classement partiel. Le client Job limite aussi le corps HTTP à 8 Mio. La mémoire dépend des identifiants observés et des meilleurs résultats conservés, dans cette limite ; elle n'est pas constante en nombre d'offres.

Les pages du service Job ne constituent pas un instantané : une modification du catalogue pendant le parcours peut affecter la cohérence ; la première version observée d'un doublon est retenue. Voir le [rapport 84.3](phase-84-3.md) et la [stratégie de persistance/cache](recommendations-storage-strategy.md). Cette stratégie et le traitement asynchrone sont définis pour la suite, pas implémentés. Aucun résultat partiel n'est présenté comme un classement global complet.

Validation :

```powershell
dotnet test tests/Matching/JobApplicationAgent.Matching.UnitTests/JobApplicationAgent.Matching.UnitTests.csproj
dotnet test tests/Matching/JobApplicationAgent.Matching.IntegrationTests/JobApplicationAgent.Matching.IntegrationTests.csproj
cd src/frontend/job-application-agent-ui
npm run build
npm test -- --watch=false
```
