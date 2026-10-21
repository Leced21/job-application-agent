# Collection — import manuel et préparation d'offres

Collection est un microservice séparé, organisé en Domain, Application, Infrastructure et Api, avec sa propre base PostgreSQL. Il prépare les offres ; Job conserve la propriété des offres utilisées par Matching. Aucune base ni assembly métier Job/Profile/Matching n'est référencée par Collection.

Collection n'extrait pas d'offres à partir d'URL arbitraires et ne fait aucun scraping. Le connecteur Adzuna utilise son API officielle quand l'utilisateur configure ses identifiants ; le connecteur local-json reçoit un export autorisé. Un import doit fournir au moins titre et entreprise ; une URL seule ne suffit pas pour créer une offre structurée. Depuis 85.2b, une action explicite peut publier une révision approuvée vers l'API locale Job. L'import et la revue ne publient rien automatiquement ; aucune candidature n'est envoyée.

## Démarrage local

API : `http://localhost:5004`. PostgreSQL Collection : port local `5435`. Gateway : `/api/v1/collection/**` relaie vers 5004. Les lectures et opérations de préparation exigent une identité ; les imports, collectes, décisions de revue et publications exigent en plus le rôle administrateur configuré. Le service reste local ; ne pas l'exposer avant la fin des contrôles de sécurité.

Les paramètres sont fournis hors Git. Pour créer/réutiliser une base, choisir les identifiants localement et les conserver dans un gestionnaire sûr ; réutiliser les mêmes valeurs pour un volume existant. Les nouvelles valeurs d'environnement ne changent pas le mot de passe d'une base déjà initialisée.

Depuis la racine PowerShell :

```powershell
$env:COLLECTION_DB_USER = Read-Host 'Utilisateur PostgreSQL Collection'
$collectionPassword = Read-Host 'Mot de passe PostgreSQL Collection' -AsSecureString
$env:COLLECTION_DB_PASSWORD = [System.Net.NetworkCredential]::new('', $collectionPassword).Password
docker compose -p job-agent-collection -f docker-compose.collection.yml up -d --wait

$collectionConnection = [System.Data.Common.DbConnectionStringBuilder]::new()
$collectionConnection['Host'] = 'localhost'
$collectionConnection['Port'] = 5435
$collectionConnection['Database'] = 'collection_db'
$collectionConnection['Username'] = $env:COLLECTION_DB_USER
$collectionConnection['Password'] = $env:COLLECTION_DB_PASSWORD
$env:ConnectionStrings__CollectionDatabase = $collectionConnection.ConnectionString

dotnet ef database update --project src/services/Collection/JobApplicationAgent.Collection.Infrastructure --startup-project src/services/Collection/JobApplicationAgent.Collection.Api
# En Development direct, associer le Bearer local à un owner de la liste admin.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Collection__LocalAuth__OwnerId = '<APPLICATIONS_OWNER_ID local>'
$env:Collection__LocalAuth__Token = '<COLLECTION_LOCAL_TOKEN local>'
$env:Collection__Admin__OwnerIds = $env:Collection__LocalAuth__OwnerId
$env:Job__CollectionPublisher__Token = '<JOB_COLLECTION_PUBLISHER_TOKEN local>'
dotnet run --project src/services/Collection/JobApplicationAgent.Collection.Api --launch-profile http
```

La chaîne de connexion n'est pas affichée par ces commandes. La conserver hors des fichiers suivis. Si `COLLECTION_DB_NAME` est personnalisé pour Compose, utiliser la même valeur dans la chaîne de connexion. La factory EF lit `ConnectionStrings__CollectionDatabase` ; il faut la définir dans le terminal d'exécution des commandes EF. L'API échoue au démarrage si sa connexion n'est pas configurée. Aucune migration automatique n'est effectuée au démarrage.

Le fichier de base Compose Collection reste séparé pour ne pas ajouter de variables obligatoires aux autres commandes Compose. Un overlay API permet désormais de démarrer Collection dans le même réseau que Job :

```powershell
Copy-Item .env.example .env
# Remplacer les valeurs d’exemple Collection/RabbitMQ dans .env avant le démarrage.
docker compose --env-file .env -f docker-compose.yml -f docker-compose.collection.yml -f docker-compose.collection-api.yml up -d --build job-db job-api collection-db rabbitmq collection-api
```

