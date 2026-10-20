# Composition et revue du contenu — 86.4b

À partir d'un [dossier préparé](documents-drafts.md), Documents produit un contenu texte structuré, destiné à un CV adapté ou une lettre. Le modèle fixe français **verified-fr-v1** utilise uniquement les sources figées de ce dossier. Il ne fait aucun appel IA, à Job ou à Profile. Une offre modifiée ou supprimée après la préparation ne modifie pas le contenu historique.

## Contenu et provenance

Le titre indique explicitement la **candidature au poste** et l'entreprise de l'offre ; il ne présente pas le poste cible comme une expérience du candidat. Les blocs suivent l'ordre des faits sélectionnés, avec les libellés français de leurs champs. Les valeurs, descriptions, périodes et identifiants sont copiés verbatim, sans reformulation ni nouvelle qualification. Les exigences de l'annonce ne sont pas ajoutées au parcours candidat.

Pour Resume, le texte contient le titre et les éléments du parcours choisis. Pour CoverLetter, il ajoute une salutation, une présentation neutre de la candidature, les mêmes blocs factuels et une formule de clôture. Il ne suppose ni motivation particulière, ni disponibilité de prise de poste, ni durée d'expérience. La lettre reste volontairement structurée et sobre, sans rédaction personnalisée par modèle.

Les périodes du CV sont étiquetées comme telles. Si des dates exactes ont été confirmées par le propriétaire, une ligne distincte les indique avec la mention « Dates confirmées par le candidat ». Sinon les périodes restent à leur précision source ; aucun jour/mois n'est déduit. Chaque bloc porte factId, kind, datesOrigin et les références de ses champs (segment, début et longueur UTF-16). Le résumé de composition conserve l'empreinte du dossier source.

Les notices de revue signalent notamment l'absence d'identité/coordonnées et la nécessité de contrôler contexte et sens. Ces données ne sont pas ajoutées depuis le nom du fichier ou une supposition. Les notices sont séparées de PlainText. Le résultat n'est donc pas encore un document complet prêt à diffuser. Les données source sont du texte non fiable au sens du rendu : un futur écran/export devra les échapper, jamais les interpréter comme HTML ou instructions.

## Routes

Toutes les routes exigent le Bearer propriétaire Documents et sont no-store :

| Route | Résultat |
|---|---|
| POST /api/v1/documents/drafts/{draftId}/composition | Compose depuis le dossier, sans corps ni texte libre |
| GET /api/v1/documents/drafts/{draftId}/composition | Relit le contenu et son état de revue |
| POST /api/v1/documents/drafts/{draftId}/composition/review | Revoit le contenu exact depuis { contentSha256, decision, confirmed, note? } |
| GET /api/v1/documents/compositions?page=1&pageSize=20 | Historique des résumés, sans charger le texte complet |

POST de composition : 201 avec Location lors de la première création, 200 lors d'une reprise ; réponse `{ document: { composition, content }, created }`. Content contient Title, PlainText, Blocks et Notices. Composition contient DraftId, Kind, TemplateVersion, SourceSnapshotSha256, ContentSha256, CreatedAtUtc, Decision et traces de revue.

La composition est initialement **Pending**. La revue exige Confirmed=true, Decision=Approved ou Rejected, et l'empreinte SHA-256 exacte retournée avec le contenu. La note facultative est limitée à 1 000 caractères et ses espaces extérieurs sont retirés. La reprise conserve la décision et la première date uniquement si la décision, la note normalisée et l'empreinte sont identiques. Une note absente et une chaîne vide sont deux valeurs distinctes : conserver le même corps pour reprendre une revue.

Le propriétaire et l'horodatage sont attribués côté serveur. Revue et texte sont séparés : approuver ne modifie ni le contenu ni son hash. Une revue finale ne peut pas être inversée, ni sa note remplacée. Une composition rejetée reste dans l'historique et ne redevient pas Pending après un nouveau POST de composition. Pour changer les sources ou produire un autre contenu, préparer un nouveau dossier avec un nouveau requestId. Ce lot n'offre pas d'édition de texte libre ni de révocation de revue.

