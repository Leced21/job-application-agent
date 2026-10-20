# Phase 95.20 — Retirer une candidature active

Le candidat peut clôturer le suivi d'une candidature active en fournissant un motif et en cochant une confirmation explicite. L'API enregistre le retrait dans l'historique et annule les relances en attente ; aucun message n'est envoyé. Les dossiers déjà retirés, refusés ou ayant reçu une offre ne proposent pas cette action.

Le scénario Playwright vérifie le motif, la confirmation, le changement de statut et la disparition de l'action après retrait. `npm run build` réussit et `npx playwright test --list` détecte 16 scénarios. L'exécution Chromium reste à faire : le serveur local configuré par Playwright n'a pas ouvert le port 4200.
