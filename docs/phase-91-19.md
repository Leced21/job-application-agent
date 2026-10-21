# Phase 91.19 — jetons utilisateur pour Documents

L’API Documents valide désormais les jetons d’accès OpenIddict émis par le Gateway, avec l’audience `jobagent_api`. Le propriétaire des données provient du claim signé `jobagent_owner_id`; les jetons locaux `Documents:LocalAuth` restent disponibles pour une transition progressive. Le Gateway échange la session utilisateur contre un jeton API dédié avant de transmettre les requêtes au cluster Documents.

Le canal `ApplicationsChannel` de lecture des reçus de liaison reste distinct et conserve son identité de service configurée. Les routes de confirmation et de consultation propriétaire utilisent `DocumentsAuth`, qui choisit OpenIddict pour les JWT et le schéma local pour l’ancien jeton. La route privée `GET /api/v1/documents/application-bindings/{id}` accepte uniquement `ApplicationsChannel`.

Configuration requise : définir `OpenIddict:Issuer` sur le Gateway et l’API Documents, et conserver la configuration BFF (`OpenIddict:Bff:ClientId` et `OpenIddict:Bff:ClientSecret`) ainsi que le stockage Identity/OpenIddict persistant. Sans issuer valide, l’API Documents reste en mode de transition avec le schéma local. Après validation opérationnelle, une phase ultérieure pourra supprimer ce repli.

Vérification effectuée : compilation de l’API Documents. Les tests n’ont pas été exécutés.
