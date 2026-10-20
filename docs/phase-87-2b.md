# Phase 87.2b — Pièces vérifiées et revue HTTP

Le [guide de configuration et du parcours](applications-review.md) décrit la migration Documents, le canal dédié et les routes. Les règles ont été ajoutées dans [applications.md](applications.md) avant leur implémentation.

## Résultat

Documents conserve une confirmation humaine immuable du lien propriétaire/profil/export, avec offre, type et empreintes des sources. Un canal de lecture dédié permet à Applications de vérifier ce reçu sans recevoir le jeton propriétaire Documents. Les routes de rattachement, mise en revue, approbation explicite et retour en Draft sont disponibles. Les pièces sont revérifiées avant la revue et l'approbation ; la révision exacte est vérifiée sous verrou SQL après les appels distants.

Les pièces remplacées restent dans l'historique. Les changements concurrents empêchent l'approbation d'une ancienne révision. Le retour en Draft fonctionne pendant une panne Documents. Un export ou Approved n'entraîne aucun envoi.

## Code et schéma

- Documents.Domain : DocumentApplicationBinding, reçu par export.
- Documents.Application : service de confirmation et de relecture vérifiée ; approbation, hashes, snapshot et faits figés contrôlés.
- Documents.Infrastructure : dépôt transactionnel avec verrou par export, unicité et FK propriétaire restrictive ; migration **20261016015951_AddDocumentApplicationBindings** et snapshot.
- Documents.Api : canal facultatif avec jeton dédié, validation de configuration et comparaison à temps constant ; trois routes, limites et scopes séparés. Toutes les fonctions existantes restent utilisables sans activer le canal.
- Applications.Application : orchestration de la vérification avant mutation, contrôle du statut/révision et retour hors ligne.
- Applications.Infrastructure : client HTTP local à jeton de service, sans redirection/cookie, réponse de 16 Kio maximum et délai de 10 secondes.
- Applications.Api : quatre nouvelles routes authentifiées et erreurs contrôlées.

Aucun nouveau projet, aucun schéma Applications changé. Les configurations locales doivent déclarer le même propriétaire et profil dans les deux services. Le profil est associé explicitement au document par son propriétaire ; aucun transfert de faits vers Profile n'est déduit de ce lien.

## Validation

**388 tests réussis, zéro échec et zéro ignoré** : Applications 85 unitaires et 50 dans la suite d'intégration/configuration ; Documents 96 unitaires et 157 d'intégration. Les compteurs des quatre TRX ont été relus. Le lot ajoute 51 tests Applications aux 84 du lot précédent.

Compilation complète des **38 projets réussie, zéro erreur et zéro avertissement**.

Les tests utilisent PostgreSQL 17 éphémère et les vrais hôtes Applications, Documents, Job et Profile via TestServer. Des CV synthétiques, faits revus, compositions et vrais exports DOCX sont préparés pour les scénarios ; aucun vrai CV n'est traité. Les tests couvrent parcours complet et relecture, confirmation absente, mauvaise offre/profil/hash, export non lié, approbation ou octets altérés, panne Documents, retour en Draft, refus de modification en revue, révision concurrente, approbations et confirmations concurrentes, séparation des jetons et configuration absente/invalide.

Les tests unitaires couvrent également le client de reçus (champs, erreurs HTTP/JSON/bornes, origines, jeton dédié et annulation) et une mutation entre vérification distante et écriture. Les suites Documents existantes couvrent import, extraction réelle, faits, transferts, versions, brouillons, composition et exports avec la nouvelle migration.

```powershell
dotnet test tests/Applications/JobApplicationAgent.Applications.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-2b" -m:1 --disable-build-servers
dotnet test tests/Applications/JobApplicationAgent.Applications.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase87-2b" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-2b" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase87-2b" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase87-2b" -m:1 --disable-build-servers
```

Les limites de corps Kestrel et le transport réseau/TLS ne sont pas validés par TestServer. Aucune base persistante migrée, aucun commit/push ni changement de branche. Les modifications préexistantes sont conservées. Les autres suites et Angular ne sont pas relancés.

## Suite

**87.3 : déclaration d'une soumission déjà réalisée manuellement, statuts après soumission et retours recruteurs**, puis relances. L'identité centralisée et la soumission automatisée autorisée restent dans leurs phases 91 et 96–97. Il n'existe pas de transaction distribuée entre les bases : le contrôle porte sur des pièces immuables par leurs API au moment de la vérification, avec contrôle final de révision dans Applications.
