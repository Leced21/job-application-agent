# Phase 86.2b — structuration sourcée et revue des données candidates

Lot livré le 15 octobre 2026. Quatre catégories peuvent être annotées à partir du texte extrait : compétences, expériences, formations et certifications. Chaque valeur est copiée depuis un passage sélectionné, puis une revue explicite peut l'approuver ou la rejeter. Le [guide des faits](documents-facts.md) décrit les champs, preuves, routes et exemples.

## Résultat et périmètre

La structuration est assistée par annotation manuelle des segments, pas une extraction automatique de qualifications par IA. Le client fournit catégorie, nom de champ et positions dans le texte ; il ne fournit pas de valeur libre à attribuer au CV. Le serveur dérive la valeur exacte et un extrait de contexte, avec référence à l'extraction, son moteur et au SHA256 du CV. Les champs d'un même fait peuvent provenir de segments différents.

Les faits commencent Pending, sans inférence de dates, durée, niveau ou équivalence de diplôme. Les champs facultatifs absents ne sont pas fabriqués. Une sélection textuelle de Azure dans « Not certified in Azure » reste une annotation non vérifiée avec sa négation dans le contexte ; ce n'est pas une certification automatiquement approuvée. Le propriétaire doit contrôler le document original avant sa décision.

La revue conserve décision, note facultative, date UTC et identifiant du propriétaire authentifié. Une répétition identique préserve la date. Une décision opposée ou une modification de la note après revue donne 409. Une transaction avec verrou de ligne empêche deux décisions concurrentes de s'écraser.

La consultation peut filtrer les seuls faits Approved ; les faits Pending et Rejected restent exclus de ce sous-ensemble. **Aucun transfert vers Profile n'est effectué.** L'inspection de ses contrats a confirmé qu'ils ne relient pas encore les données à l'identité Documents et ne fournissent pas de réception idempotente des faits. Leur ajout est le lot suivant 86.2c. La revue d'un fait n'approuve pas automatiquement tout le texte du CV.

## Architecture et stockage

Domain ajoute ResumeCandidateFact et ses règles de revue. Application définit schémas typés, sélections de preuve, construction canonique, DTO, service et port repository. Infrastructure implémente les écritures et lectures EF ; API fournit les endpoints authentifiés. Aucun nouveau projet ou package, aucune référence de production vers un autre microservice.

Le schéma générique est accessible sous `/api/v1/documents/fact-schema`. Création, lecture, pagination et revue sont sous `/api/v1/documents/resumes/{resumeId}/facts`. Les valeurs de champ sont limitées à 1 000 unités UTF-16 dans un segment existant ; les bornes ne peuvent pas couper une paire Unicode. Catégories/champs inconnus, doublons et champ obligatoire absent sont refusés avant persistance. L'extrait de contexte reste limité ; il faut consulter la source complète pour une vérification sémantique.

L'empreinte d'annotation inclut extraction, catégorie et références triées par champ. Un index unique empêche les doublons sous concurrence. La création est sérialisée par CV via verrou transactionnel consultatif PostgreSQL ; elle contrôle aussi la limite de 200 faits par CV, toutes décisions et versions confondues. À cette limite, une répétition existante reste autorisée. Les lectures et revues filtrent OwnerId et ResumeId dans SQL.

Écritures limitées à 16 Kio par corps HTTP et 30 appels par fenêtre fixe d'une minute pour l'instance, sans attente. Pagination bornée à 100 lignes ; filtre de décision validé. La route de schéma expose seulement une description générique, aucune donnée de CV. Les protections no-store/nosniff et Bearer local de Documents restent actives.

## Migration

Migration **20261015132831_AddResumeCandidateFacts** : table resume_candidate_facts, preuves JSONB, identités et empreinte source, catégorie, décisions et traces de revue, index unique extraction/fingerprint et index propriétaire/CV/décision/date/ID. La FK vers l'extraction conserve la suppression en cascade depuis les CV. Aucun ancien document, texte ou profil n'est modifié et aucun fait n'est généré automatiquement à la migration.

Migration appliquée seulement aux PostgreSQL 17 éphémères des tests, pas à une base persistante utilisateur. À appliquer avant de lancer les nouveaux binaires Documents. Les schémas Profile/Matching/Job/Collection ne changent pas.

## Fichiers concernés

Créés :

