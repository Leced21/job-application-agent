# API des candidatures — phase 87.2a

Le lot initial permet de créer un brouillon depuis une offre Job réelle et le profil local configuré, de consulter les dossiers, d'ajouter des notes et de retirer une candidature. Le [lot 87.2b](applications-review.md) ajoute les pièces vérifiées et la revue/approbation humaine après configuration du canal et migration Documents. Le [lot 87.3](applications-follow-up.md) ajoute la déclaration d'un envoi manuel et le suivi recruteur. Les [règles métier](applications.md) définissent les transitions. Aucun endpoint d'envoi n'existe.

## Configuration locale

Applications écoute sur **http://localhost:5007** ; sa base dédiée PostgreSQL 17 écoute sur **127.0.0.1:5437**. Job (5002) et Profile (5001) doivent être lancés avec leur profil HTTP et leurs bases migrées. Le profil doit déjà exister. Aucun secret ni UUID utilisateur n'est fourni dans Git.

| Variable | Valeur attendue |
| --- | --- |
| `ConnectionStrings__ApplicationsDatabase` | Connexion PostgreSQL de la base dédiée Applications |
| `Applications__LocalAuth__OwnerId` | UUID propriétaire stable, à conserver ; utiliser le même propriétaire que Documents pour la future liaison |
| `Applications__LocalAuth__Token` | Jeton propre à Applications, 32–512 caractères sans espaces |
| `Applications__Sources__ProfileId` | UUID du profil local que ce propriétaire est autorisé à utiliser |
| `Applications__Sources__JobBaseUrl` | Origine locale Job, défaut `http://localhost:5002` |
| `Applications__Sources__ProfileBaseUrl` | Origine locale Profile, défaut `http://localhost:5001` |

Le lien propriétaire/profil est vérifié à chaque création par les APIs sources. Le bearer d’accès est transmis à Profile pour conserver l’identité autorisée ; aucun cookie n’est relayé. Job fournit son catalogue public. Les origines doivent être HTTP(S), loopback, sans chemin, identifiants, requête ou fragment. Les redirections sont désactivées, les réponses sont bornées à 128 Kio et le délai HTTP est de 10 secondes par requête.

Pour une **nouvelle base**, choisir les identifiants et les conserver hors Git ; réutiliser les valeurs existantes aux démarrages suivants. Exemple d'initialisation PowerShell, après choix du propriétaire stable :

```powershell
$env:APPLICATIONS_DB_USER = 'applications_local'
$env:APPLICATIONS_DB_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:Applications__LocalAuth__Token = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
# Renseigner Applications__LocalAuth__OwnerId avec votre UUID propriétaire stable.
$profile = Invoke-RestMethod 'http://localhost:5001/api/v1/profile'
$env:Applications__Sources__ProfileId = $profile.id
$env:ConnectionStrings__ApplicationsDatabase = "Host=localhost;Port=5437;Database=applications_db;Username=$env:APPLICATIONS_DB_USER;Password=$env:APPLICATIONS_DB_PASSWORD"
docker compose -f docker-compose.applications.yml up -d
dotnet ef database update --project src/services/Applications/JobApplicationAgent.Applications.Infrastructure --startup-project src/services/Applications/JobApplicationAgent.Applications.Api --context ApplicationsDbContext
dotnet run --project src/services/Applications/JobApplicationAgent.Applications.Api --launch-profile http
```

Pour le démarrage conteneurisé avec Profile, Job et Documents, copier `.env.example` vers `.env`, puis remplacer les mots de passe, jetons, propriétaire et profil d’exemple par les valeurs locales réelles. Depuis la racine :

```powershell
Copy-Item .env.example .env
# Remplacer les propriétaires/profil et jetons d'exemple par les valeurs locales réellement configurées.
docker compose --env-file .env -f docker-compose.yml -f docker-compose.applications.yml -f docker-compose.applications-api.yml -f docker-compose.documents.yml -f docker-compose.documents-api.yml up -d --build profile-db profile-api job-db job-api applications-db applications-api documents-db documents-api
$env:ConnectionStrings__ApplicationsDatabase = "Host=localhost;Port=5437;Database=$env:APPLICATIONS_DB_NAME;Username=$env:APPLICATIONS_DB_USER;Password=$env:APPLICATIONS_DB_PASSWORD"
dotnet ef database update --project src/services/Applications/JobApplicationAgent.Applications.Infrastructure --startup-project src/services/Applications/JobApplicationAgent.Applications.Api --context ApplicationsDbContext
```

L’API conteneurisée écoute sur `127.0.0.1:5007`. Elle accepte Job, Profile et Documents comme sources internes uniquement sous leurs noms Compose exacts, en HTTP sur le port 8080. Le jeton local Applications est configuré comme canal dédié de confirmation auprès de Documents ; le jeton direct Documents reste distinct.

La migration **20261016013140_InitialApplicationsSchema** crée uniquement Candidacies et CandidacyRequests dans cette base. Le service ne migre pas automatiquement. La génération et les tests de ce lot n'ont modifié aucune base persistante du poste. Les variables doivent aussi être disponibles dans la console utilisée pour appeler l'API.

## Routes

Toutes les routes exigent `Authorization: Bearer <jeton Applications>`. Les données sont filtrées par le propriétaire authentifié côté serveur. Un dossier appartenant à un autre propriétaire retourne 404. Réponses `Cache-Control: no-store` et `X-Content-Type-Options: nosniff`.