Collection est publié sur `127.0.0.1:5004` et appelle Job à `http://job-api:8080/`. RabbitMQ démarre avec le relay désactivé ; activer `Collection__EventTransport__Enabled` uniquement après avoir appliqué les migrations outbox et vérifié la configuration du broker. Le Compose a été validé, sans démarrage de conteneur pendant ce lot.

## Endpoints

| Méthode | Route | Réponse |
|---|---|---|
| POST | `/api/v1/collection/imports` | 201 pour une nouvelle identité, 200 pour un réimport |
| GET | `/api/v1/collection/imports/{id}` | Offre normalisée et provenance, ou 404 |
| GET | `/api/v1/collection/imports?page=1&pageSize=20` | `items`, `totalCount`, `page`, `pageSize`, `totalPages` |

Le POST renvoie `offer`, `isDuplicate`, `contentChanged`, avec Location pour une création. Un GET d'identifiant vide et une pagination invalide donnent 400. Bornes : page 1–1 000 000 et taille 1–100. Le POST accepte au maximum 256 Kio côté serveur. Les validations incluent limites de champs, salaires cohérents/précision de deux décimales, devise de trois lettres si salaire présent, dates UTC et expiration postérieure ou égale à la publication si les deux dates existent.

Toutes les routes Collection exigent un Bearer valide. Les GET sont accessibles à tout utilisateur authentifié ; l'import, la collecte, la revue et la publication requièrent également un `jobagent_owner_id` présent dans `Collection:Admin:OwnerIds`. Pour un appel direct local, demander le jeton sans l'afficher et le placer dans l'en-tête :

```powershell
$tokenSecure = Read-Host 'Jeton Bearer Collection' -AsSecureString
$token = [System.Net.NetworkCredential]::new('', $tokenSecure).Password
$headers = @{ Authorization = "Bearer $token" }
```

Exemple manuel, sur la base locale uniquement :

```powershell
$offer = @{
  title = 'Data Engineer'
  companyName = 'Example Company'
  location = 'Paris'
  workMode = 'Télétravail'
  contractType = 'CDI'
  skills = @('Python', 'pyspark')
  sourceName = 'Manual'
  sourceUrl = 'https://example.invalid/jobs/42?utm_source=manual'
  description = 'Offre saisie manuellement, à vérifier avant publication.'
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri 'http://localhost:5004/api/v1/collection/imports' -Headers $headers -ContentType 'application/json' -Body $offer
Invoke-RestMethod -Uri 'http://localhost:5004/api/v1/collection/imports?page=1&pageSize=20' -Headers $headers
```

L'URL ci-dessus est stockée après normalisation ; elle n'est jamais visitée par Collection. Il est possible d'omettre l'URL, la description, les salaires, dates et compétences. Les champs absents ne sont pas inventés.

## Normalisation et identité

- Espaces normalisés dans titre, entreprise et localisation ; casse visible préservée et `originalTitle` conservé. Aucune géolocalisation ou équivalence de villes n'est inférée.
- WorkMode : valeurs canoniques Unknown, OnSite, Hybrid, Remote. Alias français explicites : présentiel, hybride, télétravail/à distance. Une valeur non reconnue est refusée ; une valeur absente devient Unknown.
- Contrats : mêmes noms canoniques que Job, avec alias CDI, CDD, stage, alternance, indépendant et intérim.
- Compétences **fournies** seulement : trim, déduplication et synonymes connus (Spark/PySpark, K8s/Kubernetes, AWS/Amazon Web Services, Azure/Microsoft Azure, SQL/Python). Pas d'extraction automatique ou de lecture LLM du texte.
- Devise en majuscules ; description avec fins de lignes harmonisées ; aucune conversion de devise ou déduction de périodicité salariale.
- URL HTTP(S) sans identifiants embarqués, hôte/port canonicalisés, fragment et paramètres de suivi connus `utm_*`, `gclid`, `fbclid`, `msclkid` retirés. Chemin, slash final et paramètres métier conservés, sans tri des paramètres qui pourrait changer leur sens.

