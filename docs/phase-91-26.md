# Phase 91.26 — limiter les Bearer statiques des API privées

Les API Agents, Applications et Documents acceptaient leur jeton `LocalAuth` dans tous les environnements. Ces identités représentent un propriétaire fixe et auraient permis de contourner l’isolation des comptes si un port ou un secret était exposé.

Comme pour Profile, les Bearer statiques de compatibilité ne sont maintenant activés qu’en `Development` et `Testing`. Dans les autres environnements, les API ignorent toute configuration `LocalAuth` et n’acceptent que le jeton OpenIddict validé par l’émetteur configuré. Les tests vérifient que des credentials statiques pourtant valides donnent une identité désactivée hors environnements locaux ; les configurations locales invalides restent rejetées.

Validation : Agents 1/1, Applications 1/1 et Documents 4/4 tests ciblés réussis en Release ; les trois projets d’API et leurs projets de test compilent sans erreur.
