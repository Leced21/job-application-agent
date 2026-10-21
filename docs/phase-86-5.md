# Phase 86.5 — Exports conservés et prévisualisation protégée

## Résultat

Le propriétaire peut exporter le contenu Approved exact en PDF ou DOCX, relire l'historique et prévisualiser avant téléchargement. Le PDF inline et la pièce jointe utilisent les mêmes octets conservés ; le DOCX a un aperçu HTML du contenu, avec pagination Word non garantie. Toute génération, lecture d'aperçu et lecture de fichier recontrôle l'approbation et les empreintes. Voir le [guide des exports](documents-export.md).

Le contenu n'est pas enrichi : les informations manquantes restent absentes. Aucun appel IA, à Job/Profile ou service externe, aucune candidature envoyée. La fidélité de pagination Word reste à vérifier dans un lecteur compatible ; ce lot ne prétend pas une prévisualisation Word native.

## Changements

- Documents.Domain/DocumentExport : octets immuables, SHA du contenu et du fichier, approbation, format/moteur, dates et bornes.
- Documents.Application/DocumentExportService : gate Approved/auteur/hash, reprise, contrôles de lecture et DTO d'historique sans octets.
- Documents.Infrastructure/ApprovedDocumentRenderer : PDF PdfPig 0.1.16, DOCX ZIP/XML, police Vera embarquée et licence, pagination/retours/limites, deux rendus simultanés.
- Documents.Infrastructure/DocumentExportRepository : transaction, verrou dossier/format et unicité ; lectures propriétaires.
- Documents.Api/DocumentExportsController et Program : cinq routes, limites, pièces jointes, PDF inline et HTML échappé sous CSP.
- Migration **20261015184131_AddDocumentExports** et snapshot : table bytea, contrainte de longueur, FK propriétaire restrictive, unicité et index historique.
- Tests Documents : refus avant approbation, génération et reprise, concurrence, droits, corruption, texte/glyphes/marges/pagination, structure OOXML et aperçu sûr. La fixture nettoie les exports avant les compositions.

Pas de code de production des autres services ni frontend modifié. Aucune base persistante migrée, aucun vrai CV exporté, aucun commit, push ou changement de branche ; les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les deux suites Documents totalisent **253 tests réussis, zéro échec et zéro ignoré : 96 unitaires et 157 d'intégration**. Les deux TRX finaux ont été relus. Ce lot ajoute 32 tests : 11 unitaires et 21 d'intégration/moteur.

Les tests couvrent CV/lettre dans les deux formats, octets PDF identiques entre aperçu et téléchargement, conservation et hash des fichiers, erreurs de format/hash, approbation absente/rejetée, concurrence et reprise après saturation, accès propriétaire/anonyme, fichier altéré, approbation changée en base, CSP/échappement de l'aperçu, historique et protection de la composition.

Les tests de moteur vérifient police embarquée, accents français, marges, retours à la ligne et dernière ligne sur plusieurs pages, refus d'un glyphe non supporté, taille/pages/caractères de contrôle, texte DOCX exact y compris markup et Unicode, absence de liens externes/macros et réimport DOCX accepté. Les fichiers PDF de test ont été rendus à 120 dpi avec le Poppler du runtime puis toutes leurs pages inspectées : aucune troncature, superposition ou glyphe manquant. Les DOCX ont été relus avec le python-docx du runtime et la police obfusquée contrôlée.

Le script render_docx.py a été exécuté mais a échoué parce que LibreOffice soffice.exe est absent du runtime. Le rendu Word n'est donc pas validé visuellement et l'aperçu proposé reste celui du texte, pas de sa pagination. Aucun document DOCX candidat n'est livré comme artefact final dans cette tâche de backend. Les skills PDF et Documents ont servi à la QA ; les sorties synthétiques/captures restent des intermédiaires.

Les migrations et contrôles EF s'exécutent sur PostgreSQL 17 éphémère. Les suites antérieures Documents, dont le vrai worker et les hôtes Job/Profile de test, restent incluses. Les autres suites et Angular ne sont pas relancées ; leurs projets .NET compilent. Les limites de transport ne sont pas testées par un nouveau serveur Kestrel.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-exports" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-exports" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-exports" -m:1 --disable-build-servers
```

Les artefacts TEMP préservent les binaires des API locales éventuellement actives. La police et sa licence sont embarquées dans le projet pour éviter toute dépendance aux polices du poste lors du rendu PDF.

## Suite et limites

Le socle documentaire dispose des versions, sources approuvées, préparation, composition/revue et exports conservés. La pagination DOCX doit encore être vérifiée visuellement avant usage réel ; l'interface premium, identité centralisée et rétention restent dans leurs phases prévues. Prochaine tranche : **phase 87, socle du suivi des candidatures**, avec rattachements explicites aux offres et documents choisis. Aucun passage automatique en candidature soumise n'est autorisé par un export.
