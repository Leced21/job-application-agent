# JOB APPLICATION AGENT — PLAN DE DÉVELOPPEMENT RESTANT

## 1. Contexte du projet

Tu interviens sur le projet **JobApplicationAgent**, une plateforme intelligente de recherche d'emploi et de gestion automatisée des candidatures.

**Dépôt GitHub :** `Leced21/job-application-agent`

**Branche de développement :** `dev`

**Environnement :** Windows 11, PowerShell, Docker Desktop.

### Stack technique

- Backend : C# / .NET 10 / ASP.NET Core.
- Frontend : Angular 21 / TypeScript.
- Architecture : microservices, Clean Architecture.
- Base de données : PostgreSQL 17.
- ORM : Entity Framework Core.
- API Gateway : YARP.
- Messagerie asynchrone prévue : RabbitMQ.
- Tests : xUnit, NSubstitute, WebApplicationFactory, Testcontainers.
- Conteneurisation : Docker / Docker Compose.
- IA : intégration de modèles LLM et agents spécialisés.
- Exécution initiale : **100 % locale**, sans dépendance obligatoire à une infrastructure cloud.

### Contraintes impératives

1. Conserver l'architecture microservices et Clean Architecture existante.
2. Ne pas recréer les fonctionnalités déjà implémentées.
3. Inspecter le dépôt et les contrats existants avant toute modification.
4. Respecter les conventions de nommage et l'organisation des projets.
5. Maintenir la compatibilité des endpoints existants.
6. Écrire des tests unitaires et d'intégration pour chaque nouvelle fonctionnalité.
7. Utiliser des migrations EF Core pour les modifications de schéma.
8. Ne jamais enregistrer de secrets ou d'identifiants dans Git.
9. Travailler par phases, avec compilation et tests à chaque étape.
10. Ne pas développer l'ensemble des fonctionnalités en une seule modification.
11. Documenter les décisions techniques et les limitations.
12. Éviter toute action de candidature réelle sans validation explicite de l'utilisateur.

---

# PHASE 0 — AUDIT DU DÉPÔT ET VALIDATION DE L'EXISTANT

**Priorité : P0**

Avant toute implémentation :

- Examiner `JobApplicationAgent.slnx`.
- Inventorier les projets, services, contrôleurs, handlers, repositories, migrations et tests.
- Examiner `docker-compose.yml`, les fichiers de configuration et le Gateway.
- Vérifier l'état Git et identifier les modifications non commitées.
- Exécuter la compilation et tous les tests.
- Identifier les erreurs, avertissements, dépendances vulnérables et éventuelles migrations manquantes.
- Vérifier les contrats HTTP entre Profile, Job et Matching.
- Produire un rapport distinguant les fonctionnalités terminées, partielles et absentes.

**Point de référence :** la dernière validation globale comptait 515 tests réussis. Depuis, le RankingScore et ses tests ont été ajoutés.

**Livrable :** rapport d'audit, matrice des services et backlog ajusté au code réel.

---

# PHASE 84 — FINALISER LE JOB SERVICE ET LES RECOMMANDATIONS

## 84.1 — Filtrage des offres recommandables

**Priorité : P0**

Statuts actuellement connus :

- `Discovered = 1`
- `Saved = 2`
- `Ignored = 3`
- `Expired = 4`

Règle proposée :

- `Discovered` et `Saved` : recommandables.
- `Ignored` et `Expired` : exclues.

Vérifier cette règle avant de la figer, notamment si `Saved` représente une préférence personnelle plutôt qu'une disponibilité réelle.

Travaux :

- Finaliser `IJobRepository.GetPageByStatusesAsync`.
- Finaliser l'implémentation EF Core dans `JobRepository`.
- Finaliser `GetRecommendableJobsHandler`.
- Enregistrer le handler dans la DI.
- Créer `GET /api/v1/jobs/recommendable`.
- Conserver `GET /api/v1/jobs` inchangé.
- Vérifier la gestion des statuts inconnus, pages invalides et listes vides.
- Garantir que le filtrage s'effectue avant la pagination.
- Ajouter les tests unitaires et d'intégration.

**Attention :** les premières modifications de cette phase ont été proposées, mais leur présence effective dans le dépôt doit être vérifiée.

## 84.2 — Adapter le Matching Service

**Priorité : P0**

- Ajouter une méthode dédiée à la récupération des offres recommandables dans `IJobMatchingDataClient`.
- Implémenter cette méthode dans `JobMatchingHttpClient`.
- Adapter `RecommendationHandler`.
- Ne pas modifier le comportement de `CalculateMatchingHandler` sans nécessité.
- Gérer les erreurs HTTP, les timeouts et les réponses invalides.
- Ajouter les tests de contrats et d'intégration.

