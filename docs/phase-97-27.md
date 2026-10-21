# Phase 97.27 — Restauration contrôlée d’un hôte local

Cette phase ajoute `tools/Restore-LocalRecoverySet.ps1` et rend le jeu de reprise transportable entre hôtes. La sauvegarde RabbitMQ contient maintenant aussi une archive de l’image exacte du broker ; le manifeste passe au format 2, tandis que le test existant continue d’accepter les anciens manifestes au format 1 pour leurs vérifications sur l’hôte source.

Sur le nouvel hôte, installer le même commit du dépôt, restaurer le fichier `.env` et les secrets depuis leur gestionnaire protégé, démarrer Docker Desktop en mode Linux, puis utiliser le même nom de projet Compose que pour la sauvegarde. PowerShell 7 et assez d’espace pour les bases, l’ancienne copie locale et le nouveau volume RabbitMQ sont requis.

```powershell
$recoveryDirectory = 'E:\JobAgentBackups\recovery-20261020-120000'
./tools/Restore-LocalRecoverySet.ps1 `
    -RecoveryDirectory $recoveryDirectory `
    -ComposeProjectName 'job-application-agent' `
    -EnvFile .env `
    -AllowServiceRestart
```

La commande vérifie les empreintes de l’index, des manifestes, des huit archives PostgreSQL, de l’archive RabbitMQ et de son image. Elle teste les bases dans des bases temporaires, restaure chaque base dans une base de préparation et conserve toute base déjà présente sous `jobagent_previous_<id>`. Elle extrait RabbitMQ dans un nouveau volume, charge et épingle l’image sauvegardée, puis démarre un broker temporaire avec le nom d’hôte et l’identité de nœud d’origine pour valider ses métadonnées. Le volume Compose précédent n’est jamais écrasé.

Après validation, le script écrit `.local/rabbitmq-recovery.yml`, qui relie le service au nouveau volume et fixe l’image et l’identité du broker. Ce fichier est ignoré par Git et ajouté automatiquement par le guide de démarrage Windows ainsi que par les sauvegardes ultérieures. Le script démarre les APIs et l’interface, attend leurs contrôles de santé, puis interroge le Gateway et le frontend. Il demande confirmation avant l’opération ; `-AllowServiceRestart` est également obligatoire.

## Contrôles après reprise

Depuis le dépôt, vérifier l’état Compose et les endpoints :

```powershell
docker compose @compose ps
Invoke-RestMethod 'http://127.0.0.1:5000/health/ready'
Invoke-WebRequest 'http://127.0.0.1:4200/'
```

Se connecter ensuite dans le navigateur avec un compte restauré et vérifier son profil et ses candidatures. Les anciennes bases portent les noms temporaires communiqués par le script ; ne pas les supprimer avant d’avoir validé les données. L’ancien volume RabbitMQ reste conservé lui aussi.

Une erreur au milieu de la restauration laisse les APIs arrêtées afin d’éviter des écritures sur des bases partiellement remplacées. Les anciennes bases et l’ancien volume restent disponibles ; inspecter l’erreur, les journaux Compose et le fichier d’override avant de poursuivre. Si l’override a déjà été écrit, il est conservé avec le volume restauré pour permettre une reprise manuelle.

## Éléments à restaurer séparément

Le jeu ne contient ni `.env`, ni certificats OpenIddict ou TLS, ni réglages propres à la machine, ni modèle Ollama. Restaurer les valeurs RabbitMQ d’origine dans `.env` : les identifiants des API doivent correspondre aux comptes du broker sauvegardé. Recréer ou télécharger les autres images de conteneurs si nécessaire. La CI exerce le script complet sur un runner éphémère ; un run vert sur le commit livré et un exercice contrôlé sur l’hôte réel restent les preuves de sortie V1.