Avec URL : identité SHA256 de l'URL canonique. Sans URL : identité SHA256 du contenu normalisé et de la source. La publication/expiration font partie du contenu lorsqu'elles sont fournies. Les représentations décimales `50000` et `50000.00` ont la même empreinte. Deux URL différentes restent deux identités, même si leur contenu est identique : cette tranche ne fait pas de rapprochement flou entre sources.

Une contrainte unique PostgreSQL garantit une seule ligne par identité. Chaque réimport augmente atomiquement `seenCount` et conserve un `lastSeenAtUtc` monotone, y compris en concurrence ou si l'horloge recule. `firstCollectedAtUtc` et le contenu initial sont conservés. Les dates de collecte proviennent du serveur, jamais du client.

Si le contenu d'une même URL a changé, `contentChanged` vaut true dans la réponse et `offer.hasPendingChanges` reste enregistré. Depuis 85.2a, chaque contenu normalisé distinct est conservé dans une révision complète, sans écraser l'import initial. Le marqueur historique `hasPendingChanges` reste vrai après revue : il signale une variation observée par rapport à l'original, pas le nombre de révisions en attente. Les décisions de revue se consultent dans les révisions.

L'expiration est calculée à la lecture à partir de `expiresAtUtc` : Expired si la date est atteinte, PendingReview sinon. L'absence de date ne prouve pas que l'offre est toujours en ligne. Il n'y a ni surveillance de plateforme ni changement automatique de statut dans Job.

## Suite de la phase 85

1. **85.2a et 85.2b livrés** : révisions complètes, revue explicite et publication idempotente vers Job. Utiliser le nouvel endpoint de publication, pas un retry du POST CRUD Job existant.
2. **85.3 livré** : connecteur d'export JSON local autorisé, lots atomiques, limites et provenance. Aucun scraping générique à partir d'une URL arbitraire.
3. **85.4 livré** : événements d'acquisition versionnés et outbox transactionnelle, décrits dans le [contrat](collection-events.md). Le transport et la consommation restent à implémenter en phase 89. Aucun événement n'est actuellement envoyé à Matching ; celui-ci continue à lire les offres canoniques de Job.
4. **Adzuna ajouté comme source continue** : API officielle de recherche, avec une synchronisation périodique configurable et attribution dans l'interface. Cette intégration nécessite une clé personnelle et reste soumise aux limites et conditions du compte Adzuna.

Voir le [détail de la phase 85.5](phase-85-5-adzuna.md) pour les limites, la configuration et la validation.

La phase 85 complète n'est donc pas déclarée terminée. Les événements de collecte sont enregistrés en outbox ; leur livraison et leur consommation restent à réaliser en phase 89. L'authentification, la politique administrateur et l'identité du validateur persistée ont été ajoutées en phase 91. L'UI de revue reste à développer ; Adzuna ne fournit qu'un extrait de description.

## Révisions et revue — 85.2a

Appliquer les migrations Collection avec la commande `dotnet ef database update` indiquée plus haut, avant de relancer les nouveaux binaires. La migration `20261015114728_AddOfferRevisions` crée `offer_revisions` et reprend chaque import existant comme révision Pending. Les variantes observées avant cette migration, qui n'étaient pas stockées par 85.1, ne peuvent pas être reconstituées : les réimporter si nécessaire. La migration `20261018130853_AddRevisionReviewer` ajoute l'identité nullable du validateur ; les anciennes décisions restent sans identité connue. Les tests migrent uniquement leurs bases Docker éphémères.

| Méthode | Route | Réponse |
|---|---|---|
| GET | `/api/v1/collection/imports/{offerId}/revisions?page=1&pageSize=20` | Tableau de révisions, dates décroissantes puis ID ; taille 1–100 |
| POST | `/api/v1/collection/imports/{offerId}/revisions/{revisionId}/review` | Révision après décision ; corps `{"decision":"Approved"}` ou `{"decision":"Rejected"}` |

Chaque révision expose `id`, `offerId`, `fingerprint`, `originalTitle`, `content` (tous les champs normalisés), `normalizationVersion`, `collectedAtUtc`, `decision`, `reviewedAtUtc` et `reviewedByOwnerId`. Un contenu déjà observé réutilise sa révision et sa décision ; un contenu nouveau est Pending. Sans URL, un contenu changé reste une autre identité selon les règles 85.1.

