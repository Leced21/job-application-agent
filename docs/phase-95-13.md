# Phase 95.13 — Enregistrer un retour recruteur

L'écran Applications permet de consigner un retour reçu pour une candidature envoyée, en cours ou en entretien. Le candidat saisit le statut suivant parmi les transitions autorisées, la date, le canal et le message, puis confirme qu'il a reçu ce retour. L'historique et le statut sont actualisés après enregistrement.

Le test Playwright simule l'API et vérifie le jeton antiforgery, la révision attendue, la transition vers un entretien et le message conservé. La saisie est manuelle : aucun message n'est envoyé à un recruteur.

Validation locale : build Angular, formatage, compilation TypeScript et découverte Playwright. Chromium est exécuté par la CI.
