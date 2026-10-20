# Phase 91.31 — Réactiver l’inscription multi-compte

Le Gateway n’acceptait jusque-là que le premier compte Identity, ce qui empêchait de créer un compte supplémentaire même lorsque les APIs savaient isoler les données par propriétaire. L’inscription reste contrôlée par `Identity:AllowRegistration` et désactivée par défaut hors `Development`.

Le premier compte doit toujours recevoir `Identity:InitialOwnerId`, configuré avec le propriétaire des données locales historiques. Chaque compte créé ensuite reçoit un UUID propriétaire neuf généré côté serveur ; le navigateur ne peut pas choisir cet identifiant. La page de connexion expose l’inscription lorsque la configuration l’autorise et qu’un propriétaire initial existe ou qu’un compte est déjà présent. La transaction sérialisable conserve la protection contre deux premières inscriptions concurrentes.

Validation : les tests unitaires Gateway passent (11/11). Le test d’intégration démarre PostgreSQL avec Testcontainers, applique les migrations Identity/OpenIddict, puis vérifie le parcours HTTP complet : visibilité de l’inscription, création et connexion de deux comptes, et attribution de propriétaires distincts. Exécution : `dotnet test tests/Gateway/JobApplicationAgent.Gateway.IntegrationTests/JobApplicationAgent.Gateway.IntegrationTests.csproj --configuration Release`.