## 84.3 — Classement global et pagination

**Priorité : P0**

Problème actuel : le Matching Service récupère une page du Job Service, puis classe uniquement les offres de cette page.

Objectif : calculer un classement global sur l'ensemble des offres éligibles avant d'appliquer la pagination finale.

Travaux :

- Définir un contrat paginé incluant `items`, `totalCount`, `page`, `pageSize`, `totalPages`.
- Définir une stratégie de récupération exhaustive ou de calcul par lots.
- Ne pas confondre pagination de récupération des offres et pagination des recommandations.
- Éviter de charger toutes les offres en mémoire lorsque le volume devient important.
- Prévoir un traitement asynchrone pour les volumes élevés.
- Définir une stratégie de cache ou de persistance des scores.
- Déterminer comment invalider les recommandations lorsque le profil ou l'offre change.
- Garantir un ordre déterministe en cas d'égalité.
- Tester les classements sur plusieurs pages, les doublons et les ensembles vides.

## 84.4 — Améliorer le moteur de scoring

**Priorité : P1**

Conserver :

- `OverallScore` : compatibilité sur les critères évalués.
- `EvaluationCoverage` : proportion des critères évalués.
- `RankingScore` : indicateur de classement.

Formule actuelle :

`RankingScore = OverallScore × EvaluationCoverage / 100`

Améliorations :

- Distinguer critères manquants et critères non applicables.
- Rendre les pondérations configurables.
- Ajouter éventuellement les critères salaire, contrat, expérience, langues et diplômes.
- Améliorer la normalisation des intitulés de poste et compétences.
- Gérer les synonymes techniques.
- Prévoir un matching sémantique, sans remplacer immédiatement le scoring déterministe.
- Expliquer chaque score à l'utilisateur.
- Versionner les règles de scoring.
- Ajouter des tests de non-régression.

---

# PHASE 85 — COLLECTE ET NORMALISATION DES OFFRES

**Priorité : P1**

Créer un microservice dédié à l'acquisition des offres.

Responsabilités :

- Intégrer des sources d'offres autorisées.
- Utiliser prioritairement les API officielles.
- Respecter les conditions d'utilisation, les quotas et les restrictions des plateformes.
- Permettre l'import manuel d'une URL ou d'une offre.
- Récupérer titre, entreprise, description, localisation, salaire, contrat, date et URL.
- Normaliser les intitulés, compétences, lieux et modalités de travail.
- Détecter les doublons.
- Détecter les offres expirées.
- Enregistrer la provenance et les dates de collecte.
- Publier des événements pour le Matching Service.

Prévoir des connecteurs indépendants pour chaque source, afin qu'un changement de plateforme n'affecte pas tout le système.

**Livrables :** service de collecte, API, stockage, connecteurs, tests et documentation.

---

# PHASE 86 — GESTION DES CV ET DOCUMENTS

**Priorité : P1**

Créer un service documentaire permettant :

- Import de CV PDF et DOCX.
- Extraction structurée des expériences, compétences, diplômes et certifications.
- Validation des données extraites avant intégration au profil.
- Conservation de plusieurs versions du CV.
- Génération d'un CV adapté à une offre.
- Génération de lettres de motivation.
- Export PDF et DOCX.
- Prévisualisation avant export.
- Historique des documents générés.

Contraintes :

- Ne jamais inventer d'expériences, de compétences ou de diplômes.
- Préserver les informations vérifiées du candidat.
- Demander confirmation avant d'utiliser des informations incertaines.
- Sécuriser les fichiers et limiter leur accès au propriétaire.

---

# PHASE 87 — APPLICATION SERVICE

**Priorité : P1**

Créer le microservice de gestion des candidatures.

Fonctionnalités :

- Créer une candidature à partir d'une offre.
- Associer un profil, un CV et une lettre de motivation.
- Suivre le statut de chaque candidature.
- Enregistrer les dates et événements.
- Éviter les candidatures en double.
- Gérer les notes et les commentaires.
- Enregistrer les retours des recruteurs.
- Gérer les relances.
- Produire un historique complet.

Proposer une machine à états explicite, par exemple :

`Draft → ReadyForReview → Approved → Submitted → InReview → Interview → Offer / Rejected / Withdrawn`

Définir les transitions autorisées et les règles métier avant l'implémentation.

**Important :** une candidature ne doit pas être soumise automatiquement sans autorisation explicite.

---

# PHASE 88 — INTÉGRATION DES AGENTS IA

