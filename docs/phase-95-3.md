# Phase 95.3 — Parcours candidat dans Chromium

Playwright exécute maintenant un véritable test navigateur de la connexion et des recommandations. Il démarre Angular en mode développement, simule les endpoints du Gateway et Matching, vérifie la redirection d'une session anonyme, envoie le formulaire avec le jeton antiforgery, puis suit le candidat du tableau de bord vers le classement. Le scénario valide le rendu navigateur, les routes, l'en-tête antiforgery et les contrats HTTP attendus sans utiliser de compte ou de données réels.

Le job frontend CI installe Chromium, exécute `npm run test:e2e` après les tests unitaires et conserve le rapport Playwright pendant 14 jours. Le serveur Angular est démarré et arrêté par Playwright. Angular limite explicitement la découverte Vitest aux specs de `src` pour ne pas charger les specs Playwright. Les données simulées n'effectuent aucun envoi de candidature.

Validation locale : la configuration et le scénario sont vérifiés par compilation TypeScript, sans exécuter le test navigateur. Pour l'exécuter sur un poste où Chromium Playwright est installé : `npm run test:e2e` depuis `src/frontend/job-application-agent-ui`.
