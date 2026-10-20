# Phase 95.14 — Réaliser ou annuler une relance

Le détail d'une candidature avec une relance planifiée permet maintenant de la marquer comme effectuée en enregistrant la date, le canal, le résultat et une confirmation. Le candidat peut aussi annuler le rappel en indiquant un motif. Les deux décisions sont conservées dans l'historique et ne déclenchent aucun message.

Le parcours Playwright vérifie la complétion d'une relance avec une réponse API simulée, le jeton antiforgery, la révision attendue et le résultat affiché.

Validation locale : build Angular, formatage, compilation TypeScript et découverte Playwright. Le navigateur est exécuté par la CI.
