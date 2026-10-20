# Phase 97.1 — Architecture et installation Windows

La documentation V1 commence par une vue d'architecture Mermaid des composants et événements dans [architecture.md](architecture.md), puis un parcours d'installation Windows 11 qui couvre les prérequis, les paramètres locaux, Compose, les migrations, le démarrage et les tests dans [windows-install.md](windows-install.md).

Le guide reprend les versions d'outils utilisées par la CI (`.NET 10`, Node 22), décrit le démarrage à partir de `.env.example` sans enregistrer de secrets dans Git, et renvoie aux guides de migration par service. Il distingue l'interface intégrée Compose de `npm start`, qui nécessite un Gateway local sur le port configuré par le proxy frontend.

Vérification documentaire : la configuration Compose complète est validée avec `docker compose config --quiet` à partir de `.env.example`. Aucun conteneur ni aucune base persistante ne sont démarrés par cette étape.