Une décision est finale : répéter la même décision renvoie 200 avec la date initiale ; demander la décision opposée renvoie 409. Une révision expirée ne peut pas être approuvée (409), mais peut être rejetée. IDs vides, décision ou pagination invalides : 400 ; import/révision absents ou révision d'un autre import : 404. La revue accepte au plus 4 Kio. Les verrous PostgreSQL sérialisent les décisions concurrentes ; l'index unique `(OfferId, Fingerprint)` et le verrou d'import protègent les contenus concurrents.

Plusieurs révisions distinctes peuvent être approuvées : ce sont des décisions sur des instantanés, pas la désignation d'une version courante. La publication cible explicitement une révision approuvée et vérifie son expiration à nouveau. La revue ne modifie ni le contenu initial ni son statut historique PendingReview/Expired, et ne déclenche aucun appel vers Job. La liste n'expose pas de total et sa pagination n'est pas un instantané transactionnel entre deux requêtes.

La route de revue exige un propriétaire autorisé par `Collection:Admin:OwnerIds`. `reviewedAtUtc` et `reviewedByOwnerId` sont enregistrés ensemble avec la décision. Une répétition identique renvoie l'auteur initial sans remplacer son identité ; une décision opposée reste refusée. Les décisions précédant la migration ont un `reviewedByOwnerId` nul.

Exemple après inspection du contenu reçu :

```powershell
$importId = '<identifiant de l’import>'
$revisions = Invoke-RestMethod -Uri "http://localhost:5004/api/v1/collection/imports/$importId/revisions" -Headers $headers
$revisions | ConvertTo-Json -Depth 8
# Choisir explicitement l'identifiant de la révision inspectée.
$revisionId = '<identifiant de la révision vérifiée>'
Invoke-RestMethod -Method Post -Uri "http://localhost:5004/api/v1/collection/imports/$importId/revisions/$revisionId/review" -Headers $headers -ContentType 'application/json' -Body '{"decision":"Approved"}'
```

## Publication vers Job — 85.2b

Avant le premier démarrage, appliquer la nouvelle migration **Job** `20261015115904_AddCollectionPublications` sur la base Job configurée. Collection doit déjà être au schéma 85.2a. Les tests ont migré uniquement des bases Docker éphémères, aucune base persistante locale.

```powershell
# Utiliser la connexion Job de développement configurée pour la commande EF.
# Si elle vient de l'environnement : ConnectionStrings__JobDatabase (ne pas l'afficher).
dotnet ef database update --project src/services/Job/JobApplicationAgent.Job.Infrastructure --startup-project src/services/Job/JobApplicationAgent.Job.Api
dotnet run --project src/services/Job/JobApplicationAgent.Job.Api --launch-profile http
# Dans un autre terminal avec la connexion Collection configurée :
dotnet run --project src/services/Collection/JobApplicationAgent.Collection.Api --launch-profile http
```

`Services:Job:BaseUrl` vaut `http://localhost:5002/` dans Collection. La variable `Services__Job__BaseUrl` permet de la remplacer pour un déploiement local différent. Cette URL fixe désigne le service ; les URL des offres ne sont jamais utilisées comme destinations réseau. HTTP(S) sans credentials, query ou fragment ; redirections désactivées, timeout de 10 secondes par appel, réponse limitée à 64 Kio. Il n'y a pas de retry automatique.

La publication vers le catalogue Job s'authentifie avec un jeton de service dédié (`JOB_COLLECTION_PUBLISHER_TOKEN` dans Compose, lié à `Job:CollectionPublisher:Token`). Ce secret doit être distinct du Bearer administrateur Job et du jeton utilisateur Collection. Il autorise uniquement l'endpoint de publication ; les autres routes Job ne l'acceptent pas.

Après inspection et approbation d'une révision :

```powershell
# Réutiliser les identifiants explicites de l'exemple de revue.
$receipt = Invoke-RestMethod -Method Post -Uri "http://localhost:5004/api/v1/collection/imports/$importId/revisions/$revisionId/publish" -Headers $headers
$receipt | Format-List
# Contrôler l'offre canonique créée :
Invoke-RestMethod -Uri "http://localhost:5002/api/v1/jobs/$($receipt.jobId)"
```

