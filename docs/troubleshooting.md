# Dépannage local

Commencez par identifier le processus et sa dépendance avant de redémarrer quoi que ce soit. Depuis la racine du dépôt, vérifiez la configuration Compose, l'état des services et leurs journaux :

```powershell
docker compose --env-file .env config --quiet
docker compose --env-file .env ps
docker compose --env-file .env logs --tail 100 matching-api
Invoke-RestMethod 'http://127.0.0.1:5186/health/live'
Invoke-RestMethod 'http://127.0.0.1:5186/health/ready'
```

Remplacez `matching-api` et le port par le service concerné. Pour une topologie multi-overlay, reprenez exactement la liste de fichiers Compose de [windows-install.md](windows-install.md) et [testing.md](testing.md). Liveness confirme que le processus répond ; readiness vérifie PostgreSQL, et certains services ajoutent leurs dépendances activées.

## API répond 500 ou n'est pas prête

Consultez d'abord les logs de l'API et son endpoint `/health/ready`, puis ceux de ses dépendances. Vérifiez que la base est healthy et que les migrations du service ont été appliquées à la bonne base. Le démarrage n'applique pas automatiquement les migrations. Pour Matching, vérifiez `ConnectionStrings__MatchingDb`, `Services__Job__BaseUrl` et `Services__Profile__BaseUrl`. Une indisponibilité/expiration de Job ou Profile donne normalement un 503 au calcul ; un profil ou une offre introuvable peut donner 404. Si le log indique une erreur SQL ou une colonne manquante, comparez `__EFMigrationsHistory` et suivez [migrations.md](migrations.md) ; ne supprimez pas le volume.

Si le Gateway échoue avec `relation "OpenIddictApplications" does not exist`, la migration Identity/OpenIddict n'a pas été appliquée à la base Identity utilisée par le Gateway. Depuis la racine du dépôt, lancez :

```powershell
.\tools\Update-LocalIdentityDatabase.ps1
```

Le script lit les paramètres de `.env` via la configuration Compose en mémoire, démarre uniquement `identity-db` si nécessaire, puis applique la migration à ce projet. Il ne journalise pas les identifiants et s'arrête si un conteneur Identity d'un autre projet Compose existe, afin d'éviter de migrer une base ambiguë. La factory EF du Gateway évite de démarrer son hôte HTTP pendant l'opération. Le message `Cannot load library libgssapi_krb5.so.2` indique une bibliothèque système absente de l'image ; le Dockerfile Gateway l'installe maintenant. Il n'explique pas l'erreur de table manquante.

Si le navigateur indique que le service de comptes est indisponible, vérifiez `http://127.0.0.1:5000/auth/session`. Une réponse `200` contenant `registrationEnabled: true` signifie que le Gateway est prêt ; actualisez alors la page. Si le conteneur Gateway redémarre, consultez `docker compose @compose logs --tail 100 gateway-api` et appliquez les migrations Identity avec le script ci-dessus. Si les logs healthcheck montrent `/bin/sh: curl: not found`, reconstruisez les images API avec `docker compose @compose build profile-api job-api matching-api documents-api collection-api applications-api agents-api gateway-api`, puis recréez les conteneurs API. Les anciennes images ne contiennent pas le binaire requis par leurs healthchecks.

Hors de `Development`, le Gateway exige aussi `ConnectionStrings__IdentityDatabase` et les quatre réglages PFX principaux `OpenIddict__Certificates__Signing__Path`, `OpenIddict__Certificates__Signing__Password`, `OpenIddict__Certificates__Encryption__Path` et `OpenIddict__Certificates__Encryption__Password`. Pendant une rotation, configurez les certificats précédents avec les index `OpenIddict__Certificates__Signing__Previous__0__Path` / `__Password` et `OpenIddict__Certificates__Encryption__Previous__0__Path` / `__Password` (les index suivants sont possibles). Vérifiez les chemins PFX et `PasswordFile` montés, les permissions de lecture, la période de validité, les clés privées RSA (2 048 bits minimum) et l'usage de clé attendu (`DigitalSignature` pour la signature, `KeyEncipherment` pour le chiffrement). Tous les certificats configurés doivent rester valides au démarrage et être distincts. N'affichez pas les mots de passe dans les logs ou dans un terminal partagé.

## Erreur de compilation : DLL verrouillée

