# Phase 91.11 — endpoints du serveur OpenIddict

Le Gateway active maintenant le serveur OpenIddict lorsque sa base Identity est configurée. La découverte OIDC et les routes d’autorisation, de jeton et de UserInfo sont activées. Le code d’autorisation et le renouvellement sont les seuls flux utilisateur permis ; le code exige PKCE. Les destinations de claims sont limitées au sujet et aux claims demandés. Le handler UserInfo et son filtrage de scopes sont détaillés dans [phase 91.13](phase-91-13.md).

L’endpoint d’autorisation utilise uniquement le cookie `__Host-JobAgent.Session`, `HttpOnly`, `Secure`, `SameSite=Lax`, avec expiration de huit heures et chemin `/`. Sans session, il redirige vers `/login`. La page et les endpoints de compte avec protection antiforgery sont ajoutés en [phase 91.12](phase-91-12.md).

L’activation exige `ConnectionStrings:IdentityDatabase` et un `OpenIddict:Issuer` absolu. HTTPS est obligatoire sauf pour les URLs loopback de développement. Les clés éphémères sont permises uniquement en environnement Development ; le Gateway refuse l’activation OIDC en dehors de cet environnement jusqu’à ce que des certificats persistants soient configurés.

Vérification : le Gateway compile sans avertissement ni erreur. Le projet de tests de modèle compile également, avec un avertissement NU1900 de NuGet dû à l’indisponibilité du service de vulnérabilités. Les tests ne sont pas exécutés. Aucun conteneur ni serveur n’a été démarré.

Reste à faire : enregistrement du client BFF, consentement, validation de jetons dans les API et migration des propriétaires. Le flux de connexion cookie et la déconnexion sont décrits en phase 91.12.
