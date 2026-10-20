# Phase 97.17 — Migrations locales en une commande

Cette phase simplifie l'installation neuve : un script prépare puis migre les huit bases Compose, dont Gateway Identity, avant le démarrage des APIs. Les paramètres de connexion proviennent de la configuration Compose construite depuis `.env`; ils ne sont ni affichés ni écrits dans le dépôt.

## Utilisation

Depuis la racine du dépôt, après avoir copié et configuré `.env.example` en `.env` :

```powershell
.\tools\Update-LocalDatabases.ps1 -WhatIf
.\tools\Update-LocalDatabases.ps1
```

Le premier appel affiche les huit bases, ports et contextes sans appeler Docker pour démarrer des services ni appliquer de migration. Le second demande confirmation, refuse un service de base déjà détecté dans un autre projet Compose, démarre uniquement les huit conteneurs PostgreSQL attendus, attend leur état ready puis applique les migrations Identity, Profile, Job, Matching, Collection, Documents, Applications et Agents. Aucune suppression de volume n'est exécutée. Le script restaure les variables d'environnement du terminal après chaque appel EF.

Pour utiliser un fichier de paramètres différent, fournir `-EnvFile`, par exemple `-EnvFile .env.example -WhatIf` pour inspecter les valeurs d'exemple. Ne pas appliquer de migrations avec les secrets d'exemple.

Si une migration échoue à mi-parcours, les contextes précédents peuvent déjà avoir été mis à jour ; le script s'arrête et conserve tous les volumes. Vérifier le message EF puis relancer après correction. Sauvegarder et tester toute base contenant des données à conserver avec les outils de la [phase 97.16](phase-97-16.md). Le script Identity seul de la [phase 97.12](phase-97-12.md) reste disponible pour une mise à jour ciblée.

## Vérification

`-WhatIf -EnvFile .env.example` a correctement résolu les huit contextes et leurs ports Compose sans accéder au démon Docker. Une simulation du parcours complet avec un faux CLI vérifie les huit appels EF, la restauration des variables d'environnement, le refus d'un autre projet Compose avant démarrage et l'arrêt après un échec simulé. `ProfileDbContextFactory` lit maintenant `ConnectionStrings__ProfileDatabase`, ce qui permet au script de viser la base Compose choisie plutôt qu'une chaîne figée. Le projet Profile.Infrastructure compile en Release avec zéro avertissement et zéro erreur. Aucune migration n'a été appliquée à une base réelle pendant cette phase.
