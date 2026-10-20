# Phase 95.21 — Prévisualiser et télécharger les pièces du dossier

Les exports associés à une candidature affichent leur nom et leur format dans Applications. Le candidat peut prévisualiser un export PDF dans le dossier ou télécharger une pièce. Le contenu passe par l'API Documents authentifiée ; l'interface crée une URL blob locale et la révoque lorsque l'aperçu est fermé, remplacé ou que le composant est détruit.

Le scénario navigateur vérifie le téléchargement authentifié du PDF et l'affichage de l'aperçu intégré. `npm run build` réussit et `npx playwright test --list` détecte 16 scénarios. L'exécution Chromium reste à faire, le serveur local configuré par Playwright n'ayant pas ouvert le port 4200.
