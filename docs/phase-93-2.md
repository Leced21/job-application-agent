# Phase 93.2 — démarrage conteneurisé de Job

Le Compose principal construit le service Job et l’attache à `job-db`. Il attend le contrôle PostgreSQL avant de démarrer l’API, expose le port `5002` uniquement sur loopback et vérifie `/health/ready`. Le relay RabbitMQ est explicitement désactivé dans ce profil.

Le Dockerfile restaure et publie les projets Job requis dans une image ASP.NET .NET 10. Les migrations restent explicites et doivent être appliquées depuis l’hôte avant les opérations métier.

Vérification : `docker compose config --quiet` et `git diff --check`. L’image n’est pas construite dans ce lot car la restauration NuGet est actuellement indisponible par DNS.
