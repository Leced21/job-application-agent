# Phase 97.18 — Smoke test CI des services d’infrastructure Compose

Le workflow Repository CI vérifie maintenant le démarrage réel des huit bases PostgreSQL et de RabbitMQ après la construction de toutes les images Compose. Il attend l’état `healthy` via les contrôles définis dans les fichiers Compose, avec un délai maximal de trois minutes, puis arrête les services même si le smoke test échoue.

Le smoke test ne démarre pas les APIs : elles nécessitent des migrations et des données configurées. Il ne touche donc pas aux bases locales du développeur et ne remplace ni l’application locale des migrations de la [phase 97.17](phase-97-17.md), ni la validation complète du parcours navigateur.

## Vérification

La configuration Compose est validée et toutes les images sont construites par le job `container-images`. Sur un runner GitHub Actions, le job démarre les neuf services d’infrastructure et attend leur santé avant le nettoyage. Le démon Docker de ce poste n’étant pas disponible lors de cette modification, le démarrage réel devra être confirmé par le prochain run CI.
