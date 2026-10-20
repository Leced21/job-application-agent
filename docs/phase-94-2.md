# Phase 94.2 — Analyse de sécurité du code

Le workflow `CodeQL analysis` analyse le C# compilé et le TypeScript/JavaScript sur les pull requests et pushes vers `dev` et `main`, ainsi que chaque lundi et à la demande. Les deux langages sont traités dans des jobs séparés : CodeQL capture la compilation de la solution .NET pour le C#, tandis que l'extraction JavaScript/TypeScript ne nécessite pas de build.

Les résultats SARIF sont conservés comme artefacts GitHub Actions pendant 14 jours, sans upload dans Code Scanning. L'utilisation de CodeQL sur un dépôt privé exige GitHub Advanced Security ; vérifier cette condition avant d'activer ce workflow si le dépôt est privé. Le jeton reçoit `security-events: write` au niveau du seul job, conformément aux permissions attendues pour CodeQL.

Les actions CodeQL utilisent la version majeure v4, recommandée par le dépôt officiel de l'action. Voir [la documentation CodeQL des langages compilés](https://docs.github.com/en/code-security/how-tos/find-and-fix-code-vulnerabilities/manage-your-configuration/codeql-for-compiled-languages) et [l'action officielle CodeQL](https://github.com/github/codeql-action).

Validation locale : contrôle de formatage et de syntaxe YAML à effectuer avec Prettier. L'analyse elle-même s'exécute dans GitHub Actions, où le runner Linux peut construire la solution et produire les artefacts SARIF.
