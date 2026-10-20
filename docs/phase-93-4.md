# Phase 93.4 — démarrage conteneurisé d’Applications

Un overlay Compose ajoute Applications sans imposer ses identifiants locaux au Compose principal. Il attend PostgreSQL, Job et Profile en état healthy, publie l’API sur `127.0.0.1:5007` et exige les paramètres locaux de base, propriétaire, profil et jeton depuis `.env`.

Les appels sources peuvent utiliser les adresses `job-api:8080` et `profile-api:8080` uniquement. L’application reste protégée par son jeton Bearer local. L’overlay Documents n’étant pas encore ajouté, la vérification des pièces pour approbation reste indisponible dans cette topologie.

Vérification : validation de l’overlay avec `.env.example`, compilation de l’API et des tests unitaires Applications. Les tests n’ont pas été exécutés ; l’image n’a pas été construite.
