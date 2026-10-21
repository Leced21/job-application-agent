# Phase 86.1 — service Documents et import authentifié de CV

Lot livré le 15 octobre 2026. Six projets sont ajoutés, quatre couches Documents et deux suites de tests. La solution passe de 25 à 31 projets. Le [guide Documents](documents.md) donne les commandes, contrats et limites. La phase 86 complète reste en cours.

## Résultat et architecture

L'API accepte PDF/DOCX en multipart, conserve octets originaux et métadonnées en PostgreSQL, puis permet liste paginée, lecture et téléchargement authentifiés. Le statut UploadedUnanalysed empêche de présenter cet import comme un CV analysé ou vérifié. Les imports successifs sont distincts et conservés, sans regroupement de versions dans cette tranche. Aucun profil n'est modifié.

Domain porte ResumeDocument et son identité de propriétaire ; Application expose ResumeService, les métadonnées, le port repository et les contrôles de format ; Infrastructure implémente EF/PostgreSQL ; API compose l'identité locale, les endpoints et limites. Aucun projet de production ne référence un autre microservice et aucun package d'extraction/IA n'est ajouté.

Le dépôt ne disposait pas d'authentification existante. Un Bearer local configuré hors Git est donc lié côté serveur à un OwnerId stable. La configuration absente/invalide fait échouer le démarrage. Les requêtes ne choisissent pas leur propriétaire ; le repository filtre lecture, téléchargement et liste dans SQL. Identifiant absent ou d'un autre propriétaire : même 404. Le jeton n'est pas conservé dans un fichier suivi ni affiché par les tests.

Le stockage binaire en bytea et les métadonnées appartiennent à la même ligne, donc à la même sauvegarde transactionnelle, sans fichier public ni problème de fichier orphelin entre deux stockages. L'API restitue une pièce jointe, avec no-store et nosniff. L'index de pagination inclut OwnerId et les listes ne chargent pas les octets des CV.

La validation borne les flux d'upload à 5 Mio, associe extension et MIME et vérifie l'enveloppe PDF ou les parties DOCX transitionnelles. Le contrôle ZIP borne aussi la décompression réelle, pas seulement les tailles de métadonnées, et refuse macros, relations externes, DTD et chemins dangereux. Il n'y a ni antivirus ni analyse PDF complète : les limites précises du guide font partie du contrat livré. Ce lot fournit un import authentifié, pas une certification de sûreté du fichier.

## Base et configuration

Migration **20261015123604_InitialDocumentsSchema**, table resume_documents, index OwnerId/UploadedAtUtc/Id, contenu bytea, SHA256 et statut. La migration a été appliquée uniquement aux PostgreSQL 17 éphémères des tests. Aucune base persistante utilisateur n'a été créée ou migrée. Les autres schémas ne changent pas.

API localhost:5005, PostgreSQL localhost:5436 via docker-compose.documents.yml séparé. La route YARP additionnelle `/api/v1/documents/**` est ajoutée, l'API restant responsable de l'authentification. La gateway ne reçoit aucun nouveau secret dans sa configuration. Connexion, token et OwnerId sont fournis hors Git ; le guide indique leur préparation. Aucun token/identifiant de base par défaut n'est livré.

## Fichiers concernés

Créés : arborescences `src/services/Documents/` et `tests/Documents/`, six fichiers projet, modèles et services cités ci-dessus, DocumentsDbContext et factory EF, ResumeRepository, LocalAuthentication, Program, ResumesController, appsettings/launchSettings, migration InitialDocumentsSchema avec Designer et snapshot ; tests ResumeSamples, ResumeValidationTests, DocumentsApiFactory, ResumeApiTests et LocalIdentityTests ; `docker-compose.documents.yml`, `docs/documents.md` et `docs/phase-86-1.md`.

Modifiés : `JobApplicationAgent.slnx`, `src/gateway/JobApplicationAgent.Gateway/appsettings.json` et `README.md`. Les fichiers des autres services et du frontend ne changent pas dans cette tranche. Les modifications préexistantes sont préservées ; aucun commit, push ou changement de branche.

## Validation

Compilation finale des **31 projets : zéro erreur et zéro avertissement**. Les deux TRX finaux ont été relus : 15/15 et 12/12, sans échec.

**27 nouveaux tests réussis, zéro échec : 15 unitaires et 12 d'intégration.** Ils couvrent enveloppes PDF/DOCX, suppression des préfixes de chemin client, association extension/MIME, contrôles de noms, macros, objets embarqués, doublons ZIP, DTD, relations externes, décompression excessive, flux trop volumineux, conservation des octets et état non analysé.

Les tests API/PostgreSQL couvrent import et téléchargement des deux formats, égalité des métadonnées à la relecture, no-store/nosniff et téléchargement attachment, authentification absente/fausse, invisibilité d'un document d'un autre propriétaire même avec un en-tête usurpé, refus sans persistance, conservation de plusieurs imports et pagination. Les trois tests de configuration d'identité vérifient les refus de token absent/faible ou propriétaire absent. Le test de modèle ne détecte pas de migration manquante.

Les tests utilisent des données synthétiques et conteneurs à credentials aléatoires, aucun CV utilisateur. Les fixtures PDF testent volontairement l'enveloppe d'upload, pas un parseur PDF complet. Le transport Kestrel de la limite HTTP de 6 Mio n'est pas testé par TestServer ; le plafond applicatif de 5 Mio l'est. Le Compose a été validé avec config --quiet sans lancer sa base persistante. Les suites des autres services et Angular ne sont pas relancées dans ce lot ; la compilation complète vérifie leur intégration.

Commandes principales :

```powershell
dotnet build src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj --configuration Phase86Documents -m:1 --disable-build-servers
# Connexion de design sans credentials et identité temporaire de conception, non affichée :
$env:ConnectionStrings__DocumentsDatabase = 'Host=localhost;Database=documents_design'
$env:Documents__LocalAuth__OwnerId = [guid]::NewGuid().ToString()
$env:Documents__LocalAuth__Token = [guid]::NewGuid().ToString('N')
dotnet ef migrations add InitialDocumentsSchema --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --configuration Phase86Documents --no-build --output-dir Persistence/Migrations
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests/JobApplicationAgent.Documents.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-1" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=final' --results-directory "$env:TEMP/job-agent-phase86-1-final"
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests/JobApplicationAgent.Documents.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-1" -m:1 --disable-build-servers --no-restore --logger 'trx;LogFilePrefix=final' --results-directory "$env:TEMP/job-agent-phase86-1-final"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-1" -m:1 --disable-build-servers
docker compose -f docker-compose.documents.yml config --quiet
```

Les variables de conception n'ont pas été enregistrées ni utilisées pour une base utilisateur. Pour lancer le service avec ses documents, conserver le véritable OwnerId et le jeton dans une configuration locale appropriée selon le guide ; ne pas réutiliser les variables temporaires de conception. Les builds isolent les artefacts pour préserver les API éventuellement actives.

## Suite

Prochain lot **86.2 : extraction bornée, données candidates et revue avant intégration au profil**. Antivirus/quarantaine, OIDC multi-utilisateur, TLS et chiffrement du stockage ne sont pas livrés par ce socle local. Le système ne génère ni candidature ni document adapté et ne déduit pas d'expériences ou de diplômes à ce stade.
