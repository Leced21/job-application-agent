# Service Profile

Profile conserve le dossier métier du candidat dans sa base PostgreSQL dédiée (`profile_db`). Il gère les informations générales, les expériences, formations, compétences, langues, certifications, liens et préférences. Les données exposées à Matching sont lues par une route dédiée ; les données du candidat ne sont pas envoyées dans les réponses du catalogue Job.

## Démarrage et migrations

Depuis la racine du dépôt, démarrer PostgreSQL et appliquer les migrations avant d'utiliser l'API :

```powershell
docker compose up -d profile-db
dotnet ef database update --project src/services/Profile/JobApplicationAgent.Profile.Infrastructure --startup-project src/services/Profile/JobApplicationAgent.Profile.Api
```

Pour démarrer Profile et PostgreSQL dans Compose :

```powershell
docker compose up -d --build profile-db profile-api
```

L'API est publiée sur `http://127.0.0.1:5001` et la base sur `127.0.0.1:5432`. Dans la topologie complète, accéder aux routes métier via le Gateway `http://127.0.0.1:5000`, qui gère la session et transmet le jeton API. Les migrations ne s'appliquent pas automatiquement. Voir [phase 93.1](phase-93-1.md) pour le démarrage conteneurisé et [phase 91.10](phase-91-10.md) ainsi que [phase 91.16](phase-91-16.md) pour Identity et l'attribution des propriétaires.

## Routes

Les routes CRUD utilisent le préfixe `/api/v1/profile` et retournent JSON. Le profil courant est déduit de l'identité authentifiée ; aucun `ownerId` ou `profileId` fourni par le navigateur ne détermine le propriétaire.

| Méthode | Route | Résultat principal |
| --- | --- | --- |
| GET | `/api/v1/profile` | Profil courant, 200 ; 404 si absent |
| POST | `/api/v1/profile` | Crée le profil courant, 201 |
| PUT | `/api/v1/profile` | Remplace les champs généraux, 200 |
| GET | `/api/v1/profile/{experiences,educations,skills,languages,links,certifications}` | Liste les éléments du profil, 200 |
| POST | `/api/v1/profile/{experiences,educations,skills,languages,links,certifications}` | Ajoute un élément, 201 |
| PUT | `/api/v1/profile/{experiences,educations,skills,languages,links,certifications}/{id}` | Met à jour un élément, 200 |
| DELETE | `/api/v1/profile/{experiences,educations,skills,languages,links,certifications}/{id}` | Supprime un élément, 204 |
| GET / POST / PUT / DELETE | `/api/v1/profile/preferences` | Lit, ajoute, remplace ou supprime les préférences |
| GET | `/api/v1/profile/matching-data` | Projection privée utilisée par Matching, 200 ou 404 |

Les créations du profil, des sous-éléments et des préférences renvoient un corps JSON avec la ressource créée. Les erreurs de validation suivent le format d'erreur API ; les ressources enfants restent filtrées sur le profil appartenant au propriétaire authentifié.

Exemple de création du profil :

```json
{
  "firstName": "Camille",
  "lastName": "Martin",
  "email": "camille@example.test",
  "phoneNumber": null,
  "jobTitle": "Data Engineer",
  "summary": "Ingénieure données spécialisée en plateformes analytiques."
}
```

L'API expose son document OpenAPI en environnement Development via `/openapi/v1.json`. Les routes de profil exigent une authentification. Le Gateway valide la session navigateur et transmet un jeton d'accès OpenIddict destiné à l'audience `jobagent_api` ; les API valident le jeton et isolent les données par propriétaire. Le mécanisme historique `Profile:LocalAuth` n'est accepté qu'en `Development` et `Testing`, avec un secret local fort hors Git. Voir [phase 91.15](phase-91-15.md), [phase 91.18](phase-91-18.md) et [phase 91.25](phase-91-25.md).

## Import de faits Documents

Le contrôleur `/api/v1/profile/document-facts` n'utilise pas l'authentification navigateur ordinaire. Il est réservé au canal d'import Documents : `POST` importe un fait confirmé, `GET /{factId}` relit son reçu et les réponses imposent `Cache-Control: no-store`. Ce canal a son propre jeton, propriétaire et profil configurés côté serveur ; ne pas le publier au frontend. Le contrat de reprise et les variables de configuration sont décrits dans le [guide de transfert Documents vers Profile](documents-profile-import.md).

La santé de l'API est publiée par `/health/live` et `/health/ready`. La readiness vérifie également l'accès à PostgreSQL. Pour les tests ciblés, utiliser les projets `JobApplicationAgent.Profile.UnitTests` et `JobApplicationAgent.Profile.IntegrationTests` sous `tests/Profile` ; les intégrations nécessitent Docker et Testcontainers. Les commandes générales figurent dans le [guide des tests](testing.md).
