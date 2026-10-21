# Phase 91.7 — migration de l’authentification du navigateur

## État observé

Les pages Documents, Applications et Agents demandent chacune un jeton Bearer local. Leurs clients API conservent le jeton dans un champ en mémoire et le joignent directement aux appels HTTP ; il n’est pas conservé dans `localStorage` ou `sessionStorage`. Ces écrans utilisent des mécanismes d’authentification distincts et laissent le navigateur gérer les secrets des services.

Le Gateway ne possède actuellement ni session utilisateur ni endpoints d’authentification. Ajouter seulement une connexion OIDC au Gateway ne migrerait pas les trois écrans et ne transformerait pas les propriétaires mono-utilisateur configurés des API en comptes indépendants.

## Tranche cohérente à implémenter

La migration doit relier les éléments suivants dans le même parcours :

1. Le Gateway démarre une connexion OpenID Connect Authorization Code, crée une session cookie `HttpOnly`, `Secure` en HTTPS et `SameSite` approprié, et fournit un endpoint de session minimal à Angular.
2. Angular utilise les routes API relatives au Gateway avec les cookies de session ; les écrans de saisie des trois jetons disparaissent. Les mutations vérifient un jeton antiforgery.
3. Le Gateway transmet aux API une identité signée et à courte durée de vie ; chaque API valide émetteur, audience et expiration indépendamment.
4. Chaque API résout `(issuer, subject)` vers un identifiant interne immuable. Les dépôts continuent de vérifier ce propriétaire sur chaque ressource. Aucun identifiant de propriétaire fourni par le navigateur n’est accepté.
5. Une migration explicite rattache les données locales existantes à un compte initial, et des tests vérifient l’accès entre deux propriétaires.

Le fournisseur doit fournir son issuer, ses métadonnées OIDC, un client confidentiel, les URI de redirection et la stratégie de logout. Ces paramètres peuvent rester configurables et le protocole ne lie pas le code à un fournisseur, mais l’inscription du client et la configuration locale nécessitent le fournisseur choisi.

## Critères de validation

- Sans session : Gateway retourne 401 ou redirige vers la connexion, selon le type de route.
- Avec session : seules les ressources du compte connecté sont visibles et modifiables.
- Un appel direct aux API sans jeton signé valide retourne 401.
- Un jeton d’un autre émetteur, d’une autre audience ou expiré est rejeté.
- Les mutations sans jeton antiforgery sont rejetées.
- Angular n’envoie plus de jetons statiques de service.
- Les tests couvrent accès croisé, révocation de session, cookies et CSRF.

L’implémentation est en cours : comptes Identity, session cookie, antiforgery et proxy BFF sont décrits dans les phases [91.12](phase-91-12.md) et [91.14](phase-91-14.md). Les API n’acceptent pas encore toutes un jeton d’accès signé par OpenIddict et les données ne sont pas encore isolées par compte. Cette phase ne doit pas être considérée terminée.
