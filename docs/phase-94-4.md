# Phase 94.4 — Correctifs des dépendances frontend

L'audit npm avait trouvé sept avis de sévérité haute ou critique dans le frontend. Les dépendances Angular ont été mises à jour aux correctifs de la branche 21.2 : `@angular/build` et `@angular/cli` en 21.2.27, et les paquets d'exécution/compilation Angular en 21.2.25. Le lockfile a également reçu les versions corrigées des dépendances transitives (`piscina`, `@modelcontextprotocol/sdk`, `http-cache-semantics` et `source-map-js`). Aucun passage à Angular 22 n'est requis.

Vérification : `npm audit` retourne zéro vulnérabilité sur 579 dépendances résolues. Les plages minimales dans `package.json` sont relevées aux correctifs Angular 21.2 afin d'éviter de réinstaller les versions vulnérables. La compilation de production Angular et les tests du navigateur sont contrôlés séparément ; aucun test n'est exécuté sur le poste.
