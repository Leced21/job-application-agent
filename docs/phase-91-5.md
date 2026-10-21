# Phase 91.5 — ports des bases locales

Les ports PostgreSQL des services Profile, Job et Matching dans le compose principal sont maintenant publiés uniquement sur `127.0.0.1`. Ils restent accessibles aux outils exécutés sur le poste de développement, mais Docker ne les lie plus à toutes les interfaces réseau. Les compose dédiés Collection, Documents, Applications et Agents utilisaient déjà cette liaison locale ; RabbitMQ également.

Les numéros de ports et les chaînes de connexion locales ne changent pas. Vérification : la syntaxe Compose est validée quand `docker compose config --quiet` est disponible. Aucun conteneur n’est démarré par cette tranche.

Les API .NET restent actuellement lancées en processus locaux et écoutent selon leur configuration Kestrel. Les ports du Gateway et des API doivent être contrôlés séparément avant une exposition réseau ; cette modification ne complète pas l’authentification de la phase 91.
