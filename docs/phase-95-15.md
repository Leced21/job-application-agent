# Phase 95.15 — Annuler un rappel candidat

Le scénario navigateur annule une relance planifiée après saisie d'un motif et confirmation explicite. Il vérifie la commande API et l'antiforgery, puis contrôle la disparition du rappel actif et la conservation de l'annulation dans l'historique.

La session et l'API Applications sont simulées dans Playwright. Aucun message n'est envoyé et aucun service externe n'est contacté.

Validation locale : formatage, compilation TypeScript, build Angular et découverte Playwright. L'exécution Chromium est effectuée par la CI.
