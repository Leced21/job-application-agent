# Dossiers et historique des versions de CV — 86.3

Un dossier (« resume series ») regroupe explicitement plusieurs fichiers de CV déjà importés par le même propriétaire. Il possède un nom et un historique numéroté. Les PDF/DOCX, leurs IDs, octets, SHA-256, dates d'import, extractions et faits restent inchangés. Aucun regroupement n'est déduit d'un nom de fichier ou de son contenu. Deux dossiers peuvent avoir le même nom ; leur identité est leur GUID.

Les anciens imports restent accessibles individuellement. Ils ne sont pas regroupés pendant la migration. Un même resumeId peut appartenir à un seul dossier ; les fichiers non rattachés restent autonomes. Les fichiers déjà présents peuvent être ajoutés dans l'ordre choisi par le propriétaire : le numéro correspond à l'ordre d'ajout au dossier, pas à la date d'import du fichier.

## Routes et contrats

Toutes les routes exigent le Bearer Documents du propriétaire configuré et renvoient des réponses no-store.

| Méthode et route | Résultat |
|---|---|
| POST /api/v1/documents/resume-series | Crée un dossier et sa version 1 depuis { name, firstResumeId, label? } |
| GET /api/v1/documents/resume-series?page=1&pageSize=20 | Liste les dossiers du propriétaire |
| GET /api/v1/documents/resume-series/{seriesId} | Résumé du dossier et nombre actuel de versions |
| POST /api/v1/documents/resume-series/{seriesId}/versions | Ajoute un fichier importé depuis { resumeId, label? } |
| GET /api/v1/documents/resume-series/{seriesId}/versions?page=1&pageSize=20 | Historique, numéro décroissant |
| GET /api/v1/documents/resume-series/{seriesId}/versions/{number} | Métadonnées d'une version précise |
| GET /api/v1/documents/resumes/{resumeId}/series | Retrouve le dossier d'un fichier ; 404 si autonome/inaccessible |

Les POST renvoient `{ series, version, created }` : 201 à la première création avec Location, 200 lors d'une reprise identique. Series contient Id, Name, CreatedAtUtc et VersionCount ; Version contient SeriesId, VersionNumber, Label, AddedAtUtc et les métadonnées Resume existantes. Les listes et historiques ne chargent pas les octets des fichiers. Le téléchargement reste celui de `/resumes/{resumeId}/content`, protégé et en pièce jointe.

Le propriétaire et les dates sont attribués côté serveur. Nom : 1–200 caractères après Trim ; libellé facultatif : 200 maximum après Trim, blanc normalisé en null. Les caractères de contrôle sont refusés. Corps des POST limité à 4 KiB ; limiteur partagé de 30 écritures/minute pour ces routes. Pagination : page de 1 à 1 000 000, pageSize de 1 à 100. Chaque dossier contient au plus 1 000 versions.

## Création et ajout explicites

Après avoir importé deux fichiers avec le [guide Documents](documents.md), préparer `$firstResumeId`, `$nextResumeId` et `$documentHeaders` avec le Bearer Documents :

```powershell
$seriesName = Read-Host 'Nom du dossier de CV'
$initialLabel = Read-Host 'Libellé de la première version, facultatif'
$created = Invoke-RestMethod -Method Post -Uri 'http://localhost:5005/api/v1/documents/resume-series' `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' `
    -Body (@{ name = $seriesName; firstResumeId = $firstResumeId; label = $initialLabel } | ConvertTo-Json)
$seriesId = $created.series.id
$versionLabel = Read-Host 'Libellé de la nouvelle version, facultatif'
$added = Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5005/api/v1/documents/resume-series/$seriesId/versions" `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' `
    -Body (@{ resumeId = $nextResumeId; label = $versionLabel } | ConvertTo-Json)
Invoke-RestMethod -Uri "http://localhost:5005/api/v1/documents/resume-series/$seriesId/versions?page=1&pageSize=20" `
    -Headers $documentHeaders | ConvertTo-Json -Depth 8
```

Les commandes ne sont pas exécutées automatiquement. Aucun CV utilisateur n'a été regroupé pendant le développement.

## Reprise, concurrence et isolation

La reprise de création utilise le même firstResumeId, nom et libellé. La reprise d'ajout utilise le même seriesId, resumeId et libellé. Elle conserve le numéro et la date d'ajout initiaux ; le compteur du dossier reflète son état courant. Un nom/libellé différent lors d'une reprise est un conflit, pas une édition. Ne pas réimporter le fichier pour reprendre une requête réseau interrompue : un nouvel upload crée un autre resumeId et peut donc constituer une autre version, même à octets identiques.

Le rattachement et le libellé sont immuables. Il n'existe pas encore d'édition, de déplacement, de fusion ou de suppression de dossier/version par API. Le verrou transactionnel par fichier puis dossier empêche deux rattachements concurrents du même fichier et sérialise l'allocation des numéros. Le PK resumeId interdit la double appartenance ; l'index unique seriesId/versionNumber interdit les numéros identiques. Les FK composites incluent OwnerId pour que la base refuse aussi une association entre propriétaires différents. Un fichier rattaché ne peut pas être supprimé directement en base tant que son rattachement existe.

Les preuves et décisions restent attachées à leur resumeId/extractionId d'origine. **Ajouter une version ne copie aucune extraction, approbation, date confirmée ni import Profile.** Il faut extraire, annoter et revoir la nouvelle version selon les parcours existants. Il n'y a pas de version active implicite : les opérations d'extraction, de revue et de transfert continuent à employer un resumeId explicite. Le numéro maximal indique seulement le dernier rattachement. Aucune génération ni candidature n'est déclenchée.

401 : Bearer absent/invalide ; 400 : métadonnées, identifiant de corps, pagination ou numéro hors bornes ; 404 : dossier/fichier/version absent, autonome ou inaccessible ; 409 : rattachement/métadonnées contradictoires ou limite de versions ; 429 : limiteur. Les dossiers d'un autre propriétaire sont masqués par 404, sans modifier les données.

## Migration et limites

Appliquer **20261015175642_AddResumeSeries** à Documents avant les nouveaux binaires, connexion configurée hors Git :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

La migration crée resume_series et resume_series_versions avec leurs contraintes, et ajoute une clé alternative Id/OwnerId aux fichiers. Aucun fichier ni fait existant n'est modifié, aucun rattachement n'est créé automatiquement. Son Down supprime le regroupement et son historique, tout en conservant les fichiers ; ce retour n'est pas un moyen de gestion des versions. Seules des bases PostgreSQL 17 éphémères ont été migrées pendant le développement.

Ce lot reste destiné au propriétaire local unique du socle Documents. Les écrans Angular, l'archivage, la rétention et les modifications contrôlées de dossiers restent à prévoir. Les documents adaptés à une offre, lettres, export, prévisualisation et historique des générations constituent les prochains lots ; les agents IA restent en phases 88/89.
