# Phase 86.4b — Composition vérifiable et revue explicite

## Résultat

Un dossier préparé peut être composé avec le modèle français déterministe verified-fr-v1, en contenu CV ou lettre sobre. Les blocs reproduisent les champs approuvés et les références source, sans transformer les exigences de l'offre en compétences. Les dates confirmées restent séparées des périodes verbatim. La revue finale est attribuée au propriétaire et liée à l'empreinte exacte du contenu. Voir le [guide de composition](documents-composition.md).

La composition n'est pas une génération IA ni un document complet prêt à diffuser : identité et coordonnées absentes des sources restent absentes et signalées. L'export et sa prévisualisation suivent en 86.5 ; aucun envoi de candidature n'est ajouté. La phase 86 reste en cours.

## Changements

- Documents.Domain/DocumentComposition : texte sérialisé immuable, hash, source/modèle et revue Pending/Approved/Rejected finale, idempotente et attribuée.
- Documents.Application/DocumentCompositionService : composition neutre depuis les seules sources figées, validation de leur identité/empreinte et références, DTO, historique et validation de la revue.
- Documents.Infrastructure/DocumentCompositionRepository : transaction/verrou de création et de revue, lectures propriétaires et résumés paginés sans texte complet.
- Documents.Api/Controllers/DocumentCompositionsController et Program : quatre routes protégées, limiteur et bornes HTTP.
- Documents.Infrastructure/DocumentsDbContext et migration/snapshot **20261015182204_AddDocumentCompositions** : nouvelle table avec PK de dossier, FK propriétaire restrictive, JSON original en text et borne de 2 MiB.
- Tests Documents : échantillons synthétiques partagés, non-invention, précision des dates, reprises, concurrence, décisions finales, isolation, hashes et contraintes ; nettoyage des compositions avant les dossiers.

Aucun code de production des autres services ni frontend modifié. Pas de migration persistante utilisateur, de vrai CV composé, de revue utilisateur effectuée, d'appel externe, de commit, de push ou de changement de branche. Les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les deux suites Documents totalisent **221 tests réussis, zéro échec et zéro ignoré : 85 unitaires et 136 d'intégration**. Les deux TRX finaux ont été relus. Ce lot ajoute 30 tests : 18 unitaires et 12 d'intégration.

Les tests unitaires vérifient les deux modèles, la copie des négations/descriptions, l'absence d'ajout depuis les exigences Job, les périodes non complétées, les dates confirmées/en cours/émission selon catégorie, les références et refus de sources incompatibles, ainsi que la revue finale et son auteur.

Les tests API/PostgreSQL vérifient persistance et hash exact, texte inchangé après approbation, six créations simultanées pour une seule composition, deux revues opposées pour une seule décision, reprise de la date initiale, notes Unicode de 1 000 caractères, refus des mauvais hash/confirmation/décision, protection propriétaire/anonyme, historique paginé, source corrompue et FK protectrices. La migration et le contrôle EF s'exécutent sur PostgreSQL 17 éphémère. Les parcours Documents antérieurs, dont le vrai worker, Job et Profile de test, restent inclus. Les autres suites et Angular ne sont pas relancées ; leurs projets .NET compilent. Aucun nouveau test Kestrel des limites de transport.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-composition" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-composition" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-composition" -m:1 --disable-build-servers
```

Les artefacts TEMP préservent les binaires des API locales éventuellement actives. La revue ne modifie pas PreparedDraft.Status, qui décrit l'instantané de préparation ; le statut du contenu est Composition.Decision.

## Suite

86.5 : export PDF/DOCX et prévisualisation, avec accès propriétaire, contenu approuvé et empreinte exacte. La politique d'édition/révocation/rétention reste à définir ; un autre contenu nécessite actuellement un nouveau dossier. Les traitements IA restent prévus en phases 88/89.
