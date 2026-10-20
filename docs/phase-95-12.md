# Phase 95.12 — Déclaration d'un envoi manuel

L'écran Applications permet, pour un dossier approuvé, d'enregistrer un envoi déjà effectué par le candidat. La confirmation est obligatoire ; la date, le canal et une référence facultative sont envoyés avec la révision attendue. Le statut et l'événement `ManualSubmissionDeclared` apparaissent ensuite dans le suivi.

Le parcours Playwright simule la session et l'API Applications, vérifie le jeton antiforgery et le contrat de commande. Il s'agit d'une déclaration après envoi manuel : aucun envoi externe n'est déclenché.

Validation locale : build Angular, formatage, compilation TypeScript et découverte Playwright. L'exécution Chromium reste confiée à la CI.
