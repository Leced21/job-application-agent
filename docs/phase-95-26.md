# Phase 95.26 — Stabiliser les tests navigateur

Playwright réutilise le serveur Angular déjà lancé en local et démarre son propre serveur en CI. Les scénarios ont été réalignés sur les libellés, les éléments accessibles et les contrats actuels des APIs. Les confirmations du dossier utilisent des signaux Angular, afin que l'état des cases soit immédiatement pris en compte par les boutons.

Validation réelle avec Chromium : compilation `npm run build` réussie et 19 tests navigateur réussis. Sur un poste où le navigateur Playwright n'est pas encore présent, l'installer avec `npx playwright install chromium`.
