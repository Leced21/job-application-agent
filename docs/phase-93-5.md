# Phase 93.5 — démarrage conteneurisé de Documents

Un Dockerfile et un overlay Compose ajoutent l’API Documents sans ajouter ses secrets au Compose principal. Le service attend PostgreSQL et Job, publie l’API sur `127.0.0.1:5005` et conserve les contrôles d’état. L’accès direct utilise un jeton Documents dédié ; le canal de confirmation Applications utilise le jeton Applications, avec OwnerId et ProfileId communs aux deux services.

Applications peut désormais joindre Documents via `documents-api:8080` pour vérifier les reçus de pièces. Les sources internes Documents (Job) et Applications (Job/Profile) n’acceptent que les noms de service Compose attendus sur le port 8080, ou les origines loopback existantes. Les autres hôtes restent refusés.

Vérification : validation de l’ensemble des Compose avec `.env.example`, compilation des APIs et projets de tests Applications/Documents. Les tests n’ont pas été exécutés ; aucune image ni base n’a été démarrée.
