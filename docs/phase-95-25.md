# Phase 95.25 — Actualiser les relances échues après modification

Après la replanification d'un rappel, l'écran recharge la liste des candidatures et les échéances échues. Le test navigateur part d'une relance échue, vérifie qu'elle apparaît dans la liste, la replanifie dans le futur puis confirme que l'ancienne échéance disparaît tandis que le nouvel historique reste visible.

Validation : `npm run build` et `npx playwright test --list`.
