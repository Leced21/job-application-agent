# Phase 91.17 — correspondance explicite du compte et du propriétaire

Le compte Identity porte maintenant un `OwnerId` UUID nullable et unique. Lors de la création du premier compte, `Identity:InitialOwnerId` est obligatoire et attribué au compte dans la même transaction. Il doit correspondre au propriétaire déjà choisi pour Profile, Documents, Applications et Agents. `sub` reste l’identifiant du compte Identity ; le Gateway conserve donc la correspondance explicite `sub → OwnerId` au lieu de supposer que ces identifiants sont égaux.

Quand le scope `jobagent_api` est accordé, OpenIddict place le claim `jobagent_owner_id` dans le jeton d’accès uniquement. Il est réémis lors du renouvellement. Le claim ne figure ni dans l’ID token ni dans UserInfo. Un compte sans correspondance OwnerId ne reçoit pas de jeton API.

La migration `AddIdentityOwnerMapping` ajoute la colonne et son index unique sans inventer de correspondance pour les comptes existants. Après sauvegarde, l’opérateur doit attribuer explicitement leur propriétaire. Exemple à adapter après avoir vérifié les valeurs :

```sql
SELECT "Id", "Email", "OwnerId" FROM "AspNetUsers";
UPDATE "AspNetUsers"
SET "OwnerId" = '<UUID propriétaire configuré dans les services>'::uuid
WHERE "Id" = '<sub du compte à rattacher>';
```

La validation de ces jetons par les microservices et leur propagation par le Gateway ne sont pas encore activées ; le Gateway transmet toujours les jetons de service statiques. L’inscription reste limitée à un compte. Ne réactivez pas l’inscription multiple avant que la validation du claim, la propagation du jeton et l’isolation des données soient terminées dans chaque service.

Vérification : Gateway et son projet de tests compilent ; migration générée et modèle synchronisé. Les tests ne sont pas exécutés et la migration n’est pas appliquée.
