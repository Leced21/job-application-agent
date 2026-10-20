# Phase 93.6 — démarrage conteneurisé de Collection

Un Dockerfile et un overlay Compose ajoutent l’API Collection. Elle attend sa base, Job et RabbitMQ, et s’expose uniquement sur `127.0.0.1:5004`. L’URL de publication interne pointe vers `job-api:8080`. Le broker est disponible, tandis que le relay outbox reste explicitement désactivé par défaut.

Le fichier `.env.example` fournit des noms de variables pour la base Collection et le compte RabbitMQ local ; remplacer les valeurs avant le démarrage. Les migrations des bases Collection et Job restent explicites.

Vérification : validation de l’overlay avec les Compose principaux et `.env.example`, compilation de l’API et des projets de tests Collection. Les tests n’ont pas été exécutés et aucune image/base n’a été démarrée.
