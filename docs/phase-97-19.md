# Phase 97.19 — Démarrage complet Compose après migrations dans la CI

Après le build des images, le workflow installe le CLI EF Core, applique les huit migrations sur les bases temporaires du runner à l’aide de `tools/Update-LocalDatabases.ps1`, puis démarre toute la pile Compose. Le projet Compose est explicitement nommé `job-application-agent`, de façon cohérente entre migration, démarrage et nettoyage.

Compose attend l’état sain des API et du Gateway. La CI vérifie ensuite les endpoints `/health/ready` des huit APIs publiées, ainsi que la page du frontend. Le nettoyage exécute `docker compose down --remove-orphans` sans supprimer les volumes ; les bases sont néanmoins temporaires, car le runner est éphémère.

Les migrations utilisent `.env.example` uniquement sur le runner isolé, sans données réelles ni secret de production. Ce contrôle valide le démarrage et la readiness après migration, mais ne remplace pas un parcours complet d’inscription, les tests de contrat déjà présents, ni la validation manuelle du compte dans le navigateur.

## Vérification

La configuration Compose complète et les noms des huit ports API sont vérifiés localement. Le démon Docker de ce poste n’étant pas disponible, l’exécution réelle des migrations et le démarrage de la pile devront être confirmés par le prochain run GitHub Actions.
