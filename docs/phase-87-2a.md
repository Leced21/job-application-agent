# Phase 87.2a — PostgreSQL et API du suivi initial

Le service Applications dispose d'une API locale sur le port 5007 et d'une base dédiée PostgreSQL 17 sur 5437. Le [guide de démarrage et des routes](applications-api.md) précise configuration et migration. Le lot 87.2 est découpé : les exports Documents actuels n'identifient pas encore le profil, donc leur liaison et la revue HTTP attendent 87.2b.

## Changements

- Applications.Infrastructure : DbContext et migration **20261016013140_InitialApplicationsSchema**, dépôt transactionnel, unicités propriétaire/clé et propriétaire/profil/offre, FK incluant le propriétaire, verrou de mutation et projection paginée côté SQL.
- Client de sources : association propriétaire/profil configurée par le serveur, lecture des vraies API Profile et Job, validation des identifiants et statuts, origines locales, réponses bornées, délai et refus de redirection/cookies/transfert d'identifiants.
- Applications.Api : authentification propriétaire par jeton haché et comparaison à temps constant, cinq routes pour création, lecture, liste, notes et retrait ; limites de corps/fréquence et erreurs ProblemDetails sans détails internes.
- Domaine : reconstruction par rejeu validé des événements, conservation des anciennes pièces et contrôle de cohérence statut/révision ; historique borné avec place réservée au retrait.
- Application : service de lecture, pagination et mutations ; dates UTC serveur ne reculant pas par rapport au dernier événement.
- Compose PostgreSQL isolé, paramètres sans secrets et intégration des trois nouveaux projets dans la solution.

Aucune route de rattachement, revue, approbation ou soumission n'est encore exposée. Les statuts ReadyForReview/Approved restent dans le domaine, sans chemin HTTP dans ce lot. Aucun service de production Job/Profile/Documents ni frontend n'est modifié. Aucun stockage mémoire de production n'est enregistré.

## Vérifications

**84 tests réussis : 56 unitaires et 28 d'intégration, zéro échec et zéro ignoré.** Les compteurs des deux TRX ont été relus. Les tests comprennent un historique au plafond avec des notes Unicode de taille maximale, conservé et rejoué après retrait ; la borne SQL de 16 Mio tient compte de l'échappement JSON.

Compilation finale des **38 projets réussie, zéro erreur et zéro avertissement**.

Les tests utilisent des fichiers/données synthétiques et PostgreSQL 17 éphémère. Les hôtes Job et Profile sont leurs vraies API avec leurs migrations ; seul le transport HTTP entre TestServer est routé dans le processus. Les tests incluent création et relecture, reprise pendant une panne, clés concurrentes identiques/distinctes, conflit d'une clé sur deux offres, huit notes concurrentes, contraintes SQL testées sans les verrous du dépôt, isolation propriétaire, notes/retrait et erreurs, statuts Job réels, pagination, authentification, limitation à 60 écritures et absence de route d'envoi. Le modèle EF est comparé à la migration.

Les tests unitaires couvrent aussi le client HTTP, absence de transfert de credentials, redirections/statuts, annulation, JSON invalide/trop volumineux, origines de configuration, rejeu invalide et capacité réservée au retrait.

```powershell
dotnet test tests/Applications/JobApplicationAgent.Applications.UnitTests --artifacts-path "$env:TEMP/job-agent-phase87-2a" -m:1 --disable-build-servers
dotnet test tests/Applications/JobApplicationAgent.Applications.IntegrationTests --artifacts-path "$env:TEMP/job-agent-phase87-2a" -m:1 --disable-build-servers
dotnet build JobApplicationAgent.slnx --artifacts-path "$env:TEMP/job-agent-phase87-2a" -m:1 --disable-build-servers
```

Les autres suites et Angular ne sont pas relancés. Aucun vrai profil n'est créé par ces tests ; aucune base persistante migrée, aucun commit ou push. La configuration propriétaire/profil est locale, déclarative côté serveur ; les API historiques ne deviennent pas multi-utilisateur. Les limites de corps Kestrel ne sont pas testées par TestServer.

## Suite

**87.2b : rattachement des exports vérifiés et revue HTTP**, avec un lien explicite au profil, à l'offre et au propriétaire, puis transitions contrôlées du dossier. Ensuite : suivi après soumission manuelle, retours recruteurs et relances. Aucun export ou statut Approved ne vaut autorisation d'envoi.
