# Préparation de candidature — phase 88.8

ApplicationPreparation est un contrôle préliminaire des preuves : il retourne une entrée par exigence d'une JobAnalysis approuvée, avec zéro à trois index de faits du profil et un statut `RelevantEvidence`, `PotentialEvidence` ou `NoEvidence`. Aucun texte libre ni donnée personnelle de contact n'est généré. Le statut est une appréciation du modèle, pas une garantie de qualification ou de complétude ; chaque fait doit être relu.

La source est l'offre active et inchangée depuis l'analyse approuvée, ainsi que le profil courant vérifié par Profile à partir du bearer OpenIddict. L'évaluation peut être vide si le profil n'a encore aucun fait ou si l'analyse ne contient aucune exigence. Le connecteur dédié reste désactivé par défaut et ne contacte que les API Job et Profile locales.

Activer localement pour un profil autorisé :

```powershell
$env:Agents__ApplicationPreparation__Enabled = 'true'
$env:Agents__ApplicationPreparation__ProfileBaseUrl = 'http://localhost:5001'
$env:Agents__ApplicationPreparation__JobBaseUrl = 'http://localhost:5002'
./tools/Restart-LocalAgents.ps1
```

Après avoir analysé et approuvé l'offre avec JobAnalysis :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # garder le même identifiant pour une reprise
$body = @{ requestId = $requestId; kind = 'ApplicationPreparation'; resourceId = $jobId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

La proposition `application-preparation-v1` référence les tableaux `facts` et `requirements` de sa source figée. Les statuts et références sont validés par le serveur : chaque exigence apparaît une seule fois, les index doivent exister, les catégories doivent être compatibles et `NoEvidence` ne peut contenir aucun fait. Le modèle ne crée pas de candidature et n'interagit pas avec Documents ou Applications.

Pour poursuivre le dossier, sélectionner et approuver les documents via Documents, puis créer la candidature Draft et y joindre les exports approuvés via Applications. La revue de candidature exige un export Resume et un export CoverLetter ; cette étape reste explicite et humaine. Le [guide du service Applications](applications-review.md) décrit la revue et les transitions.
