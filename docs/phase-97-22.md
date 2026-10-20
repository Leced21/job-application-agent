# Phase 97.22 — Sauvegarde et restauration isolée du volume RabbitMQ

La phase ajoute une sauvegarde du volume nommé de RabbitMQ et une répétition de restauration qui extrait l'archive dans un nouveau volume, démarre un broker temporaire avec l'image et l'identité de nœud sauvegardées, puis interroge ses virtual hosts. Le broker d'origine est arrêté proprement pendant la copie puis redémarré et attendu en état sain. Le volume source n'est jamais monté en écriture par le conteneur d'archivage et n'est pas modifié par le test de restauration.

Les scripts `Backup-LocalRabbitMqVolume.ps1` et `Test-LocalRabbitMqBackup.ps1` exigent `-AllowServiceRestart`, car la sauvegarde cohérente et le test d'une restauration avec la même identité de nœud interrompent brièvement RabbitMQ. Ils produisent et vérifient une archive tar.gz et un manifeste SHA-256. Le manifeste mémorise l'identité du nœud et l'identifiant immuable de l'image pour que la répétition démarre exactement la version sauvegardée. L'image auxiliaire `alpine:3.22` doit être disponible localement ou téléchargeable.

Depuis la racine du dépôt, avec Docker Desktop actif et le fichier `.env` configuré :

```powershell
$backupDirectory = Join-Path (Get-Location) ('backups/local/rabbitmq-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
./tools/Backup-LocalRabbitMqVolume.ps1 -BackupDirectory $backupDirectory -AllowServiceRestart
./tools/Test-LocalRabbitMqBackup.ps1 -BackupDirectory $backupDirectory -AllowServiceRestart
```

La CI effectue les deux opérations sur sa pile jetable après les migrations, le démarrage des services et le parcours Chromium. Le volume et l'archive ne sont pas publiés comme artefacts. La répétition valide une archive et le démarrage de ses données sur un volume neuf ; le remplacement destructif d'un volume local n'est pas automatisé.

Les archives RabbitMQ peuvent contenir des messages en attente, des métadonnées et des secrets de broker. Les conserver hors du dépôt, avec chiffrement, contrôle d'accès et rétention adaptés. L'arrêt bref déconnecte les consommateurs et producteurs ; effectuer l'opération pendant une fenêtre de maintenance en dehors de la CI.

## Vérification

La syntaxe des deux scripts PowerShell, la configuration Compose utilisée et le workflow sont contrôlés localement. L'exécution réelle de Docker est confiée au runner CI, car le démon Docker local n'est pas disponible dans cet environnement.
