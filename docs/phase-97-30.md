# Phase 97.30 — Isolation multi-compte dans la pile réelle

Le smoke test navigateur de la phase 97.20 ne créait qu'un seul compte. La suite `e2e/compose-multi-account-isolation.spec.ts` crée maintenant deux comptes dans deux contextes Chromium distincts, chacun avec un profil et une expérience. Les deux profils sont créés depuis le formulaire `/profile`, puis les requêtes passent par le proxy Compose, le Gateway et les jetons OpenIddict avant d'atteindre Profile.

Le test vérifie que le second compte ne voit pas le profil avant sa création, que chaque compte ne reçoit que ses propres expériences et que la tentative de modification par le compte B d'une expérience du compte A reçoit `404`. Il vérifie ensuite que l'expérience A est intacte. Les données synthétiques restent dans la base éphémère du runner CI.

Le workflow `backend-ci.yml` exécute ce scénario avec le test d'inscription/reconnexion, après migrations et readiness de la pile. Le test est conçu pour la pile jetable de CI ; il ne doit pas être dirigé vers une base locale contenant des données utiles.

## Vérification

Playwright découvre et compile le test avec `npm run test:e2e -- --list e2e/compose-multi-account-isolation.spec.ts`. L'exécution de bout en bout dépend d'un run CI avec Docker. Les tests d'accès croisé des autres APIs restent couverts par leurs suites d'intégration dédiées.
