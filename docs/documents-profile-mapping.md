# Confirmation des dates et aperçu Profile — 86.2c.2a

Ce parcours prépare le mapping des expériences, diplômes et certifications. Une confirmation ne déclenche aucun appel réseau ni aucune modification du profil. Depuis [86.2c.2b](phase-86-2c2b.md), le [POST explicite d'import](documents-profile-import.md) peut ensuite transférer le fait confirmé avec sa provenance, après migration et configuration de Profile.

## Preuves et confirmation

Le fait doit être Approved, revu par le propriétaire authentifié et contenir les sélections source suivantes :

| Catégorie | Champs obligatoires sélectionnés dans le texte | Dates déclarées explicitement |
|---|---|---|
| Experience | Title (150 max), Company (200 max), Period | StartDate et IsCurrent ; EndDate obligatoire si terminé |
| Education | Degree (150 max), Institution (200 max), Period | StartDate et IsCurrent ; EndDate obligatoire si terminé |
| Certification | Name (200 max), Issuer (200 max), Period | IssueDate ; ExpirationDate facultative |

Period reste copié verbatim, même lorsque sa précision se limite à une année. **Aucune date n'est calculée à partir de ce champ.** Le propriétaire doit connaître et saisir les dates exactes au format YYYY-MM-DD, en contrôlant ses justificatifs. Une année ou un mois seul est refusé ; ne pas compléter arbitrairement au premier ou au dernier jour. Si la date exacte reste inconnue, ne pas confirmer le mapping.

Les dates portent la provenance `DatesOrigin: OwnerConfirmed` et sont distinguées des preuves CV. Le backend vérifie la structure et la cohérence, pas la vérité de ces déclarations. Une note explicative non blanche de 1 à 1 000 caractères et `Confirmed: true` sont obligatoires. Une date de fin doit être postérieure ou égale au début ; une expiration doit être postérieure ou égale à l'émission. IsCurrent doit être fourni explicitement pour Experience/Education. Un parcours en cours n'a pas de date de fin ; un parcours terminé doit en avoir une. Les champs de dates d'une autre catégorie sont refusés. Une expiration null signifie non renseignée, sans déduction de validité permanente.

Les libellés, la description et l'identifiant de certification éventuels restent exclusivement ceux du fait approuvé. Aucune saisie de remplacement de ces libellés n'est acceptée ici. S'il manque une entreprise, une institution, un émetteur ou la période, sélectionner ces données dans une nouvelle annotation complète, puis la revoir. Une donnée absente du CV n'est pas fabriquée. Les champs non renseignés (localisation, spécialité, URL du certificat) restent null dans l'aperçu.

## Routes

Les deux routes exigent le Bearer propriétaire Documents et vérifient ownerId, resumeId et factId :

- `POST /api/v1/documents/resumes/{resumeId}/facts/{factId}/profile-mapping` : confirme et conserve les dates, renvoie l'aperçu 200.
- `GET /api/v1/documents/resumes/{resumeId}/facts/{factId}/profile-mapping` : relit l'aperçu confirmé, 404 si absent ou inaccessible.

Le DTO inclut la preuve source, son SHA-256, les dates déclarées, leur auteur et horodatage serveur, et `TargetFields`, un aperçu des champs du contrat Profile. Le propriétaire et l'horodatage ne sont jamais fournis par le client. Les champs du corps sont StartDate, EndDate, IsCurrent, IssueDate, ExpirationDate, Confirmed et ConfirmationNote. Tous les champs non applicables doivent être omis ou null.

Le POST est limité à 16 KiB et partage le limiteur des faits (30 écritures/minute). Les réponses sont no-store. 401 : identité invalide ; 404 : fait/CV absent ou étranger ; 400 : JSON/date mal formé ; 422 : catégorie, revue, preuves ou dates insuffisantes/incohérentes ; 409 : une confirmation différente existe déjà ; 429 : limiteur.

## Exemple interactif

Après migration, avec `$resumeId`, `$fact` et `$documentHeaders` du [guide de revue](documents-facts.md), consulter d'abord l'original et les preuves du fait approuvé. Cet exemple demande les valeurs au propriétaire ; il ne propose aucune date :

```powershell
$dates = @{}
switch ($fact.kind) {
    { $_ -in 'Experience', 'Education' } {
        $dates.startDate = Read-Host 'Date de début exacte vérifiée (YYYY-MM-DD)'
        $answer = Read-Host 'Parcours actuellement en cours ? OUI ou NON'
        if ($answer -ceq 'OUI') { $dates.isCurrent = $true }
        elseif ($answer -ceq 'NON') {
            $dates.isCurrent = $false
            $dates.endDate = Read-Host 'Date de fin exacte vérifiée (YYYY-MM-DD)'
        } else { throw 'Statut explicite requis' }
    }
    'Certification' {
        $dates.issueDate = Read-Host "Date exacte d'émission vérifiée (YYYY-MM-DD)"
        $expiration = Read-Host "Date exacte d'expiration vérifiée, ou vide si non renseignée"
        if ($expiration) { $dates.expirationDate = $expiration }
    }
    default { throw 'Catégorie non prise en charge par ce mapping' }
}
$dates.confirmationNote = Read-Host 'Justificatif ou contrôle effectué pour confirmer ces dates exactes'
$dates.confirmed = (Read-Host 'Confirmer explicitement ces dates et le statut : OUI') -ceq 'OUI'
$mappingUri = "http://localhost:5005/api/v1/documents/resumes/$resumeId/facts/$($fact.id)/profile-mapping"
$mapping = Invoke-RestMethod -Method Post -Uri $mappingUri -Headers $documentHeaders `
    -ContentType 'application/json; charset=utf-8' -Body ($dates | ConvertTo-Json)
$mapping | ConvertTo-Json -Depth 10
Invoke-RestMethod -Uri $mappingUri -Headers $documentHeaders
```

## Persistance, reprise et limites

La confirmation est finale, conservée sur le fait avec JSONB, propriétaire et date UTC. Reposter le même corps normalisé conserve la première date. Les espaces autour de la note sont supprimés ; les autres différences, y compris la note, donnent 409. Le verrou PostgreSQL de ligne empêche deux confirmations concurrentes différentes de s'écraser. L'idempotence compare les valeurs JSON, indépendamment du réordonnancement effectué par JSONB.

Pour corriger une confirmation finale, il faut une nouvelle preuve/annotation distincte et une nouvelle revue ; recréer exactement la même sélection sur la même extraction retourne le fait existant. Une nouvelle version du CV permet une nouvelle extraction et une nouvelle revue. Il n'y a pas encore de révocation ni d'édition des confirmations. La suppression du CV supprime aussi la confirmation via les cascades existantes. Les droits locaux ne remplacent pas une authentification multi-utilisateur.

Migration **20261015152633_AddConfirmedProfileDates** : trois colonnes nullable sur resume_candidate_facts, sans génération de dates ni reprise automatique des anciens faits. À appliquer à Documents avant les nouveaux binaires, connexion configurée hors Git :

```powershell
dotnet ef database update --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --context DocumentsDbContext
```

Le développement n'a migré que les PostgreSQL 17 éphémères des tests. Aucun CV réel, justificatif ou profil utilisateur n'a été traité. Le [canal d'import](documents-profile-import.md) prend désormais en charge les quatre catégories ; pour les catégories datées, il utilise cette confirmation persistée et la conciliation sans écrasement décrite dans le bilan 86.2c.2b.
