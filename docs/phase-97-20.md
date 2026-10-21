# Phase 97.20 — Inscription et connexion navigateur sur la pile Compose réelle

La CI réutilise maintenant l'interface Nginx démarrée par Compose et exécute un scénario Chromium sans interception réseau. Le navigateur crée un compte synthétique via le formulaire, vérifie la session, efface les cookies, puis se reconnecte avec les mêmes identifiants. Cela exerce le proxy `/auth`, le cookie de session, l'antiforgery, le Gateway et la persistance Identity PostgreSQL ensemble.

L'adresse e-mail est unique par exécution et utilise le domaine réservé `example.test`. Le compte et la base restent sur le runner GitHub éphémère. Les autres scénarios E2E Angular continuent d'utiliser leurs réponses simulées afin de tester séparément les parcours UI.

## Vérification

Playwright découvre et compile le scénario Chromium ciblé avec `--list`. L'inscription et la reconnexion réelles sont exécutées par `container-images` après application des migrations et démarrage de toute la pile. Le démon Docker de ce poste n'étant pas disponible, le résultat d'exécution reste à confirmer par un run CI.
