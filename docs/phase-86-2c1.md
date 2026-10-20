# Phase 86.2c.1 — Transfert explicite des compétences approuvées

## Résultat

Le propriétaire local peut transférer une compétence approuvée Documents vers un profil existant lié par configuration serveur. Nouveau canal Profile authentifié par secret de service dédié, reçu durable et preuve auditée, reprise après perte de réponse et conciliation sans écrasement d'une compétence existante. Le guide [Documents → Profile](documents-profile-import.md) décrit l'activation, la migration, les erreurs et les limites.

Le périmètre reste volontairement **Skill uniquement**. Les contrats Experience, Education et Certification exigent des dates précises et d'autres informations absentes des annotations actuelles. Leur normalisation et confirmation sans invention feront l'objet de 86.2c.2. La phase 86 et son transfert complet ne sont pas déclarés terminés.

## Changements

- Documents.Application : port client et service contrôlant l'appartenance, la décision finale, le type et la longueur du nom avant tout appel réseau.
- Documents.Infrastructure : client HTTP à secret dédié, cible serveur, lecture préalable du reçu, bornes de réponse, timeout, redirections refusées et contrôle du reçu.
- Documents.Api : POST explicite profile-import, authentification propriétaire et limiteur des faits existant ; erreurs 422/503 sans contenu source dans les messages.
- Profile.Domain/Application : reçu et contrat d'import validé, lien propriétaire/profil, empreinte du corps canonique.
- Profile.Infrastructure : transaction PostgreSQL, verrous fait/profil, réutilisation du nom sans toucher les données vérifiées, audit JSONB et migration **20261015135115_AddDocumentFactImports** avec snapshot.
- Profile.Api : schéma d'authentification opt-in et deux routes dédiées ; anciens contrats conservés.
- Tests : tests de validation Profile et parcours de deux vrais hôtes API avec deux PostgreSQL 17 éphémères, transport HTTP TestServer. Les identifiants et secrets des nouveaux conteneurs sont aléatoires et temporaires.

Aucun changement de schéma Documents, aucune migration appliquée à une base persistante et aucun profil réel importé. Pas de frontend ni d'agent IA ajouté ; aucun commit, push ou changement de branche. Les modifications préexistantes sont conservées.

## Validation

Compilation finale des **32 projets : zéro erreur et zéro avertissement**. Les quatre suites ciblées totalisent **452 tests réussis, zéro échec, zéro ignoré** : Profile 305 unitaires + 71 d'intégration ; Documents 39 unitaires + 37 d'intégration. Les quatre TRX ont été relus. Ce lot ajoute 26 tests : 16 validations Profile et 10 tests d'intégration Documents/Profile. Les suites Job, Matching et Collection ainsi qu'Angular n'ont pas été relancées ; les projets .NET correspondants compilent.

Les tests ajoutés vérifient création et audit, reprise identique après suppression de la compétence, conservation des métadonnées existantes, imports concurrents, perte de réponse après commit, refus des faits en attente/rejetés/étrangers/non supportés, authentification et identité cible, corps modifié, configuration désactivée/invalide et conciliation ambiguë. La migration est appliquée sur PostgreSQL 17 de test et l'absence de changement EF non migré est contrôlée. Les parcours antérieurs d'upload, extraction avec vrai worker et revue restent verts, ainsi que les contrats Profile historiques.

Commandes depuis la racine :

```powershell
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase86-2c1" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-2c1" -m:1 --disable-build-servers
dotnet test tests/Documents/JobApplicationAgent.Documents.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-2c1" -m:1 --disable-build-servers
dotnet test tests/Profile/JobApplicationAgent.Profile.UnitTests --artifacts-path "$env:TEMP/job-agent-phase86-2c1" -m:1 --disable-build-servers
dotnet test tests/Profile/JobApplicationAgent.Profile.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase86-2c1" -m:1 --disable-build-servers
```

Les artefacts TEMP évitent de remplacer les DLL d'API locales éventuellement actives. Les tests ne font aucun appel à un service externe. Ils ne remplacent pas une vérification manuelle des deux services configurés, ni un test de transport Kestrel des limites HTTP.

## Suite

86.2c.2 : compléter explicitement les champs requis et normaliser les dates pour les autres catégories, conserver provenance et confirmation, puis étendre le récepteur sans écraser les données vérifiées. La sécurisation globale de Profile, le multi-utilisateur et la politique de rétention restent hors de cette tranche.
