# Phase 95.19 — Remplacer les pièces d'un dossier brouillon

Dans un dossier au brouillon, Applications charge les exports Documents approuvés dont la préparation cible la même offre. Le candidat peut associer un nouvel export au CV ou à la lettre ; l'API valide le propriétaire, l'offre et le type avant de l'enregistrer. L'écran empêche de re-sélectionner l'export déjà associé et permet de poursuivre vers la revue lorsque les deux pièces sont présentes.

Le scénario Playwright couvre le filtrage d'un export d'une autre offre, le remplacement du CV, puis la revue, l'approbation et le retour au brouillon. `npm run build` réussit et `npx playwright test --list` détecte 15 scénarios. L'exécution Chromium reste à faire : le serveur local configuré par Playwright n'a pas ouvert le port 4200.
