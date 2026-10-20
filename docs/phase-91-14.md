# Phase 91.14 — proxy BFF et session navigateur

Quand `ConnectionStrings:IdentityDatabase` est configurée, toutes les routes API du Gateway exigent le cookie de session Identity. Sans cette base, le Gateway répond 503 sur `/api` au lieu de relayer des appels anonymes. Une session expirée reçoit 401 sur les API. Les requêtes mutantes qui passent par le Gateway doivent aussi fournir le jeton antiforgery `X-CSRF-TOKEN`.

Le proxy retire les en-têtes `Cookie` et `Authorization` du navigateur avant chaque appel interne. Pour Documents, Applications et Agents, il ajoute le jeton de service configuré côté Gateway : `Gateway:ServiceTokens:Documents`, `Gateway:ServiceTokens:Applications`, `Gateway:ServiceTokens:Agents`, ou les clés partagées déjà utilisées par les API (`Documents:LocalAuth:Token`, `Applications:LocalAuth:Token`, `Agents:LocalAuth:Token`). Ces secrets restent dans l’environnement/configuration locale et ne sont jamais inclus dans Angular. Les ports API restent à garder liés à loopback.

Les pages Documents, Préparations, Applications et Agents utilisent maintenant le proxy et la session commune ; leurs champs de jeton et leurs en-têtes Bearer navigateur ont été supprimés. Un intercepteur Angular récupère un jeton antiforgery pour chaque écriture. L’en-tête global affiche l’adresse du compte et permet la déconnexion.

La création de compte est limitée au premier compte candidat, avec une transaction sérialisable pour éviter deux inscriptions simultanées. Les dépôts Documents, Applications et Agents utilisent encore leurs propriétaires locaux configurés, sans correspondance avec le compte Identity. Cette limite empêche d’exposer ces données partagées à plusieurs comptes via l’interface ; la migration des propriétaires est nécessaire avant de réactiver l’inscription multi-utilisateur. L’accès direct aux API dépend toujours de leur authentification locale et de leurs ports loopback.

Configuration locale : démarrer le Gateway avec `ConnectionStrings:IdentityDatabase`, `OpenIddict:Issuer` et les mêmes variables de jeton de service que les API, puis démarrer Angular avec `npm start`. Les routes `/api` de la SPA utilisent alors le cookie du Gateway ; l’API n’est pas accessible depuis l’interface sans session.

Vérification : Gateway compilé sans avertissement ni erreur ; projet de tests .NET compilé avec avertissement NU1900 (service d’audit NuGet inaccessible) ; build Angular et compilation TypeScript des specs réussis. Les tests n’ont pas été exécutés.

À suivre : remplacer les jetons statiques relayés par des jetons d’accès OpenIddict à courte durée de vie, faire valider ces jetons par les API, rattacher les dépôts au sujet immuable du compte puis ajouter les tests d’accès croisé. Le mode mono-compte reste requis jusqu’à la fin de ces migrations.
