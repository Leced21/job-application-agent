# Agents — Démarrage local persistant

Le mode processus local démarre l'API Agents sur **http://127.0.0.1:5008** avec le modèle qualifié **qwen2.5:7b** et le serveur Ollama Windows sur 11434, cloud désactivé. Pour le site servi entièrement par Docker, utiliser plutôt `tools/Start-LocalAgentsCompose.ps1` : il valide les mêmes artefacts, réutilise le stockage PostgreSQL Agents du projet `job-application-agent`, monte les poids Ollama existants en lecture seule dans un conteneur Ollama privé et active les six connecteurs locaux. Aucune donnée de prompt ne quitte le réseau Docker privé vers Ollama.

## Activer les agents dans le site Docker

Depuis la racine du dépôt, dans Windows PowerShell 5.1 ou PowerShell 7, avec `.env`, Docker et Ollama actifs :

```powershell
./tools/Start-LocalAgentsCompose.ps1
```

Le script arrête sans modifier les services si Ollama n'annonce pas `cloud.disabled=true`, si le modèle ou le tokenizer diffère de la qualification, ou si la configuration Compose est invalide. Il ne télécharge pas de modèle et ne publie pas le serveur Ollama sur un port de l'hôte. Le conteneur Ollama utilise le modèle déjà présent sur le disque en lecture seule. Au démarrage, un prompt synthétique préchauffe le GPU et garde le modèle chargé pendant 30 minutes ; il ne crée aucun run Agents et ne consomme aucun quota. Le script reconstruit aussi l’interface du navigateur ; ouvrir ensuite `/agents` pour démarrer les agents et examiner leurs propositions.

La migration initiale `20261016075234_InitialAgentsSchema` a été appliquée à la base locale `agents_db`. Aucun schéma ni contenu de la base Job existante n'a été modifié. Avec `.env`, le volume persistant appartient au projet Compose principal : `job-application-agent_agents_postgres_data`.

## Démarrer ou reprendre

Depuis la racine du dépôt, avec **PowerShell 7**, Docker et Ollama actifs :

```powershell
./tools/Start-LocalJob.ps1
./tools/Start-LocalAgents.ps1
```

Le premier démarrage Agents génère une identité stable, un jeton Bearer et des identifiants PostgreSQL aléatoires dans `.local/agents/runtime-settings.json`, ignoré par Git. Les démarrages suivants réutilisent ce fichier. Conserver ce fichier pour retrouver le propriétaire et accéder au volume ; il ne s'agit pas d'un artefact de compilation à supprimer.

Le script charge le modèle/tokenizer qualifié, vérifie le mode local d'Ollama, attend PostgreSQL, applique les migrations Agents puis lance l'API en arrière-plan. Avec `.env`, il emploie le même nom de projet que la pile complète ; si un conteneur `agents-db` existe dans un autre projet Compose, il s'arrête sans en créer un autre. Pour reprendre explicitement l'ancienne base isolée et son volume, lancer `./tools/Start-LocalAgents.ps1 -IsolatedDatabase`. Si un service compatible répond déjà sur 5008, il le réutilise sans recréer les secrets ni lancer une seconde instance. Si l'API Agents Compose occupe ce port, arrêtez uniquement son service `agents-api` avant de lancer le modèle local ; conservez `agents-db` actif. Aucun processus utilisateur n'est arrêté par le script.

Job démarre avec sa configuration Development existante et **sans migration automatique de sa base**. Un Job déjà accessible sur 5002 est réutilisé. Les compilations `LocalJob` et `LocalAgents` séparent les DLL utilisées par les serveurs de `bin/Debug`, ce qui permet de compiler normalement pendant leur exécution. Une recompilation de la configuration utilisée par un serveur nécessite toujours son arrêt.

## Utiliser les routes authentifiées

Dans le terminal qui enverra les requêtes :

```powershell
. ./tools/Use-LocalAgents.ps1
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/catalog' -Headers $headers
Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/budget' -Headers $headers
```

Le script configure uniquement le terminal courant et ses futurs processus. Il n'affiche pas le jeton ou le mot de passe. Les requêtes sans authentification ou avec un jeton erroné ont été vérifiées : HTTP **401**. Le catalogue expose la configuration, pas une sonde d'inférence ; la disponibilité réelle du modèle a été qualifiée dans le [bilan Ollama](phase-88-3-live.md).

Quota initial : **100000 tokens cumulés**, coût USD local nul. Au démarrage vérifié, zéro token consommé et zéro réservation. Le quota ne se réinitialise pas au redémarrage. Aucun appel de génération ni aucune offre synthétique n'ont été ajoutés à la base persistante pour ce démarrage.

Pour lancer une analyse sur une offre choisie du catalogue Job :

```powershell
$requestId = [guid]::NewGuid() # conserver pour toute reprise
$body = @{ requestId = $requestId; kind = 'JobAnalysis'; resourceId = $jobId } | ConvertTo-Json
$run = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/runs' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
$run | Select-Object id, state, failure, revision
```

Renseigner `$jobId` avec l'offre réellement choisie. Les descriptions sont transmises uniquement au modèle local ; une proposition PendingReview reste à vérifier. Le [guide API](agents-api.md) décrit la reprise, les revues et la récupération après interruption.

## Mise à jour du code local

L’API peut aussi être démarrée en conteneur avec les overlays PostgreSQL Agents et Agents API ; voir la [phase 93.7](phase-93-7.md). Dans cet overlay, Ollama est désactivé et les connecteurs restent indépendants et désactivés par défaut. La configuration persistante et qualifiée ci-dessus reste nécessaire pour le mode Ollama local.

Après modification, exécuter `./tools/Restart-LocalAgents.ps1`. Le script contrôle le PID, le port, la configuration LocalAgents et le chemin de l'exécutable avant d'arrêter l'instance créée par nos scripts. Il reconstruit, applique les migrations puis vérifie la conservation du budget. Pour conserver l'ancien projet isolé, ajouter `-IsolatedDatabase`. Le script Start seul réutilise une instance déjà active.

Les overlays Compose génériques conservent les agents désactivés par défaut. Le mode Ollama local lancé par `Start-LocalAgentsCompose.ps1` applique l’overlay qualifié et active le modèle ainsi que les six connecteurs locaux. Le mode processus local garde sa propre configuration ; voir le [guide 88.4](agents-profile-analysis.md) pour son activation.

## État et journaux

Les PID, dates de lancement et ports sont conservés dans `.local/agents/api-process.json` et `job-api-process.json`. Les sorties des deux processus sont dans `agents-api.stdout.log`, `agents-api.stderr.log`, `job-api.stdout.log` et `job-api.stderr.log` dans ce même répertoire. Le contrôle de démarrage est dans `runtime-verification.json`, sans secret. Ces fichiers et le tokenizer restent ignorés par Git.

Pour vérifier le conteneur (projet principal si `.env` est présent) :

```powershell
docker compose --project-name job-application-agent --env-file .env -f docker-compose.agents.yml ps
```

La lecture authentifiée du budget vérifie aussi l'accès aux tables PostgreSQL. Les scripts de lancement ont été exécutés sur la machine ; une seconde invocation a confirmé la réutilisation de l'instance et la conservation exacte des paramètres locaux. Cette vérification opérationnelle complète les 176 tests du lot de qualification ; aucune modification métier .NET n'a été nécessaire pour ce démarrage.
