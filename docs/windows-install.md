# Installation locale sous Windows 11

Ce guide démarre la topologie Compose complète sur le poste. Elle est réservée au développement local : OpenIddict utilise des clés éphémères et les valeurs de `.env.example` sont des exemples à remplacer.

Pour les limites de sécurité avant toute exposition réseau, consulter le [guide de sécurité](security.md). En cas d'échec de démarrage, d'API, de build ou d'Ollama, suivre le [guide de dépannage](troubleshooting.md).

## Prérequis

- Windows 11 avec PowerShell 7.
- Git.
- Docker Desktop démarré en mode conteneurs Linux, avec WSL 2 activé.
- SDK .NET 10 et l'outil EF Core `dotnet-ef` pour appliquer les migrations depuis l'hôte.
- Node.js 22 et npm 11.19.0 pour lancer l'interface hors conteneur. Ces versions suivent le workflow CI et `packageManager` du projet.
- Ollama est facultatif. Le déploiement Compose garde le fournisseur Agents désactivé ; son démarrage local est décrit dans [agents-local-runtime.md](agents-local-runtime.md).

Vérifier les outils depuis PowerShell :

```powershell
git --version
dotnet --version
dotnet ef --version
node --version
npm --version
docker compose version
```

Si `dotnet-ef` n'est pas installé, l'ajouter à l'environnement utilisateur avec `dotnet tool install --global dotnet-ef`, puis ouvrir un nouveau terminal si `dotnet ef` n'est pas trouvé.

## Préparer le dépôt et les secrets locaux

Cloner le dépôt ou ouvrir le checkout, puis se placer à sa racine :

```powershell
Set-Location C:\chemin\vers\job-application-agent
Copy-Item .env.example .env
notepad .env
```

Remplacer chaque valeur commençant par `replace-with-` par un secret aléatoire distinct. Garder `APPLICATIONS_OWNER_ID`, `APPLICATIONS_PROFILE_ID` et `AGENTS_OWNER_ID` stables pour cette base locale. L'identité initiale utilise `APPLICATIONS_OWNER_ID` comme propriétaire ; il doit correspondre aux valeurs partagées par Applications, Documents, Job et Gateway. Les mutations du catalogue utilisent `JOB_LOCAL_TOKEN` en accès direct local et la publication Collection utilise son secret propre `JOB_COLLECTION_PUBLISHER_TOKEN`. Ne pas commiter `.env` ni publier les jetons.

Contrôler que Git ignore le fichier puis restaurer les dépendances .NET :

```powershell
git check-ignore .env
dotnet restore JobApplicationAgent.slnx
```

## Démarrer les bases, appliquer les migrations, puis lancer l'application

Compose lit `.env` et publie les services sur des adresses loopback. Préparer une liste réutilisable de fichiers Compose :

```powershell
$compose = @(
    '--project-name', 'job-application-agent',
    '--env-file', '.env',
    '-f', 'docker-compose.yml',
    '-f', 'docker-compose.identity.yml',
    '-f', 'docker-compose.applications.yml',
    '-f', 'docker-compose.applications-api.yml',
    '-f', 'docker-compose.documents.yml',
    '-f', 'docker-compose.documents-api.yml',
    '-f', 'docker-compose.collection.yml',
    '-f', 'docker-compose.collection-api.yml',
    '-f', 'docker-compose.agents.yml',
    '-f', 'docker-compose.agents-api.yml',
    '-f', 'docker-compose.edge.yml'
)
if (Test-Path -LiteralPath '.local/rabbitmq-recovery.yml' -PathType Leaf) {
    $compose += @('-f', '.local/rabbitmq-recovery.yml')
}
docker compose @compose config --quiet
```

Démarrer d'abord les bases et RabbitMQ :

```powershell
docker compose @compose up -d identity-db profile-db job-db matching-db applications-db documents-db collection-db agents-db rabbitmq
```

Appliquer les migrations depuis l'hôte avant d'utiliser les API. Le script unique prépare les huit bases Compose, vérifie qu'aucune base du même nom de service n'appartient à un autre projet, puis applique les migrations EF dans l'ordre. Il demande confirmation avant de démarrer les bases ou de modifier leur schéma. `-WhatIf` affiche les huit cibles sans démarrer de conteneur ni appliquer de migration :

```powershell
.\tools\Update-LocalDatabases.ps1 -WhatIf
.\tools\Update-LocalDatabases.ps1
```

