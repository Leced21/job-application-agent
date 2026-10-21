# Agents — API locale et stockage (88.2)

Le service écoute sur **http://localhost:5008**, PostgreSQL sur **127.0.0.1:5438**. Il conserve les exécutions, propositions, réservations et décisions de revue. **Le fournisseur de modèle est désactivé par défaut** : une analyse d'offre valide aboutit alors à `Failed / ModelUnavailable`, sans réservation ni facturation. Le [guide Ollama 88.3](agents-ollama.md) décrit l'activation explicite du modèle local et de son tokenizer. Les tests ordinaires utilisent des modèles simulés ; la suite de qualification explicite utilise Ollama local sur des données synthétiques.

ProfileAnalysis utilise les mêmes routes, quotas et revues avec son propre schéma. Le catalogue expose `profileAnalysisEnabled` et `profileQuestions` ; une implémentation disponible peut avoir son connecteur désactivé. Voir le [guide d'activation et de confidentialité](agents-profile-analysis.md).

MatchingEnrichment utilise le même workflow avec le résultat Matching comme source ; le catalogue expose `matchingEnrichmentEnabled` et `matchingMessages`. Son accès local est désactivé par défaut et limité à un profil épinglé, voir le [guide 88.5](agents-matching-enrichment.md).

CoverLetter utilise les faits structurés du profil et les exigences d'une JobAnalysis approuvée pour l'offre. Il génère des paragraphes sourcés pour revue, sans créer de document ni l'appliquer ailleurs. Son connecteur indépendant est désactivé par défaut ; `coverLetterEnabled` indique son activation. Voir le [guide CoverLetter 88.7](agents-cover-letter.md).

ApplicationPreparation fournit un état de preuve indexé pour chaque exigence et guide la revue du dossier avant sélection des documents. Il ne crée ni candidature ni document. Son connecteur indépendant reste désactivé par défaut ; `applicationPreparationEnabled` indique son activation. Voir le [guide ApplicationPreparation 88.8](agents-application-preparation.md).

FollowUpSuggestion lit une candidature active depuis Applications avec un jeton de service local configuré séparément. Il produit un brouillon ou une recommandation d'attente, sans planifier ni envoyer ; `followUpSuggestionEnabled` signale le connecteur. Voir le [guide 88.9](agents-follow-up-suggestion.md).

## Configuration et démarrage

Le [guide de démarrage local persistant](agents-local-runtime.md) fournit les scripts configurés sur cette machine, les ports actifs et les commandes authentifiées. La procédure ci-dessous reste disponible pour une configuration manuelle.

Les valeurs sont des paramètres serveur, jamais des champs de requête utilisateur. Définir localement et conserver hors Git :

| Variable | Valeur attendue |
|---|---|
| `AGENTS_DB_USER` | Utilisateur PostgreSQL local |
| `AGENTS_DB_PASSWORD` | Mot de passe fort |
| `ConnectionStrings__AgentsDatabase` | `Host=localhost;Port=5438;Database=agents_db;Username=...;Password=...` |
| `Agents__LocalAuth__OwnerId` | UUID stable du propriétaire local |
| `Agents__LocalAuth__Token` | Secret aléatoire de 32 à 512 caractères sans espace |
| `Agents__Quota__MaxTokens` | Plafond cumulé de tokens, par exemple `100000` |
| `Agents__Quota__MaxUsd` | Plafond cumulé en USD, par exemple `1` |
| `Agents__JobBaseUrl` | Facultatif, `http://localhost:5002` par défaut |

Les exemples de quotas ne constituent pas des tarifs. Un plafond nul est valide et bloque toute réservation positive correspondante. Les plafonds sont cumulatifs, sans remise à zéro quotidienne. Un redémarrage ou une modification des plafonds conserve la consommation et les réservations existantes ; toutes les instances doivent partager la même configuration. Changer OwnerId ouvre un autre périmètre : conserver sa valeur pour retrouver les historiques.

Après configuration, depuis la racine :

```powershell
docker compose -f docker-compose.agents.yml up -d
dotnet ef database update --project src/services/Agents/JobApplicationAgent.Agents.Infrastructure --startup-project src/services/Agents/JobApplicationAgent.Agents.Api --context AgentsDbContext
dotnet run --project src/services/Agents/JobApplicationAgent.Agents.Api --launch-profile http
```

Migration initiale : `20261016075234_InitialAgentsSchema`. Le démarrage de l'API ne migre pas automatiquement la base. Le script explicite Start-LocalAgents applique les migrations avant de lancer le processus ; il a initialisé la nouvelle base Agents locale sur cette machine. La suppression du schéma par migration descendante est refusée dès que des exécutions existent, afin de préserver historique et comptabilité.

## Routes authentifiées

Toutes les routes ci-dessous commencent par `/api/v1/agents` et exigent `Authorization: Bearer <token local>`.

| Méthode | Route | Résultat |
|---|---|---|
| GET | `/catalog` | Sept agents et configuration du modèle ; `availabilityVerified: false` indique que ce catalogue ne sonde pas le serveur |
| GET | `/budget` | Plafonds, tokens/coûts imputés et réservés |
| POST | `/runs` | Exécuter ou reprendre une commande identique |
| GET | `/runs?page=1&pageSize=20` | Résumés sans texte source ni proposition ; taille 1–50, page 1–10000 |
| GET | `/runs/{id}` | Exécution et proposition/source figée éventuelle |
| GET | `/requests/{requestId}` | Retrouver une exécution après perte de réponse, même si son identifiant est inconnu |
| GET | `/runs/{id}/audit` | Métadonnées de chaque révision |
| POST | `/runs/{id}/review` | Décision humaine sur hash/révision exacts |
| POST | `/runs/{id}/recover` | Clôturer une exécution Running après expiration de son bail |

Exemple d'analyse (Job doit fonctionner sur le port 5002 et l'offre doit exister) :

