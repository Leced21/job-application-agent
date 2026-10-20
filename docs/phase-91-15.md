# Phase 91.15 — authentification interne de l’API Profile

Les routes historiques CRUD et `matching-data` du contrôleur Profile exigent désormais le schéma `ProfileService`. L’API compare en temps constant le hash SHA-256 du Bearer reçu avec `Profile:LocalAuth:Token` ; aucun jeton brut n’est conservé en mémoire. Le Gateway retire les credentials reçus du navigateur puis ajoute `Gateway:ServiceTokens:Profile`, avec `Profile:LocalAuth:Token` comme clé de repli pour les configurations locales partagées.

Les accès HTTP à travers le Gateway restent protégés par le cookie de session et l’antiforgery pour les écritures. Les appels directs à Profile sans jeton de service retournent 401. La configuration du secret doit être identique dans le Gateway et Profile, forte (32 à 512 caractères), conservée hors du dépôt et les ports Profile doivent rester liés à loopback.

Cette étape ferme l’accès anonyme direct, mais le secret représente toujours le Gateway et le stockage Profile reste mono-propriétaire. Les jetons OpenIddict ne sont pas encore validés par les microservices et il n’y a pas encore d’isolation des profils par `sub`. L’inscription demeure limitée au premier compte jusqu’à la migration des propriétaires.

Vérification : compilation de l’API Profile, du projet d’intégration Profile et du Gateway ; les tests ne sont pas exécutés.
