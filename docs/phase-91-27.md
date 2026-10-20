# Phase 91.27 — authentifier l’API Collection

Les endpoints Collection étaient accessibles directement sans identité, alors que le Gateway protège déjà ses routes par session. L’API valide maintenant les jetons OpenIddict de l’audience `jobagent_api` et exige une authentification sur les routes d’import, de révision et de collecte. Le Gateway transmet le jeton courant à Collection.

Un Bearer local de compatibilité est accepté uniquement en `Development` et `Testing`. Compose configure l’issuer pour Collection et un secret local distinct, ce qui préserve le démarrage de développement ; `.env.example` documente la nouvelle variable `COLLECTION_LOCAL_TOKEN`. Les mutations internes de publication vers Job restent inchangées dans cette tranche.

Validation : le test HTTP sans Bearer reçoit 401 ; les 49 tests d’intégration Collection passent, y compris les publications et les scénarios de rollback. Le Gateway et l’API Collection compilent en Release.
