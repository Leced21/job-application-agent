# Phase 86.2a — texte sourcé PDF/DOCX et worker séparé

Lot livré le 15 octobre 2026. La première partie de 86.2 extrait le texte des CV importés et le conserve à vérifier, sans données de qualifications inventées ni modification du profil. La structuration et la revue des données candidates restent le lot 86.2b. Le [guide d'extraction](documents-extraction.md) détaille configuration, routes, limites et sources techniques.

## Résultat

POST `/api/v1/documents/resumes/{id}/extraction` produit ou retrouve un résultat de la version du moteur. GET sur la même route le consulte. Le résultat contient les références au CV, son SHA256, la version du moteur, une date UTC et des segments indexés par page PDF ou paragraphe DOCX. Son état reste PendingReview. Le statut d'import UploadedUnanalysed ne devient pas une déclaration de CV vérifié.

L'accès est filtré par le propriétaire authentifié dans SQL, comme l'import. Les documents absents ou d'un autre propriétaire restent invisibles ; aucune extraction ne démarre dans ce cas. Le texte n'est jamais envoyé à un service externe, ni transformé automatiquement en compétences, expériences ou diplômes. Une phrase négative sur une certification reste une phrase, pas une certification candidate.

L'extraction PDF utilise PdfPig 0.1.16 épinglé et ContentOrderTextExtractor, avec parsing non permissif et profondeur limitée. Les API et version ont été vérifiées auprès des [sources du projet](https://github.com/UglyToad/PdfPig) et de [NuGet](https://www.nuget.org/packages/PdfPig/0.1.16). DOCX reprend les validations d'import avant la lecture bornée du corps principal OpenXML. Aucun rendu, macro ou contenu actif n'est exécuté par l'implémentation.

## Architecture et limites

Un septième projet Documents est ajouté : ExtractionWorker, exécutable .NET, qui référence Application et PdfPig. La solution passe de 31 à 32 projets. L'API ne charge pas PdfPig dans son processus : Infrastructure implémente le port IResumeTextExtractor en démarrant le worker sans fenêtre, sans shell, avec octets d'entrée sur stdin et résultat JSON sur stdout. Le chemin du worker est une configuration serveur absolue, pas une entrée utilisateur. L'environnement du worker exclut les credentials de base et le jeton API.

Deux workers simultanés au maximum, sans attente ; 15 secondes de délai par défaut, jusqu'à 30 secondes configurables ; 5 Mio de source, 40 pages PDF/2 000 paragraphes DOCX et 200 000 caractères de texte. Sorties stdout/stderr bornées. Le tas managé est limité à 256 Mio et le working set surveillé au seuil de 512 Mio. Une annulation ou un dépassement interrompt le processus et son arbre et attend sa sortie. Les diagnostics de parseur ne sont pas exposés et le contenu du CV n'est pas journalisé.

Le processus séparé protège la disponibilité de l'API contre un parseur non coopératif ; il ne constitue pas une sandbox OS, un antivirus ou une garantie absolue de pic mémoire. Ces limites et celles de lecture des mises en page sont explicites dans le guide.

Application valide aussi le contrat renvoyé avant toute persistance. Les documents sans texte exploitable, chiffrés, malformés ou au-delà des bornes donnent 422, sans résultat partiel. Worker absent, occupé ou hors délai : 503. Le POST a un quota propre de dix appels par minute. Aucun OCR ni extraction automatique de tous les anciens imports n'est lancé.

Les résultats sont conservés une fois par document/version de moteur via index unique PostgreSQL. Deux extractions concurrentes peuvent calculer, mais la transaction gagnante fixe l'identifiant et l'autre retrouve la même ligne. La répétition renvoie le résultat existant sans redémarrer le worker. Les anciens résultats pourront être conservés lors d'une future version du moteur ; le GET courant ne liste pas cet historique.

## Migration et déploiement local

Migration **20261015131355_AddResumeTextExtractions** : table resume_text_extractions, résultat JSONB, empreinte source, propriétaire, statut et version, index unique document/version et index propriétaire/document. La FK supprime les extractions si le document est supprimé. Les CV existants ne sont pas modifiés. Seules les bases PostgreSQL de tests ont été migrées, pas la base persistante utilisateur.

Avant de lancer les nouveaux binaires, appliquer les migrations Documents puis compiler le worker et configurer Documents__Extraction__WorkerPath vers sa DLL réelle et ses dépendances. Le guide donne les commandes Release complètes. Aucun nouveau secret requis. Les imports/lectures fonctionnent sans worker configuré ; les nouvelles extractions répondent alors 503.

## Fichiers concernés

Créés :

- `src/services/Documents/JobApplicationAgent.Documents.Domain/ResumeTextExtraction.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Application/TextExtraction.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/ProcessResumeTextExtractor.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/ResumeExtractionRepository.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/Persistence/Migrations/20261015131355_AddResumeTextExtractions.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/Persistence/Migrations/20261015131355_AddResumeTextExtractions.Designer.cs`
- `src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/JobApplicationAgent.Documents.ExtractionWorker.csproj`
- `src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/Program.cs`
- `tests/Documents/JobApplicationAgent.Documents.UnitTests/ExtractedTextTests.cs`
- `tests/Documents/JobApplicationAgent.Documents.IntegrationTests/ResumeExtractionTests.cs`
- `docs/documents-extraction.md`
- `docs/phase-86-2a.md`

Modifiés : Documents.Infrastructure/DocumentsDbContext.cs et snapshot ; Documents.Api/Program.cs et ResumesController.cs ; Documents.IntegrationTests/DocumentsApiFactory.cs et projet de tests (référence de build du worker, copie de ses dépendances et PdfPig pour générer de vrais PDF synthétiques) ; `JobApplicationAgent.slnx`, `docs/documents.md` et `README.md`. Aucun changement d'un autre microservice de production ni de frontend. Modifications préexistantes conservées ; aucun commit, push ou changement de branche.

## Validation

Compilation des **32 projets : zéro erreur, zéro avertissement**. **42 tests Documents réussis, zéro échec : 22 unitaires et 20 d'intégration**, soit 15 nouveaux tests. Les suites des autres services et Angular ne sont pas relancées ; leurs projets compilent. L'audit NuGet du worker et de ses dépendances ne signale aucune vulnérabilité connue avec les sources actuelles.

Les tests unitaires couvrent version/type/index des segments, absence de texte, limites de caractères/pages et conservation d'une phrase négative sans inférence de qualification. Les tests d'intégration démarrent le vrai worker séparé et PostgreSQL 17 : PDF valide construit avec PdfPig, DOCX synthétique, persistance du texte et SHA source, résultat PendingReview, reprise, index unique sous concurrence, rejet des faux PDF d'enveloppe, PDF sans texte et document de 41 pages. Le test d'identité refuse extraction et lecture anonymes et d'un autre propriétaire. Les tests de processus vérifient l'absence de worker et un délai de 1 ms, sans résultat en base.

Le modèle EF ne présente pas de changement non migré selon le test API. Les fixtures initiales de 86.1 restent vertes : l'acceptation d'une enveloppe à l'import n'est pas confondue avec un parsing réussi. Les tests ne lisent aucun CV utilisateur ni ne contactent de service externe. Les credentials de conteneur sont aléatoires et temporaires. Les seuils mémoire ne sont pas éprouvés par un test de stress ; ce lot teste les limites de contenu et le mécanisme d'arrêt sur délai.

Les deux TRX relus contiennent 22/22 et 20/20. Les dépendances du worker sont copiées dans le répertoire des tests, y compris sous --artifacts-path, et les builds isolés préservent les binaires des API locales éventuellement actives.

Commandes principales :

```powershell
dotnet build src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/JobApplicationAgent.Documents.ExtractionWorker.csproj --configuration Phase86Extraction -m:1 --disable-build-servers
dotnet build src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj --configuration Phase86Extraction -m:1 --disable-build-servers
# Connexion de design et identité temporaires, sans connexion à la base :
$env:ConnectionStrings__DocumentsDatabase = 'Host=localhost;Database=documents_design'
$env:Documents__LocalAuth__OwnerId = [guid]::NewGuid().ToString()
$env:Documents__LocalAuth__Token = [guid]::NewGuid().ToString('N')
dotnet ef migrations add AddResumeTextExtractions --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --configuration Phase86Extraction --no-build --output-dir Persistence/Migrations
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests/JobApplicationAgent.Documents.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-2a" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase86-2a-results"
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests/JobApplicationAgent.Documents.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-2a" -m:1 --disable-build-servers --no-restore --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase86-2a-results"
dotnet list src/services/Documents/JobApplicationAgent.Documents.ExtractionWorker/JobApplicationAgent.Documents.ExtractionWorker.csproj package --vulnerable --include-transitive --no-restore
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-2a" -m:1 --disable-build-servers
```

## Suite

**86.2b : données candidates structurées, preuves par segment et revue explicite.** Ni expérience ni diplôme ne sera inventé ou transféré au profil parce qu'un mot apparaît dans le CV. Le présent lot ne fournit pas d'endpoint d'approbation des données ni d'intégration au profil ; son PendingReview reste un état de préparation. La phase 86 reste en cours.
