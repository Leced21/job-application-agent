# Phase 87.1 — Domaine du suivi des candidatures

Les [règles et transitions](applications.md) ont été écrites avant le code. Trois projets .NET 10 rejoignent la solution : Applications.Domain, Applications.Application et Applications.UnitTests.

## Livré

- Dossier Draft lié à un propriétaire, un profil et une offre ; titre et entreprise conservés.
- Pièces vérifiées CV/lettre avec identifiants et empreintes ; chaque ancienne référence reste dans les événements lors d'un remplacement.
- Revue, confirmation humaine de la révision exacte, retour en édition et retrait explicite avec motif. Aucun état Submitted ni mécanisme d'envoi.
- Historique séquentiel, horodaté UTC et attribué au propriétaire ; notes bornées, accès par un autre acteur et révisions périmées refusés.
- Service de création avec ports de vérification des sources et de persistance. Reprise sans rappeler les sources, conflit si une clé change de profil/offre, déduplication du triplet propriétaire/profil/offre même après retrait. Le dépôt devra réserver atomiquement la clé et l'identité métier.

## Validation

**35 tests unitaires réussis, zéro échec et zéro ignoré**, TRX relu. Les tests couvrent les transitions, pièces manquantes, refus sans confirmation, révisions périmées, identité/auteur, empreintes, références historiques, UTC, dates décroissantes, notes, retrait définitif et reprise de création après indisponibilité des sources. Les tests applicatifs simulent également un gagnant concurrent et un conflit au moment de l'insertion ; ils ne prouvent pas encore l'atomicité PostgreSQL.

```powershell
dotnet test tests/Applications/JobApplicationAgent.Applications.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-1" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase87-1" -m:1 --disable-build-servers
```

Compilation complète des **35 projets réussie, zéro erreur et zéro avertissement**. Les artefacts temporaires évitent de remplacer les binaires des API déjà actives. Les autres suites et le frontend ne sont pas exécutés dans ce lot. Aucun service de production existant ne change.

## Suite

**87.2 : Infrastructure et API** : base dédiée, migrations, contraintes de doublons sous concurrence, authentification propriétaire, adaptateurs Job/Profile/Documents, routes de création, lecture, revue et historique. Les références de pièces reçues par le domaine devront provenir d'un adaptateur de confiance ; les données du navigateur ne constituent pas une preuve d'approbation ou de propriété.

Aucune API de candidature n'est encore disponible, aucun stockage mémoire n'est enregistré en production et aucune migration n'est nécessaire pour 87.1. Les états après soumission, retours recruteurs et relances restent à implémenter. La soumission contrôlée reste dans les phases 96–97. Les fichiers préexistants sont conservés ; aucun commit ni push.