Le POST Collection renvoie 200 avec `offerId`, `revisionId`, `jobId`, `fingerprint`, `publishedAtUtc`, aussi bien à la création qu'à la reprise. Une révision Pending ou Rejected, une expiration atteinte avant première publication ou une révision différente déjà publiée donnent 409 ; IDs vides : 400 ; révision absente ou d'un autre import : 404. Les erreurs réseau/timeout et indisponibilités Job donnent 503 ; réponse JSON/contrat invalide : 502. Après un 503, le résultat peut déjà être enregistré côté Job : reprendre **le même endpoint avec la même révision**, sans appeler le POST CRUD Job.

Collection consulte d'abord le reçu Job. Un reçu correspondant est renvoyé même si la date d'expiration est ensuite atteinte : c'est la récupération d'une publication passée, pas une nouvelle création. Sans reçu, Collection puis Job vérifient l'expiration. Job sérialise les publications d'une identité Collection au moyen d'un verrou transactionnel PostgreSQL et enregistre l'offre et son reçu atomiquement. La première publication crée une offre Discovered ; les suivantes renvoient le même reçu. Une autre révision ou un corps différent pour la même identité renvoie 409. Les modifications ultérieures par CRUD Job ne sont jamais écrasées par une reprise.

Le reçu est conservé même après suppression de l'offre Job. Une reprise renvoie alors l'ancien identifiant et le GET Job répond 404 : elle ne ressuscite pas l'offre. Ne pas supprimer les reçus ou rétrograder leur migration tant que des reprises sont possibles. Aucun statut Published ni reçu local Collection n'est enregistré : le reçu Job est la référence, ce qui évite une double écriture entre deux bases.

Contrat de réception interne Job :

| Méthode | Route | Résultat |
|---|---|---|
| GET | `/api/v1/jobs/collection-publications/{offerId}` | 200 reçu, 404 absent, 400 ID vide |
| POST | `/api/v1/jobs/collection-publications` | 201 première création, 200 reprise exacte, 409 conflit/expiration, 400 validation |

Le corps du POST contient `offerId`, `revisionId`, `fingerprint`, `job` (contrat CreateJobCommand existant), `skills` et `expiresAtUtc`. Sa taille est limitée à 256 Kio. Le reçu stocke le corps validé en JSONB et son SHA256 calculé par Job ; l'empreinte Collection est une identité opaque du contenu normalisé. Les champs compatibles deviennent ceux de l'offre Job, avec `sourceName` mappé vers `source`. Compétences et expiration sont préservées dans le reçu, car le modèle Job actuel n'a pas ces champs : elles ne deviennent pas des critères structurés Matching et ne provoquent pas de désactivation automatique après publication. Une synchronisation ultérieure devra définir ce comportement explicitement.

Les deux APIs exigent une identité. Les opérations de mutation Collection nécessitent un propriétaire administrateur configuré ; le endpoint de réception Job valide un Bearer de service dédié, distinct des jetons utilisateur et administrateur. L'UI de revue et les mises à jour de versions publiées restent à développer. Les tests entre deux API utilisent deux bases indépendantes et le transport HTTP TestServer réel ; aucune publication sur une base utilisateur ni candidature réelle n'a été effectuée.

## Connecteur d’export JSON local — 85.3

Le premier connecteur choisi est `local-json`, pour un export dont l'utilisateur dispose du droit d'importation. Il ne vérifie pas les droits auprès d'une plateforme et ne prétend pas être une API officielle. Il n'ouvre aucun fichier serveur ni URL : le client envoie le contenu JSON. L'import manuel unitaire reste disponible.

| Méthode | Route | Réponse |
|---|---|---|
| GET | `/api/v1/collection/sources` | Sources installées : id, name, transport et maxOffersPerBatch |
| POST | `/api/v1/collection/sources/local-json/collect` | Bilan du lot validé et enregistré |

