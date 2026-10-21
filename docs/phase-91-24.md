# Phase 91.24 — Séparer le canal Documents et l’identité utilisateur

La vérification privée d’un reçu Documents utilise désormais exclusivement le schéma `ApplicationsChannel`. `ApplicationDocumentClient` envoie le jeton de service dédié configuré par `Applications:Documents:Token` ; le bearer utilisateur n’est pas substitué à ce secret. Les endpoints propriétaires de confirmation et de consultation restent sous `DocumentsAuth`, de sorte que le canal ne permet ni de lire les autres ressources ni de confirmer un lien.

La validation des candidatures continue à transmettre le bearer à Profile, selon le contrat de la phase 91.21, et ne transmet aucun cookie. Les tests d’intégration vérifient séparément le bearer vers les sources, l’absence de cookie, l’accès propriétaire refusé à la route de reçu et l’accès accepté avec le seul jeton du canal.

Validation : les tests d’intégration précédemment en échec passent isolément après correction. La suite complète Applications réussit : 82 tests, aucun échec.
