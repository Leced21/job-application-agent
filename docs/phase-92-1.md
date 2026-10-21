# Phase 92.1 — identifiant de corrélation du Gateway

Le Gateway accepte `X-Correlation-ID` uniquement lorsqu’il contient un GUID valide. Pour toute valeur absente, ambiguë ou mal formée, il génère un identifiant. Il renvoie cet identifiant dans la réponse, l’ajoute au scope des logs et le transmet aux services derrière YARP. Le même identifiant devient le `TraceIdentifier` ASP.NET et le tag `correlation.id` de l’activité courante, quand une activité existe.

Cette première tranche rend les requêtes HTTP traçables depuis l’entrée Gateway jusqu’aux services routés sans journaliser de données de profil. Les métriques, les traces OpenTelemetry exportées et la corrélation des messages RabbitMQ restent à traiter dans les étapes suivantes de la phase 92.

Vérification : compilation de l’API Gateway et du projet de tests unitaires réussie. Les tests ont été ajoutés, mais ne sont pas exécutés conformément à la consigne de travail en cours.
