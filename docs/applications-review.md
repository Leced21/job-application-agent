# Pièces vérifiées et revue humaine — 87.2b

Une candidature peut maintenant recevoir un CV et une lettre exportés par Documents, passer en ReadyForReview puis en Approved après confirmation humaine de sa révision exacte. Les pièces sont revérifiées à chaque transition. **Approved n'envoie rien** et ne crée aucun statut Submitted.

## Configuration et migration

Les deux services doivent représenter le même propriétaire stable et le même profil local. La liaison propriétaire/profil reste déclarée dans la configuration serveur, comme en 87.2a. Documents enregistre en plus une confirmation humaine par export : cela ne signifie pas que les faits de ce document ont été transférés dans Profile.

| Service | Variable | Rôle |
| --- | --- | --- |
| Documents | `Documents__Applications__OwnerId` | Même UUID que Documents LocalAuth et Applications LocalAuth |
| Documents | `Documents__Applications__ProfileId` | Même UUID que `Applications__Sources__ProfileId` |
| Documents | `Documents__Applications__Token` | Jeton du canal de lecture, distinct des jetons propriétaires |
| Applications | `Applications__Documents__Token` | Même secret que le canal Documents ci-dessus |
| Applications | `Applications__Documents__BaseUrl` | Origine loopback Documents, défaut `http://localhost:5005` |

Les jetons doivent avoir 32–512 caractères sans espaces. Les trois identifiants d'authentification sont distincts : jeton propriétaire Applications, jeton propriétaire Documents et jeton du canal. Le canal ne donne aucun droit de modification dans Documents. Il n'utilise ni jeton du navigateur ni cookie ; les redirections sont désactivées. L'URL de Documents doit être une origine HTTP(S) locale sans chemin, userinfo, query ou fragment. Réponse limitée à 16 Kio, délai de 10 secondes par vérification d'export.

Si les trois paramètres du canal Documents sont absents, le canal reste désactivé et les fonctions Documents existantes continuent à fonctionner. Une configuration partielle ou invalide empêche le démarrage. Si Applications n'a pas de canal utilisable, les opérations qui vérifient les pièces échouent sans modifier le dossier ; les fonctions initiales et le retour en Draft restent disponibles.

Exemple de configuration initiale dans une console où les UUID existants sont déjà définis, à conserver ensuite hors Git et à rendre disponible dans chaque console de service :

