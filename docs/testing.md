# Guide des tests

Les commandes de ce guide suivent le workflow [Repository CI](../.github/workflows/backend-ci.yml). Elles exécutent des tests locaux et n'envoient pas de candidature.

## Backend .NET

Depuis la racine du dépôt, restaurer puis compiler la solution en Release :

```powershell
dotnet restore JobApplicationAgent.slnx
dotnet build JobApplicationAgent.slnx --configuration Release --no-restore
```

Les tests sont répartis entre projets unitaires et projets d'intégration. Pour lancer tous les projets de test de façon séquentielle sous PowerShell :

```powershell
Get-ChildItem tests -Filter *.csproj -Recurse | ForEach-Object {
    dotnet test $_.FullName --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Échec : $($_.FullName)" }
}
```

Les suites d'intégration qui utilisent PostgreSQL via Testcontainers nécessitent Docker Desktop démarré en mode Linux. Pour cibler un seul projet, passer directement son chemin à `dotnet test`, par exemple :

```powershell
dotnet test tests/Matching/JobApplicationAgent.Matching.UnitTests/JobApplicationAgent.Matching.UnitTests.csproj
dotnet test tests/Matching/JobApplicationAgent.Matching.IntegrationTests/JobApplicationAgent.Matching.IntegrationTests.csproj
```

## Frontend Angular

Depuis le dossier Angular, installer les versions verrouillées du fichier `package-lock.json`, lancer Vitest en mode non interactif et compiler la version de production :

```powershell
Set-Location src/frontend/job-application-agent-ui
npm ci
npm test -- --watch=false
npm run build
```

Les scénarios navigateur utilisent Playwright et Chromium. Installer le navigateur une seule fois, puis lancer la suite :

```powershell
npx playwright install chromium
npm run test:e2e
```

Playwright démarre le serveur frontend configuré dans `playwright.config.ts`. Les tests sont locaux et utilisent les API simulées définies dans les scénarios.

## Vérifications Compose

Le workflow CI valide les scripts de migration Identity et de génération des certificats sous Windows PowerShell 5.1, ainsi que les garde-fous de reprise et le scénario simulé d'attribution du profil historique sous PowerShell 7. Ce dernier contrôle le consentement, le lien au propriétaire initial Gateway, le mode `-WhatIf` et le refus des profils ambigus sans contacter Docker. Le job Windows crée aussi une paire PFX temporaire, vérifie les empreintes et supprime les fichiers. Sur le runner Ubuntu, il valide la fusion de tous les overlays Compose, construit toutes les images, applique les huit migrations sur le runner éphémère, puis démarre la pile complète jusqu'à la readiness des APIs et du Gateway. Il vérifie les huit endpoints API exposés, le frontend et un vrai parcours Chromium d'inscription puis de reconnexion via l'interface et Identity PostgreSQL. La phase 97.26 crée et vérifie un jeu avec les huit bases, le volume RabbitMQ et l'image exacte du broker. La phase 97.27 restaure les huit bases en conservant leur version antérieure, extrait l'archive RabbitMQ dans un nouveau volume, teste le broker avec son identité originale, l'active par un overlay Compose puis vérifie la readiness de la pile. Le compte `example.test`, les bases et les archives restent sur le runner et ne sont pas publiés comme artefacts. Les scénarios UI simulés restent exécutés séparément. La CI démontre cette procédure sur un runner jetable ; les secrets et paramètres propres à un véritable hôte sont restaurés séparément. Depuis la racine du dépôt, la commande PowerShell correspondant à la validation de configuration est :

```powershell
$compose = @(
    '--env-file', '.env.example',
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
docker compose @compose config --quiet
if ($LASTEXITCODE -ne 0) { throw 'La configuration Compose est invalide.' }
```

Cette commande valide la configuration sans démarrer de conteneur. La CI couvre les migrations, le démarrage complet, l'inscription réelle et les répétitions isolées de restauration PostgreSQL et RabbitMQ ; elle ne remplace pas la vérification de votre propre installation locale. La sauvegarde RabbitMQ arrête brièvement le broker et requiert l'option explicite `-AllowServiceRestart`. La construction des images et l'image auxiliaire `alpine:3.22` peuvent télécharger des images de base et des dépendances.

## Résultats et dépannage

Les commandes `dotnet test` retournent un code non nul dès qu'un projet échoue. Les tests d'intégration peuvent échouer si Docker Desktop n'est pas disponible ou si les ports nécessaires sont occupés. Les rapports TRX et Playwright sont aussi générés par la CI et publiés comme artefacts du workflow.

Si la restauration .NET signale `NU1900` parce que le service d'audit NuGet est inaccessible, rétablir l'accès réseau puis relancer la restauration avant de conclure sur l'état des vulnérabilités. Une réussite de compilation ne remplace pas l'exécution des tests.
