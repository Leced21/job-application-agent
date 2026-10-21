# Phase 95.8 — Historique de versions des CV

Le test navigateur crée un dossier de versions en sélectionnant explicitement un CV importé et en attribuant un libellé à la version initiale. Il vérifie le contrat POST, l'antiforgery et l'affichage du dossier après rechargement des données.

Documents et la session sont simulés dans Playwright. Le scénario ne regroupe aucun fichier automatiquement et ne transmet aucune donnée à un service réel. Il est exécuté par le job Chromium de la CI avec `npm run test:e2e`.

Validation locale : formatage, compilation TypeScript et découverte des tests sont vérifiés sans lancer le navigateur.
