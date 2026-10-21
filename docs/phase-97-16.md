# Phase 97.16 — Sauvegarde et contrôle de restauration PostgreSQL local

Cette phase fournit un export logique non destructif des huit bases PostgreSQL Compose et un contrôle de restauration isolé. La restauration contrôlée d'une base existante depuis une archive vérifiée est décrite dans la [phase 97.24](phase-97-24.md).

## Utilisation

Depuis la racine du dépôt, avec Docker Desktop démarré, un fichier `.env` configuré et les huit services de base en marche :

```powershell
.\tools\Backup-LocalDatabases.ps1
```

Le script crée un nouveau dossier horodaté sous `backups/local/`, exporte chaque base au format PostgreSQL custom, vérifie chaque archive et écrit un manifeste avec tailles et empreintes SHA-256. Pour choisir une destination explicite :

```powershell
.\tools\Backup-LocalDatabases.ps1 -BackupDirectory 'D:\JobAgentBackups\backup-20261020'
```

Après la sauvegarde, vérifier que chaque archive peut être restaurée dans une base temporaire :

```powershell
.\tools\Test-LocalDatabaseBackup.ps1 -BackupDirectory 'D:\JobAgentBackups\backup-20261020'
```

Les deux scripts acceptent aussi `-EnvFile` pour choisir explicitement le fichier Compose, par exemple `.env.example` sur un runner isolé. Ne pas appliquer cette configuration d'exemple à des bases locales persistantes.

Pour vérifier une seule base, fournir son service Compose, par exemple `-DatabaseService identity-db`. Le script compare d'abord les empreintes au manifeste, restaure dans une base au nom aléatoire, exécute une requête, puis supprime uniquement cette base temporaire. Il ne modifie pas la base d'origine. Si le nettoyage échoue, il affiche le nom exact de la base temporaire à supprimer après vérification.

## Limites opérationnelles

- Les exports couvrent `profile-db`, `job-db`, `matching-db`, `identity-db`, `applications-db`, `documents-db`, `collection-db` et `agents-db`. Chaque archive est cohérente pour sa base, mais les huit dumps ne forment pas un instantané transactionnel commun. Pour une copie cohérente entre services, arrêter d'abord les API et traitements en arrière-plan, laisser les bases en marche, puis lancer la sauvegarde.
- Les fichiers contiennent des données de profil, candidatures et identité. Le dossier `backups/` est ignoré par Git ; conserver les copies sur un stockage privé, avec chiffrement et rétention adaptés. Le script ne chiffre pas les archives.
- Le volume RabbitMQ, les secrets, les certificats, les images de conteneurs et les réglages de l'hôte ne sont pas inclus. Ce contrôle ne remplace pas une stratégie de sauvegarde de ces éléments.
- Le contrôle de cette phase restaure dans une base temporaire sans toucher aux sources. La [phase 97.24](phase-97-24.md) ajoute une promotion contrôlée pour une base Compose existante, avec conservation de sa version remplacée. Cela ne prouve pas la récupération d'un hôte ou d'un volume Docker perdu.

## Vérification de la phase

Les deux scripts passent l'analyse syntaxique PowerShell. Un test avec un faux CLI Docker couvre la création du manifeste, la restauration isolée des huit bases et le rejet d'une archive altérée avant toute restauration. La configuration de l'ensemble Compose reste valide avec `.env.example`. Le démon Docker n'était pas disponible pendant cette implémentation ; l'exécution réelle de sauvegarde et de restauration reste à faire sur l'installation locale avant de déclarer ce critère V1 validé.
