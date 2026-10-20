# Analyse de profil — 88.4

ProfileAnalysis propose jusqu'à cinq questions pour compléter les informations absentes. Une rubrique absente ne signifie pas une qualification manquante. L'agent ne rédige aucune expérience, compétence ou valeur de profil et ne modifie ni Profile ni les documents, même après approbation humaine.

## Source et confidentialité

Le connecteur lit uniquement `GET /api/v1/profile/matching-data` sur une origine locale, avec le bearer OpenIddict de l'utilisateur courant. Profile vérifie le propriétaire et l'API Agents vérifie que la réponse correspond au `resourceId` demandé. La réponse est bornée à 128 Kio, les collections à 100 éléments et les propriétés dupliquées sont refusées. Les valeurs professionnelles sont lues en mémoire puis remplacées par onze indicateurs de présence : JobTitle, Summary, Skills, Experiences, Educations, Languages, DesiredJobTitles, PreferredLocations, ContractTypes, WorkModes et AvailableFrom.

Le modèle reçoit ces booléens, l'identifiant technique et une empreinte, sans les textes du profil, coordonnées, noms d'employeurs ou CV. La source figée persistée contient les indicateurs. Ce traitement ne constitue pas une anonymisation complète : identifiant et indicateurs restent des métadonnées du profil. Une instruction cachée dans un résumé devient seulement `Summary: true` et n'est pas transmise au modèle.

Le contrôle de propriétaire repose sur le jeton validé par Profile. Le connecteur reste désactivé par défaut.

## Contrat et revue

Prompt `profile-analysis-fr-v1`, schéma `profile-analysis-v1` :

```json
{"schemaVersion":"profile-analysis-v1","questions":[{"field":"Skills","priority":"High"}]}
```

Priorités autorisées : High, Medium, Low. Le champ doit appartenir aux onze rubriques et être absent dans la source figée. Cinq questions au maximum, sans doublon ni propriété supplémentaire ; une liste vide est acceptée seulement lorsque toutes les rubriques sont renseignées. Les priorités restent une suggestion du modèle, sans effet sur le classement Matching.

Le catalogue authentifié `/api/v1/agents/catalog` expose `profileQuestions`, les formulations françaises fixes à afficher, et `profileAnalysisEnabled`. Le modèle choisit uniquement champ et priorité ; il ne produit pas de question libre. Les questions sur les expériences et études permettent une réponse « sans objet ».

Les règles habituelles d'idempotence, quota, audit, validation stricte et revue humaine s'appliquent. Une approbation valide la proposition exacte, sans remplir automatiquement les rubriques.

## Activation locale explicite

La fonctionnalité est désactivée par défaut. Dans le terminal de lancement, après choix d'un profil autorisé :

```powershell
$env:Agents__ProfileAnalysis__Enabled = 'true'
$env:Agents__ProfileAnalysis__BaseUrl = 'http://localhost:5001'
./tools/Restart-LocalAgents.ps1
```

Profile doit être disponible sur cette origine. Le jeton Agents et les paramètres Ollama sont chargés par les scripts locaux. Pour demander une proposition :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # conserver pour les reprises
$body = @{ requestId = $requestId; kind = 'ProfileAnalysis'; resourceId = $profileId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

Renseigner `$profileId` avec l'identifiant du profil associé à la session courante. Profile refuse tout profil qui n'appartient pas au porteur du jeton. Aucun changement de schéma PostgreSQL n'est nécessaire pour 88.4.