Si MSBuild signale que le fichier ne peut pas être copié parce qu'il est verrouillé par une API, arrêtez proprement l'instance qui tourne dans le terminal correspondant avec `Ctrl+C`, puis relancez la compilation. Pour repérer l'écoute sur un port local :

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5003 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress, LocalPort, OwningProcess
```

Vérifiez le processus associé avant de l'arrêter ; ne terminez pas un processus à l'aveugle si plusieurs configurations ou solutions sont ouvertes.

## Tests d'intégration PostgreSQL échouent

Les suites Testcontainers ont besoin de Docker Desktop en mode Linux. Vérifiez `docker info`, puis que le port requis n'est pas déjà utilisé. Les tests configurent des bases éphémères ; une erreur de pull d'image est généralement un problème d'accès réseau au registre Docker. Relancez le projet de tests ciblé après avoir rétabli Docker, puis la suite complète selon [testing.md](testing.md). Un avertissement NuGet `NU1900` signale que l'audit en ligne n'a pas pu être joint ; rétablissez l'accès NuGet et restaurez à nouveau avant d'évaluer l'audit.

## Deux conteneurs `agents-db`

Compose crée un conteneur et un volume par projet. La pile complète utilise maintenant explicitement le projet `job-application-agent`. `tools/Start-LocalAgents.ps1` réutilise ce projet et sa base quand `.env` existe ; s'il détecte une base `agents-db` active sous un autre nom de projet, il s'arrête au lieu d'en démarrer une troisième. Vérifiez les projets et leurs conteneurs :

```powershell
docker compose ls
docker ps --filter label=com.docker.compose.service=agents-db --format 'table {{.Names}}\t{{.Label "com.docker.compose.project"}}'
```

Avant d'arrêter un ancien projet Agents, identifiez le volume qui contient les données à garder ; `stop` conserve son volume, tandis que `down -v` le supprime.

Après arrêt d'une ancienne base isolée, le service `agents-db` du projet principal doit être recréé avec les fichiers Compose complets pour rejoindre `job-application-agent_default`. La base du projet principal réutilise `agents_postgres_data` ; n'ajoutez pas le volume de l'ancien projet ni `down -v` pour faire disparaître le doublon.

## Frontend Angular signale une feuille SCSS absente

Vérifiez que le fichier déclaré existe à côté du composant et que son nom/casse correspond à `styleUrl` ou `styleUrls`. Ce projet sépare les composants en fichiers `.ts`, `.html` et `.scss`. Après un changement de dépendance, lancez `npm ci` depuis le dossier Angular, puis `npm run build`.

## Ollama ne répond pas ou le téléchargement échoue

Vérifiez que le serveur Ollama fonctionne et écoute localement, puis contrôlez l'inventaire et un prompt simple :

```powershell
ollama --version
Invoke-RestMethod 'http://127.0.0.1:11434/api/tags'
ollama run qwen2.5:7b 'Réponds en une phrase : à quoi sert SQL ?'
```

Si `/api/tags` ne contient pas le modèle, le téléchargement n'est pas terminé. Si `ollama pull` échoue en DNS ou TLS, vérifiez la connexion VPN/proxy et la résolution du registre avant de relancer. L'API locale peut fonctionner sans accès Internet une fois le modèle présent. Pour garder les fonctions cloud coupées dans le terminal courant, régler `OLLAMA_NO_CLOUD=1` avant le démarrage d'Ollama. Ne copiez pas les prompts contenant des données candidat dans un rapport public.

## Connexion ou mutation refusée

Une réponse 401 indique généralement une session absente/expirée ou un bearer invalide ; reconnectez-vous et vérifiez que Gateway et les API partagent le même issuer et la même configuration. En `Development`, un redémarrage du Gateway invalide les jetons signés par ses clés éphémères. Une réponse 400 sur une mutation `/api` peut indiquer que l'en-tête `X-CSRF-TOKEN` manque ou est périmé ; rechargez la page pour obtenir un nouveau jeton antiforgery. Une réponse 503 sur `/api` du Gateway signifie que la base Identity n'est pas configurée. Vérifiez aussi les migrations Identity et l'état ready.

Un HTTP 429 vient d'une limite configurée dans l'API ; attendez la fenêtre indiquée avant de réessayer. N'effectuez pas de relances rapides en boucle sur les routes d'import, extraction ou génération.

Pour éviter une perte de données, ne lancez pas `down -v`, ne supprimez pas `bin/obj` pendant qu'un processus les utilise, et ne rejouez pas manuellement un message RabbitMQ en file morte avant d'avoir inspecté la cause. Les guides [sécurité](security.md), [événements](events.md) et [migrations](migrations.md) décrivent les procédures associées.