Pour appliquer seulement Identity, `tools/Update-LocalIdentityDatabase.ps1` reste disponible et s'exécute aussi sous le Windows PowerShell 5.1 déjà inclus dans Windows. Les autres outils PowerShell du dépôt peuvent exiger PowerShell 7. Avant toute mise à jour d'une base contenant des données à conserver, créez et testez une sauvegarde avec la [phase 97.16](phase-97-16.md). Les bases Compose existantes conservent les identifiants utilisés lors de leur première initialisation ; changer `.env` ne modifie pas leur mot de passe. Le détail des projets, contextes et migrations figure dans le [guide EF](migrations.md).

Si cette base Profile contient un profil créé avant l'ajout de son propriétaire, sa colonne `owner_id` peut rester vide après les migrations. Après sauvegarde, vérifier puis attribuer l'unique profil historique au `APPLICATIONS_OWNER_ID` prévu pour le premier compte avec la [procédure contrôlée de la phase 97.28](phase-97-28.md). Les profils déjà attribués et les installations neuves ne nécessitent pas cette étape.

Une fois les migrations appliquées, construire et démarrer la topologie complète :

```powershell
docker compose @compose up -d --build
docker compose @compose ps
```

Les bases locales peuvent être sauvegardées sans arrêter PostgreSQL. Pour une sauvegarde cohérente de toutes les bases et de RabbitMQ, suivre le [runbook de sauvegarde](phase-97-26.md) : il arrête brièvement les APIs et les workers puis les redémarre. Pour restaurer un nouvel hôte depuis ce jeu, suivre la [procédure de reprise complète](phase-97-27.md). Ces opérations nécessitent PowerShell 7 et `-AllowServiceRestart`. Le jeu ne contient pas les secrets, les certificats ni les réglages locaux et doit être conservé sur un support privé chiffré. Pour remettre une seule base en service depuis une archive, suivre la [procédure de restauration contrôlée](phase-97-24.md) ; elle prépare et valide la base avant la bascule, puis garde l'ancienne base pour un éventuel retour arrière.

L'application est disponible sur **http://127.0.0.1:4200**. Le Gateway est publié sur **http://127.0.0.1:5000**. En environnement Development, l'inscription est activée par défaut : le premier compte reprend `APPLICATIONS_OWNER_ID` pour accéder aux données locales existantes ; chaque compte suivant reçoit un propriétaire indépendant. Si l'inscription n'apparaît pas, vérifier que le Gateway utilise les fichiers Compose incluant `docker-compose.edge.yml`, que les migrations Identity ont été appliquées et que `APPLICATIONS_OWNER_ID` est défini dans `.env`. Si une ancienne session ne fonctionne plus après le redémarrage du Gateway, se reconnecter : les clés OIDC de développement ne sont pas persistées.

Contrôler l'état du Gateway et les services :

```powershell
Invoke-RestMethod 'http://127.0.0.1:5000/health/live'
Invoke-RestMethod 'http://127.0.0.1:5000/health/ready'
docker compose @compose ps
```

## Développement et tests

Les commandes et scénarios de vérification sont détaillés dans le [guide des tests](testing.md).

Pour lancer Angular avec le proxy local, dans un autre terminal :

```powershell
Set-Location src/frontend/job-application-agent-ui
npm ci
npm start
```

`npm start` relaie `/auth` et `/api` vers le Gateway Compose publié sur `http://127.0.0.1:5000`. Il démarre l’interface sur `http://127.0.0.1:4200`, où le parcours de connexion complet est disponible.

Pour exécuter les tests backend, Docker Desktop doit rester actif : plusieurs suites démarrent PostgreSQL avec Testcontainers.

```powershell
Get-ChildItem tests -Filter *.csproj -Recurse | ForEach-Object {
    dotnet test $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Échec : $($_.FullName)" }
}
```

Tests et build frontend :

```powershell
Set-Location src/frontend/job-application-agent-ui
npm test -- --watch=false
npm run build
```

## Arrêt et dépannage

Arrêter la topologie sans effacer les bases :

```powershell
docker compose @compose down
```

`down` conserve les volumes PostgreSQL et RabbitMQ. Vérifier les journaux d'un conteneur avec `docker compose @compose logs --tail 100 gateway-api` (remplacer le nom du service au besoin). Si un service reste unhealthy, appliquer ses migrations avant de vérifier sa dépendance amont. Une erreur de connexion Docker indique généralement que Docker Desktop n'est pas démarré ou que le moteur Linux n'est pas sélectionné.

Les ports locaux, les health checks et les limites de cette topologie sont détaillés dans [l'architecture](architecture.md) et les guides par service. Les services Agents Compose n'appellent pas Ollama par défaut ; voir [configuration Ollama](agents-ollama.md) pour activer explicitement le modèle local.
