# Phase 97.21 — Vérification CI des sauvegardes et restaurations PostgreSQL

Après le test navigateur, la CI sauvegarde les huit bases Compose du runner vers un dossier unique sous `$RUNNER_TEMP`, vérifie les archives et manifeste SHA-256, restaure chaque dump dans une base temporaire aléatoire et interroge les bases restaurées. Les bases sources ne sont pas modifiées ; aucune archive n'est publiée comme artefact.

Les scripts `Backup-LocalDatabases.ps1` et `Test-LocalDatabaseBackup.ps1` acceptent maintenant `-EnvFile` (par défaut `.env`) pour permettre cet usage sans créer ni modifier le `.env` du dépôt. `.env.example` n'est utilisé que pour les bases jetables de GitHub Actions.

## Vérification

La syntaxe PowerShell et les fichiers YAML du workflow sont vérifiés localement ; les appels CI ciblent les huit services configurés. Le démon Docker de ce poste n'étant pas disponible, la sauvegarde et la restauration réelles seront confirmées par le prochain run CI.
