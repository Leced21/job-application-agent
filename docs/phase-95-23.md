# Phase 95.23 — Replanifier une relance

Le candidat peut modifier la date, le canal et la note d'une relance en attente depuis le détail du dossier. L'API conserve le même identifiant de relance et ajoute un événement `FollowUpRescheduled`, ce qui maintient l'historique des plans successifs. L'écriture utilise la révision courante du dossier.

Le scénario navigateur vérifie la requête protégée par antiforgery, la nouvelle échéance et la conservation de l'événement dans l'historique. Validation : `npm run build` et `npx playwright test --list`.
