# Phase 97.29 — Inventaire local des propriétaires

Le script fonctionne avec Windows PowerShell 5.1 ou PowerShell 7.

La phase 97.28 permet de corriger un profil historique unique sans propriétaire. Avant de décider quoi faire des autres données existantes, il faut voir quels UUID sont réellement présents dans les services. `tools/Get-LocalOwnerDataInventory.ps1` interroge les huit bases PostgreSQL locales en lecture seule et résume, pour chaque table portant une colonne `OwnerId` ou `owner_id`, le nombre de lignes, les lignes sans propriétaire et le nombre de lignes par UUID.

L’inventaire ne lit aucun nom, CV, texte de candidature ou autre contenu candidat. Il n’écrit dans aucune base et ne démarre ni n’arrête de service. Toutes les bases sélectionnées doivent déjà être démarrées. Pour examiner toute la pile :

```powershell
./tools/Get-LocalOwnerDataInventory.ps1 -EnvFile .env
```

Pour limiter le contrôle à une base déjà active :

```powershell
./tools/Get-LocalOwnerDataInventory.ps1 -EnvFile .env -Service profile-db,applications-db
```

`OwnerId` a un rôle différent selon les services. Un UUID d’administration ou d’exécution ne doit pas être assimilé sans vérification à un compte candidat. Le rapport expose ces UUID et les volumes par table afin de guider une revue sur une copie sauvegardée. Les colonnes de revue telles que `ReviewedByOwnerId` ne sont volontairement pas traitées comme propriétaire de la ligne.

## Vérification

`tests/scripts/Get-LocalOwnerDataInventory.Tests.ps1` remplace Docker par un simulacre, vérifie le précontrôle des services et s’assure que la commande n’envoie que des requêtes `SELECT`. La CI Windows exécute ces tests sans daemon Docker. L’inventaire effectif des données de la machine de développement reste à exécuter localement.
