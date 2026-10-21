# Phase 97.31 — Création du profil depuis le navigateur

Un compte nouvellement inscrit n'a pas encore de profil candidat. La page `/profile` interprète maintenant la réponse `404` comme cet état normal et affiche un formulaire de première création. L'adresse e-mail du compte est préremplie depuis la session Gateway ; le candidat complète ses nom et prénom, puis peut ajouter ses coordonnées et son objectif professionnel.

Le formulaire valide les champs et leurs longueurs, normalise les valeurs avant l'envoi, transforme les champs facultatifs vides en `null` et appelle `POST /api/v1/profile`. En cas de succès, le profil créé est affiché sans rechargement. Un conflit affiche une indication invitant à recharger la page ; les autres erreurs restent présentées sans détails serveur.

Le composant respecte la convention du projet : fichiers TypeScript, HTML et SCSS séparés. Ses tests couvrent le préremplissage de l'e-mail, la normalisation du contrat, les validations et l'erreur de création. Le test multi-compte Compose de la [phase 97.30](phase-97-30.md) passe désormais par l'interface pour créer les profils synthétiques.

## Vérification

- Tests Angular : 71 réussis sur 14 fichiers.
- Build Angular : réussi. Les avertissements de budget SCSS déjà présents demeurent.
- Playwright découvre le scénario multi-compte ; la compilation TypeScript stricte du test E2E réussit.
- L'E2E réel requiert la pile Compose jetable de CI et n'a pas été dirigé vers une base locale.
