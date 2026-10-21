# Phase 86.2c.2a — Mapping sourcé et confirmation durable des dates

## Résultat et périmètre

Les expériences, diplômes et certifications approuvés disposent d'un aperçu des champs Profile et d'une confirmation explicite des dates exactes par leur propriétaire. Les libellés viennent des sélections CV ; les dates restent des déclarations OwnerConfirmed, sans interprétation automatique d'une année ou d'une période. Auteur, note et date de confirmation sont conservés. Le [guide](documents-profile-mapping.md) détaille le contrat, la migration et les commandes interactives.

**Le transfert de ces trois catégories n'est pas encore implémenté.** Le socle est découpé en confirmation durable (ce lot) puis extension du récepteur et conciliation (86.2c.2b). L'import des compétences de 86.2c.1 reste opérationnel. La phase 86.2c complète et la phase 86 ne sont pas déclarées terminées.

## Changements

- Documents.Domain/ResumeCandidateFact : confirmation finale attribuée au propriétaire, indépendante de la revue initiale ; reprise JSON équivalente sans changement de date.
- Documents.Application/ProfileFactMapping : schémas requis des trois catégories, dates explicites et cohérentes, note de confirmation obligatoire, aperçu TargetFields sans champ inventé.
- Documents.Infrastructure : verrou de ligne pour la confirmation, trois colonnes nullable sur les faits et migration **20261015152633_AddConfirmedProfileDates** avec snapshot.
- Documents.Api : GET/POST profile-mapping sous authentification, limites HTTP et limiteur des faits existants.
- Tests Documents : validations métier, protections d'accès, source préservée, date partielle refusée, notes Unicode, reprise et confirmations concurrentes.

Aucun service Profile/Job/Matching/Collection de production ni frontend modifié dans cette tranche. Aucune base persistante, aucun import utilisateur, aucun service externe sollicité. Aucun commit, push ou changement de branche ; les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. **106 tests Documents réussis : 43 unitaires et 63 d'intégration, zéro échec et zéro ignoré** ; les deux TRX finaux ont été relus. Ce lot ajoute 30 tests (4 unitaires et 26 d'intégration).

Les tests d'intégration utilisent PostgreSQL 17 éphémère et l'API TestServer ; les tests antérieurs du vrai worker d'extraction et du transfert de compétences restent inclus. L'absence de différence EF non migrée est contrôlée. Les trois mappings sont relus et repris après stockage JSONB, sans changer preuves ni date de revue. Deux confirmations concurrentes différentes donnent une réussite et un conflit ; la reprise du gagnant conserve son horodatage. Les dates partielles au format année/mois sont refusées ; une note Unicode de 1 000 caractères reste acceptée et idempotente. Les autres suites ne sont pas relancées ; leurs projets .NET sont compilés.

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-dates" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-dates" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-dates" -m:1 --disable-build-servers
```

Les binaires des API locales ne sont pas remplacés grâce aux artefacts TEMP. Il ne s'agit pas d'un test manuel de deux services déployés ni d'un test Kestrel des limites de transport.

## Suite

86.2c.2b : transmettre le mapping confirmé avec sa provenance, généraliser le reçu au type d'entité importée, étendre le récepteur Profile aux trois catégories et tester leur conciliation transactionnelle sans écrasement des données déjà vérifiées. La politique de correction/révocation et la sécurisation globale restent à planifier.
