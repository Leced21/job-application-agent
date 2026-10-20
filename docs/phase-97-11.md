# Phase 97.11 — réutiliser PostgreSQL Agents dans le projet Compose principal

Le démarrage local d’Ollama créait auparavant le projet Compose `job-application-agent-agents`, tandis que la topologie complète lançait son propre `agents-db` dans `job-application-agent`. Ces deux projets possédaient des conteneurs et volumes PostgreSQL séparés.

Quand `.env` existe, `tools/Start-LocalAgents.ps1` utilise maintenant par défaut le nom du projet principal, lit les paramètres Agents depuis la configuration Compose sans les afficher, puis migre et utilise la base publiée sur `127.0.0.1:5438`. Un conteneur `agents-db` déjà présent dans un autre projet provoque un arrêt explicite, afin de ne pas créer silencieusement une base concurrente. Le mode isolé antérieur reste accessible avec `-IsolatedDatabase` ou en l’absence de `.env` ; cette option reprend son volume précédent. Le script ne stoppe aucun API ni conteneur existant.

Le guide Windows fixe le nom `job-application-agent`, et le guide Agents précise le conflit de port 5008 avec l’API Agents Compose. Le dépannage décrit l’inspection des projets et des volumes avant l’arrêt d’un ancien projet.

Validation : la configuration Compose Agents est lisible avec `.env.example` et le script PowerShell passe l’analyse syntaxique. Le démarrage Docker n’a pas été exécuté dans cet environnement ; l’accès au moteur Docker est refusé. Vérifier localement que le script réutilise le conteneur `agents-db` déjà lancé par le projet principal avant de poursuivre le parcours Ollama.
