# Guide des migrations EF Core

Les huit contextes PostgreSQL sont migrés séparément : Identity, Profile, Job, Matching, Collection, Documents, Applications et Agents. Chaque service conserve sa propre table `__EFMigrationsHistory`. Le démarrage des API n'applique pas automatiquement les migrations. La liste des bases, tables principales et snapshots est dans [database-schemas.md](database-schemas.md).

## Procédure sûre

Pour la topologie Compose locale, [Update-LocalDatabases.ps1](../tools/Update-LocalDatabases.ps1) applique les huit contextes après confirmation, avec les paramètres de connexion tirés de la configuration Compose. Utiliser `-WhatIf` pour afficher le plan. Avant une base contenant des données à conserver, consulter la [procédure de sauvegarde/restauration locale](phase-97-16.md).

1. Sauvegarder la base concernée et confirmer son nom, son port et son service propriétaire.
2. Vérifier que le SDK .NET 10 et `dotnet-ef` sont installés (`dotnet ef --version`).
3. Définir la chaîne `ConnectionStrings__...` du service dans le terminal ou un gestionnaire de secrets local. Ne pas la placer dans les commandes partagées, un fichier suivi, un ticket ou un log.
4. Lister les migrations et comparer à `__EFMigrationsHistory` : `dotnet ef migrations list`.
5. Générer un script idempotent, le relire et le faire approuver selon la procédure d'exploitation de l'environnement.
6. Appliquer `dotnet ef database update` sur une base de développement ou de test, vérifier readiness et tests, puis planifier les bases persistantes selon le processus de livraison.

Ne pas modifier manuellement les fichiers de migration déjà appliqués. Une nouvelle évolution de modèle exige une migration nouvelle, relue ; ne pas supprimer la base ou son volume pour contourner une migration. Un rollback (`database update <MigrationCible>`) exécute les méthodes `Down` et peut supprimer ou tronquer des données : sauvegarde et revue préalable obligatoires.

## Commandes par contexte

Les commandes suivantes sont à lancer depuis la racine du dépôt après avoir démarré la base correspondante. Les exemples utilisent les noms de projets et contextes actuels. Définir au préalable la variable d'environnement propre au service.

| Contexte | Variable de connexion | Projet EF (`--project`) | Projet de démarrage (`--startup-project`) | Répertoire de migrations |
| --- | --- | --- | --- | --- |
| `GatewayIdentityDbContext` | `ConnectionStrings__IdentityDatabase` | `src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj` | Même projet Gateway | `Identity/Migrations` |
| `ProfileDbContext` | `ConnectionStrings__ProfileDatabase` | `src/services/Profile/JobApplicationAgent.Profile.Infrastructure` | `src/services/Profile/JobApplicationAgent.Profile.Api` | `Persistence/Migrations` |
| `JobDbContext` | `ConnectionStrings__JobDatabase` | `src/services/Job/JobApplicationAgent.Job.Infrastructure` | `src/services/Job/JobApplicationAgent.Job.Api` | `Persistence/Migrations` (les migrations initiales résident aussi dans `Migrations`) |
| `MatchingDbContext` | `ConnectionStrings__MatchingDb` | `src/services/Matching/JobApplicationAgent.Matching.Infrastructure` | `src/services/Matching/JobApplicationAgent.Matching.Api` | `Persistence/Migrations` |
| `CollectionDbContext` | `ConnectionStrings__CollectionDatabase` | `src/services/Collection/JobApplicationAgent.Collection.Infrastructure` | `src/services/Collection/JobApplicationAgent.Collection.Api` | `Persistence/Migrations` |
| `DocumentsDbContext` | `ConnectionStrings__DocumentsDatabase` | `src/services/Documents/JobApplicationAgent.Documents.Infrastructure` | `src/services/Documents/JobApplicationAgent.Documents.Api` | `Persistence/Migrations` |
| `ApplicationsDbContext` | `ConnectionStrings__ApplicationsDatabase` | `src/services/Applications/JobApplicationAgent.Applications.Infrastructure` | `src/services/Applications/JobApplicationAgent.Applications.Api` | `Persistence/Migrations` |
| `AgentsDbContext` | `ConnectionStrings__AgentsDatabase` | `src/services/Agents/JobApplicationAgent.Agents.Infrastructure` | `src/services/Agents/JobApplicationAgent.Agents.Api` | `Persistence/Migrations` |

Après définition de la variable correspondante, utiliser la même forme pour inspecter et mettre à jour. Pour la base Identity du Compose local, `tools/Update-LocalIdentityDatabase.ps1` lit les valeurs de `.env` en mémoire et configure la factory de conception du Gateway ; les autres contextes suivent les commandes génériques ci-dessous.

```powershell
$project = 'src/services/Profile/JobApplicationAgent.Profile.Infrastructure'
$startup = 'src/services/Profile/JobApplicationAgent.Profile.Api'
$context = 'ProfileDbContext'
$outputDir = 'Persistence/Migrations'
dotnet ef migrations list --project $project --startup-project $startup --context $context
dotnet ef migrations script --idempotent --project $project --startup-project $startup --context $context --output "$env:TEMP/profile-migrations.sql"
# Relire le SQL avant d'appliquer : cette commande modifie la base sélectionnée.
dotnet ef database update --project $project --startup-project $startup --context $context
```

Pour les autres services, remplacer les trois variables par une ligne du tableau. Le Gateway Identity utilise son projet `.csproj` comme projet EF et de démarrage ; sa factory de conception accepte `ConnectionStrings__IdentityDatabase`, ou les variables `IDENTITY_DB_NAME`, `IDENTITY_DB_USER` et `IDENTITY_DB_PASSWORD` utilisées par le script local. Les contextes Profile, Collection, Documents, Applications et Agents possèdent également une factory de conception. Le guide [installation Windows](windows-install.md) donne l'ordre de démarrage des bases ; les guides [Identity](phase-91-10.md), [Profile](phase-93-1.md), [Job](jobs.md), [Matching](matching.md), [Collection](collection.md), [Documents](documents.md), [Applications](applications-api.md) et [Agents](agents-api.md) précisent leurs migrations et prérequis particuliers.

## Créer une migration

Après modification du modèle d'un seul service, régler `project`, `startup` et `context` comme ci-dessus, puis créer un nom descriptif unique :

```powershell
dotnet ef migrations add AddDescriptiveChange --project $project --startup-project $startup --context $context --output-dir $outputDir
dotnet ef migrations script --idempotent --project $project --startup-project $startup --context $context --output "$env:TEMP/profile-review.sql"
```

Vérifier la migration, son fichier Designer et le `ModelSnapshot` du service. Les fichiers doivent rester dans le projet Infrastructure concerné (Identity dans le projet Gateway). La commande `migrations add` peut nécessiter d'autres paramètres de conception propres au service ; consulter son guide avant de la lancer. Ne pas générer ni appliquer une migration avec une chaîne visant une base partagée ou de production par erreur.
