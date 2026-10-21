# Phase 95.2 — Parcours complet d'une candidature

Un test d'intégration Applications traverse maintenant une candidature de bout en bout dans les vraies applications ASP.NET et la base PostgreSQL de test. Il crée une offre Job, crée le dossier avec le profil Profile du fixture, demande deux exports approuvés et liés par l'API Documents, attache le CV et la lettre, fait passer la revue et l'approbation explicites, déclare un envoi manuel, consigne le retour du recruteur, planifie puis réalise une relance. Enfin, il relit l'historique complet et vérifie le filtre de liste.

Ce scénario complète les tests ciblés existants en validant l'ordre des révisions et la cohérence des événements entre les services. Les dépendances interservices sont hébergées par les `WebApplicationFactory` et reliées à PostgreSQL via Testcontainers. La CI exécute déjà ce projet comme une suite d'intégration ; aucune nouvelle dépendance ou configuration de workflow n'est nécessaire.

Validation locale : la compilation du projet d'intégration est effectuée sans lancer les tests, conformément à la pratique de ce dépôt. Le scénario lui-même s'exécute sur le runner CI avec Docker.
