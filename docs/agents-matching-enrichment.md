# Enrichissement Matching — 88.5

MatchingEnrichment classe les critères d'un résultat Matching pour guider une vérification humaine. Il ne recalcule pas le score et n'ajoute aucun fait au profil ou à l'offre. Son contrat `matching-enrichment-fr-v1` expose au modèle le nom du critère et une catégorie de signal calculée par le serveur ; il choisit une priorité High, Medium ou Low.

Pour chaque critère `JobTitle`, `Skills` et `Location`, le serveur transforme le statut et le score en un signal : `InformationMissing` pour une évaluation absente, `NoMatchReported` pour un score de 0, `PartialMatchReported` pour un score de 1 à 99 et `MatchReported` pour un score de 100. Les critères désactivés ou de poids nul sont omis. Un score bas n'est jamais présenté comme la preuve d'une compétence absente. Les libellés visibles viennent du catalogue fixe exposé par l'API Agents (`matchingMessages`).

Le connecteur local lit `GET /api/v1/matching/results/{id}` sur `http://localhost:5003` avec le bearer OpenIddict de l'utilisateur courant. Matching vérifie que le résultat appartient au profil courant. L'activation exige uniquement `Agents:MatchingEnrichment:Enabled=true`. La réponse est bornée à 32 Kio. Les explications, identifiants d'offre, textes et valeurs de profil sont écartés avant la construction de la source figée. Celle-ci conserve le statut, le score et le poids de chaque critère pour l'audit. Le prompt n'envoie que la version du calcul, le score global et la couverture, puis le nom et le focus des critères éligibles.

Pour activer explicitement le connecteur après démarrage des API locales :

```powershell
$env:Agents__MatchingEnrichment__Enabled = 'true'
$env:Agents__MatchingEnrichment__BaseUrl = 'http://localhost:5003'
./tools/Restart-LocalAgents.ps1
```

Créer une proposition pour un résultat Matching déjà calculé :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid()
$body = @{ requestId = $requestId; kind = 'MatchingEnrichment'; resourceId = $matchingResultId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

La proposition doit être revue avec sa révision et son empreinte exactes. Même approuvée, elle ne change ni le résultat Matching ni le profil. Aucun changement de schéma PostgreSQL n'est requis. Le connecteur reste désactivé par défaut.
