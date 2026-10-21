# Phase 97.24 — restauration contrôlée d'une base PostgreSQL locale

La sauvegarde PostgreSQL de la [phase 97.16](phase-97-16.md) peut maintenant remplacer une base Compose locale après validation de son manifeste et de son empreinte. `tools/Restore-LocalDatabase.ps1` restaure d'abord l'archive dans une base de préparation, vérifie qu'elle se relit et contient des tables applicatives, puis arrête les services applicatifs qui étaient actifs et bascule la base restaurée. La base précédente est renommée et conservée pour permettre un retour arrière manuel. Les bases PostgreSQL, RabbitMQ et Ollama ne sont pas arrêtées.

La restauration sélectionne une seule base à la fois. Elle exige `-ReplaceExisting` et demande une confirmation PowerShell ; `-WhatIf` affiche l'opération sans modifier la base. Le script vérifie le projet Compose, le nom de la base, la présence du service, l'archive et son empreinte avant de modifier les services. Il redémarre les services applicatifs actifs après la bascule, y compris lorsqu'une erreur survient. Il ne supprime jamais la base précédente.

## Procédure

Depuis la racine du dépôt, avec une sauvegarde privée déjà vérifiée et le service PostgreSQL ciblé en marche :

```powershell
$backupDirectory = 'D:\JobAgentBackups\backup-20261020'
.\tools\Test-LocalDatabaseBackup.ps1 -BackupDirectory $backupDirectory -DatabaseService identity-db
.\tools\Restore-LocalDatabase.ps1 -BackupDirectory $backupDirectory -DatabaseService identity-db -ReplaceExisting -WhatIf
.\tools\Restore-LocalDatabase.ps1 -BackupDirectory $backupDirectory -DatabaseService identity-db -ReplaceExisting
```

Remplacer `identity-db` par le service de la base à récupérer. Contrôler l'aperçu et confirmer l'opération à l'invite. Après le message de réussite, vérifier la readiness des APIs et les données restaurées. Conserver la base `jobagent_previous_<identifiant>` jusqu'à la validation métier ; elle contient les données qui se trouvaient en place avant la restauration et reste soumise aux mêmes règles de confidentialité que la sauvegarde.

La CI sauvegarde toutes les bases du Compose jetable, vérifie leurs restaurations isolées, puis effectue une bascule réelle d'Identity vers l'archive et vérifie à nouveau la readiness complète. Aucun volume ni compte de développeur n'est touché : la restauration s'exécute seulement sur le runner éphémère.

## Limites

Cette procédure récupère une base PostgreSQL. Elle ne restaure pas automatiquement les volumes PostgreSQL disparus avant le démarrage du serveur, le volume RabbitMQ, les certificats OpenIddict, les fichiers téléversés, les secrets ou la configuration de l'hôte. La restauration réelle sur le poste local et la stratégie de reprise complète après perte d'hôte restent à valider.
