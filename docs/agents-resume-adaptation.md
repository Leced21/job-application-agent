# Adaptation du CV — 88.6

ResumeAdaptation propose des associations entre les exigences d'une offre et des faits structurés du profil. Il n'écrit ni ne remplace le texte du CV. La proposition contient seulement des index de faits et d'exigences existants, plus une priorité. L'interface peut afficher les extraits d'origine à côté des associations ; la personne décide si elle souhaite les reprendre et les reformuler.

L'agent exige une analyse JobAnalysis de la même offre déjà approuvée. Le connecteur vérifie que l'offre existe toujours et que sa description est identique à celle analysée ; une modification impose une nouvelle analyse et revue. Il lit ensuite Profile `matching-data` avec le bearer OpenIddict de l'utilisateur courant. Profile choisit le profil du propriétaire vérifié. Les compétences, expériences, formations, certifications et langues fournissent les faits. Le résumé et l'intitulé du profil peuvent aussi être présentés comme faits de type `Other`. Préférences, coordonnées et identifiants de profil sont écartés.

Les exigences transmises proviennent des citations exactes de l'analyse approuvée. Seuls les faits structurés, les exigences citées et leurs catégories sont envoyés au modèle Ollama local configuré. La proposition est validée contre les index, types de catégorie et priorités autorisés ; aucun texte libre, chiffre ou qualification n'est généré. Elle attend une revue humaine exacte, et l'approbation ne modifie aucun document.

Le connecteur reste désactivé par défaut. Le contrôle du propriétaire repose sur le bearer validé par Profile.

Après avoir fait tourner JobAnalysis pour l'offre et approuvé sa proposition, activer le connecteur dans le terminal local :

```powershell
$env:Agents__ResumeAdaptation__Enabled = 'true'
$env:Agents__ResumeAdaptation__ProfileBaseUrl = 'http://localhost:5001'
$env:Agents__ResumeAdaptation__JobBaseUrl = 'http://localhost:5002'
./tools/Restart-LocalAgents.ps1
```

Créer une proposition à partir de l'identifiant de l'offre :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # garder le même identifiant pour une reprise
$body = @{ requestId = $requestId; kind = 'ResumeAdaptation'; resourceId = $jobId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

L'accès au modèle consomme le quota Ollama local configuré. Aucun changement de schéma Agents n'est nécessaire.
