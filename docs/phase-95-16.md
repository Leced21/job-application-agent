# Phase 95.16 — Créer un dossier depuis une préparation approuvée

Depuis une préparation dont le contenu est approuvé et exporté, le candidat peut créer ou retrouver le dossier Applications associé à son profil et à l’offre. L’écran associe le PDF approuvé correspondant, ou le DOCX lorsqu’aucun PDF n’existe. La création ne soumet pas la candidature ; le candidat peut retrouver le suivi dans Applications.

Le scénario Playwright vérifie la commande de création, la protection antiforgery et l’association de l’export avec la révision attendue. `npm run build` réussit. `npx playwright test --list` confirme le chargement des 14 scénarios ; l’exécution ciblée n’a pas démarré dans cet environnement, le serveur local configuré par Playwright n’ouvrant pas son port.
