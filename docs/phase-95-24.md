# Phase 95.24 — Valider les échéances de relance

Les formulaires de création et de replanification limitent maintenant l'échéance à une date située entre une minute dans le futur et 365 jours. La validation est aussi appliquée avant l'appel API. Les réponses HTTP 400 affichent un message de saisie invalide, tandis qu'un conflit de révision conserve l'invitation à actualiser le dossier.

Le scénario navigateur vérifie les bornes min/max du champ de date lors d'une replanification. Validation : `npm run build` et `npx playwright test --list`.
