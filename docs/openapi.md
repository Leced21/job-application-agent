# Contrats OpenAPI

Chaque API métier expose son document OpenAPI JSON sur `/openapi/v1.json` uniquement en environnement `Development`. ASP.NET Core génère le document à partir des routes et types du binaire ; aucune copie OpenAPI statique n'est maintenue.

| API | URL locale Compose | Document en Development |
| --- | --- | --- |
| Profile | `http://127.0.0.1:5001` | Activé |
| Job | `http://127.0.0.1:5002` | Activé |
| Matching | `http://127.0.0.1:5186` | Activé |
| Collection | `http://127.0.0.1:5004` | Activé |
| Documents | `http://127.0.0.1:5005` | Activé |
| Applications | `http://127.0.0.1:5007` | Activé |
| Agents | `http://127.0.0.1:5008` | Activé |

Pour consulter, par exemple, le contrat Profile :

```powershell
$openApi = Invoke-RestMethod 'http://127.0.0.1:5001/openapi/v1.json'
$openApi.paths.Keys | Sort-Object
```

Les documents se consultent directement sur les ports loopback des APIs, pas par le Gateway. La documentation du Gateway et des endpoints OpenIddict n'est pas encore agrégée. Les détails d'accès et le démarrage avec bases et migrations sont décrits dans les guides [Profile](profile.md), [Job](jobs.md), [Matching](matching.md), [Collection](collection.md), [Documents](documents.md), [Applications](applications-api.md) et [Agents](agents-api.md).

Les contrats décrivent les routes et schémas exposés. Ils ne remplacent pas les règles d'authentification : celles-ci restent appliquées aux routes métier et sont décrites dans chaque guide de service. Les documents OpenAPI ne sont pas mappés hors de Development.
