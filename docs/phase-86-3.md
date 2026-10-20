# Phase 86.3 — Dossiers et historique des versions de CV

## Résultat

Un propriétaire peut créer un dossier nommé depuis un fichier importé, y ajouter d'autres fichiers et consulter l'historique numéroté ou retrouver le dossier d'un resumeId. Les fichiers et preuves restent immuables ; les nouveaux rattachements ne copient pas les validations d'une autre version. Voir le [guide des versions](documents-versions.md).

Les numéros sont attribués sous transaction et verrou par dossier. Un fichier ne peut être rattaché qu'une fois ; la reprise identique conserve numéro et horodatage. Les métadonnées contradictoires donnent 409. Les propriétaires sont contrôlés par les requêtes et des FK composites. Les historiques sont paginés en SQL, sans lecture des octets. La limite est de 1 000 versions par dossier, sans empêcher une reprise existante à cette limite.

## Fichiers et migration

- Documents.Domain/ResumeSeries.cs : dossier et rattachement immuables, bornes et dates UTC.
- Documents.Application/ResumeSeriesService.cs : commandes, DTO de métadonnées, port et validations.
- Documents.Infrastructure/ResumeSeriesRepository.cs : lectures propriétaires, transaction, verrous, allocation et reprise.
- Documents.Infrastructure/DocumentsDbContext et migration/snapshot **20261015175642_AddResumeSeries** : deux nouvelles tables, unicité, numéros bornés et FK propriétaires ; fichiers rattachés protégés contre la suppression directe.
- Documents.Api/Controllers/ResumeSeriesController et Program : sept routes protégées, corps 4 KiB, limiteur 30 écritures/minute et erreurs 409.
- Tests Documents : invariants, API, concurrence, limite et migration ; la fixture nettoie les dossiers éphémères avant les fichiers.

Les imports anciens restent autonomes après migration. Aucun octet, SHA, statut, extraction, fait, date confirmée ni reçu Profile existant n'est modifié. Pas de migration utilisateur, de CV réel regroupé, d'appel externe, de frontend, de commit, de push ou de changement de branche. Les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les deux suites Documents totalisent **153 tests réussis, zéro échec et zéro ignoré : 57 unitaires et 96 d'intégration**. Les deux TRX finaux ont été relus. Ce lot ajoute 28 tests : 14 unitaires et 14 d'intégration.

Les tests d'intégration couvrent l'historique de fichiers PDF/DOCX et leur téléchargement, la conservation d'un fait approuvé sur sa version d'origine, huit créations initiales identiques concurrentes, ajouts et reprises simultanés, deux dossiers concurrents pour un seul fichier, accès anonyme/étranger, métadonnées et pagination invalides, limite à 1 000, contraintes propriétaires/suppression et migration d'un fichier préexistant sans regroupement automatique.

Les tests utilisent PostgreSQL 17 éphémère et ASP.NET TestServer. Le contrôle EF vérifie l'absence de modification de modèle non migrée. Les anciens tests d'extraction avec le vrai worker, revue, confirmation et transfert vers Profile restent inclus. Les autres suites et Angular ne sont pas relancées ; leurs projets .NET sont compilés. Les limites de transport ne font pas l'objet d'un nouveau test Kestrel.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-series" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-series" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-series" -m:1 --disable-build-servers
```

Les artefacts TEMP préservent les binaires des API locales éventuellement actives.

## Suite

Le socle de conservation et consultation de versions est disponible. La phase 86 reste en cours : prochain lot **86.4, préparation contrôlée des documents adaptés à une offre**, avant export et prévisualisation. Il faudra sélectionner explicitement les sources vérifiées et conserver leur provenance, sans inventer de qualifications. Les traitements IA viendront selon les phases 88/89 ; le présent lot ne génère aucun texte.
