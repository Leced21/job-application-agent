# Phase 93.3 — démarrage conteneurisé de Matching

Le Compose principal construit Matching et attend sa base PostgreSQL. Les adresses Job et Profile pointent sur les services Docker correspondants. L’API est publiée uniquement sur `127.0.0.1:5186`, avec un contrôle `/health/ready`. Le consommateur RabbitMQ reste désactivé dans le profil local.

Lancer le socle backend conteneurisé :

```powershell
docker compose up -d --build profile-db profile-api job-db job-api matching-db matching-api
```

Les migrations des trois bases restent explicites depuis l’hôte. L’authentification OpenIddict et le Gateway ne sont pas encore inclus dans cette topologie partielle ; les routes Matching protégées nécessitent donc l’étape d’intégration correspondante avant un parcours utilisateur complet.

Vérification : `docker compose config --quiet`, contrôle des chemins `COPY` du Dockerfile et compilation Matching API/tests. La construction d’image reste conditionnée à l’accès NuGet et aux images .NET.
