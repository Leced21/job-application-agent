# Phase 91.10 — stockage Identity/OpenIddict

Le Gateway reçoit le socle technique d’OpenIddict, choisi comme fournisseur OIDC local intégré à .NET. Les versions OpenIddict 7.7.2 prennent en charge ASP.NET Core/.NET 10 selon la [documentation officielle d’intégration](https://documentation.openiddict.com/integrations/aspnet-core). Le stockage EF Core suit le [guide officiel OpenIddict/EF Core](https://documentation.openiddict.com/integrations/entity-framework-core).

`GatewayIdentityDbContext` combine ASP.NET Core Identity avec les tables OpenIddict pour les applications clientes, autorisations, scopes et jetons. Les comptes utilisent un UUID interne. La migration `InitialGatewayIdentity` crée ce schéma dans une base séparée ; elle ne touche pas aux bases métier.

La chaîne `ConnectionStrings:IdentityDatabase` active le stockage et le serveur OIDC dans le Gateway. `OpenIddict:Issuer` est aussi obligatoire et doit correspondre à l’URL publique du Gateway, par exemple `https://localhost:7024/`. Les routes discovery, `/connect/authorize`, `/connect/token` et `/connect/userinfo` sont configurées. Le flux autorisation utilise Authorization Code, impose PKCE et permet le renouvellement.

En développement, OpenIddict utilise des clés éphémères ; les jetons émis deviennent invalides au redémarrage. Hors `Development`, le Gateway exige sa base Identity et deux PFX RSA distincts, configurés par `OpenIddict__Certificates__Signing__Path`/`Password` et `OpenIddict__Certificates__Encryption__Path`/`Password`. Le certificat de signature doit permettre `DigitalSignature`, celui de chiffrement `KeyEncipherment`; les deux doivent contenir une clé privée RSA d'au moins 2 048 bits. Les chemins relatifs sont résolus depuis le répertoire de contenu. Voir le [guide de sécurité](security.md) pour la provision, les montages en lecture seule et les mots de passe venant d'un gestionnaire de secrets. Le renouvellement et la rotation avec chevauchement ne sont pas encore automatisés. Pour créer la base locale, `docker-compose.identity.yml` lie PostgreSQL au seul loopback sur le port 5439 et exige des variables `IDENTITY_DB_USER` et `IDENTITY_DB_PASSWORD`. Exemple PowerShell pour le même terminal local :

```powershell
$env:IDENTITY_DB_USER = 'jobagent'
$env:IDENTITY_DB_PASSWORD = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:ConnectionStrings__IdentityDatabase = "Host=localhost;Port=5439;Database=identity_db;Username=$env:IDENTITY_DB_USER;Password=$env:IDENTITY_DB_PASSWORD"
$env:OpenIddict__Issuer = 'https://localhost:7024/'
docker compose -f docker-compose.identity.yml up -d
dotnet ef database update --project src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj --startup-project src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj
```

Garder ces variables dans ce terminal ou les enregistrer dans un fichier `.env` local ignoré par Git. La connexion Identity et les variables Compose doivent employer le même mot de passe.

Vérification : le Gateway compile avec .NET 10 et la migration EF Core a été générée. Le projet de tests compile aussi ; son test de modèle vérifie la présence des tables Identity/OpenIddict et l’identifiant UUID. Les tests ne sont pas exécutés dans le flux de travail demandé. La configuration Compose Identity est valide avec des valeurs temporaires en environnement de processus ; aucun conteneur ni migration de base n’est lancé dans cette tranche. La restauration hors ligne a produit un avertissement NU1900 car NuGet ne pouvait pas joindre son service de vulnérabilités.

Limite : le flux autorisation n’est pas encore utilisable de bout en bout : le client BFF n’est pas enregistré et la route `/account/login` sera ajoutée avec l’inscription et l’écran de connexion dans la tranche suivante. La validation des jetons par les API et la migration des propriétaires restent également à faire. Les clés de développement ne conviennent pas à une installation persistante ou exposée.