| Méthode et route | Fonction |
| --- | --- |
| POST `/api/v1/applications` | `{requestId, profileId, jobId}` ; création ou reprise, réponse 200 et Location |
| GET `/api/v1/applications/{id}` | Détail avec pièces et événements conservés |
| GET `/api/v1/applications?page=1&pageSize=20&status=Draft` | Historique des dossiers, page de 1 à 1 000 000, taille de 1 à 100 ; statut facultatif |
| POST `/api/v1/applications/{id}/notes` | `{expectedRevision, note}` ; note de 1 à 2 000 caractères utiles |
| POST `/api/v1/applications/{id}/withdraw` | `{expectedRevision, confirmed:true, reason}` ; motif de 1 à 2 000 caractères utiles |
| GET `/api/v1/applications/submission-platforms` | État d’activation et identifiants configurés qui satisfont les règles de sécurité ; aucune donnée secrète |
| GET `/api/v1/applications/{id}/submission-preview` | Aperçu authentifié des pièces et empreinte de la révision approuvée |
| POST `/api/v1/applications/{id}/submission-approval` | `{expectedRevision, confirmed:true, reviewSha256}` ; accord humain persistant |
| POST `/api/v1/applications/{id}/submission-attempts` | Réservation idempotente d’une tentative sur une plateforme du registre serveur |
| POST `/api/v1/applications/{id}/submission-attempt-result` | Résultat confirmé `Submitted`, `DefinitelyNotSubmitted` ou `Unknown` |

Les dossiers sont triés par date de création décroissante puis identifiant. Les éléments de liste sont des résumés ; le détail contient les événements. Les statuts sont Draft, Withdrawn, ReadyForReview/Approved avec les routes du [lot 87.2b](applications-review.md), puis Submitted, InReview, Interview, Offer et Rejected avec le [suivi 87.3](applications-follow-up.md).

La création exige une offre Discovered ou Saved et un profil conforme à la configuration. Elle fige le titre et l'entreprise. Une seule candidature existe par propriétaire/profil/offre, y compris après retrait. Les clés de requête sont uniques par propriétaire : réutiliser une clé pour un autre profil/offre produit 409. Les reprises retrouvent le dossier sans dépendre des sources, même si elles sont ensuite indisponibles. Une erreur de source avant création ne consomme pas la clé.

Les notes et retraits exigent la révision exacte lue dans le détail. Après réponse perdue ou conflit 409, relire le dossier avant toute nouvelle action. Le retrait n'est pas réversible dans ce lot. Les événements sont plafonnés à 1 000 ; une place est réservée au retrait. Les notes ordinaires sont donc refusées à partir de 999 événements. Une note n'est jamais interprétée comme une soumission ou une relance envoyée.

Le parcours contrôlé des phases 96.1–96.8 est une réservation et une déclaration de résultat : l’API n’envoie aucun formulaire ni document. L’activation et les plateformes doivent être configurées côté serveur, et le serveur n’accepte que les capacités enregistrées. `Unknown` bloque toute nouvelle réservation afin d’éviter un doublon ; `Submitted` passe le dossier à l’état Submitted. Consulter le [détail de la phase 96.8](phase-96-8.md) avant d’activer le parcours.

Écritures limitées à **60 par minute** par instance locale ; corps de création 4 Kio, notes/retrait 16 Kio. Codes : 400 données invalides, 401 authentification, 404 dossier absent, 409 révision/état/clé en conflit ou offre inactive, 422 source absente ou profil non lié, 429 limite d'écritures, 503 source indisponible ou réponse invalide. Les limites de corps Kestrel ne sont pas validées par les tests TestServer.

```powershell
$headers = @{ Authorization = "Bearer $env:Applications__LocalAuth__Token" }
$requestId = [guid]::NewGuid().ToString() # Conserver cette clé pour une reprise du même dossier.
$jobId = '<UUID offre existante>'
$body = @{ requestId = $requestId; profileId = $env:Applications__Sources__ProfileId; jobId = $jobId } | ConvertTo-Json
$application = Invoke-RestMethod 'http://localhost:5007/api/v1/applications' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
Invoke-RestMethod 'http://localhost:5007/api/v1/applications?page=1&pageSize=20' -Headers $headers
$note = @{ expectedRevision = $application.revision; note = 'Dossier en préparation' } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$($application.id)/notes" -Method Post -Headers $headers -ContentType 'application/json' -Body $note
```

## Persistance et limites

Le dépôt utilise des transactions, des verrous de création et deux contraintes d'unicité SQL ; la liaison clé/dossier comporte une FK incluant le propriétaire. Les mutations verrouillent la ligne et vérifient la révision. L'historique est conservé en texte JSON borné à 16 Mio, ce qui permet les 1 000 événements même avec des notes Unicode échappées, et rejoué par le domaine à la lecture, sans setters permettant de contourner les transitions. Les snapshots de titre et entreprise ne sont pas resynchronisés depuis Job.

Le jeton représente un propriétaire local unique. OIDC, sessions et autorisation multi-utilisateur restent en phase 91 ; chiffrement/rétention et intégration Angular suivent leurs phases. L'API n'est pas encore routée dans Gateway. Les pièces et la revue sont décrites dans le [guide 87.2b](applications-review.md), les retours recruteurs dans le [guide 87.3](applications-follow-up.md) et les relances dans le [guide 87.4](applications-reminders.md). Les phases 96–97 encadreront un éventuel envoi autorisé.
