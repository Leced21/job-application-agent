# Export PDF et DOCX et prévisualisation — 86.5

Le propriétaire peut générer un fichier depuis une [composition](documents-composition.md) **Approved**, en fournissant son empreinte exacte. Le service conserve les octets et leur SHA-256 dans Documents. Le PDF peut être prévisualisé avant téléchargement ; le DOCX dispose d'un aperçu HTML sûr de son contenu, distinct d'un rendu Word fidèle.

L'export ne complète aucune donnée absente : identité et coordonnées manquantes dans la composition restent absentes. Il n'ajoute ni qualification ni motivation et n'envoie rien à un recruteur. Seul PlainText est mis en page ; les notices, preuves, identifiants techniques et commentaires de revue ne sont pas ajoutés au corps du document. Le PDF ajoute une pagination administrative.

## Parcours et routes

Toutes les routes sont protégées par le Bearer Documents, no-store et nosniff :

| Route | Comportement |
|---|---|
| POST /api/v1/documents/drafts/{draftId}/exports | Génère depuis { format: Pdf ou Docx, contentSha256 } |
| GET /api/v1/documents/exports/{id} | Métadonnées et empreintes |
| GET /api/v1/documents/exports?page=1&pageSize=20 | Historique des métadonnées, sans lecture des octets |
| GET /api/v1/documents/exports/{id}/preview | PDF inline, ou HTML du texte DOCX avec pagination Word non garantie |
| GET /api/v1/documents/exports/{id}/content | Téléchargement en pièce jointe |

Le POST renvoie `{ export, created }` avec 201 et Location à la création, puis 200 pour une reprise du même dossier/format/empreinte. Les métadonnées incluent ID, DraftId, Format, RendererVersion, CompositionSha256, FileSha256, FileName, MediaType, Length, PageCount, CreatedAtUtc et ApprovedAtUtc. PageCount est connu pour le PDF et null pour le DOCX : aucune pagination Word n'est prétendue.

La génération conserve un fichier interne pour pouvoir prévisualiser ensuite ces octets avant de les télécharger. Le PDF inline et le PDF téléchargé sont exactement le même fichier. Le DOCX est un package OOXML, sans macro, lien externe ni contenu actif, contenant le texte approuvé et une police embarquée. Son aperçu HTML reprend le texte approuvé avec échappement intégral, CSP restrictive et avertissement de pagination ; il ne simule pas Word. Les caractères source `<script>` restent du texte.

Le service vérifie l'auteur et la date de l'approbation, l'empreinte attendue et le hash du JSON composé avant génération. À chaque prévisualisation ou téléchargement, il recontrôle l'approbation, le hash de la composition et celui du fichier. Les en-têtes X-File-SHA256 et X-Composition-SHA256 distinguent les deux empreintes. Les noms de fichiers sont fabriqués depuis le GUID du dossier, sans chemin ni titre fourni par le client.

## Utilisation locale

Avec `$draftId` et `$documentHeaders` des guides précédents, après revue explicite du contenu :

```powershell
$composed = Invoke-RestMethod -Uri "http://localhost:5005/api/v1/documents/drafts/$draftId/composition" -Headers $documentHeaders
if ($composed.composition.decision -ne 'Approved') { throw 'Approuver explicitement le contenu avant export' }
$format = Read-Host 'Format souhaité : Pdf ou Docx'
$exportBody = @{ format = $format; contentSha256 = $composed.composition.contentSha256 } | ConvertTo-Json
$created = Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/documents/drafts/$draftId/exports" `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' -Body $exportBody
$artifact = $created.export
$artifact | Format-List
# Consulter /exports/{id}/preview avec les mêmes en-têtes via un client API.
# Pour télécharger après contrôle de l'aperçu :
$destination = Read-Host 'Chemin local du fichier à enregistrer'
Invoke-WebRequest -Uri "http://localhost:5005/api/v1/documents/exports/$($artifact.id)/content" `
    -Headers $documentHeaders -OutFile $destination
(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -eq $artifact.fileSha256
```

