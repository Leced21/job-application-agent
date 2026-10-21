# Phase 95.18 — Renvoyer un dossier en revue au brouillon

Depuis un dossier en revue ou approuvé, le candidat peut confirmer son retour au brouillon afin de modifier ses pièces. Les documents associés sont conservés comme point de départ ; toute nouvelle revue et approbation utilisent ensuite la révision actualisée du dossier.

Le scénario navigateur couvre la demande de revue, l'approbation confirmée, puis le retour au brouillon confirmé. `npm run build` réussit et `npx playwright test --list` détecte 15 scénarios. L'exécution navigateur reste à faire : le serveur local configuré par Playwright n'a pas ouvert le port 4200.
