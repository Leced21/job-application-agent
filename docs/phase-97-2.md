# Phase 97.2 — Guide des tests

Le guide [testing.md](testing.md) documente les commandes réellement utilisées pour compiler la solution, exécuter chaque projet de test backend, lancer Vitest et Playwright, construire Angular et valider la topologie Compose. Il précise les dépendances locales des tests d'intégration et distingue la validation de configuration Compose du démarrage de conteneurs.

La référence des commandes CI est [backend-ci.yml](../.github/workflows/backend-ci.yml). Les exemples sont PowerShell et suivent la structure actuelle du dépôt.

Vérification de cette tranche : `npm run build` réussit pour Angular et la configuration Compose complète passe `docker compose config --quiet`. La suite de tests frontend locale avait précédemment été bloquée par une erreur d'accès temporaire (`EPERM` lors d'un renommage dans le cache Vitest) ; elle doit être relancée hors de cette restriction d'environnement. La suite backend complète a passé 145 tests, avec 2 tests Ollama ignorés, avant cette tranche documentaire.
