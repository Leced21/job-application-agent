# Documents — import authentifié de CV, 86.1

Documents possède sa base PostgreSQL et quatre couches Domain, Application, Infrastructure, Api. Il conserve des imports immuables et leurs métadonnées ; il ne modifie pas le profil et ne prétend pas avoir analysé ou validé le contenu du CV. Statut initial : `UploadedUnanalysed`. L'extraction de texte est disponible dans le [lot 86.2a](documents-extraction.md). La [structuration sourcée et revue manuelle 86.2b](documents-facts.md) est disponible. Regroupement de versions, génération et prévisualisation viennent dans les tranches suivantes de 86.

## Démarrage local

API localhost:5005, PostgreSQL localhost:5436. La gateway relaie `/api/v1/documents/**` vers l'API qui effectue elle-même l'authentification et le filtrage propriétaire. Aucun fichier n'est placé dans un répertoire web public. Le stockage du contenu binaire et des métadonnées se fait atomiquement dans une ligne PostgreSQL ; la liste ne charge pas les contenus binaires.

Cette première identité locale représente **un seul propriétaire configuré** pour l'instance. Elle est indépendante du profil métier et n'est jamais lue depuis un en-tête OwnerId ou une donnée fournie à l'import. Le jeton Bearer est associé côté serveur à Documents:LocalAuth:OwnerId. Conserver le même OwnerId au redémarrage ; changer cet identifiant ne transfère pas les documents déjà stockés. Le jeton peut être renouvelé en conservant cet OwnerId.

Les paramètres doivent rester hors Git. Générer une fois un GUID avec `(New-Guid).Guid` et le conserver comme OwnerId stable. Créer un jeton aléatoire fort dans un gestionnaire de secrets, entre 32 et 512 caractères sans espace. Aucun jeton ni propriétaire par défaut n'est fourni ; le service refuse de démarrer si la configuration d'identité ou de base manque.

Depuis la racine PowerShell :

```powershell
$env:DOCUMENTS_DB_USER = Read-Host 'Utilisateur PostgreSQL Documents'
$documentDbPassword = Read-Host 'Mot de passe PostgreSQL Documents' -AsSecureString
$env:DOCUMENTS_DB_PASSWORD = [System.Net.NetworkCredential]::new('', $documentDbPassword).Password
$env:Documents__LocalAuth__OwnerId = Read-Host 'OwnerId stable (GUID conservé hors Git)'
$documentToken = Read-Host 'Jeton Documents aléatoire fort' -AsSecureString
$env:Documents__LocalAuth__Token = [System.Net.NetworkCredential]::new('', $documentToken).Password

docker compose -p job-agent-documents -f docker-compose.documents.yml up -d --wait

$documentConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$documentConnection['Host'] = 'localhost'
$documentConnection['Port'] = 5436
$documentConnection['Database'] = 'documents_db'
$documentConnection['Username'] = $env:DOCUMENTS_DB_USER
$documentConnection['Password'] = $env:DOCUMENTS_DB_PASSWORD
$env:ConnectionStrings__DocumentsDatabase = $documentConnection.ConnectionString

dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api
dotnet run --project src/services/Documents/JobApplicationAgent.Documents.Api --launch-profile http
```

La migration `20261015123604_InitialDocumentsSchema` doit être appliquée avant le démarrage. Elle a été exécutée uniquement sur les bases éphémères des tests ; aucune base persistante utilisateur n'a été créée ou migrée. La factory EF utilise ConnectionStrings__DocumentsDatabase. Le service n'applique pas de migration automatiquement.

Pour un volume PostgreSQL existant, réutiliser ses identifiants : changer les variables Compose ne modifie pas le mot de passe de la base. Si DOCUMENTS_DB_NAME est personnalisé, utiliser la même valeur dans la connexion. Le Compose séparé ajoute seulement PostgreSQL et n'affecte pas les commandes des autres services. Il a été vérifié par `config --quiet`, sans lancement d'une base persistante pendant le développement.

