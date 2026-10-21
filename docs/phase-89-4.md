# Phase 89.4 — vérification du transport Job vers Matching

Un test d’intégration de bout en bout relie l’outbox PostgreSQL Job, le dispatcher et le publisher RabbitMQ réels au worker Matching hébergé et à son inbox PostgreSQL. Job écrit un message `job.published.v1` durable ; Matching le traite avec des clients Job et Profile synthétiques, configurés uniquement dans le test.

Le test republie le même identifiant d’événement, puis un payload invalide. Il vérifie qu’un seul événement et un seul résultat Matching sont conservés, et que le message invalide arrive dans `matching.job-published.v1.dead`. Les conteneurs RabbitMQ et PostgreSQL sont éphémères et réservés à ce test. Son exécution nécessite Docker ; il n’a pas été exécuté dans cette étape. Aucun conteneur de développement, broker ou base persistante n’a été démarré ou modifié.

La compilation ciblée du projet d’intégration Matching réussit sans avertissement ni erreur. Le parcours HTTP de publication Collection vers Job reste couvert séparément par les tests Job du lot 89.2 ; ici, le test démarre au message déjà écrit dans l’outbox afin d’exercer le transport réel et le consommateur complet.
