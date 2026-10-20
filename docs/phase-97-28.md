# Phase 97.28 — Attribution contrôlée du profil historique

La migration `AddProfileOwner` ajoute `candidate_profiles.owner_id` comme colonne nullable afin de préserver les bases déjà créées. Les lectures Profile filtrent maintenant sur cette colonne. Un profil ancien peut donc rester invisible après la connexion du premier compte, même si ce compte reprend correctement `APPLICATIONS_OWNER_ID`.

Le script `tools/Assign-LocalLegacyProfileOwner.ps1` traite le cas non ambigu : une seule ligne `candidate_profiles` sans propriétaire, attribuée à l'UUID `APPLICATIONS_OWNER_ID` configuré dans le Gateway. Il ne change rien lorsqu'aucune ligne n'est sans propriétaire. Il refuse plusieurs lignes orphelines, un propriétaire différent de la configuration Gateway et un propriétaire qui possède déjà un profil.

Depuis la racine du dépôt, avec Docker Desktop actif et `profile-db` démarrée, sauvegarder d'abord la base avec la [procédure de sauvegarde PostgreSQL](phase-97-16.md). Reprendre exactement l'UUID de `APPLICATIONS_OWNER_ID` depuis `.env`, puis examiner les changements prévus :

```powershell
$ownerId = [Guid]::Parse('11111111-1111-4111-8111-111111111111') # remplacer par APPLICATIONS_OWNER_ID de .env
./tools/Assign-LocalLegacyProfileOwner.ps1 `
    -OwnerId $ownerId `
    -EnvFile .env `
    -AllowDataUpdate `
    -WhatIf
```

Si le contrôle identifie le bon profil, exécuter sans `-WhatIf`. Le script demande alors confirmation. Il utilise une mise à jour conditionnelle `owner_id IS NULL`, puis vérifie le résultat. `-AllowDataUpdate` est obligatoire ; aucune API ni base n'est démarrée ou arrêtée par le script.

Si plusieurs profils restent sans propriétaire, ne pas leur attribuer le même UUID : une revue et une correspondance explicites profil/propriétaire sont nécessaires sur une copie protégée. Le script n'affiche que les identifiants et les nombres de lignes, jamais les données du CV.

## Vérification

`tests/scripts/Assign-LocalLegacyProfileOwner.Tests.ps1` simule le client Docker pour vérifier le consentement requis, l'absence d'UPDATE avec `-WhatIf`, le refus d'un cas ambigu et la mise à jour d'une seule ligne. La CI Windows exécute ces contrôles sans dépendre d'un daemon Docker. La répétition sur une copie locale des données reste à faire avant de déclarer l'attribution historique validée.