```powershell
$env:Documents__Applications__OwnerId = $env:Documents__LocalAuth__OwnerId
$env:Documents__Applications__ProfileId = $env:Applications__Sources__ProfileId
$env:Documents__Applications__Token = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Applications__Documents__Token = $env:Documents__Applications__Token
$env:Applications__Documents__BaseUrl = 'http://localhost:5005'
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

La migration **20261016015951_AddDocumentApplicationBindings** ajoute la table de reçus et la clé propriétaire/export utilisée par sa FK restrictive. La connexion `ConnectionStrings__DocumentsDatabase` habituelle est requise. Redémarrer Documents (5005) et Applications (5007) avec ces variables. Aucune nouvelle migration Applications n'est nécessaire ; son schéma initial 87.2a reste requis. Le développement a migré uniquement des bases éphémères de test.

## Confirmer le lien dans Documents

Préparer, composer, approuver et exporter le CV et la lettre comme dans les [guides Documents](documents-export.md). Relire les fichiers exacts. Pour chaque export, le propriétaire Documents confirme le profil configuré et l'empreinte du fichier :

```powershell
$documentHeaders = @{ Authorization = "Bearer $env:Documents__LocalAuth__Token" }
$exportId = '<UUID export>'
$export = Invoke-RestMethod "http://localhost:5005/api/v1/documents/exports/$exportId" -Headers $documentHeaders
$binding = @{ profileId = $env:Documents__Applications__ProfileId; fileSha256 = $export.fileSha256; confirmed = $true } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5005/api/v1/documents/exports/$exportId/application-binding" -Method Post -Headers $documentHeaders -ContentType 'application/json' -Body $binding
```

POST et GET `/api/v1/documents/exports/{id}/application-binding` utilisent le jeton propriétaire Documents. POST est borné à 4 Kio et partage la limite de 30 écritures/minute des compositions. Une confirmation absente ou un autre profil est refusé (422) ; une empreinte différente produit 409. Reprendre la même confirmation retourne le reçu initial, avec sa date initiale, y compris sous concurrence.

Le reçu est immuable et unique par export : propriétaire, profil, offre, type Resume/CoverLetter, draft, empreinte du fichier, de la composition et du snapshot source, date de confirmation UTC. Il ne contient pas le CV, les faits ni les coordonnées. Les exports existants ne sont pas rattachés rétroactivement. Un export déjà lié à un profil ne peut pas être réaffecté à un autre ; une nouvelle sélection exige un nouveau dossier documentaire et ses exports.

GET `/api/v1/documents/application-bindings/{id}` est réservé au jeton du canal. Il revérifie l'approbation, le hash de la composition, les octets du fichier, le snapshot et les faits figés avant de retourner le reçu. Un jeton propriétaire ne donne pas accès à cette route ; le jeton du canal ne permet pas de confirmer un lien ni d'appeler les autres routes propriétaires.

## Rattacher et faire relire la candidature

Toutes les routes suivantes utilisent le jeton propriétaire **Applications**, la limite existante de 60 écritures/minute et un corps de 4 Kio maximum. Le propriétaire vient de l'authentification et les données des pièces viennent de Documents, jamais du navigateur.

| POST sous `/api/v1/applications/{id}` | Corps | Condition |
| --- | --- | --- |
| `/documents` | `{expectedRevision, exportId}` | Draft ; reçu vérifié, même propriétaire/profil/offre ; type et hash fournis par Documents |
| `/review` | `{expectedRevision}` | Draft avec CV et lettre distincts ; revalidation des deux reçus |
| `/approve` | `{expectedRevision, confirmed:true}` | ReadyForReview ; revalidation des deux reçus et confirmation de la révision exacte |
| `/return-to-draft` | `{expectedRevision}` | ReadyForReview ou Approved ; aucun appel Documents |

Le rattachement d'un export identique avec la révision courante est sans effet. Remplacer une pièce exige Draft et conserve l'ancienne référence dans l'historique. Revenir en Draft invalide l'état Approved ; il faut repasser par la revue. Une note incrémente aussi la révision : relire le dossier avant de confirmer.

```powershell
$headers = @{ Authorization = "Bearer $env:Applications__LocalAuth__Token" }
$id = '<UUID candidature>'
$resumeExportId = '<UUID export CV confirmé>'
$letterExportId = '<UUID export lettre confirmée>'
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id" -Headers $headers
foreach ($selectedExport in @($resumeExportId, $letterExportId)) {
    $body = @{ expectedRevision = $application.revision; exportId = $selectedExport } | ConvertTo-Json
    $application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/documents" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
}
$review = @{ expectedRevision = $application.revision } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/review" -Method Post -Headers $headers -ContentType 'application/json' -Body $review
# Relire le dossier et ses deux fichiers avant la confirmation explicite :
$approval = @{ expectedRevision = $application.revision; confirmed = $true } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/approve" -Method Post -Headers $headers -ContentType 'application/json' -Body $approval
```

Un dossier d'un autre propriétaire est invisible (404). Une révision périmée, une pièce manquante ou un état incompatible produit 409. Un reçu absent/non confirmé, un lien incohérent ou une pièce altérée/refusée produit 422. Une panne, une erreur d'authentification du canal ou une réponse invalide produit 503. Ces échecs n'ajoutent pas d'événement de revue/approbation. Après une réponse perdue, relire le dossier : les transitions avec l'ancienne révision ne sont pas rejouées automatiquement.

Les vérifications distantes précèdent la transaction ; la révision est revérifiée sous verrou à l'écriture. Les pièces sont immuables par leurs API. Il n'y a ni transaction distribuée ni surveillance continue après Approved : une modification administrative de Documents doit être détectée lors d'une nouvelle vérification, pas rétroactivement dans le dossier déjà approuvé. Le [lot 87.3](applications-follow-up.md) permet de déclarer un envoi manuel déjà effectué et de suivre les retours recruteurs ; aucun envoi automatique ne découle de cette revue.
