# Phase 87.3 — Soumission déclarée et suivi recruteur

Le [guide de suivi manuel](applications-follow-up.md) définit les règles écrites avant le code, la migration et les deux nouvelles routes. Le propriétaire peut déclarer un envoi déjà effectué du dossier approuvé, puis enregistrer les retours recruteurs jusqu'à une offre, un refus ou un retrait. Aucune candidature ni message n'est transmis par ce service.

## Changements

- Domaine : nouveaux statuts ajoutés sans renuméroter les anciens ; événements de déclaration et de retour, dates métier distinctes des dates serveur, canaux contrôlés, références/messages bornés, transitions explicites et statuts terminaux.
- Historique : champs facultatifs rétrocompatibles, reconstruction par rejeu des nouvelles règles, pièces figées après soumission et notes conservées après clôture.
- Application/API : deux mutations authentifiées, révision exacte sous verrou de ligne, scopes propriétaire, limites de corps/fréquence et aucune dépendance distante pour le suivi historique.
- Infrastructure : migration **20261016022042_AddManualApplicationFollowUp**, contrainte SQL acceptant les statuts 1–9, rollback bloqué si des événements de suivi existent, y compris après retrait.
- Documentation : guide de migration et d'utilisation, règles et avancement mis à jour.

## Validation

**246 tests réussis, zéro échec et zéro ignoré** : 187 unitaires et 59 dans la suite d'intégration/configuration Applications. Les compteurs des deux TRX ont été relus. Ce lot ajoute 102 tests unitaires et neuf tests d'intégration aux 135 tests Applications précédents.

Compilation complète des **38 projets réussie, zéro erreur et zéro avertissement**.

Les tests unitaires couvrent la matrice complète de 81 transitions recruteurs, les états autorisés pour la déclaration, la confirmation, la chronologie, les champs bornés, les révisions/auteurs, les statuts terminaux et le rejeu des anciens historiques sans les nouveaux champs facultatifs.

Les tests d'intégration utilisent les vrais hôtes Job, Profile, Documents et Applications, avec PostgreSQL 17 éphémère. Ils préparent des exports synthétiques, les font approuver, puis vérifient le suivi jusqu'à Offer, les filtres SQL, les confirmations/révisions, les dates invalides, l'isolation propriétaire, les déclarations concurrentes, l'état terminal et le rollback empêché après soumission puis retrait. Les sources sont rendues indisponibles pour vérifier que la déclaration historique ne les appelle pas.

```powershell
dotnet test tests/Applications/JobApplicationAgent.Applications.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-3" -m:1 --disable-build-servers
dotnet test tests/Applications/JobApplicationAgent.Applications.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase87-3" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase87-3" -m:1 --disable-build-servers
```

Aucun vrai CV/profil traité, aucune base persistante migrée, aucun commit ou push. Aucun code de production des autres services ni frontend modifié. Les autres suites ne sont pas relancées ; les hôtes Job/Profile/Documents interviennent dans les tests du parcours. Les limites de corps Kestrel et le réseau réel ne sont pas validés par TestServer.

## Suite

**87.4 : relances planifiées et suivi de leur réalisation**, sans envoi automatique. L'intégration IA, les files asynchrones et l'interface suivent les phases 88–90 ; l'automatisation autorisée des candidatures reste en 96–97. Une déclaration manuelle ne constitue pas une preuve externe de livraison ni une autorisation pour envoyer un autre dossier.
