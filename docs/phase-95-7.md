# Phase 95.7 — Import d'un CV

Le parcours Chromium importe un PDF dans la bibliothèque Documents. Il vérifie l'état vide initial, la sélection du fichier, le POST multipart avec le jeton antiforgery et le rendu du CV conservé avec son statut non analysé.

Les endpoints Documents et la session sont simulés dans Playwright. Le fichier PDF est synthétique, aucun service ni compte réel n'est utilisé et aucun document n'est transmis à l'extérieur.

Validation locale : formatage, compilation TypeScript et découverte Playwright sont vérifiés sans lancer Chromium. L'exécution navigateur est effectuée en CI ou depuis un poste équipé du navigateur Playwright.
