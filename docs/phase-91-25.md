# Phase 91.25 — désactiver l’identité statique Profile hors environnement local

Le Bearer `Profile:LocalAuth` autorisait toutes les opérations CRUD du profil sous un propriétaire fixe, y compris lorsque l’API tournait hors de `Development`. Il est maintenant accepté uniquement en `Development` et `Testing`. En `Production` et dans les autres environnements, l’identité statique est désactivée même si un ancien secret est encore présent en configuration ; les appels utilisateur doivent passer par un jeton OpenIddict validé.

Les tests vérifient que cette désactivation prime sur une configuration contenant un secret valide, et que le mode de transition local continue à dériver son propriétaire du paramètre configuré. `Profile:DocumentImport` reste un canal dédié, à secret distinct et limité à l’import de faits confirmés.

Validation : tests ciblés `ProfileServiceIdentityTests` et compilation Release du service Profile.
