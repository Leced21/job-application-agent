# Phase 88.1 — Socle d'orchestration des agents

Le [guide des agents](agents.md) fixe les règles et les limites. Le lot livre une bibliothèque Domain/Application et des tests, sans API, stockage de production ni fournisseur connecté.

## Livré

- Catalogue des sept agents du cahier des charges ; seul Job Analysis est exécutable dans ce premier lot, avec prompt job-analysis-fr-v1 et sortie job-analysis-v1.
- Pipeline : clé idempotente propriétaire/requête, lecture de source figée, validation de son identité/hash/volume, séparation instructions/données non fiables, comptage des tokens, réservation maximale avant modèle, timeout, validation structurée et proposition à revoir.
- Validation : citations exactes, offsets UTF-16, catégories contrôlées, limites d'items/champs/JSON, rejet des actions et propriétés inattendues, ranges dupliqués, sorties inventées et découpes Unicode.
- Budget : politique serveur et barème configurable, quota réservé avant appel, comptabilisation des usages connus même sur sortie invalide, consommation maximale imputée si un appel a une consommation inconnue, journalisation/refus des dépassements. Le modèle local peut être gratuit monétairement.
- Domaine : Running/PendingReview/Approved/Rejected/Failed ; revue explicite de la révision et du hash exacts, décisions finales et reprise du résultat initial.
- Journal : identités techniques, codes, versions, empreintes et consommation, sans texte de source/prompt ni erreur brute ; les propositions et sources figées seront stockées séparément avec protection dans le prochain lot.
- Ports : source, modèle/tokenizer et dépôt atomique. Les doubles se trouvent uniquement dans les tests. Aucun autre service ne référence ce socle à ce stade.

## Vérification

**63 tests unitaires réussis, zéro échec et zéro ignoré**. Les compteurs du TRX ont été relus. Aucun de ces tests n'utilise un LLM réel, un serveur distant ou une base de production.

Compilation complète des **41 projets réussie, zéro erreur et zéro avertissement**.

Les tests simulés couvrent succès, canonisation, reprise, conflit d'identité, exécution unique concurrente, quotas réservés concurrents, annulation et timeout d'un modèle ignorant le token, consommation inconnue, erreurs de source/modèle/audit, accusé de réservation perdu, surconsommation, sources non autorisées/altérées/trop longues, injection de prompt, revue par hash/révision et décisions concurrentes. La revue concurrente force les lectures du même état avant les comparaisons de révision.

```powershell
dotnet test tests/Agents/JobApplicationAgent.Agents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase88-1" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase88-1" -m:1 --disable-build-servers
```

Les tarifs des tests sont synthétiques, sans correspondance à un fournisseur. Aucun modèle réel ni service externe n'est appelé, aucun vrai CV/profil traité, aucune base persistante migrée, aucun secret requis. Aucun commit/push ni changement de branche. Les autres suites et Angular ne sont pas relancés ; les projets .NET existants sont inclus dans la compilation.

## Limites et suite

Les doubles ne valident pas les garanties SQL, la persistance, la récupération après crash ou l'authentification réseau. Un adaptateur doit compter réellement les tokens du modèle, appliquer les caps et borner le transport ; la bibliothèque ne peut pas annuler un coût déjà engagé par un fournisseur qui viole son contrat. Le coût est calculé selon le barème configuré, pas une facture réelle.

L'analyse reste une extraction proposée, pas une garantie de catégorisation ni une solution complète aux injections. Les champs d'actions ne sont pas acceptés et aucune proposition, même Approved, n'est appliquée à Job/Profile/Documents/Applications. Les six autres agents sont explicitement indisponibles.

**Prochain lot : 88.2, stockage durable, API authentifiée et sources locales**, puis branchement de modèles selon un choix/configuration explicite. Les traitements asynchrones restent en phase 89.
