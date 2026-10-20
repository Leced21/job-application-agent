# Phase 93.8 — interface et Gateway dans Compose

Le frontend Angular est construit dans une image Node puis servi par Nginx. Nginx relaie `/api` et `/auth` au Gateway, de sorte que le navigateur utilise une seule origine. Le Gateway possède son image .NET, son stockage Identity dédié et des destinations YARP qui pointent vers les services Compose.

Dans cette topologie Development, l’émetteur OpenIddict est `http://gateway-api:5000/`, joignable depuis le Gateway et les API qui valident les jetons. Seuls les services Profile, Matching, Applications, Documents et Agents acceptent ce nom interne en HTTP, uniquement en environnement Development ; les autres configurations gardent leur validation HTTPS/loopback. L’inscription reste limitée au premier compte et son OwnerId doit correspondre aux données Applications/Profile locales. Les clés OpenIddict étant éphémères en Development, les jetons d’accès ne survivent pas au redémarrage du Gateway.

Copier `.env.example` vers `.env`, remplacer les valeurs de démonstration puis inclure les overlays de bases, APIs et edge lors du démarrage :

```powershell
docker compose --env-file .env -f docker-compose.yml -f docker-compose.identity.yml -f docker-compose.applications.yml -f docker-compose.applications-api.yml -f docker-compose.documents.yml -f docker-compose.documents-api.yml -f docker-compose.collection.yml -f docker-compose.collection-api.yml -f docker-compose.agents.yml -f docker-compose.agents-api.yml -f docker-compose.edge.yml up -d --build
```

Appliquer les migrations Identity et métier explicitement avant le premier usage ; les guides de chaque service indiquent leurs commandes EF. Cette topologie ne modifie aucune donnée existante à elle seule.

Vérification : validation de la configuration Compose complète, compilation du Gateway et des APIs configurées pour valider l’émetteur interne, compilation de l’interface Angular. Les tests ne sont pas exécutés et aucun conteneur ni aucune base ne sont démarrés.