**Priorité : P1**

Créer une couche d'orchestration des agents IA.

Agents envisagés :

1. **Profile Agent** : analyser le profil et identifier les informations manquantes.
2. **Job Analysis Agent** : analyser et structurer les offres.
3. **Matching Agent** : enrichir les évaluations de compatibilité.
4. **CV Agent** : proposer une adaptation du CV.
5. **Cover Letter Agent** : générer une lettre de motivation.
6. **Application Agent** : préparer les candidatures.
7. **Follow-up Agent** : suggérer les relances.

Exigences :

- Interfaces indépendantes du fournisseur LLM.
- Prompts versionnés.
- Sorties structurées et validées.
- Gestion des erreurs et timeouts.
- Journalisation des décisions.
- Contrôle des coûts et du volume de tokens.
- Possibilité d'utiliser un modèle local.
- Validation humaine pour les actions sensibles.
- Tests avec modèles simulés.
- Protection contre les injections de prompt issues des offres externes.

---

# PHASE 89 — RABBITMQ ET COMMUNICATION ASYNCHRONE

**Priorité : P1**

Avancement : le relay RabbitMQ de l'outbox Collection est livré en [phase 89.1](phase-89-1.md), l’événement Job canonique en [phase 89.2](phase-89-2.md), le consommateur Matching idempotent en [phase 89.3](phase-89-3.md), et le test RabbitMQ de bout en bout en [phase 89.4](phase-89-4.md).


Introduire RabbitMQ lorsque les workflows asynchrones seront nécessaires.

Événements candidats :

- `JobDiscovered`
- `JobUpdated`
- `JobExpired`
- `ProfileUpdated`
- `MatchingRequested`
- `MatchingCompleted`
- `ApplicationPrepared`
- `ApplicationApproved`
- `ApplicationSubmitted`

Travaux :

- Définir les contrats d'événements.
- Ajouter RabbitMQ dans Docker Compose.
- Implémenter producteurs et consommateurs.
- Ajouter une stratégie de retry.
- Ajouter une dead-letter queue.
- Gérer l'idempotence.
- Utiliser l'Outbox Pattern pour les événements transactionnels.
- Ajouter la traçabilité et les tests d'intégration.

Ne pas transformer les appels HTTP existants en événements sans justification métier.

---

# PHASE 90 — FRONTEND ANGULAR PREMIUM

**Priorité : P1**

Développer une interface Angular 21 moderne, responsive, avec un thème clair premium.

Écrans attendus :

- Connexion et création de compte.
- Tableau de bord.
- Profil candidat.
- Expériences et compétences.
- Préférences professionnelles.
- Liste des offres.
- Détail d'une offre.
- Recommandations classées.
- Détail des scores de compatibilité.
- Gestion des CV.
- Génération des documents.
- Préparation des candidatures.
- Suivi des candidatures.
- Paramètres et configuration des agents.

Pour les recommandations, afficher distinctement :

- `OverallScore`
- `EvaluationCoverage`
- `RankingScore`
- Critères évalués
- Critères non évalués
- Explications du matching

Exigences :

- Angular standalone components.
- Typage strict TypeScript.
- Architecture modulaire par fonctionnalités.
- Services API typés.
- Gestion des états loading, empty et error.
- Validation des formulaires.
- Accessibilité.
- Responsive desktop et mobile.
- Tests unitaires et end-to-end.

Commencer par des maquettes cohérentes avant l'implémentation des écrans.

---

# PHASE 91 — AUTHENTIFICATION ET SÉCURITÉ

**Priorité : P0 avant toute exposition externe**

- Authentification sécurisée.
- Gestion des comptes utilisateurs.
- Autorisation par utilisateur et par ressource.
- Isolation des profils, CV et candidatures.
- Protection des endpoints.
- Gestion sécurisée des tokens.
- Stockage sécurisé des secrets.
- Validation des entrées.
- Limitation des requêtes.
- Protection contre les accès non autorisés.
- Gestion des fichiers téléchargés.
- Journalisation des événements de sécurité.
- Tests d'autorisation et de sécurité.

Évaluer une solution standard OpenID Connect/OAuth 2.0 plutôt que créer un système d'authentification personnalisé.

---

# PHASE 92 — OBSERVABILITÉ

**Priorité : P1**

Mettre en place :

- Logs structurés.
- Correlation IDs.
- Traces distribuées.
- Métriques applicatives.
- Health checks.
- Surveillance des appels HTTP.
- Surveillance des traitements RabbitMQ.
- Mesures de latence des calculs de matching.
- Tableaux de bord techniques.
- Alertes sur les erreurs critiques.

