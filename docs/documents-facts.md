# 86.2b — données candidates sourcées et revue explicite

Cette tranche structure des passages du texte extrait en données candidates : Skill, Experience, Education et Certification. La structuration est **assistée par annotation explicite des segments**, pas une détection automatique de qualifications par IA. Le serveur copie chaque valeur depuis la sélection demandée ; le client ne fournit pas une valeur libre que le serveur pourrait attribuer au CV.

Toutes les données sont Pending à la création. Leur propriétaire lit le contexte puis approuve ou rejette chaque fait. Une présence textuelle, même exacte, n'est pas une preuve de qualification réelle : « Not certified in Azure » contient Azure, mais ne prouve aucune certification. Le serveur conserve ce contexte et ne transforme pas la présence du mot en approbation.

## Migration

Appliquer la migration Documents `20261015132831_AddResumeCandidateFacts` avant de démarrer les nouveaux binaires, avec la connexion et l'identité locales du [guide Documents](documents.md) :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api
dotnet run --project src/services/Documents/JobApplicationAgent.Documents.Api --launch-profile http
```

Le worker et l'extraction 86.2a restent nécessaires pour obtenir le texte source. Aucune annotation ni revue automatique n'est lancée sur les CV existants. La migration a été appliquée seulement aux bases éphémères de tests, pas à une base persistante utilisateur.

## Schémas et preuves

| Catégorie | Champ obligatoire | Autres champs autorisés |
|---|---|---|
| Skill | Name | Aucun |
| Experience | Title | Company, Period, Description |
| Education | Degree | Institution, Period, Description |
| Certification | Name | Issuer, Period, Identifier |

GET `/api/v1/documents/fact-schema`, authentifié, expose ces schémas génériques sans données de CV. Les noms et valeurs de catégories sont sensibles à la casse. Les champs facultatifs absents ne sont pas inventés ; Period reste le texte du CV, sans date ou durée calculée.

Chaque champ soumis est une référence : field, segmentIndex, start et length. L'index de segment commence à 1 ; start commence à 0 et length compte les unités UTF-16, comme les indices de string .NET ou JavaScript. La sélection doit rester dans son segment, compter 1–1 000 caractères et ne pas couper une paire de substitution Unicode. Une sélection de blanc seul, un champ inconnu, doublonné ou obligatoire absent est refusé. Les champs d'un même fait peuvent référencer des segments différents.

Le résultat contient la valeur exacte extraite et un excerpt autour de la sélection, d'environ 120 caractères de chaque côté, sans couper une paire Unicode. Pour vérifier davantage de contexte, consulter l'extraction et le CV d'origine : cet extrait limité ne remplace pas le document complet. Les champs conservent leurs positions, et le fait conserve extractionId, sourceSha256, engineVersion et unitKind. L'extraction source est immuable dans l'application.

## Endpoints

Toutes les routes sont authentifiées et limitées au propriétaire du document. Les en-têtes OwnerId et champs libres d'identité ne choisissent jamais le propriétaire. Les réponses héritent de no-store et nosniff. Un identifiant absent, un fait d'un autre CV ou d'un autre propriétaire répond 404. Une création ou liste exige une extraction courante déjà présente.

| Méthode | Route | Résultat |
|---|---|---|
| POST | `/api/v1/documents/resumes/{resumeId}/facts` | 201 nouvelle annotation, 200 annotation identique déjà présente |
| GET | `/api/v1/documents/resumes/{resumeId}/facts/{factId}` | Fait avec ses champs, contexte et revue |
| GET | `/api/v1/documents/resumes/{resumeId}/facts?decision=Approved&page=1&pageSize=20` | Tableau paginé des seuls faits approuvés |
| POST | `/api/v1/documents/resumes/{resumeId}/facts/{factId}/review` | Décision finale Approved ou Rejected |

Le filtre decision peut être omis pour tout voir ou valoir Pending, Approved ou Rejected. Page : 1–1 000 000 ; pageSize : 1–100. Ordre de création puis ID ; ni total ni instantané entre pages sous revues concurrentes. Les faits conservent la version d'extraction à laquelle ils se rapportent, y compris lors d'une future version du parseur.

Les écritures acceptent au plus 16 Kio de corps HTTP et partagent une limite de 30 requêtes par fenêtre fixe d'une minute pour l'instance, sans file d'attente. Le quota inclut les demandes invalides et répétées et reste en mémoire. Un CV accepte au plus 200 faits, toutes versions d'extraction et décisions confondues. La limite ne bloque pas une reprise d'une annotation déjà enregistrée. Dépassement de ce plafond : 409 ; quota temporel : 429 ; référence/schéma invalide : 400 ; authentification absente/fausse : 401.

L'identité d'une annotation inclut extraction, catégorie et références de champs triées par nom. Réordonner les champs dans une répétition ne crée pas de doublon. La création est sérialisée par CV dans PostgreSQL, avec un index unique sur extraction/fingerprint ; huit créations simultanées identiques produisent une seule ligne. Une reprise après revue retourne la même décision, sans réouvrir le fait.

## Revue

Corps : `{"decision":"Approved","note":"Contrôlé dans le CV original"}` ou Rejected. Note facultative, au plus 1 000 caractères. Une décision finale conserve reviewedAtUtc, reviewedByOwnerId et la note. Le propriétaire authentifié est l'auteur de la revue ; le jeton local ne constitue pas une vérification d'identité externe ou de diplôme.

Répéter exactement la même décision et la même note renvoie 200 sans changer la date. Une décision opposée ou une note modifiée après décision renvoie 409. null et chaîne vide restent deux notes distinctes pour cette règle de répétition. Deux décisions concurrentes opposées ne peuvent pas s'écraser : une seule réussit, grâce au verrou de ligne transactionnel.

Les décisions sont finales dans cette tranche. Pour corriger une mauvaise sélection, créer une autre annotation sourcée ; aucun endpoint de suppression, révocation ou remplacement d'un fait approuvé n'est ajouté. La consultation Approved peut donc présenter plusieurs faits que le propriétaire devra concilier avant un futur transfert. Le statut de l'extraction brute PendingReview n'est pas changé par la revue d'un fait particulier.

## Exemple manuel

Après l'import et l'[extraction](documents-extraction.md), utiliser le terminal où le token est configuré :

```powershell
$resumeId = Read-Host 'Identifiant du CV extrait'
$documentHeaders = @{ Authorization = "Bearer $env:Documents__LocalAuth__Token" }
$text = Invoke-RestMethod -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/extraction" -Headers $documentHeaders
$text.segments | Format-Table index, text -Wrap
Invoke-RestMethod -Uri 'http://localhost:5005/api/v1/documents/fact-schema' -Headers $documentHeaders