Format : `{"offers":[{...}, {...}]}`, chaque entrée suivant les champs du contrat d'import manuel. `sourceName` est remplacé par `LocalJsonExport` : une déclaration dans un fichier ne suffit pas à attester une provenance de plateforme. Titre original, URL normalisée, dates de collecte et contenu normalisé restent conservés par le parcours existant. Sans URL, la déduplication tient compte de cette source, selon les règles habituelles.

Limites : 1 à 100 entrées non nulles, 1 Mio de corps HTTP côté serveur, 30 secondes de traitement et 10 requêtes de collecte par minute pour l'instance API entière. La limite de requêtes est partagée entre appels de collecte et inclut les appels invalides ; elle est en mémoire, sans file d'attente, et se réinitialise au redémarrage. Elle ne remplace pas les quotas d'une future API externe. Les autres endpoints ne consomment pas ce quota.

Avant toute écriture, toutes les offres sont validées et normalisées. Une offre invalide donne 400 pour le lot entier, avec un chemin comme `Offers[1].SalaryMin`. Entrées absentes/nulles, lot vide ou plus de 100 offres : 400. Source inconnue : 404. Corps trop volumineux : 413 côté serveur HTTP ; quota atteint : 429 ; délai de traitement dépassé : 503. Les erreurs internes restent génériques (500). Les annulations du client sont propagées.

Les écritures du lot utilisent une seule transaction : les créations, révisions, compteurs et dates sont toutes validées ensemble. Une panne avant commit annule le lot entier. L'ordre des verrous suit les clés de déduplication pour éviter les interblocages entre lots inversés ; le bilan revient dans l'ordre du fichier. La normalisation, la déduplication, les révisions et l'expiration déclarée sont celles de 85.1/85.2 ; aucune logique parallèle de création d'offre n'est ajoutée.

Le bilan 200 contient `sourceId`, `received`, `created`, `duplicates`, `changed`, `items`. Chaque item contient `index` (base zéro), `offerId`, `isDuplicate`, `contentChanged`. `created + duplicates = received` ; `changed` compte les observations différentes de l'import initial, pas nécessairement les nouvelles révisions. Deux entrées de même identité dans un même fichier sont deux observations d'une même offre. Réenvoyer un fichier incrémente les compteurs de réobservation, mais ne crée ni identité ni révision identique supplémentaires. Ce n'est pas une idempotence des compteurs au niveau d'un numéro de lot.

Si la connexion se coupe au commit ou avant réception du bilan, le résultat peut déjà être entièrement enregistré : réenvoyer le même fichier réutilise les mêmes identités. Il n'y a pas de journal durable de lots ni de retry automatique. La collecte ne valide aucune révision et ne publie aucune offre vers Job ; utiliser ensuite les endpoints de revue et de publication explicites.

Exemple depuis la racine du dépôt, API Collection démarrée et base aux migrations 85.1/85.2a (aucune nouvelle migration pour 85.3) :

```powershell
Invoke-RestMethod -Uri 'http://localhost:5004/api/v1/collection/sources' -Headers $headers
# Fichier d'exemple contenant deux offres fictives, sans aucun téléchargement d'URL.
$exportJson = Get-Content -Raw -Encoding UTF8 -LiteralPath '.\samples\collection\local-export.example.json'
$batch = Invoke-RestMethod -Method Post `
    -Uri 'http://localhost:5004/api/v1/collection/sources/local-json/collect' `
    -Headers $headers `
    -ContentType 'application/json; charset=utf-8' `
    -Body $exportJson
$batch | Select-Object sourceId, received, created, duplicates, changed
$batch.items | Format-Table index, offerId, isDuplicate, contentChanged
```

Pour importer vos données, fournir un fichier de même forme provenant d'une source autorisée. Les URL seules ne sont pas extraites, les compétences absentes ne sont pas inventées et aucune disponibilité en ligne n'est inférée. Les autres connecteurs implémenteront le port `IOfferSourceConnector` avec leurs propres contrats, accès et quotas ; le chemin commun conserve la validation et la persistance transactionnelle.

## API Adzuna et synchronisation continue

Le connecteur `adzuna` appelle l'API REST officielle de recherche d'offres. Il est limité aux résultats France et demande `app_id` et `app_key` Adzuna. Créez votre clé depuis le portail développeur Adzuna et conservez-la uniquement dans le fichier `.env` local ignoré par Git :

