# Phase 91.13 — endpoint UserInfo et claims OIDC

Le Gateway transmet maintenant `/connect/userinfo` au gestionnaire applicatif OpenIddict pour les requêtes GET et POST. OpenIddict valide le jeton d’accès avant que le handler ne cherche le compte local associé au sujet. Le handler retourne `sub` et n’inclut `name`, `email`, `email_verified` ou `role` que si le jeton porte le scope correspondant. Les réponses indiquent `Cache-Control: no-store` et `Pragma: no-cache`.

Les claims inclus dans les jetons d’identité et d’accès au renouvellement suivent aussi les scopes `profile`, `email` et `roles`. L’API UserInfo reflète l’état actuel de l’adresse et des rôles du compte, tandis que les jetons déjà émis restent limités à leurs claims au moment de l’émission.

Vérification : compilation du Gateway sans avertissement ni erreur ; compilation du projet `JobApplicationAgent.Gateway.UnitTests` réussie. Deux tests unitaires couvrent le filtrage par scope ; ils ont été compilés mais non exécutés. NuGet affiche un avertissement NU1900 parce que `api.nuget.org` n’est pas accessible pour son audit de vulnérabilités.

La phase suivante protège le proxy BFF avec la session Identity et retire les jetons du navigateur. Les jetons statiques des API sont encore relayés côté serveur en attendant leur validation OpenIddict et la migration des propriétaires.
