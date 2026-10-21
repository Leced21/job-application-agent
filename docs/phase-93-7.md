# Phase 93.7 — démarrage conteneurisé d’Agents

Un Dockerfile et un overlay Compose ajoutent l’API Agents avec son PostgreSQL dédié. L’API est publiée uniquement sur `127.0.0.1:5008`; ses appels internes vers Job, Profile et Matching utilisent les noms DNS du réseau Compose. Les connecteurs métier et le fournisseur Ollama restent désactivés par défaut. L’identité locale, le jeton et les paramètres PostgreSQL viennent de `.env`.

Les validateurs d’origine acceptent les boucles locales utilisées hors Docker et uniquement les hôtes Compose autorisés sur le port interne 8080. Les autres noms d’hôte, ports et schémas sont refusés. Ollama doit être activé avec sa configuration et son tokenizer qualifiés conformément au guide local ; cet overlay ne l’active pas.

Copier `.env.example` vers `.env`, remplacer les valeurs de démonstration et lancer :

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.agents.yml -f docker-compose.agents-api.yml up -d --build
```

Appliquer explicitement la migration Agents depuis l’environnement .NET local, puis contrôler `/health/ready`. La base Job n’est pas modifiée par cette étape. L’overlay Agents n’inclut pas Applications ; activer le connecteur de suivi exige aussi son overlay et sa configuration d’authentification.

Vérification : validation de la configuration Compose combinée avec `.env.example`, compilation de l’API Agents et des projets de tests Agents. Les tests ne sont pas exécutés et aucun conteneur ni aucune base ne sont démarrés.