Pour démarrer l’API Documents avec Profile, Job et Applications conteneurisés, copier `.env.example` vers `.env`, remplacer les valeurs d’exemple, puis lancer les Compose combinés depuis la racine :

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.applications.yml -f docker-compose.applications-api.yml -f docker-compose.documents.yml -f docker-compose.documents-api.yml up -d --build profile-db profile-api job-db job-api applications-db applications-api documents-db documents-api
```

Le service Documents écoute sur `127.0.0.1:5005`. Il partage l’OwnerId et le ProfileId local d’Applications, mais son jeton d’accès direct reste distinct. Un canal Applications dédié utilise le jeton Applications et les mêmes identifiants de propriétaire/profil afin que la revue humaine puisse confirmer un export. Les migrations Documents et Applications restent à appliquer explicitement depuis l’hôte.

## API et contrôles

Toutes les routes suivantes exigent `Authorization: Bearer <jeton configuré>`. Jeton absent ou incorrect : 401 avec challenge Bearer. Le jeton est comparé via SHA256 et comparaison à temps constant. Un document d'un autre propriétaire répond 404, comme un identifiant absent ; il n'apparaît pas dans la liste.

| Méthode | Route | Réponse |
|---|---|---|
| POST | `/api/v1/documents/resumes` | Multipart avec champ `file`, 201 et Location |
| GET | `/api/v1/documents/resumes?page=1&pageSize=20` | Tableau paginé de métadonnées, limité au propriétaire |
| GET | `/api/v1/documents/resumes/{id}` | Métadonnées, sans contenu binaire |
| GET | `/api/v1/documents/resumes/{id}/content` | Octets originaux, Content-Disposition attachment |

Les métadonnées sont id, fileName, mediaType, length, sha256, uploadedAtUtc et status. Les dates serveur sont en UTC, à la précision microseconde PostgreSQL. Chaque import reçoit un nouvel identifiant, même pour un fichier déjà envoyé ; les anciens imports restent conservés. Il ne s'agit pas encore d'un historique de versions regroupées.

Limites : fichier non vide de 5 Mio au maximum, requête multipart de 6 Mio au maximum côté serveur, nom final de 200 caractères sans caractères de contrôle ni contrôle Unicode bidirectionnel. Le préfixe de chemin d'un nom client est supprimé ; il n'est jamais utilisé comme chemin serveur. Les uploads authentifiés partagent une limite de 10 requêtes par fenêtre fixe d'une minute, en mémoire pour l'instance, sans file d'attente. Pagination : page 1–1 000 000, taille 1–100. Les listes sont ordonnées par date décroissante et ID et n'exposent pas de total transactionnel.

Le fichier doit associer extension et type MIME exacts : `.pdf` avec `application/pdf`, ou `.docx` avec `application/vnd.openxmlformats-officedocument.wordprocessingml.document`. Les erreurs de format/taille applicative donnent 400 ; dépassement de taille HTTP côté serveur : 413 ; quota : 429. Un champ file absent est refusé par la validation API. Les réponses portent `Cache-Control: no-store` et `X-Content-Type-Options: nosniff`. Aucun document n'est rendu inline par le service.

PDF : contrôle du marqueur de version 1.0–1.7/2.0 et de la fin de fichier, **pas un parsing complet du PDF**. L'acceptation d'un import ne garantit ni sa lisibilité ni l'absence de contenu actif ou malveillant. DOCX : ZIP limité à 200 entrées, au plus 5 Mio par entrée et 20 Mio décompressés au total, sur les tailles déclarées et réellement lues. Les parties principales OpenXML transitionnelles doivent être présentes ; chemins relatifs dangereux, doublons, macros, objets embarqués binaires, DTD et relations externes sont refusés. Un DOCX courant comportant des relations externes, par exemple certains liens hypertexte, peut donc être refusé dans cette tranche. Le format Strict OpenXML n'est pas pris en charge.

Ces contrôles ne remplacent pas un antivirus ou un pipeline de quarantaine/analyse. L'extraction 86.2a utilise un worker séparé et des limites de traitement, décrits dans son guide ; elle n'exécute pas le contenu du document. Le téléchargement restitue le fichier original au propriétaire, sans le certifier sûr.

## Exemple d'import explicite

Le client .NET ci-dessous permet d'indiquer le bon type MIME, y compris avec Windows PowerShell sans `Invoke-RestMethod -Form`. Utiliser un terminal où le jeton Documents est configuré ; ne pas l'afficher ni le mettre dans l'URL.

```powershell
Add-Type -AssemblyName System.Net.Http
$resumePath = Read-Host 'Chemin absolu du CV PDF ou DOCX à importer'
$resumeName = [System.IO.Path]::GetFileName($resumePath)
$resumeMime = if ([System.IO.Path]::GetExtension($resumePath) -ieq '.pdf') {
    'application/pdf'
} else {
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
}
$documentHttp = [System.Net.Http.HttpClient]::new()
$documentHttp.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $env:Documents__LocalAuth__Token)
$documentForm = [System.Net.Http.MultipartFormDataContent]::new()
try {
    $documentPart = [System.Net.Http.StreamContent]::new([System.IO.File]::OpenRead($resumePath))
    $documentPart.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new($resumeMime)
    $documentForm.Add($documentPart, 'file', $resumeName)
    $documentResponse = $documentHttp.PostAsync('http://localhost:5005/api/v1/documents/resumes', $documentForm).GetAwaiter().GetResult()
    try {
        $documentResponse.EnsureSuccessStatusCode() | Out-Null
        $resume = $documentResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $resume | Format-List
    } finally { $documentResponse.Dispose() }
} finally {
    $documentForm.Dispose()
    $documentHttp.Dispose()
}
```

Le code n'est pas exécuté automatiquement : le développement a utilisé uniquement des fichiers synthétiques, aucun CV utilisateur.

## Limites et suite

Ce service est destiné au développement local. Le jeton partagé représente le propriétaire configuré, pas une authentification multi-utilisateur OIDC. Les protections des autres microservices ne changent pas. HTTP doit rester local ; TLS, authentification centralisée, gestion/révocation de sessions, sécurité des backups, chiffrement au repos et politique de rétention relèvent de la phase de sécurisation. Le stockage PostgreSQL n'est pas chiffré par cette implémentation et reste accessible aux administrateurs de la base.

La phase 86 reste en cours. Le [lot 86.2a](documents-extraction.md) conserve désormais le texte sourcé. La [revue des faits sourcés 86.2b](documents-facts.md) permet leur approbation ou rejet. Le [transfert explicite des compétences 86.2c.1](documents-profile-import.md) relie un propriétaire à un profil existant avec canal authentifié et reçu durable. Le [mapping confirmé 86.2c.2a](documents-profile-mapping.md) prépare les autres catégories sans les transférer. Le [lot 86.2c.2b](phase-86-2c2b.md) permet leur transfert explicite avec reçu durable et conciliation. Le [lot 86.3](documents-versions.md) regroupe explicitement les fichiers importés et conserve un historique numéroté. Le [lot 86.4a](documents-drafts.md) conserve un dossier de préparation depuis une offre Job locale et des faits approuvés explicitement sélectionnés. Le [lot 86.4b](documents-composition.md) compose un contenu vérifiable et enregistre sa revue explicite. Le [lot 86.5](documents-export.md) exporte le contenu approuvé en PDF/DOCX avec historique, aperçu PDF et aperçu du texte DOCX. La pagination Word reste à vérifier visuellement. Le [socle métier 87.1](applications.md) prépare le suivi des candidatures ; le [lot 87.2a](applications-api.md) ajoute le stockage et les routes initiales. Le [lot 87.2b](applications-review.md) ajoute le rattachement confirmé des exports au profil et la revue HTTP après migration Documents et configuration du canal dédié. Aucun diplôme, compétence ou parcours ne sera inventé.