- `src/services/Documents/JobApplicationAgent.Documents.Domain/ResumeCandidateFact.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Application/CandidateFacts.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/ResumeCandidateFactRepository.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Api/CandidateFactsController.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/Persistence/Migrations/20261015132831_AddResumeCandidateFacts.cs`
- `src/services/Documents/JobApplicationAgent.Documents.Infrastructure/Persistence/Migrations/20261015132831_AddResumeCandidateFacts.Designer.cs`
- `tests/Documents/JobApplicationAgent.Documents.UnitTests/CandidateFactTests.cs`
- `tests/Documents/JobApplicationAgent.Documents.IntegrationTests/CandidateFactApiTests.cs`
- `docs/documents-facts.md`
- `docs/phase-86-2b.md`

Modifiés : Documents.Infrastructure/DocumentsDbContext.cs et snapshot ; Documents.Api/Program.cs ; `docs/documents.md`, `docs/documents-extraction.md` et `README.md`. Aucun autre service de production ni frontend modifié ; aucun commit, push ou changement de branche. Les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. **66 tests Documents réussis, zéro échec : 39 unitaires et 27 d'intégration**, soit 24 nouveaux tests. Les deux TRX ont été relus avec les compteurs 39/39 et 27/27. Les suites des autres services et Angular ne sont pas relancées ; leurs projets compilent. Les anciens parcours d'import et d'extraction avec le vrai worker restent verts.

Les nouveaux tests unitaires couvrent les quatre catégories, copie verbatim, conservation d'une négation, schémas/références invalides, dépassement d'index, sélection blanche et paire Unicode, empreinte indépendante de l'ordre des champs, revue finale/idempotente et auteur propriétaire.

Les tests API/PostgreSQL vérifient la chaîne annotation/revue/consultation Approved, la préservation de source et date, la reprise d'une annotation approuvée, le rejet explicite d'une fausse certification candidate, les erreurs sans persistance, l'isolation propriétaire/CV et le refus anonyme. Huit annotations concurrentes identiques créent une seule ligne ; deux revues opposées concurrentes donnent une réussite et un conflit. Un CV alimenté de 200 annotations sourcées refuse la 201e tout en acceptant une reprise existante. Le test EF ne détecte pas de migration manquante.

Les tests de cette tranche utilisent des extractions synthétiques persistées pour isoler la structuration ; les tests 86.2a continuent à exercer le vrai parsing. Aucun CV utilisateur, appel vers Profile ou service externe. Les credentials des conteneurs sont aléatoires et temporaires. La taille HTTP et le limiteur sont configurés, sans nouveau test Kestrel de transport dans ce lot.

Commandes principales depuis la racine :

```powershell
dotnet build src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj --configuration Phase86Facts -m:1 --disable-build-servers
# Connexion et identité temporaires de conception, non utilisées sur une base utilisateur :
$env:ConnectionStrings__DocumentsDatabase = 'Host=localhost;Database=documents_design'
$env:Documents__LocalAuth__OwnerId = [guid]::NewGuid().ToString()
$env:Documents__LocalAuth__Token = [guid]::NewGuid().ToString('N')
dotnet ef migrations add AddResumeCandidateFacts --project src/services/Documents/JobApplicationAgent.Documents.Infrastructure --startup-project src/services/Documents/JobApplicationAgent.Documents.Api --configuration Phase86Facts --no-build --output-dir Persistence/Migrations
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests/JobApplicationAgent.Documents.UnitTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-2b" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase86-2b-results"
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests/JobApplicationAgent.Documents.IntegrationTests.csproj --artifacts-path "$env:TEMP/job-agent-phase86-2b" -m:1 --disable-build-servers --logger 'trx;LogFilePrefix=validated' --results-directory "$env:TEMP/job-agent-phase86-2b-results"
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-2b" -m:1 --disable-build-servers
```

Les artefacts TEMP préservent les binaires des API locales éventuellement actives. Aucun secret n'est ajouté aux fichiers suivis.

## Limites et suite

Ce lot ne propose pas automatiquement des qualifications, ne vérifie pas un diplôme auprès d'un tiers et ne garantit pas la vérité sémantique d'une sélection. Il conserve une annotation et la décision du propriétaire local. Les décisions sont finales, sans révocation ni conciliation automatique des faits approuvés ; l'interface de revue viendra en phase 90. L'identité locale ne remplace pas l'authentification centralisée prévue avec la sécurisation.

Prochain lot **86.2c : identité propriétaire, mapping complet, réception idempotente et transfert explicite des faits approuvés vers Profile**. Les données vérifiées existantes ne devront pas être écrasées, ni les champs requis manquants inventés. La phase 86 reste en cours ; versions regroupées, génération de documents et export suivent ensuite.