# Sélection explicite dans le texte inspecté, sans valeur libre ajoutée au CV.
$kind = Read-Host 'Catégorie (Skill, Experience, Education ou Certification)'
$field = Read-Host 'Champ obligatoire de cette catégorie (Name, Title ou Degree)'
$segmentIndex = [int](Read-Host 'Index du segment')
$start = [int](Read-Host 'Position de début UTF-16, base zéro')
$length = [int](Read-Host 'Longueur UTF-16 du passage')
$annotation = @{
    kind = $kind
    fields = @(@{ field = $field; segmentIndex = $segmentIndex; start = $start; length = $length })
} | ConvertTo-Json -Depth 6
$fact = Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/facts" `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' -Body $annotation
$fact | ConvertTo-Json -Depth 8
```

Après inspection du champ, du contexte et de l'original, décider explicitement ; rejeter une sélection qui affirme ce que le CV nie :

```powershell
$decision = Read-Host 'Décision après contrôle : Approved ou Rejected'
$note = Read-Host 'Note de revue (facultative)'
$review = @{ decision = $decision; note = $note } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/facts/$($fact.id)/review" `
    -Headers $documentHeaders -ContentType 'application/json; charset=utf-8' -Body $review
Invoke-RestMethod -Uri "http://localhost:5005/api/v1/documents/resumes/$resumeId/facts?decision=Approved&page=1&pageSize=20" -Headers $documentHeaders
```

Ces commandes ne sont pas exécutées automatiquement. Aucun CV réel utilisateur n'a été annoté pendant le développement.

## Transfert vers Profile

La consultation Approved fournit une représentation structurée avec preuve et revue ; **elle ne transfère rien automatiquement vers Profile**. Depuis 86.2c.1, un [POST explicite d'import](documents-profile-import.md) transfère les compétences approuvées au profil lié côté serveur, avec canal authentifié, reçu durable et préservation des données existantes.

Le [mapping 86.2c.2a](documents-profile-mapping.md) prépare les champs requis et la confirmation explicite des dates pour les expériences, diplômes et certifications. Le [transfert 86.2c.2b](documents-profile-import.md) utilise ensuite ce mapping sur demande explicite, avec reçu durable et conciliation sans écrasement. Un fait Approved peut rester incomplet pour un contrat Profile exigeant, par exemple, une date ou une entreprise : ces champs ne seront pas fabriqués. La proposition automatique de données par des agents viendra avec les phases IA ; la présente version fournit la structuration et la revue manuelles sourcées.
