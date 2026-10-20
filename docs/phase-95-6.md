# Phase 95.6 — Préférences professionnelles

Le scénario navigateur ajoute la configuration des postes recherchés, localisations, contrats, modes de travail et salaire minimum. Il vérifie que les listes sont nettoyées, que la devise est normalisée en majuscules, que les modes sélectionnés sont sérialisés selon le contrat Profile et que la préférence enregistrée est affichée.

La session, Profile et l'antiforgery sont simulés dans Playwright. Aucun compte ni service réel n'est utilisé. La CI Chromium exécute ce scénario avec les autres tests `npm run test:e2e`.

Validation locale : formatage, compilation TypeScript et découverte Playwright sont vérifiés sans lancer Chromium. L'exécution complète reste confiée à la CI ou à un poste avec le navigateur Playwright installé.
