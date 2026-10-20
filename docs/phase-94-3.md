# Phase 94.3 — Mises à jour des dépendances

Dependabot vérifie chaque lundi les dépendances NuGet de `src` et `tests`, npm du frontend, les images Docker déclarées sous `src` et les références d'actions GitHub. Il peut ouvrir au maximum cinq pull requests ouvertes par écosystème afin de limiter le bruit. Les versions restent soumises aux validations de la CI 94.1, qui compile, teste et audite les dépendances avant fusion.

La configuration utilise les blocs `directories` et leurs motifs glob pour couvrir le monorepo, comme décrit dans la [référence officielle Dependabot](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference#directories-or-directory), et le fuseau `Europe/Paris` pour fixer l'heure des vérifications. Les mises à jour de sécurité Dependabot doivent également être activées dans les paramètres du dépôt si elles ne le sont pas déjà.

Validation locale : le YAML est contrôlé avec Prettier. L'ouverture effective de pull requests est gérée par GitHub et n'est pas déclenchée depuis le poste local.
