# Phase 97.32 — Démarrage local Identity et readiness Compose

L'écran de connexion affichait « Le service de comptes est indisponible » parce que le Gateway redémarrait sur l'absence des tables `OpenIddictApplications`. Les migrations n'avaient pas encore été appliquées à la base Identity locale. `tools/Update-LocalIdentityDatabase.ps1` a appliqué les migrations Identity sur le volume existant, sans le supprimer. Après redémarrage, `/auth/session` répond `200` avec `registrationEnabled: true`.

Le démarrage complet avait également un défaut indépendant : les healthchecks HTTP des conteneurs .NET utilisaient `curl`, mais les images runtime ne l'incluaient pas. `curl` est maintenant installé dans les huit images API ; l'image Gateway garde aussi `libgssapi-krb5-2`, requise par le client PostgreSQL dans l'environnement Linux.

La base Agents du projet principal était détachée du réseau `job-application-agent_default`, tandis qu'une ancienne base isolée du projet `job-application-agent-agents` occupait le port local 5438. L'ancienne instance a été arrêtée, mais son conteneur et son volume sont conservés. Le conteneur de la base Agents principale a été recréé sur le réseau Compose en réutilisant son volume existant. La base historique isolée reste disponible pour une récupération explicite.

## Vérification locale

- Les migrations Gateway Identity se sont appliquées et les tables ASP.NET Identity/OpenIddict sont présentes.
- `GET /auth/session` renvoie `200`, `registrationEnabled: true` et `isAuthenticated: false`.
- Les sept APIs, le Gateway et les bases Compose sont signalés `healthy` par Docker après reconstruction et redémarrage.
- L'image Gateway démarre sans l'avertissement `libgssapi_krb5.so.2`.
- Aucune inscription synthétique n'a été ajoutée à la base locale.

Après actualisation de la page de connexion, l'action « Créer un compte » est disponible. Une fois inscrit, le candidat peut créer son profil depuis `/profile` selon la [phase 97.31](phase-97-31.md).