```powershell
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$requestId = [guid]::NewGuid() # conserver pour les reprises ; ne pas en générer un nouveau après une réponse perdue
$body = @{ requestId = $requestId; kind = 'JobAnalysis'; resourceId = $jobId } | ConvertTo-Json
$run = Invoke-RestMethod -Uri 'http://localhost:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
$run | Select-Object id, state, failure, revision
```

Réenvoyer le même corps reprend le résultat initial sans relire Job ni rappeler le modèle. Réutiliser requestId pour une autre commande renvoie 409. Une exécution encore Running renvoie aussi 409 ; utiliser `/requests/{requestId}` pour la retrouver. Une erreur de persistance peut laisser une opération effectivement enregistrée malgré une réponse 503 : consulter/reprendre avec la même clé.

La revue exige `{ "expectedRevision": 2, "outputSha256": "<hash exact>", "confirmed": true, "approved": true, "note": "..." }`. Une décision finale ne peut pas être modifiée. Un hash/révision incorrects ou un bail non expiré renvoient 409 ; identité/pagination invalides : 400 ; autre propriétaire ou ressource absente : 404 ; authentification absente/invalide : 401. POST `/runs` est limité à dix tentatives par minute et propriétaire **par instance** (429 au-delà). Les routes de lecture sont limitées à 60 requêtes par minute et propriétaire, également par instance. Ces limites ne sont pas distribuées. Les réponses portent `Cache-Control: no-store`.

Un résultat `Failed` est une exécution persistée et se consulte avec HTTP 200 ; toujours lire `state` et `failure`. Une nouvelle tentative fonctionnelle après correction demande une nouvelle requestId ; les anciennes restent idempotentes et conservent leur résultat.

## Garanties de persistance et récupération

Les mutations PostgreSQL sont sérialisées par propriétaire avec un verrou transactionnel partagé entre instances. L'unicité propriétaire/requestId est aussi imposée en base. Début, clôture et revue écrivent leur audit dans la même transaction. Une clôture rejouée après perte de réponse ne refacture pas. La revue compare la révision et un seul concurrent peut décider.

Le budget disponible est calculé à partir des consommations et réservations persistées. Une réservation reste comptée jusqu'à la clôture ; une consommation connue remplace la réservation, même au-delà du plafond en cas de surconsommation. Les montants `numeric` ne subissent pas d'arrondi de stockage. Aucun endpoint ne permet de supprimer l'historique ou de réinitialiser la consommation.

Chaque nouvelle exécution possède un bail de cinq minutes, supérieur au timeout maximal de deux minutes du socle. Après un crash ou une réponse perdue, **aucun modèle n'est relancé automatiquement**. Tant que le bail court, la récupération est refusée. Après expiration, POST recover clôture en `Failed / Interrupted` : sans réservation, consommation zéro connue ; avec réservation, le maximum réservé est imputé comme consommation inconnue. C'est volontairement conservateur, y compris si le crash a précédé l'appel au modèle. Les réservations et clôtures tardives ne rouvrent pas l'exécution. La récupération se fait à la demande ; un traitement automatique relève de la phase 89.

## Source locale et limites

ResumeAdaptation requiert une exécution JobAnalysis approuvée par le même propriétaire pour l'offre demandée. L'offre doit toujours être `Discovered` ou `Saved`, et sa description doit être inchangée depuis cette analyse. Le profil du compte courant est lu via OpenIddict ; le connecteur reste désactivé par défaut (`Agents:ResumeAdaptation:Enabled=false`). Une proposition ne contient que des index de faits et d'exigences avec une priorité. Voir le [guide ResumeAdaptation](agents-resume-adaptation.md).

Seule la **description** exacte d'une offre Job `Discovered` ou `Saved` est figée. Les offsets des citations portent sur cette description, pas sur le titre ou un contenu recomposé. Offre manquante, ignorée, expirée, identifiant incohérent ou description vide : `SourceRejected`. Description limitée à 10000 caractères et réponse HTTP à 128 Kio, y compris sans Content-Length ; timeout client dix secondes. L'origine configurée doit être loopback ou l'hôte interne Compose Job explicitement autorisé, sans identifiants, chemin, requête ni fragment ; redirections et cookies désactivés, aucun jeton utilisateur transmis à Job.

Job reste un catalogue local partagé sans propriété utilisateur. La configuration du propriétaire Agents autorise sa lecture dans ce contexte local ; elle ne crée pas une isolation multi-utilisateur dans Job. Les snapshots de description et propositions sont stockés en clair dans la base dédiée, accessibles par l'API authentifiée. L'audit conserve uniquement identifiants, états, versions, hashes et consommation, sans texte, note de revue ni exception brute. Le chiffrement au repos, les identités multi-utilisateurs et le durcissement global restent en phase 91.

Même approuvée, aucune proposition ne modifie Job, Profile, Matching, Documents ou Applications. Aucun CV réel n'est traité, aucun service distant appelé et aucune candidature envoyée par ces lots. Le [guide Ollama](agents-ollama.md) décrit le modèle/tokenizer qualifié ; le [guide local](agents-local-runtime.md) fournit la configuration persistante et les scripts de lancement du service.