Envisager OpenTelemetry, Prometheus et Grafana selon les besoins.

Ne pas enregistrer de données personnelles sensibles dans les logs.

---

# PHASE 93 — DOCKER ET ENVIRONNEMENT LOCAL

**Priorité : P1**

Finaliser une configuration Docker Compose capable de démarrer l'ensemble de la plateforme.

Inclure selon les services effectivement développés :

- API Gateway.
- Profile Service.
- Job Service.
- Matching Service.
- Application Service.
- Document Service.
- Agent Orchestrator.
- RabbitMQ.
- Bases PostgreSQL.
- Frontend Angular.

Prévoir :

- Réseaux Docker.
- Volumes persistants.
- Health checks.
- Variables d'environnement.
- Fichiers de configuration d'exemple.
- Scripts de démarrage et d'arrêt.
- Stratégie de migrations.
- Documentation de démarrage.

---

# PHASE 94 — CI/CD ET QUALITÉ

**Priorité : P1**

Mettre en place GitHub Actions.

Pipeline attendu :

1. Restauration des dépendances.
2. Compilation .NET.
3. Tests unitaires.
4. Tests d'intégration.
5. Analyse statique.
6. Audit des dépendances.
7. Build Angular.
8. Tests frontend.
9. Construction des images Docker.
10. Publication des artefacts selon la configuration retenue.

Prévoir des règles de validation des pull requests vers `dev` et la branche principale.

---

# PHASE 95 — TESTS END-TO-END

**Priorité : P1**

Tester les parcours utilisateur complets :

- Créer un profil.
- Ajouter des compétences.
- Configurer les préférences.
- Importer une offre.
- Calculer le matching.
- Consulter les recommandations.
- Générer un CV adapté.
- Préparer une candidature.
- Approuver une candidature.
- Enregistrer sa soumission.
- Suivre son évolution.

Prévoir également des scénarios d'erreur, de timeout, de redémarrage des services et de reprise des traitements.

---

# PHASE 96 — AUTOMATISATION CONTRÔLÉE DES CANDIDATURES

**Priorité : P2**

Étudier l'automatisation de la soumission des candidatures sur les plateformes qui l'autorisent.

Exigences :

- Privilégier les API officielles.
- Respecter les règles des plateformes.
- Ne pas contourner CAPTCHA ou contrôles d'accès.
- Ne pas soumettre plusieurs fois la même candidature.
- Présenter les informations avant envoi.
- Obtenir une approbation explicite.
- Enregistrer la preuve et le résultat de soumission.
- Permettre de désactiver immédiatement l'automatisation.

Commencer par un mode de préparation et de validation manuelle.

Avancement : la [phase 96.1](phase-96-1.md) ajoute la vérification préalable locale du dossier approuvé. La [phase 96.2](phase-96-2.md) formalise une règle métier fail-closed pour l’accord explicite lié à la révision et au contenu. La [phase 96.3](phase-96-3.md) expose l’aperçu et conserve l’accord humain dans l’historique. La [phase 96.4](phase-96-4.md) réserve atomiquement une tentative avec une clé idempotente. La [phase 96.5](phase-96-5.md) ajoute le registre serveur vide et désactivé par défaut. La [phase 96.6](phase-96-6.md) conserve un résultat authentifié et bloque les reprises ambiguës. La [phase 96.7](phase-96-7.md) expose la réservation, la [phase 96.8](phase-96-8.md) l’intègre au parcours Applications, la [phase 96.9](phase-96-9.md) le vérifie dans Chromium et la [phase 96.10](phase-96-10.md) confirme le blocage des reprises après un résultat incertain ; aucun connecteur de soumission externe n’est activé.


---

# PHASE 97 — DOCUMENTATION ET PRÉPARATION DE LA V1

**Priorité : P1**

Produire :

- README principal.
- Guide d'installation Windows.
- Documentation de chaque microservice.
- Diagrammes d'architecture.
- Contrats OpenAPI.
- Documentation des événements.
- Schémas des bases de données.
- Guide des migrations.
- Guide des tests.
- Guide de configuration IA.
- Guide de sécurité.
- Guide de dépannage.

Définir les critères de livraison de la première version fonctionnelle.

