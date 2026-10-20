# Préparation contrôlée des documents — 86.4a

Ce parcours conserve un **dossier de préparation structuré**, destiné à un futur CV adapté ou à une lettre. Il ne rédige pas encore de texte et ne produit aucun PDF/DOCX. Le statut PreparedForReview signifie que les sources sélectionnées restent à examiner avant la génération du contenu. La préparation ne modifie ni Profile, ni l'offre, ni le CV, et ne déclenche aucune candidature.

## Sources explicites

Le propriétaire sélectionne un jobId du service Job local, un resumeId précis et 1 à 50 factIds distincts, dans l'ordre souhaité. Tous les faits doivent appartenir à ce fichier et être Approved par le même propriétaire. Les faits d'une autre version, les faits en attente/rejetés et les faits d'un autre propriétaire sont refusés. Aucun fait supplémentaire n'est choisi automatiquement.

L'instantané `prepared-draft-v1` sépare :

- Job : ID, titre, entreprise, localisation, description, provenance, URL source, statut et date de modification lus depuis Job. Les exigences de l'annonce ne deviennent pas des qualifications du candidat.
- Candidate : métadonnées et SHA-256 du fichier, identité éventuelle du dossier/version lors de la préparation, faits approuvés avec preuves, IDs d'extraction, empreintes source et revue.
- Dates éventuelles : valeurs explicitement confirmées, note, auteur et date de confirmation, séparées de la période copiée du CV. DatesOrigin vaut OwnerConfirmed si présentes, NotProvided sinon. Une période partielle reste verbatim ; aucun jour ni mois n'est inventé.

Un fichier autonome est accepté ; Version est alors null, même s'il est rattaché ultérieurement à un dossier. Les champs d'identité ou de parcours absents ne sont pas complétés à partir de Profile ou de suppositions. Aucune vérité sémantique externe n'est vérifiée par ce parcours.

## Routes et reprise

Toutes les routes sont protégées par le Bearer Documents et no-store :

| Route | Comportement |
|---|---|
| POST /api/v1/documents/drafts | Prépare depuis { requestId, jobId, resumeId, kind, factIds } |
| GET /api/v1/documents/drafts/{id} | Relit le résumé et l'instantané conservé |
| GET /api/v1/documents/drafts?page=1&pageSize=20 | Historique des résumés du propriétaire, sans charger les instantanés |

Kind vaut Resume ou CoverLetter. Les réponses POST contiennent `{ document: { draft, snapshot }, created }` : 201 et Location à la création ; 200 pour une reprise identique. Le résumé Draft inclut ID, RequestId, ResumeId, JobId, Kind, Status, SnapshotSha256 et CreatedAtUtc. Le hash identifie la sérialisation originale conservée en base, pas les octets de la réponse HTTP mise en forme par MVC.

**Générer le requestId une seule fois avant l'appel et le réutiliser en cas d'interruption.** Le même propriétaire/requestId doit conserver exactement la même sélection, y compris l'ordre des faits et le type de document ; une autre sélection donne 409. Un nouveau requestId demande un nouveau dossier et une nouvelle lecture de l'offre. Les requêtes concurrentes identiques aboutissent à une seule ligne et au même instantané enregistré.

La reprise d'un dossier existant le retrouve avant de relire ses sources. Elle fonctionne même si Job a changé, a été supprimé ou est indisponible. L'instantané historique ne prétend donc pas représenter l'offre actuelle ; une nouvelle préparation doit la relire. Ignored et Expired refusent une nouvelle préparation, tandis que Discovered et Saved sont acceptés.

## Utilisation locale

Appliquer la migration Documents **20261015180845_AddPreparedDocumentDrafts**, connexion configurée hors Git :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

Démarrer Documents selon son [guide](documents.md) et Job avec son profil HTTP sur localhost:5002. La configuration optionnelle `Documents:Drafts:JobBaseUrl` (variable `Documents__Drafts__JobBaseUrl`) vaut http://localhost:5002 par défaut. Elle accepte uniquement une origine HTTP/HTTPS de boucle locale, sans identifiants, chemin supplémentaire, query ou fragment. Les redirections sont refusées. Le client utilise 10 secondes de délai et une réponse bornée à 128 KiB ; il ne transmet aucun Bearer utilisateur et ne visite pas les URL source des annonces.

Avec `$documentHeaders`, `$resumeId` et `$jobId` déjà choisis et après inspection des preuves via le [guide des faits](documents-facts.md) :

```powershell
$selected = Read-Host 'IDs des faits approuvés de ce CV, séparés par une virgule, dans l’ordre souhaité'
$factIds = @($selected.Split(',') | ForEach-Object { [guid]$_.Trim() })
$kind = Read-Host 'Type souhaité : Resume ou CoverLetter'
$draftRequestId = [guid]::NewGuid()
$draftBody = @{ requestId = $draftRequestId; jobId = $jobId; resumeId = $resumeId; kind = $kind; factIds = $factIds } | ConvertTo-Json
# Conserver draftRequestId et draftBody pour une reprise : ne pas les recréer après une erreur réseau.
$prepared = Invoke-RestMethod -Method Post -Uri 'http://localhost:5005/api/v1/documents/drafts' `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' -Body $draftBody
$prepared.document | ConvertTo-Json -Depth 15
$draftId = $prepared.document.draft.id
Invoke-RestMethod -Uri "http://localhost:5005/api/v1/documents/drafts/$draftId" -Headers $documentHeaders
```

Aucune commande de préparation n'est exécutée automatiquement pour le propriétaire. Aucun vrai CV ni dossier candidat n'a été utilisé pendant le développement.

## Bornes, sécurité et limites

Corps POST : 16 KiB ; sélection : 1–50 faits ; limiteur : 10 préparations/minute ; instantané sérialisé : 2 MiB maximum, contrôlé par l'application et PostgreSQL. Les listes utilisent page 1–1 000 000 et pageSize 1–100. Les refus sont 400 pour une sélection/pagination mal formée, 401 sans identité, 404 pour une source ou un dossier absent/inaccessible, 409 pour une clé réutilisée autrement, 422 pour des faits non approuvés, une offre inactive ou un instantané trop volumineux, 429 pour le limiteur, 503 pour Job/configuration/réponse indisponible ou invalide.

La FK propriétaire/CV empêche une association inter-propriétaires et protège le fichier source contre une suppression directe tant que le dossier existe. L'offre est une source distante au sens des microservices, sans FK inter-base ; son instantané reste lisible après sa suppression dans Job. La table prepared_document_drafts conserve le JSON original en text pour ne pas altérer les octets de son SHA-256. Les dossiers sont immuables par API, sans édition ni suppression dans ce lot ; la politique de rétention et de purge reste à prévoir. Les anciens endpoints Job restent locaux/anonymes ; ce client ne constitue pas la sécurisation globale prévue en phase 91.

La préparation seule ne génère ni n'approuve du texte. Le [parcours séparé de composition 86.4b](documents-composition.md) est disponible : modèle français déterministe fondé sur ces sources, contenu traçable et revue explicite liée à son empreinte. Le [parcours d'export 86.5](documents-export.md) conserve ensuite les fichiers PDF/DOCX approuvés, avec aperçu PDF et aperçu du texte DOCX ; aucune diffusion n'est ajoutée. Les appels à des modèles et l'orchestration IA restent en phases 88/89.
