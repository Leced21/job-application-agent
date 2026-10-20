# Phase 95.4 — Reprise après indisponibilité de Matching

Le parcours Chromium couvre les erreurs et reprises du parcours de connexion et des recommandations. Il simule une réponse HTTP 503 de Matching, vérifie le message d'indisponibilité puis la reprise au clic sur « Réessayer ». Il couvre l'expiration de session sur une route protégée, la conservation de l'URL de retour, et le retour effectif à cette destination après une connexion réussie. Enfin, une tentative de connexion avec des identifiants invalides doit afficher un message clair et rester sur la page de connexion.

Le scénario ne contacte aucun service externe et ne manipule aucune candidature. Le job frontend CI exécute déjà `npm run test:e2e` dans Chromium et inclut ce contrôle.

Validation locale : formatage, compilation TypeScript et découverte Playwright (`npm run test:e2e -- --list`) sont vérifiés sans lancer le navigateur. L'exécution complète reste confiée à la CI ou à un poste avec Chromium Playwright installé.
