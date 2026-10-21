# Phase 90.1 — socle visuel et tableau de bord

Cette première tranche de la phase 90 établit une direction visuelle claire et une première page d’accueil utile, sans inventer de données de candidature.

## Maquette fonctionnelle

- Navigation persistante à gauche, avec les quatre destinations actuellement implémentées : tableau de bord, recommandations, offres et profil. Sur petit écran, le menu reste piloté par l’en-tête mobile.
- Tableau de bord : bandeau d’accueil, nombre d’offres Matching disponibles, meilleur score parmi les trois offres affichées, aperçu des recommandations et accès directs au profil et au catalogue.
- Recommandations : liste classée, trois indicateurs distincts (classement, compatibilité, couverture), détails des critères à la demande, états de chargement, erreur et liste vide, pagination.
- Thème clair, palette bleu-violet, surfaces aérées, focus clavier visible et réduction des animations selon la préférence système.

## Implémentation et données

Le tableau de bord et la liste utilisent l’API Matching existante. Le tableau de bord demande les trois premières offres ; son meilleur score est explicitement limité à cet aperçu. La navigation ne présente pas les routes de candidatures, CV, agents ou paramètres tant que leurs pages ne sont pas disponibles.

## Vérification

`npm run build` dans `src/frontend/job-application-agent-ui` réussit. Angular signale des budgets SCSS dépassés pour le tableau de bord (styles intégrés au composant) et plusieurs écrans existants, sans échec de compilation. Les tests n’ont pas été lancés.

## Suite

La phase 90 reste à réaliser : écrans détaillés du profil et des expériences, détail d’offre, documents et CV, préparation et suivi des candidatures, paramètres, puis parcours d’authentification selon les contrats backend disponibles.
