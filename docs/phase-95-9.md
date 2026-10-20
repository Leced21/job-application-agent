# Phase 95.9 — Ajouter une version de CV

Le parcours navigateur ouvre l'historique d'un dossier, ajoute un CV déjà importé avec le libellé choisi, puis rouvre l'historique pour vérifier les deux versions et leurs libellés. Il contrôle le chemin API, le contrat POST et le jeton antiforgery.

La session et Documents sont simulés dans Playwright. L'ajout est une organisation locale du dossier candidat : aucun envoi de candidature n'a lieu. La CI Chromium inclut ce scénario dans `npm run test:e2e`.

Validation locale : formatage, compilation TypeScript et découverte des tests sont vérifiés sans exécuter Chromium.