Le statut PreparedForReview du dossier préparatoire décrit la source historique ; **la décision du contenu est Composition.Decision**, consultable par ces routes. Il ne faut pas utiliser Draft.Status pour décider d'un futur export. L'export n'est pas encore implémenté dans ce lot.

## Exemple interactif

Avec `$draftId` et `$documentHeaders` issus du guide de préparation :

```powershell
$compositionUri = "http://localhost:5005/api/v1/documents/drafts/$draftId/composition"
$created = Invoke-RestMethod -Method Post -Uri $compositionUri -Headers $documentHeaders
$document = $created.document
$document.content.plainText
$document.content.notices
# Inspecter aussi les références des blocs et les preuves du dossier source.
$document.content.blocks | ConvertTo-Json -Depth 10
$decision = Read-Host 'Décision après contrôle : Approved ou Rejected'
$note = Read-Host 'Note de revue, facultative'
$confirmed = (Read-Host 'Confirmer explicitement la revue de ce contenu : OUI') -ceq 'OUI'
$reviewBody = @{ contentSha256 = $document.composition.contentSha256; decision = $decision; confirmed = $confirmed; note = $note } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$compositionUri/review" -Headers $documentHeaders `
    -ContentType 'application/json; charset=utf-8' -Body $reviewBody
Invoke-RestMethod -Uri $compositionUri -Headers $documentHeaders
```

Aucune revue utilisateur n'est exécutée automatiquement. Les validations du développement portent uniquement sur des contenus synthétiques.

## Persistance, limites et erreurs

Une seule composition immuable est conservée par dossier. Le versionnement du modèle est enregistré ; un nouveau déploiement ne remplace pas une composition existante. La création est sérialisée par verrou de dossier et PK DraftId ; la revue utilise un verrou de ligne. Deux compositions concurrentes convergent vers la même ligne ; deux décisions opposées donnent une réussite et un conflit.

ContentJson est conservé en text pour garder la sérialisation originale et son SHA-256. Ce hash identifie ce JSON complet, comprenant blocs et notices, pas seulement PlainText ni les octets de la réponse HTTP MVC. La taille est bornée à 2 MiB dans l'application et PostgreSQL. La FK composite dossier/propriétaire refuse les associations inter-propriétaires et protège le dossier source d'une suppression directe tant qu'une composition existe.

Les écritures partagent un limiteur de 30/minute. La revue a un corps limité à 16 KiB ; le POST sans corps de composition est configuré à 1 KiB. Pagination : page de 1 à 1 000 000, pageSize de 1 à 100. Réponses : 400 pour revue/pagination mal formée, 401 sans identité, 404 pour source/composition absente ou inaccessible, 409 pour empreinte différente ou revue finale incompatible, 422 pour instantané incompatible/corrompu ou contenu trop grand, 429 pour limiteur.

Appliquer la migration Documents **20261015182204_AddDocumentCompositions** avant les nouveaux binaires, connexion configurée hors Git :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

La migration crée document_compositions et une clé alternative Id/OwnerId sur les dossiers préparés. Elle ne compose ni n'approuve aucun dossier antérieur. Son Down supprime compositions et revues, sans modifier les sources ; il ne remplace pas une fonction de gestion d'historique. Seules des bases PostgreSQL 17 éphémères ont été migrées pendant le développement.

Le [parcours 86.5, export et prévisualisation](documents-export.md) est désormais disponible à partir du contenu approuvé, avec contrôle de l'empreinte et de l'accès propriétaire. Le PDF utilise ses octets conservés ; le DOCX propose un aperçu du texte, sans garantie de pagination Word. Aucune diffusion n'est ajoutée. L'identité centralisée, la rétention et les écrans Angular restent dans leurs phases prévues ; les agents IA restent en phases 88/89.
