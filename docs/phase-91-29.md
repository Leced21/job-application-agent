# Phase 91.29 — réserver les mutations Collection aux administrateurs

Toutes les routes Collection continuent d'exiger une identité. Les lectures restent disponibles à tout utilisateur authentifié, tandis que l'import unitaire, la collecte JSON locale, les décisions de revue et les publications vers Job exigent maintenant la politique `CollectionAdmin`. Elle vérifie que le claim `jobagent_owner_id` est présent dans `Collection:Admin:OwnerIds` ; une liste vide refuse toutes les mutations.

En `Development` et `Testing`, le Bearer local comprend maintenant l'owner ID configuré dans `Collection:LocalAuth:OwnerId`. Compose relie l'owner initial `APPLICATIONS_OWNER_ID` à l'identité locale et à la liste administrateur. Dans les autres environnements, le Bearer local reste désactivé et seuls les jetons OpenIddict sont validés. Le secret de publication vers Job reste une identité de service séparée et ne donne aucun accès aux routes Collection.

Le test d'intégration d'un utilisateur authentifié non administrateur vérifie qu'il peut lire les sources et les imports, mais reçoit 403 sur les quatre familles de mutations. Les appels anonymes continuent de recevoir 401.

Validation : les 50 tests d'intégration Collection passent en Release ; la configuration Compose complète est valide avec `.env.example`. Les guides Collection, sécurité et readiness décrivent les nouvelles règles.
