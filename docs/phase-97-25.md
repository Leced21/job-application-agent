# Phase 97.25 — génération locale des certificats OpenIddict

`tools/New-OpenIddictCertificates.ps1` génère une paire de certificats RSA auto-signés pour les essais locaux ou de staging : un certificat de signature avec `DigitalSignature` et un certificat de chiffrement avec `KeyEncipherment`. Le script est compatible Windows PowerShell 5.1, exige deux mots de passe saisis sous forme de `SecureString` (16 caractères minimum), refuse les chemins relatifs et les sorties dans le dépôt, refuse d'écraser les noms de fichiers réservés et produit un manifeste sans secret avec les empreintes SHA-256.

Les clés sont créées en mémoire avec l'API .NET [`CertificateRequest.CreateSelfSigned`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.certificaterequest.createselfsigned?view=net-10.0), puis exportées dans deux fichiers PFX chiffrés. Le script importe chaque PFX avec `EphemeralKeySet` pour vérifier le mot de passe, la clé privée et l'usage avant de produire le manifeste. Il dispose les objets cryptographiques, efface les tableaux temporaires de PFX et ne crée pas de fichier de mot de passe. Protéger le dossier de sortie avec les ACL Windows appropriées et ne jamais ajouter les fichiers PFX au dépôt ou aux artefacts CI. Le caractère auto-signé convient aux tests ; la production doit utiliser les certificats approuvés par l'organisation et son gestionnaire de secrets.

Depuis la racine du dépôt, choisir un dossier privé absolu en dehors du dépôt :

```powershell
$outputDirectory = Join-Path 'D:\JobAgentSecrets\OpenIddict' (Get-Date -Format 'yyyy-MM-dd')
$signingPassword = Read-Host 'Mot de passe PFX de signature (16 caractères minimum)' -AsSecureString
$encryptionPassword = Read-Host 'Mot de passe PFX de chiffrement (16 caractères minimum)' -AsSecureString
try {
    .\tools\New-OpenIddictCertificates.ps1 `
        -OutputDirectory $outputDirectory `
        -SigningPassword $signingPassword `
        -EncryptionPassword $encryptionPassword
}
finally {
    Remove-Variable signingPassword, encryptionPassword -ErrorAction SilentlyContinue
}
```

Les noms générés sont `openid-signing.pfx`, `openid-encryption.pfx` et `manifest.json`. Utiliser deux mots de passe différents, puis les stocker séparément dans le gestionnaire de secrets retenu et les monter dans le conteneur en lecture seule. Configurer les chemins PFX et `PasswordFile` comme indiqué dans le [guide de sécurité](security.md). Le manifeste sert à vérifier les empreintes avant le déploiement ; il ne permet pas de reconstituer les mots de passe.

Pour une rotation, déployer le nouveau certificat principal tout en conservant l'ancien dans `Previous__0` pour le même usage. Vérifier la discovery OIDC, le JWKS, l'émission d'un nouveau jeton et la validation d'un jeton existant. Conserver l'ancien certificat jusqu'à l'expiration ou la révocation des jetons et artefacts signés ou chiffrés avec lui, puis retirer l'entrée `Previous__0` et vérifier à nouveau le service. Sauvegarder les PFX et leurs mots de passe dans les emplacements privés prévus avant le déploiement.

La CI Windows vérifie l'analyse syntaxique sous Windows PowerShell 5.1, génère les certificats dans un dossier temporaire, relit les PFX, contrôle le manifeste, confirme que `-WhatIf` et les contrôles de mot de passe/écrasement fonctionnent, puis supprime les fichiers à la fin du job.
