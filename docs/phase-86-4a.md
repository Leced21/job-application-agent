# Phase 86.4a — Dossier de préparation d'un document adapté

## Résultat et périmètre

Le propriétaire choisit explicitement une offre Job locale, un CV/version et un ensemble ordonné de faits approuvés. Le dossier conserve leurs instantanés et provenance, sous statut PreparedForReview. L'historique et la reprise idempotente restent disponibles après modification ou disparition de l'offre. Le [guide de préparation](documents-drafts.md) décrit les contrats et la migration.

Le lot ne produit pas encore le CV ni la lettre : Resume/CoverLetter indiquent l'intention du dossier, pas un fichier généré. La composition et revue du contenu suivent en 86.4b. Aucun appel IA, export ou envoi de candidature n'est ajouté ; la phase 86 reste en cours.

## Changements

- Documents.Domain/PreparedDocumentDraft : instantané immuable, SHA-256, clés de requête, source et date UTC, statut non final.
- Documents.Application/PreparedDraftService : sélection bornée et ordonnée, provenance des faits/dates, validation avant accès Job, reprise et DTO d'historique.
- Documents.Infrastructure/PreparedDraftRepository : projection des sources sans octets, propriétaire dans toutes les requêtes, transaction et verrou de clé d'idempotence.
- Documents.Infrastructure/DraftJobClient : GET borné vers Job local, sans Bearer ni redirection ; contrôle du contrat et des IDs/statuts.
- Documents.Api/Controllers/PreparedDraftsController et Program : trois routes protégées et limitées, erreurs explicites.
- Migration **20261015180845_AddPreparedDocumentDrafts** : table de dossiers, unicité propriétaire/requestId, taille d'instantané et FK propriétaire/CV restrictive ; aucun dossier créé automatiquement.
- Tests Documents : Job.Api référencé sous alias dans les tests, deux vrais hôtes API et deux PostgreSQL 17 éphémères, cas de transport invalides interceptés localement. La fixture nettoie les dossiers avant les fichiers.

Aucun code de production Job/Profile/Matching/Collection ni frontend modifié. Aucune base persistante migrée, aucune donnée utilisateur préparée, aucun accès externe, commit, push ou changement de branche. Les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les deux suites Documents totalisent **191 tests réussis, zéro échec et zéro ignoré : 67 unitaires et 124 d'intégration**. Les deux TRX finaux ont été relus. Ce lot ajoute 38 tests : 10 unitaires et 28 d'intégration.

Les tests couvrent les deux intentions Resume/CoverLetter, la sélection exclusive et son ordre, les exigences Job non reprises comme compétences, les sources/propriétaires, les faits non approuvés, la provenance des dates confirmées, les offres inactives, les réponses invalides/trop grandes, les origines non locales refusées, la reprise après suppression et panne de Job, la concurrence, la pagination, le SHA original et les FK protectrices.

Les tests antérieurs d'upload, extraction avec vrai worker, revue, confirmation, transfert Profile et versions restent inclus. La migration est appliquée sur PostgreSQL 17 éphémère et l'absence de changement EF non migré est contrôlée. Les autres suites ne sont pas relancées ; les projets .NET correspondants compilent. Les limites de transport ne font pas l'objet d'un nouveau test Kestrel.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-drafts" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-drafts" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-drafts" -m:1 --disable-build-servers
```

Les artefacts TEMP évitent de remplacer les DLL des API locales éventuellement actives.

## Suite

86.4b : composition contrôlée d'un contenu fondé sur les sources gelées et revue explicite, sans inventer de qualifications ni transformer les exigences d'annonce en faits candidat. L'export PDF/DOCX, sa prévisualisation et l'historique des générations suivent ensuite. L'identité reste le socle local ; multi-utilisateur et rétention restent dans la sécurisation.
