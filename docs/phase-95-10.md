# Phase 95.10 — Revue humaine du contenu préparé

Le parcours Chromium ouvre une préparation et relit le contenu composé. Il vérifie que l'approbation est désactivée avant confirmation explicite, puis envoie la décision avec l'empreinte du contenu, la confirmation et la note de revue. L'état approuvé et la note apparaissent ensuite dans l'interface.

La session et les endpoints Documents sont simulés. Le scénario n'exporte aucun fichier et ne soumet pas de candidature à un recruteur. La CI exécute ce test avec la suite `npm run test:e2e`.

Validation locale : formatage, compilation TypeScript et découverte des tests sont vérifiés sans exécuter Chromium.
