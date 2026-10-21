# Lettre de motivation avec IA — phase 88.7

CoverLetter produit jusqu'à cinq paragraphes de brouillon en français. Chaque paragraphe référence exactement un fait structuré du profil et une exigence citée de l'offre. L'API conserve la sortie en `PendingReview` ; il n'y a ni génération ni modification de document Documents, ni envoi de candidature. La qualité sémantique du texte reste à vérifier par la personne : les références rendent les sources consultables mais ne prouvent pas à elles seules chaque formulation.

La source est construite comme pour ResumeAdaptation : une JobAnalysis approuvée pour l'offre courante dont la description n'a pas changé, et le profil du propriétaire authentifié lu avec son bearer OpenIddict. Préférences et coordonnées ne sont pas incluses dans le prompt. L'offre et le profil sont lus uniquement depuis les API locales configurées ; les redirections, cookies et proxy sont désactivés.

L'accès source a sa propre configuration et reste désactivé par défaut :

```powershell
$env:Agents__CoverLetter__Enabled = 'true'
$env:Agents__CoverLetter__ProfileBaseUrl = 'http://localhost:5001'
$env:Agents__CoverLetter__JobBaseUrl = 'http://localhost:5002'
./tools/Restart-LocalAgents.ps1
```

Créer une JobAnalysis de l'offre et approuver d'abord son résultat. Puis demander un brouillon :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # garder le même identifiant pour une reprise
$body = @{ requestId = $requestId; kind = 'CoverLetter'; resourceId = $jobId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
$run | Select-Object id, state, failure, revision
```

La sortie contient `schemaVersion: cover-letter-v1` et un tableau `paragraphs`. Chaque élément comprend `requirementIndex`, `factIndex` et `text` ; ces index s'appliquent à la source figée de la proposition. La validation refuse les références hors limites, catégories incompatibles, paires répétées, balisage, caractères de contrôle, texte vide ou sortie hors borne. Elle ne peut pas prouver que la formulation libre ne comporte aucune affirmation implicite non étayée : relire chaque phrase et ses sources avant réutilisation.

Ollama et le fournisseur restent désactivés par défaut. Aucun coût ou token n'est consommé avant une requête réelle ; une génération locale reste imputée au quota persistant configuré.
