# Phase 91.16 — propriété des profils candidats

Chaque `candidate_profiles` porte maintenant un `owner_id` UUID. L’identité interne Profile dérive cet identifiant de `Profile:LocalAuth:OwnerId` et le place dans le claim `NameIdentifier` après validation du Bearer de service. Le handler de création affecte ce propriétaire côté serveur ; le client ne peut pas choisir l’identifiant. Le dépôt filtre toutes ses lectures, y compris les données de matching, par cet UUID et refuse l’ajout d’un profil portant un autre propriétaire. Une contrainte unique permet un seul profil par propriétaire.

La migration `AddProfileOwner` ajoute une colonne nullable pour préserver les données historiques sans deviner leur propriétaire. Après sauvegarde et application de la migration, vérifier qu’il existe au plus un profil sans propriétaire. Pour une base mono-propriétaire confirmée, l’opérateur peut attribuer explicitement la valeur de `Profile:LocalAuth:OwnerId` :

```sql
SELECT count(*) FROM candidate_profiles WHERE owner_id IS NULL;
UPDATE candidate_profiles
SET owner_id = '<UUID Profile:LocalAuth:OwnerId>'::uuid
WHERE owner_id IS NULL;
```

Ne pas exécuter cet UPDATE si plusieurs profils historiques doivent appartenir à des comptes distincts ; établir d’abord leur correspondance. Tant que la migration n’est pas faite, les profils historiques non attribués ne sont pas visibles par les routes Profile. L’API exige désormais `Profile:LocalAuth:OwnerId` et le même UUID doit être configuré comme propriétaire des autres services locaux concernés.

Cette tranche ne relie pas encore le `sub` OpenIddict au propriétaire interne : le jeton de service représente toujours un propriétaire unique, et l’inscription multi-utilisateur reste désactivée. Tests d’intégration ajoutés pour refuser l’accès au profil d’un autre propriétaire ; compilation uniquement, sans exécuter les tests.
