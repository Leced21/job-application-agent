# Phase 97.10 — parcours d’inscription dans le navigateur

Un scénario Chromium couvre désormais l’inscription d’un nouveau candidat : l’interface affiche l’action lorsque Gateway autorise les inscriptions, bascule vers le formulaire de création, transmet l’adresse e-mail et le mot de passe avec le jeton antiforgery, puis ouvre le tableau de bord après la session créée. Le test d’intégration Gateway ajouté à la phase 91.31 couvre en parallèle la persistance PostgreSQL, la session cookie et l’attribution de propriétaires distincts à deux comptes.

Le build Angular Release passe. Le 19 octobre 2026, le scénario Chromium d'inscription a réussi (1/1) après autorisation de l'accès loopback nécessaire au serveur local `127.0.0.1:4200`. Les appels Gateway sont simulés dans ce test ; il vérifie l'interface, le jeton antiforgery et la redirection, mais ne crée pas de compte dans la base locale. Les tests unitaires Angular ont précédemment échoué avant leur exécution sur une erreur `EPERM` de renommage dans le répertoire temporaire géré par Windows.

La validation du vrai déploiement dépend toujours de l’application des migrations Gateway Identity/OpenIddict à la base utilisée par Compose, puis d’une inscription réelle dans le navigateur. Cette procédure est décrite dans [le dépannage](troubleshooting.md). Aucun compte ni aucune base persistante n’a été modifié pendant cette phase.
