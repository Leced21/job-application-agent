# Préférences de recherche

Un profil possède au maximum un ensemble de préférences. Le profil doit déjà exister.

- GET /api/v1/profile/preferences : 200 avec les préférences, 404 si le profil ou les préférences sont absents.
- PUT /api/v1/profile/preferences : crée ou remplace toutes les préférences et renvoie 200. Conserve la date de création lors des modifications.
- DELETE /api/v1/profile/preferences : 204 après suppression, 404 si le profil ou les préférences sont absents.

Exemple de corps PUT :

```json
{
  "desiredJobTitles": ["Backend Developer", "Data Engineer"],
  "preferredLocations": ["Paris", "Lyon"],
  "contractTypes": ["CDI", "Freelance"],
  "workModes": ["Hybrid", "Remote"],
  "minimumAnnualGrossSalary": 55000,
  "salaryCurrency": "EUR",
  "availableFrom": "2027-01-01"
}
```

Les quatre listes doivent être fournies ; une liste vide signifie aucun critère.
Postes : 20 éléments maximum, 150 caractères par élément.
Localisations : 20 éléments maximum, 200 caractères par élément.
Contrats : libellés libres (CDI, CDD, Freelance...), 20 éléments maximum, 100 caractères par élément.
Modes : OnSite, Hybrid, Remote (trois éléments maximum).
Les éléments textuels vides sont refusés.

Le salaire est annuel brut, positif ou nul, avec deux décimales maximum.
Il nécessite une devise au format de trois lettres majuscules ; aucun taux de change ni
contrôle de la liste des devises existantes n'est effectué.
Sans salaire, salaryCurrency doit être null.
availableFrom est une date facultative. Une date passée est autorisée pour exprimer une disponibilité déjà effective.

PUT remplace les critères, ce n'est pas une modification partielle.
Les critères invalides renvoient 400. La suppression du profil supprime ses préférences.

Migration à appliquer en développement depuis la racine du dépôt :

```powershell
dotnet ef database update --project src/services/Profile/JobApplicationAgent.Profile.Infrastructure --startup-project src/services/Profile/JobApplicationAgent.Profile.Api
```
