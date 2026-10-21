# Phase 88.2 — Persistance, API authentifiée et source Job locale

Le [guide API Agents](agents-api.md) donne configuration, migration, routes et exemples PowerShell. Le [guide du socle](agents.md) conserve les règles d'orchestration et de validation.

## Livré

- Agents.Infrastructure et Agents.Api, PostgreSQL dédié sur 5438, API sur 5008, compose séparé et migration `20261016075234_InitialAgentsSchema`. Aucun démarrage avec migration automatique.
- Exécutions et propositions persistées avec snapshots reconstruits par les invariants du domaine ; unicité propriétaire/requestId, résultat initial repris sans nouvel appel ni relecture de Job.
- Réservations et consommation conservées entre instances/redémarrages ; verrou PostgreSQL par propriétaire, plafonds cumulés serveur, libération du reliquat, imputations connues au-delà du quota et inconnues au maximum réservé. Montants sans arrondi de stockage.
- Audit des révisions dans la même transaction que l'état et la comptabilité, métadonnées sans texte source/sortie/note ni erreur brute. Clôture rejouable sans double imputation, une seule décision humaine concurrente.
- Authentification Bearer locale avec propriétaire configuré côté serveur, consultation isolée, catalogue, budget, liste paginée, recherche par requestId, détail, audit et revue par hash/révision exacts. Cache désactivé et limite de dix POST runs par minute/propriétaire/instance.
- Source HTTP Job locale : description exacte d'une offre Discovered/Saved, contrôle d'identité, origine loopback, réponse bornée même sans Content-Length, redirections/cookies désactivés et aucune transmission du jeton utilisateur.
- Récupération explicite des Running après un bail de cinq minutes : Failed/Interrupted, aucune relance automatique du modèle ; réservation éventuelle imputée au maximum comme usage inconnu, clôtures et réservations tardives refusées.
- Migration descendante refusée en présence d'historique, pour conserver les données de quota et d'audit.

## Vérification

**115 tests Agents réussis : 65 unitaires et 50 dans le projet d'intégration, zéro échec et zéro ignoré.** Compteurs relus dans les TRX. Les intégrations de persistance/API utilisent PostgreSQL 17 éphémère et une API Job de test ; les contrôles du client HTTP utilisent des réponses simulées. Tous les modèles sont simulés dans les tests.

**Compilation des 44 projets réussie, zéro erreur et zéro avertissement.**

Scénarios vérifiés : unicité concurrente, conflits de clé, réservations concurrentes sur plusieurs instances du dépôt, chaque plafond, accusés rejoués, consommation réelle/inconnue/surconsommation, précision décimale, persistance des propositions et revues, isolation propriétaire sur toutes les consultations/décisions, reprise après suppression de Job, fournisseur désactivé sans coût, récupération avant/après expiration, refus de réponse tardive, audit atomique avec collision SQL injectée, migration alignée et protection du downgrade, origines interdites et réponse HTTP sans longueur dépassant la borne.

```powershell
dotnet test tests/Agents/JobApplicationAgent.Agents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
dotnet test tests/Agents/JobApplicationAgent.Agents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase88-2" -m:1 --disable-build-servers
```

Les artefacts et TRX sont isolés dans le répertoire temporaire `job-agent-phase88-2` pour préserver les services locaux en cours. Aucune base persistante utilisateur migrée, aucun commit/push ni changement de branche. Les autres suites et Angular ne sont pas relancés ; leurs projets .NET sont inclus dans la compilation de solution.

## Limites et suite

Le fournisseur de production est **désactivé**. Une demande sur une offre valide échoue durablement avec ModelUnavailable, sans réservation ni appel de LLM. Le catalogue l'annonce ; aucune génération simulée n'est présentée comme une analyse réelle. Aucun secret fournisseur ou tarif réel n'est ajouté.

La source Job est un catalogue local partagé ; l'identité Agents ne remplace pas une gestion multi-utilisateur de Job. Les snapshots sont en clair dans PostgreSQL et protégés à l'accès HTTP ; le durcissement global et le chiffrement au repos restent en phase 91. Les quotas sont cumulatifs, sans remise à zéro ; la limitation HTTP est propre à chaque instance. Les coûts restent ceux du barème configuré, pas une facture fournisseur.

La récupération est à la demande et conservatrice. Elle ne peut pas déterminer si un calcul a eu lieu avant un crash : une réservation abandonnée reste imputée au maximum. Le traitement automatique et les files RabbitMQ restent en phase 89. Une approbation n'applique toujours rien dans les autres services et aucune candidature n'est envoyée.

**Prochain lot : 88.3, adaptateur de modèle et tokenizer avec configuration explicite du modèle, de ses plafonds, de son barème et des données autorisées.** Les six autres agents seront développés avec leurs schémas et validations propres.
