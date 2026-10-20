# Phase 87.4 — Relances planifiées et réalisation manuelle

Le [guide des relances](applications-reminders.md) fixe les règles écrites avant le code, la migration et les routes. Le propriétaire peut planifier, reprogrammer, annuler une relance ou déclarer sa réalisation manuelle ; une liste paginée expose les échéances dues. Aucun message ou rappel externe n'est envoyé.

## Livré

- Domaine : une relance Planned par dossier actif soumis, dates UTC et précision microseconde, canaux et textes bornés, Completed/Cancelled définitifs, références et anciens plans conservés dans les événements.
- Clôture : Offer, Rejected et Withdrawn annulent la relance en attente dans le même événement, sans consommer une entrée supplémentaire de l'historique. Les statuts de candidature ne sont pas modifiés par les opérations de relance.
- Application : mutations sous révision exacte et verrou SQL, requête des échéances à l'heure serveur, aucune dépendance distante.
- Infrastructure : projection de la relance en attente dans la même transaction que l'historique ; relecture contrôlant sa cohérence ; index partiel et contrainte empêchant les projections partielles ou associées à un état terminal.
- Migration **20261016070757_AddApplicationFollowUpReminders** : quatre colonnes facultatives, index et contrainte ; rollback bloqué si un historique de relance existe, y compris après annulation.
- API : cinq routes authentifiées, pagination propriétaire, limites de corps/fréquence et erreurs contrôlées. Les historiques précédents sans données de relance restent lisibles.

## Vérification

**288 tests réussis, zéro échec et zéro ignoré** : 214 unitaires et 74 dans la suite d'intégration/configuration Applications. Les compteurs des deux TRX finaux ont été relus. Le lot ajoute 27 tests unitaires et 15 tests d'intégration aux 246 précédents.

Compilation complète des **38 projets réussie, zéro erreur et zéro avertissement**.

Les tests unitaires couvrent les neuf états de candidature, les dates/canaux/textes, l'unicité du rappel en attente, les révisions et auteurs, la reprogrammation, l'action manuelle réellement datée, les annulations et clôtures, le rejeu et la précision à l'échéance immédiate.

Les tests d'intégration utilisent PostgreSQL 17 et les vrais hôtes Applications/Job/Profile/Documents avec exports synthétiques approuvés. Ils vérifient le parcours complet, la persistance, la liste des échéances et sa pagination avec une horloge contrôlée sans attente réelle, l'isolation propriétaire, les courses entre planifications et entre réalisation/annulation, les clôtures, les contraintes SQL, le rollback refusé et les pannes de sources. Les scénarios des lots antérieurs restent inclus.

Le test de downgrade 87.3 a été adapté à la nouvelle couche de migration : une migration récente peut être retirée avant le refus d'une plus ancienne ; le schéma courant est réappliqué avant d'utiliser les binaires courants. Après une coupure de connexion PostgreSQL pendant le premier passage, les classes de cette suite sont limitées à deux en parallèle pour borner le nombre de conteneurs/hôtes. Les courses explicites Task.WhenAll restent inchangées.

```powershell
dotnet test tests/Applications/JobApplicationAgent.Applications.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-4" -m:1 --disable-build-servers
dotnet test tests/Applications/JobApplicationAgent.Applications.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase87-4" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase87-4" -m:1 --disable-build-servers
```

Aucune base persistante migrée, aucun vrai CV/profil traité, aucun commit/push. Les autres suites et Angular ne sont pas relancés ; leurs projets .NET compilent. Les limites de corps Kestrel et le transport réel ne sont pas validés par TestServer. Les modifications préexistantes sont conservées.

## Suite

Le socle backend de la phase 87 couvre création, association des pièces, revue, suivi des statuts, notes, retours et relances avec historique. Prochain lot : **88.1, socle d'orchestration des agents IA**. Les transports asynchrones, l'interface et la sécurité centralisée suivent les phases 89–95. Une relance réalisée reste une déclaration humaine ; les phases 96–97 encadreront d'éventuels envois automatisés autorisés.
