# Suggestions de relance — phase 88.9

FollowUpSuggestion prépare un brouillon de message ou recommande d'attendre/ne pas relancer. Il lit une candidature locale par l'API Applications en lecture seule. Seuls le statut actif, le titre du poste, l'entreprise, la date de soumission, la date du dernier retour recruteur et l'échéance/canal d'une relance planifiée sont transmis au modèle. Les notes, messages recruteur, coordonnées, pièces jointes et identifiants personnels sont écartés.

Le service n'accepte que les candidatures `Submitted`, `InReview` ou `Interview`, avec une soumission manuelle enregistrée. Si une échéance planifiée est future, la validation serveur impose `Wait` et un message vide. La proposition ne planifie ni n'envoie une relance et ne modifie jamais Applications. Le brouillon doit être vérifié et envoyé manuellement ; toute planification ou déclaration d'envoi se fait ensuite par les routes Applications existantes.

Le connecteur est désactivé par défaut. Pour l'activer, configurer l'origine loopback d'Applications hors Git. Le bearer OpenIddict de l'utilisateur courant est transmis à cette API, qui filtre le dossier selon son propriétaire :

```powershell
$env:Agents__FollowUpSuggestion__Enabled = 'true'
$env:Agents__FollowUpSuggestion__ApplicationsBaseUrl = 'http://localhost:5007'
./tools/Restart-LocalAgents.ps1
```

Créer un run pour l'UUID d'une candidature :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # conserver pour la reprise
$body = @{ requestId = $requestId; kind = 'FollowUpSuggestion'; resourceId = $candidacyId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

La sortie `follow-up-suggestion-v1` contient `recommendation` (`DraftMessage`, `Wait` ou `NoFollowUp`) et `draftMessage`. Le contrat refuse toute action, balisage ou texte hors limite ; un message n'est permis que pour `DraftMessage`. Les références sources et le statut exact sont conservés dans la proposition Agents pour revue.
