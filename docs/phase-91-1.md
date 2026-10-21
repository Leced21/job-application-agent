# Phase 91.1 — audit d’authentification et architecture cible

## État vérifié

- Le Gateway YARP relaie les routes sans authentifier l’utilisateur. Il ne configure ni cookie, ni OpenID Connect, ni politique d’autorisation. Les routes Agents et Applications le traversent désormais ; elles sont relayées vers leurs API locales, qui appliquent encore leurs jetons statiques.
- Les API Job, Matching et Collection ne déclarent pas d’authentification globale. Elles sont donc atteignables sans identité par les routes que le Gateway leur relaie.
- L’API Profile n’exige pas d’authentification pour son contrôleur de profil. Une authentification séparée protège seulement l’import de faits depuis Documents.
- Documents, Applications et Agents emploient chacun un jeton Bearer local fixe, un seul `OwnerId` configuré au démarrage et un schéma d’authentification propre. Ces jetons sont saisis dans le navigateur par les pages locales Documents, Applications et Agents ; ils ne représentent pas des comptes utilisateurs.
- Documents et Applications ont des enregistrements propriétaires et vérifient cet identifiant dans leurs dépôts. Le propriétaire vient toutefois de la configuration, pas de l’identité de l’utilisateur HTTP. Agents applique également son propriétaire configuré aux exécutions.
- Matching rattache les résultats à un `CandidateProfileId`, mais le contrôleur n’établit pas que le profil appartient à l’appelant. Les offres Job et les sources Collection constituent actuellement des catalogues partagés.
- Des limites de débit existent pour plusieurs opérations coûteuses ou mutantes, mais elles restent définies API par API et ne constituent pas une politique commune d’identité.

L’application est actuellement configurée pour un usage local mono-propriétaire. L’isolation présente dans certains dépôts limite les accès à l’identifiant local configuré, mais ne fournit ni comptes, ni séparation multi-utilisateur.

## Décision d’architecture proposée

Pour le navigateur, retenir un **Backend for Frontend (BFF)** dans le Gateway. Le Gateway devient le client OIDC confidentiel et gère la session avec un cookie `HttpOnly`, `Secure` en HTTPS et `SameSite` adapté. Angular ne reçoit ni jeton d’accès ni secret de client. Les opérations d’écriture du BFF doivent inclure une protection CSRF.

Utiliser le flux Authorization Code avec PKCE et un fournisseur OpenID Connect standard. Les API valident indépendamment les jetons d’accès (émetteur, audience, durée et scopes), même si le Gateway filtre déjà les requêtes. Les API dérivent l’identité propriétaire du sujet validé et ne font jamais confiance à un `OwnerId` fourni par le navigateur.

Deux formes de fournisseur restent possibles avant l’implémentation : un fournisseur OpenID Connect auto-hébergé (par exemple Keycloak/Authentik) ou un serveur intégré à l’écosystème .NET avec OpenIddict. OpenID Connect ne définit pas le stockage des profils ni leur cycle de vie ; dans les deux cas, il faudra une table de correspondance locale unique `(issuer, subject)` vers l’identifiant interne du compte, ainsi que les parcours d’inscription, récupération et désactivation.

Microsoft recommande le modèle BFF pour les applications Web et le flux code confidentiel avec PKCE ; ASP.NET Core Identity API propose aussi des tokens propriétaires destinés aux scénarios simples, mais ce n’est pas un fournisseur OIDC complet. Les références sont la [documentation ASP.NET Core sur OIDC et BFF](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication) et la [documentation ASP.NET Core Identity API](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0). OpenIddict documente son [support d’ASP.NET Core 10](https://documentation.openiddict.com/integrations/aspnet-core) et l’application de [PKCE côté serveur](https://documentation.openiddict.com/configuration/proof-key-for-code-exchange).

## Ordre de migration

1. Ajouter le fournisseur choisi, le stockage des comptes et les parcours BFF OIDC, avec HTTPS local/dev, cookies et CSRF.
2. Créer la correspondance fiable `issuer + subject → UserId` et migrer le profil vers une propriété propriétaire ; préserver l’accès aux données mono-propriétaire via une migration explicite.
3. Remplacer les propriétaires statiques Documents, Applications et Agents par le compte authentifié. Garder les identifiants de service distincts pour les appels internes.
4. Protéger chaque API, puis imposer l’autorisation au niveau ressource pour Profile, Matching, Documents, Applications et Agents. Restreindre les routes de mutation Job/Collection aux identités de service ou rôles d’administration.
5. Garder les ports des API internes hors exposition publique ; le Gateway ne doit pas être le seul contrôle d’accès.
6. Ajouter des tests négatifs : sans identité (401), identité sans droit (403 ou 404 selon la politique anti-énumération), et tentative d’accès croisé entre deux utilisateurs pour chaque type de ressource.

## Limites de cette tranche

Cette tranche documente l’état et la cible ; elle ne crée pas encore de comptes ni de fournisseur OIDC. Il faut choisir et configurer le fournisseur avant de définir le protocole de provisionnement, les secrets/certificats, le stockage du compte et les migrations de propriété. En attendant l’application complète de la phase 91, ne pas exposer le Gateway ou les API au réseau public.
