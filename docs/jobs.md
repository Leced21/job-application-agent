# Offres d’emploi

Le service Job utilise PostgreSQL (`job_db`, port local 5433) indépendamment du service Profile.
L’écran Angular est disponible sur `/jobs` depuis le menu **Offres**. Il affiche les offres. La création, la modification, la suppression et le changement de statut restent disponibles via l’API.
La liste affiche 20 offres par page, de la plus récente à la plus ancienne.

## Lancement local

Depuis la racine du dépôt :

```powershell
docker compose up -d job-db
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project src/services/Job/JobApplicationAgent.Job.Infrastructure --startup-project src/services/Job/JobApplicationAgent.Job.Api
dotnet run --project src/services/Job/JobApplicationAgent.Job.Api --launch-profile http
```

L’API Job écoute sur http://localhost:5002. Dans des terminaux séparés, lancer la gateway et Angular si nécessaire :

```powershell
dotnet run --project src/gateway/JobApplicationAgent.Gateway --urls http://localhost:8080
```

```powershell
cd src/frontend/job-application-agent-ui
npm start
```

Le proxy Angular `/api` pointe vers la gateway (8080), qui relaie `/api/v1/jobs` vers Job (5002).
Les migrations sont appliquées explicitement, pas automatiquement au démarrage.
Hors développement, fournir `ConnectionStrings__JobDatabase` via la configuration de l’environnement.

## Contrat HTTP

| Méthode | Route | Résultat |
| --- | --- | --- |
| GET | /api/v1/jobs?page=1&pageSize=20 | Tableau paginé, 200 |
| GET | /api/v1/jobs/{id} | Offre, 200 ou 404 |
| POST | /api/v1/jobs | Création, 201 et Location |
| PUT | /api/v1/jobs/{id} | Remplacement des champs éditables, 200 ou 404 |
| PATCH | /api/v1/jobs/{id}/status | Changement de statut, 200 ou 404 |
| DELETE | /api/v1/jobs/{id} | Suppression, 204 ou 404 |

Les erreurs de validation renvoient 400. `page` est compris entre 1 et 1 000 000, `pageSize` entre 1 et 100.

```json
{
  "title": "Data Engineer",
  "companyName": "Example",
  "location": "Paris",
  "workMode": "Hybrid",
  "contractType": "Permanent",
  "salaryMin": 50000,
  "salaryMax": 65000,
  "salaryCurrency": "EUR",
  "description": "Construction de plateformes de données",
  "source": "Manual",
  "sourceUrl": "https://example.com/jobs/123",
  "publishedAtUtc": "2026-09-23T09:00:00Z"
}
```

Titre et entreprise : obligatoires, 200 caractères maximum. Source : obligatoire, 100 caractères.
Localisation : 200 caractères ; description : 10 000 ; URL HTTP(S) : 2 000.
Les salaires annuels bruts sont facultatifs, positifs ou nuls, avec deux décimales maximum et un plafond
à 9 999 999 999,99 ; maximum >= minimum. Une devise de trois lettres majuscules est requise si un salaire est fourni.
La date de publication est facultative, au format UTC ; le formulaire indique explicitement UTC.

Modes : `Unknown`, `OnSite`, `Hybrid`, `Remote`.
Contrats : `Unknown`, `Permanent`, `FixedTerm`, `Freelance`, `Internship`, `Apprenticeship`, `Temporary`.
Statuts : `Discovered` (création), `Saved`, `Ignored`, `Expired`.
Exemple PATCH : `{ "status": "Saved" }`. La modification d’une offre conserve son statut et sa date de création.
Les offres peuvent être ajoutées sans profil candidat. Cette version utilise la saisie manuelle ; aucun import automatique n’est configuré.

## Tests

```powershell
dotnet test tests/Job/JobApplicationAgent.Job.UnitTests
dotnet test tests/Job/JobApplicationAgent.Job.IntegrationTests
```

Les tests d’intégration nécessitent Docker et créent une base PostgreSQL isolée avec Testcontainers.
Les tests frontend font partie de `npm test -- --watch=false`.
