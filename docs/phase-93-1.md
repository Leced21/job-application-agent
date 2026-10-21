# Phase 93.1 — démarrage conteneurisé de Profile

Le Compose principal construit l’image Profile à partir du Dockerfile existant, attend que PostgreSQL soit prêt, puis publie l’API sur `127.0.0.1:5001`. Un contrôle Docker interroge `/health/ready`. PostgreSQL reste publié uniquement sur loopback et conserve son volume nommé.

Le fichier `.dockerignore` exclut les caches de build, dépendances frontend, fichiers d’environnement locaux et répertoires de secrets du contexte d’image.

Depuis la racine du dépôt :

```powershell
docker compose up -d --build profile-db profile-api
```

La base vide nécessite l’application préalable des migrations Profile depuis l’hôte ; l’API ne lance pas de migration automatiquement. Après configuration locale du SDK .NET et des dépendances déjà restaurées :

```powershell
dotnet ef database update --project src/services/Profile/JobApplicationAgent.Profile.Infrastructure --startup-project src/services/Profile/JobApplicationAgent.Profile.Api
```

Vérification : `docker compose config --quiet`. La construction et l’exécution de l’image nécessitent l’accès au moteur Docker et aux images de base ; elles ne sont pas déclenchées par ce lot.
