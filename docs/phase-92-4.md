# Phase 92.4 — liveness et readiness PostgreSQL

Le Gateway et les APIs Profile, Job, Matching, Agents, Applications, Documents et Collection exposent désormais `/health/live`, `/health/ready` et `/health`. La route live vérifie uniquement que le processus répond ; les routes ready et health vérifient l’accès à PostgreSQL. Le Gateway ajoute le contrôle Identity uniquement lorsque sa base est configurée.

Les réponses health exposent l’état global sans détail d’exception ou de chaîne de connexion. Les appels PostgreSQL utilisent `CanConnectAsync` et un contexte de durée limitée. RabbitMQ et les dépendances du modèle Ollama ne bloquent pas encore la readiness ; leurs contrôles seront ajoutés selon l’activation effective des workers.

Vérification : compilation des APIs concernées réussie ; tests non exécutés conformément à la consigne de travail en cours.
