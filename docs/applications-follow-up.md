# Suivi manuel — règles 87.3

Règles définies avant implémentation : aucun appel ne transmet de candidature, message ou fichier. Le propriétaire déclare des actions et retours déjà survenus.

| Départ | Arrivée | Condition |
| --- | --- | --- |
| Approved | Submitted | Confirmation explicite de l'envoi manuel des pièces approuvées, date UTC réelle, canal et référence facultative |
| Submitted | InReview, Interview, Offer ou Rejected | Retour recruteur confirmé, date UTC, canal, message |
| InReview | Interview, Offer ou Rejected | Même preuve déclarative |
| Interview | Offer ou Rejected | Même preuve déclarative |
| Submitted, InReview, Interview | Même statut | Nouveau retour sans progression, notamment plusieurs entretiens |
| Submitted, InReview, Interview | Withdrawn | Retrait explicite avec motif, enregistré localement ; aucun message de retrait envoyé |

Offer, Rejected et Withdrawn sont terminaux ; les notes restent autorisées. Un dossier soumis ne retourne pas en Draft et ses pièces ne changent pas. Les doublons restent interdits même pour les statuts terminaux. Les valeurs existantes 1–4 sont conservées ; les nouveaux statuts sont ajoutés à l'enum.

La date de déclaration serveur et la date de l'événement métier sont distinctes. La soumission doit dater de l'approbation ou après, sans être future. Les retours ne peuvent précéder la soumission ou le dernier retour enregistré, ni être futurs. Toutes les dates sont UTC. Canaux : Email, JobBoard, CompanyWebsite, Phone, InPerson, Other. Référence facultative de 500 caractères ; message requis de 2 000 caractères maximum. Une référence est du texte, jamais une URL appelée par le backend.

Chaque action exige la révision exacte et le propriétaire authentifié. Une réponse perdue impose une relecture ; rejouer l'ancienne révision échoue sans dupliquer l'événement. La déclaration n'est pas une preuve externe de livraison ou du contenu réellement envoyé. Elle atteste que le propriétaire déclare avoir envoyé les pièces du dossier approuvé. Aucune connexion à une messagerie ou plateforme, aucun nouveau contrôle Documents requis pour enregistrer cet historique manuel.

Le [lot 87.4](applications-reminders.md) ajoute les relances planifiées et la liste locale des échéances. Les phases 96–97 restent responsables d'une éventuelle soumission automatisée autorisée.

## Migration et utilisation

Appliquer la migration **20261016022042_AddManualApplicationFollowUp** dans la base Applications avant de redémarrer l'API. Elle élargit la contrainte des statuts sans changer les valeurs existantes ni réécrire les dossiers. Les nouveaux événements ajoutent des données facultatives à l'historique JSON ; les anciens historiques restent lisibles. Le rollback est refusé si des statuts ou événements de suivi existent, même si la candidature a ensuite été retirée.

```powershell
dotnet ef database update --project src/services/Applications/JobApplicationAgent.Applications.Infrastructure --startup-project src/services/Applications/JobApplicationAgent.Applications.Api --context ApplicationsDbContext
```

La connexion `ConnectionStrings__ApplicationsDatabase` habituelle est requise. Aucune nouvelle configuration de jeton ni migration Documents n'est nécessaire pour ce lot. Les prérequis de préparation et revue 87.2 restent applicables. Pendant le développement, seules des bases éphémères de test ont été migrées.

| POST sous `/api/v1/applications/{id}` | Corps | Résultat |
| --- | --- | --- |
| `/manual-submission` | `{expectedRevision, confirmed:true, occurredAtUtc, channel, reference?}` | Déclare l'envoi manuel du dossier Approved ; passe en Submitted |
| `/recruiter-updates` | `{expectedRevision, confirmed:true, status, occurredAtUtc, channel, message}` | Ajoute le retour et applique une transition autorisée |

Ces routes utilisent le jeton propriétaire Applications, le filtrage propriétaire, la transaction et le verrou de ligne existants. Les limites restent de 60 écritures/minute ; corps de 8 Kio pour la déclaration et 16 Kio pour les retours. Les valeurs de statuts/canaux sont les noms de l'enum. Les dates doivent porter le suffixe UTC `Z` ; fournir la date réelle de l'envoi ou de la réception du retour, pas une date future d'entretien prévu. Cette dernière peut être mentionnée dans le message, sans être interprétée comme un événement déjà survenu.

Le détail GET conserve les événements et expose `submission` ; les événements de retour portent `recruiterUpdate`. Chaque événement a son auteur et sa date d'enregistrement serveur. La liste accepte désormais les filtres Submitted, InReview, Interview, Offer et Rejected en plus des statuts existants.

```powershell
$headers = @{ Authorization = "Bearer $env:Applications__LocalAuth__Token" }
$id = '<UUID candidature approuvée>'
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id" -Headers $headers
# Uniquement après avoir réellement envoyé les deux pièces approuvées : renseigner la date réelle.
$sentAtUtc = '<date réelle UTC au format 2026-10-16T10:30:00Z>'
$body = @{ expectedRevision = $application.revision; confirmed = $true; occurredAtUtc = $sentAtUtc; channel = 'CompanyWebsite'; reference = 'Référence du dépôt manuel' } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/manual-submission" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

Après réception effective d'un retour, utiliser la révision actuelle, le statut voulu et un message explicite :

```powershell
$receivedAtUtc = '<date réelle de réception UTC>'
$body = @{ expectedRevision = $application.revision; confirmed = $true; status = 'Interview'; occurredAtUtc = $receivedAtUtc; channel = 'Email'; message = 'Invitation à un entretien reçue' } | ConvertTo-Json
$application = Invoke-RestMethod "http://localhost:5007/api/v1/applications/$id/recruiter-updates" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
```

Codes : 400 pour dates/canaux/textes invalides, 401 pour authentification absente/invalide, 404 pour dossier absent ou autre propriétaire, 409 pour révision, confirmation ou transition incompatible, 429 pour la limite d'écritures. Après 409 ou réponse perdue, relire le dossier avant de décider d'une nouvelle action.

Le suivi manuel fonctionne pendant une panne de Job/Profile/Documents : il n'envoie rien et ne certifie pas la validité actuelle d'une offre ou des exports. Après soumission déclarée, l'historique et les références des pièces approuvées constituent le dossier conservé ; toute nouvelle préparation ne doit pas réécrire cette déclaration. La prévention des doublons reste active pour le même propriétaire/profil/offre, même après refus ou offre reçue.
