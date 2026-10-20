# Phase 97.4 — Documents OpenAPI de développement

Agents, Applications et Documents génèrent maintenant `/openapi/v1.json` en environnement `Development`, comme Profile, Job, Matching et Collection. Le [guide des contrats OpenAPI](openapi.md) répertorie les URLs loopback de la topologie Compose.

Les documents ne sont pas exposés hors de Development ; les règles d'authentification des API restent appliquées. Le Gateway et ses endpoints OpenIddict ne disposent pas d'un document agrégé dans cette tranche.

Validation : compilation Release réussie des trois API modifiées ; les trois tests d'intégration OpenAPI vérifient le JSON généré et la présence d'une route métier par service ; la configuration Compose complète est valide. Aucun endpoint métier ni schéma de base n'a été modifié.
