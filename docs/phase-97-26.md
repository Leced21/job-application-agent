# Phase 97.26 — Jeu de reprise cohérent pour l’hôte local

La phase ajoute `tools/Backup-LocalRecoverySet.ps1`. Pendant la fenêtre de maintenance, le script vérifie la configuration Compose complète et l’état des huit bases PostgreSQL et de RabbitMQ, arrête les services applicatifs actifs, sauvegarde les bases et le volume du broker, vérifie chaque archive par une restauration isolée, puis redémarre les services précédemment actifs. Un index `recovery-set.json` relie les deux manifestes avec leurs empreintes, le commit et l’état de l’arbre de travail. Une erreur pendant la création laisse un marqueur `INCOMPLETE.txt` lorsque le dossier de sauvegarde a été créé.

Depuis la racine du dépôt, avec PowerShell 7, Docker Desktop actif et `.env` configuré, choisir une destination neuve hors du dépôt :

```powershell
$backupDirectory = 'D:\JobAgentBackups\recovery-20261020-120000'
./tools/Backup-LocalRecoverySet.ps1 `
    -BackupDirectory $backupDirectory `
    -EnvFile .env `
    -AllowServiceRestart
```

L’option explicite `-AllowServiceRestart` est requise : les APIs, workers et RabbitMQ sont brièvement arrêtés. Le script refuse les chemins relatifs, les destinations du dépôt et l’écrasement d’un dossier existant. Il signale aussi les changements non commités via le manifeste, sans les incorporer à une sauvegarde exécutable.

La CI crée ce jeu sur son runner jetable et contrôle les empreintes. La [phase 97.27](phase-97-27.md) le restaure ensuite sur ce même hôte temporaire, en préservant les bases et le volume précédents. Les données et archives ne sont pas publiées en artefacts.

## Limites de reprise

Le jeu couvre les huit bases PostgreSQL, les données du volume RabbitMQ et l’image exacte du broker. Il n’inclut pas `.env`, les secrets, les PFX OpenIddict, les certificats TLS, les fichiers de configuration runtime propres à l’hôte, ni les autres images de conteneurs ou poids Ollama. Les CV importés sont stockés dans Documents PostgreSQL ; vérifier tout stockage de fichiers ajouté localement avant de s’appuyer sur cette couverture. Le modèle Ollama doit être retéléchargé ou sauvegardé séparément.

Les archives contiennent des données personnelles, des candidatures, des identifiants et des messages de broker. Les scripts n’assurent aucun chiffrement. Conserver le dossier sur un support privé chiffré, avec une copie hors hôte, une rétention contrôlée et des secrets récupérables depuis un gestionnaire distinct. Une configuration sauvegardée sans secrets ne permet pas à elle seule de redémarrer l’application.

La [phase 97.27](phase-97-27.md) active le broker depuis un nouveau volume et conserve l’ancien volume Compose. Une reprise après perte complète de l’hôte exige encore la récupération séparée des secrets et réglages, la recréation des autres images et modèles, puis la validation du compte candidat avant réouverture. Le script ne sauvegarde pas non plus les processus Ollama ou workers lancés hors Compose ; les arrêter manuellement pendant la fenêtre de maintenance.

La syntaxe PowerShell et les garde-fous de chemin sont vérifiés localement. L’exécution de Docker, des huit restaurations et du redémarrage réel des services est exercée par la CI sur un runner éphémère ; un run vert sur le SHA livré et une répétition complète sur le poste cible restent nécessaires.