Avancement : le README, l'installation Windows, l'architecture, les guides de tests et de microservices, OpenAPI, événements, schémas et migrations sont documentés dans [phases 97.1 à 97.6](phase-97-6.md). La [phase 97.7](phase-97-7.md) ajoute les guides de sécurité et de dépannage ainsi que les critères V1. La configuration IA locale figure dans [agents-local-runtime.md](agents-local-runtime.md). La [phase 97.8](phase-97-8.md) consigne une validation locale étendue. La [phase 97.13](phase-97-13.md) ajoute les certificats persistants OpenIddict hors développement, la [phase 97.14](phase-97-14.md) leur chevauchement pendant la rotation, la [phase 97.15](phase-97-15.md) la lecture des mots de passe depuis des fichiers secrets, la [phase 97.16](phase-97-16.md) l'export PostgreSQL et son contrôle de restauration isolé et la [phase 97.17](phase-97-17.md) la commande de migration des huit bases locales. La [phase 97.18](phase-97-18.md) teste l'infrastructure Compose ; la [phase 97.19](phase-97-19.md) migre les bases éphémères de CI, démarre toute la pile et vérifie la readiness de chaque API et du frontend ; la [phase 97.20](phase-97-20.md) crée puis reconnecte un compte synthétique via Chromium et PostgreSQL Identity ; la [phase 97.21](phase-97-21.md) sauvegarde puis restaure les huit bases dans des bases temporaires ; la [phase 97.22](phase-97-22.md) archive le volume RabbitMQ et vérifie son redémarrage depuis un nouveau volume ; la [phase 97.23](phase-97-23.md) vérifie que la migration Identity fonctionne avec Windows PowerShell 5.1 ; la [phase 97.24](phase-97-24.md) restaure une base PostgreSQL depuis une archive vérifiée tout en conservant son état précédent ; la [phase 97.25](phase-97-25.md) génère et vérifie une paire de certificats OpenIddict de test sous Windows PowerShell 5.1. Le gestionnaire de secrets de production, la rotation sur un déploiement réel, les protections de branche, un run CI vert sur le SHA de livraison, l’exercice de la reprise complète sur l’hôte local reste à effectuer.

La [phase 97.26](phase-97-26.md) automatise un jeu de reprise cohérent et le vérifie sur la CI jetable.
La [phase 97.27](phase-97-27.md) restaure le jeu sur un nouvel hôte, conserve les volumes précédents et démarre les services avec le broker rétabli. La récupération des secrets et l'exercice réel restent à faire.
La [phase 97.28](phase-97-28.md) ajoute une attribution explicite et limitée du profil historique resté sans propriétaire ; les cas ambigus et l'essai sur copie restent à valider.
La [phase 97.29](phase-97-29.md) fournit l'inventaire en lecture seule des UUID propriétaires dans les huit bases locales ; il reste à l'exécuter sur l'hôte de développement, puis à revoir les éventuelles lignes orphelines sur une copie protégée.
La [phase 97.30](phase-97-30.md) étend le test navigateur Compose à deux comptes et vérifie que le second ne peut pas lire ni modifier l'expérience du premier ; l'exécution sur runner CI reste à confirmer.
La [phase 97.31](phase-97-31.md) rend possible la création du profil candidat depuis `/profile` après l'inscription, avec préremplissage de l'e-mail du compte et validation du formulaire.
La [phase 97.32](phase-97-32.md) corrige les healthchecks Docker des images API et consigne le rétablissement de la base Identity et du réseau Agents local.


---

# MÉTHODE DE TRAVAIL OBLIGATOIRE

Pour chaque phase :

1. Inspecter les fichiers existants.
2. Présenter les modifications prévues.
3. Identifier les dépendances et impacts.
4. Implémenter une évolution limitée et cohérente.
5. Ajouter les tests.
6. Exécuter la compilation.
7. Exécuter les tests concernés.
8. Vérifier les contrats HTTP et les migrations.
9. Résumer les fichiers modifiés.
10. Présenter les résultats, risques et prochaines étapes.

Ne pas annoncer qu'une fonctionnalité est terminée sans vérification.

Ne pas modifier des fichiers sans rapport avec la tâche.

Ne pas effectuer de commit, de push ou d'action externe sensible sans demande explicite.

# PREMIÈRE MISSION

Commence par la **phase 0 : audit du dépôt**, puis reprends la **phase 84 : filtrage des offres recommandables**.

Vérifie si les modifications proposées dans `IJobRepository`, `JobRepository` et `GetRecommendableJobsHandler` existent déjà.

Termine le filtrage, ajoute les tests, puis expose l'endpoint dédié.

Ne commence pas les phases suivantes avant d'avoir validé cette première étape.

À la fin, fournis :

- Les fichiers créés et modifiés.
- Les décisions d'architecture.
- Les commandes exécutées.
- Les résultats de compilation.
- Le nombre de tests réussis et échoués.
- Les limitations restantes.
- La prochaine tâche recommandée.