```dotenv
ADZUNA_APP_ID=votre_app_id
ADZUNA_APP_KEY=votre_app_key
ADZUNA_RESULTS_PER_PAGE=20
ADZUNA_SYNC_ENABLED=true
ADZUNA_SYNC_INTERVAL_MINUTES=360
ADZUNA_SEARCH_1_WHAT=developpeur dotnet
ADZUNA_SEARCH_1_WHERE=France
ADZUNA_SEARCH_2_WHAT=ingenieur logiciel
ADZUNA_SEARCH_2_WHERE=France
```

La synchronisation prend la première page de chaque recherche, triée par date, immédiatement après le démarrage de Collection puis à l'intervalle configuré (60 à 1440 minutes). Chaque recherche peut renvoyer jusqu'à 50 résultats par cycle ; la limite par défaut est 20. Le nombre de recherches est limité à dix. Une annonce est normalisée et dédupliquée par son URL Adzuna, puis entre dans le circuit de collecte : elle n'est ni approuvée ni publiée automatiquement dans Job. Une personne administratrice doit encore la revoir et déclencher sa publication. La source ne garantit pas que chaque annonce provienne directement de HelloWork ou d'un employeur donné, et l'API ne fournit qu'un extrait de description.

Le connecteur conserve les paramètres d'attribution Adzuna de l'URL de redirection. Les cartes et fiches d'offres publiées affichent un lien « Adzuna » d'au moins 116 × 23 pixels vers le domaine français. La synchronisation est désactivée par défaut. Après avoir ajouté les valeurs au `.env`, redémarrez Collection avec les fichiers Compose du projet :

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.collection.yml -f docker-compose.collection-api.yml up -d --build collection-api
Invoke-RestMethod 'http://localhost:5004/health/ready'
Invoke-RestMethod 'http://localhost:5004/api/v1/collection/sources' -Headers $headers
```

L'endpoint administrateur `POST /api/v1/collection/sources/adzuna/collect` accepte `{"what":"developpeur dotnet","where":"France","page":1}` pour une recherche ponctuelle. Comme pour le worker, ses résultats sont seulement collectés pour revue. Les recherches manuelles sont soumises à la limite de dix requêtes par minute de l'API Collection.

L'API Adzuna documente une clé gratuite pour l'évaluation et des plans payants selon le volume ; vérifiez les limites associées à votre compte avant une synchronisation durable. Ses conditions autorisent la recherche d'emploi personnelle et imposent l'attribution des annonces ; un usage commercial, un produit générant des revenus ou des prospects nécessite un accord écrit. Si l'accès est résilié, les conditions demandent de retirer les données Adzuna. Cette intégration ne contourne pas les conditions d'utilisation de HelloWork et ne garantit pas HelloWork comme provenance.
## Outbox de collecte — 85.4

Appliquer la migration Collection `20261015122119_AddCollectionOutbox` avant le démarrage des nouveaux binaires, avec la commande `dotnet ef database update` du guide de lancement et votre connexion Collection locale. Elle crée l'outbox et prépare un événement par révision déjà présente, sans modifier ses décisions de revue. Aucune migration persistante utilisateur n'a été appliquée pendant le développement.

Chaque nouvelle révision est sauvegardée avec un événement d'acquisition versionné, dans la même transaction ; les réimports identiques n'ajoutent pas d'événement. Les lots JSON gardent leur atomicité, y compris sur les messages. L'événement contient des références et métadonnées : il n'autorise ni approbation, ni création de Job, ni calcul de Matching.

Voir le [contrat complet](collection-events.md) pour le JSON, les garanties et l'inspection SQL en lecture seule. Aucun dispatcher n'est actif et `DispatchedAtUtc` reste null. Broker, réservation des messages, confirmations, retries et déduplication des consommateurs seront réalisés en phase 89. Les messages restent donc en attente ; ne pas les déclarer envoyés manuellement.

Le socle Collection est prêt pour poursuivre le parcours candidat en **86.1 : service documentaire et import de CV PDF/DOCX**. La livraison effective des événements de phase 85 sera achevée avec la phase 89.