Le Bearer doit être envoyé par le client API ; une URL seule dans un navigateur sans authentification ne donne pas accès au fichier. Aucun export de CV réel n'a été exécuté pendant le développement. L'appel de création ne transmet aucun fichier à l'extérieur.

## Conservation, bornes et caractères

Une seule sortie immuable est conservée par dossier et format. La reprise relit les octets existants, sans nouvelle mise en page. Deux requêtes concurrentes convergent vers le même export par verrou transactionnel et index unique dossier/format ; le moteur autorise au plus deux rendus simultanés et peut renvoyer 503 aux autres. Reprendre alors le même corps. Un changement de moteur ne remplace pas les anciens fichiers : une nouvelle composition exige un nouveau dossier.

Rendu **verified-export-v1-pdfpig-0.1.16** : PDF A4 avec marges, retours à la ligne, titres et pagination ; DOCX A4 avec styles Title/Heading2/Normal et police Bitstream Vera Sans embarquée sous licence redistribuable conservée dans Assets. Le générateur PDF utilise la version PdfPig déjà employée par le worker, sans lire de PDF utilisateur dans ce parcours. Le DOCX est écrit avec les API ZIP/XML .NET, sans exécutable Office. Aucun secret, nom d'auteur utilisateur ou ressource distante n'est incorporé automatiquement.

Le texte est limité à 100 000 caractères et 2 000 sauts de ligne. Les autres caractères de contrôle sont refusés. Le PDF est limité à 40 pages et vérifie la couverture de police : un glyphe absent provoque 422, sans carré de remplacement ni translittération. La police couvre les accents français testés mais pas tous les alphabets ; le DOCX préserve l'Unicode et son lecteur peut choisir une police de remplacement. Aucun texte n'est tronqué pour contourner une borne.

Chaque fichier est limité à 5 MiB. Le POST est limité à 4 KiB et 10 écritures/minute. Pagination de l'historique : page 1–1 000 000, pageSize 1–100. Erreurs : 400 pour format/hash/pagination mal formé, 401 sans identité, 404 pour source/fichier absent ou inaccessible, 409 pour empreinte contradictoire, 422 pour contenu non approuvé/corrompu/non représentable/trop grand, 429 pour limiteur, 503 pour moteur occupé. Les règles d'accès sont appliquées aussi aux aperçus et téléchargements.

## Migration et validation

Appliquer Documents **20261015184131_AddDocumentExports** avant les nouveaux binaires, connexion configurée hors Git :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

La migration crée document_exports, la clé alternative propriétaire/composition, les contraintes de taille et l'unicité dossier/format. La FK propriétaire/composition empêche une association inter-propriétaires et protège la composition d'une suppression directe. Aucun export d'une ancienne composition n'est créé automatiquement. Son Down supprime les fichiers exportés ; les compositions restent conservées. Seules les bases PostgreSQL 17 éphémères des tests ont été migrées pendant le développement.

Les PDF de CV et lettre synthétiques ont été rendus par Poppler et inspectés visuellement. La suite vérifie aussi le texte, les marges des glyphes, les accents, les pages multiples et la dernière ligne. Les DOCX ont été relus par python-docx et contrôlés en ZIP/XML : texte exact, styles, police et relations internes. **La mise en page Word n'a pas été vérifiée visuellement ici : LibreOffice est absent du runtime disponible.** L'aperçu DOCX est donc explicitement un aperçu de contenu ; vérifier le fichier dans un lecteur Word avant usage réel. Les captures et documents synthétiques sont des intermédiaires QA, pas des documents candidat livrés.

La politique de suppression/rétention et les écrans Angular restent à venir. Aucune diffusion ni soumission n'est ajoutée. La suite du plan porte sur la phase 87, suivi des candidatures, avec les limites de rendu ci-dessus à conserver dans le bilan.

## Rattachement aux candidatures

Le [lot 87.2b](applications-review.md) permet de confirmer explicitement le profil associé à un export, puis de le rattacher à une candidature avec vérification du propriétaire, de la même offre et des empreintes. Il exige la migration Documents AddDocumentApplicationBindings et un canal de lecture dédié. Aucun export ne vaut approbation ou soumission de candidature.
