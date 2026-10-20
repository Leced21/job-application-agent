# Relances manuelles — règles 87.4

Règles définies avant implémentation. Aucun message ni rappel externe n'est envoyé. Une liste des échéances à traiter constitue le rappel local ; les agents et notifications asynchrones restent dans leurs phases respectives.

- Une candidature Submitted, InReview ou Interview peut avoir **une seule relance Planned** à la fois. Après réalisation ou annulation, une nouvelle relance est possible ; les précédentes restent dans l'historique.
- Une planification ou reprogrammation exige une date UTC comprise entre maintenant et maintenant + 365 jours, un canal connu et une note de 1 à 1 000 caractères. Les dates sont conservées à la précision PostgreSQL (microseconde).
- Une réalisation exige confirmation explicite, date réelle UTC entre la dernière planification/reprogrammation et maintenant, canal réellement utilisé et compte rendu de 1 à 1 000 caractères. Elle peut précéder l'échéance, mais ne signifie jamais qu'un message a été envoyé par le backend.
- Une annulation manuelle exige confirmation et motif de 1 à 1 000 caractères. Completed et Cancelled sont définitifs.
- Offer, Rejected et Withdrawn annulent la relance Planned dans le même événement métier ; aucune mutation séparée ni tâche de fond n'est nécessaire. Les notes restent possibles sur les dossiers clos.
- Toutes les écritures exigent le propriétaire authentifié et la révision exacte du dossier, sous verrou SQL. Un événement de relance incrémente cette révision sans changer le statut de candidature.
- La liste des relances dues est paginée, filtrée par propriétaire et calculée selon l'heure UTC du serveur, triée par échéance puis identifiant de candidature. Elle ne modifie aucun état. Les dossiers clos sont exclus.

Le détail expose les relances et leurs événements ; une projection SQL du rappel en attente est mise à jour dans la même transaction que l'historique. Les historiques antérieurs, sans relances, restent lisibles. La capacité de 1 000 événements avec réserve pour le retrait est conservée ; la clôture ne consomme pas un événement supplémentaire pour annuler une relance.

## Migration et routes

Appliquer **20261016070757_AddApplicationFollowUpReminders** dans la base Applications avant de relancer l'API :

```powershell
dotnet ef database update --project src/services/Applications/JobApplicationAgent.Applications.Infrastructure --startup-project src/services/Applications/JobApplicationAgent.Applications.Api --context ApplicationsDbContext
```

La migration ajoute quatre colonnes facultatives de projection, un index partiel propriétaire/échéance et une contrainte de cohérence. Les dossiers antérieurs commencent sans relance ; aucune relance n'est créée automatiquement. Le rollback est refusé dès qu'un historique de relance existe, même après réalisation ou annulation. La connexion et le jeton propriétaire Applications habituels sont utilisés ; aucune configuration de notification ou nouvelle migration Documents n'est requise. Seules des bases de test éphémères ont été migrées pendant le développement.

Arrêter l'API pour changer de version de schéma. Lors d'une descente sur plusieurs migrations, EF peut avoir retiré une couche récente avant qu'une protection plus ancienne bloque la suite ; ne relancer les binaires courants qu'après avoir réappliqué toutes leurs migrations. Le test du rollback 87.3 rétablit ainsi le schéma courant avant la relecture.

| Méthode et route sous `/api/v1/applications` | Corps / rôle |
| --- | --- |
| GET `/follow-ups/due?page=1&pageSize=20` | Relances Planned échues à l'heure serveur ; page de 1 à 1 000 000 et taille de 1 à 100 |
| POST `/{id}/follow-ups` | `{expectedRevision, dueAtUtc, channel, note}` ; crée une relance avec UUID serveur |
| POST `/{id}/follow-ups/{followUpId}/reschedule` | Même corps ; conserve l'identifiant et l'ancien plan dans les événements |
| POST `/{id}/follow-ups/{followUpId}/complete` | `{expectedRevision, confirmed:true, occurredAtUtc, channel, outcome}` ; déclaration d'une relance déjà réalisée manuellement |
| POST `/{id}/follow-ups/{followUpId}/cancel` | `{expectedRevision, confirmed:true, reason}` ; annulation explicite |

Les écritures sont limitées à 8 Kio et partagent les 60 écritures/minute de l'API. Les canaux sont Email, JobBoard, CompanyWebsite, Phone, InPerson et Other. Les dates doivent porter le suffixe UTC `Z` ; la borne de planification est évaluée à la microseconde. Notes, comptes rendus et motifs ont au maximum 1 000 caractères utiles. Le canal réellement utilisé peut différer du canal prévu.

Les réponses d'écriture sont les détails à jour du dossier. GET détail expose `followUps` et `pendingFollowUp`. Chaque événement concerné porte un `followUp` ; la clôture du dossier y conserve également le motif d'annulation automatique. La liste des échéances ne charge pas l'historique JSON : elle fournit identifiant de candidature, titre, entreprise, statut, révision, identifiant de relance, échéance, canal et note. `asOfUtc` indique l'instant serveur utilisé pour la sélection.

Codes : 400 pour dates/canaux/textes/pagination invalides, 401 pour authentification, 404 pour candidature absente ou autre propriétaire, 409 pour révision, confirmation, état de dossier ou relance incompatible, 429 pour la limite d'écritures. Un identifiant de relance qui n'est pas celui actuellement en attente produit 409. Après réponse perdue ou conflit, relire le dossier avant une nouvelle action : une ancienne révision ne sera pas rejouée.

```powershell
$headers = @{ Authorization = "Bearer $env:Applications__LocalAuth__Token" }
$id = '<UUID candidature Submitted, InReview ou Interview>'
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id" -Headers $headers
$dueAtUtc = [DateTime]::UtcNow.AddDays(7).ToString('o')
$body = @{ expectedRevision = $application.revision; dueAtUtc = $dueAtUtc; channel = 'Email'; note = 'Demander un point sur ma candidature' } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/follow-ups" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
$followUpId = $application.pendingFollowUp.plan.id
Invoke-RestMethod 'http://localhost:5007/api/v1/applications/follow-ups/due?page=1&pageSize=20' -Headers $headers
```

Après avoir réellement relancé le recruteur, relire le dossier et déclarer l'action :

```powershell
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id" -Headers $headers
$occurredAtUtc = '<date réelle UTC de la relance>'
$body = @{ expectedRevision = $application.revision; confirmed = $true; occurredAtUtc = $occurredAtUtc; channel = 'Phone'; outcome = 'Appel réalisé, retour attendu' } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/follow-ups/$followUpId/complete" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

La liste est un suivi local à consulter. Il n'existe ni worker de rappel, ni email/SMS/push, ni suggestion IA dans ce lot. Le suivi reste utilisable pendant une panne des sources Job/Profile/Documents. L'interface et les traitements asynchrones seront intégrés dans leurs phases prévues.
