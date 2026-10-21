# Phase 95.5 — Mise à jour du profil candidat

Le parcours Playwright ajoute une compétence au profil par l'interface Angular. Il vérifie le rendu du profil, la validation du formulaire, le contrat JSON envoyé à l'API Profile, l'en-tête antiforgery et l'affichage de la compétence enregistrée. Les réponses Profile et la session sont simulées dans le navigateur : aucun compte ni service réel n'est utilisé.

La CI Chromium exécute le scénario avec les autres parcours `npm run test:e2e`.

Validation locale : formatage, compilation TypeScript et découverte Playwright sont vérifiés sans exécuter Chromium. L'exécution complète est effectuée en CI ou depuis un poste doté du navigateur Playwright.
